import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export function validateRun(run, repository, runId) {
  assert.match(runId, /^[1-9][0-9]*$/, 'Supply the successful integration workflow run ID');
  assert.equal(String(run.id), runId);
  assert.equal(run.repository?.full_name, repository);
  assert.equal(run.head_repository?.full_name, repository);
  assert.equal(run.path, '.github/workflows/deploy.yml');
  assert.equal(run.event, 'workflow_dispatch');
  assert.equal(run.head_branch, 'develop');
  assert.equal(run.status, 'completed', 'Integration run is not complete');
  assert.equal(run.conclusion, 'success', 'Integration run must have succeeded');
  assert.ok(Number.isSafeInteger(run.run_attempt) && run.run_attempt > 0);
  assert.match(run.head_sha, /^[a-f0-9]{40}$/);
}

export function validateProof(proof, run, release) {
  assert.equal(proof.schema, 1);
  assert.equal(proof.environment, 'integration');
  assert.equal(proof.repository, run.repository.full_name);
  assert.equal(proof.run_id, String(run.id));
  assert.equal(proof.run_attempt, run.run_attempt, 'Evidence is from an earlier run attempt');
  assert.equal(proof.workflow_commit, run.head_sha);
  assert.equal(proof.release, release, 'Integration tested a different release');
  assert.match(proof.manifest_sha256, /^[a-f0-9]{64}$/);
  return proof.manifest_sha256;
}

export function prepareDeployment(env, gh = args => execFileSync('gh', args, { encoding: 'utf8' })) {
  assert.match(env.RELEASE_TAG ?? '', /^release-[0-9]+-[0-9]+$/);
  assert.ok(['integration', 'production'].includes(env.DEPLOY_ENVIRONMENT), 'Select integration or production');
  if (env.DEPLOY_ENVIRONMENT === 'integration') return '';
  const runId = env.INTEGRATION_RUN_ID ?? '';
  assert.match(runId, /^[1-9][0-9]*$/, 'Production requires a successful integration run ID');
  const run = JSON.parse(gh(['api', `repos/${env.GITHUB_REPOSITORY}/actions/runs/${runId}`]));
  validateRun(run, env.GITHUB_REPOSITORY, runId);
  const directory = mkdtempSync(join(tmpdir(), 'averio-proof-'));
  try {
    gh(['run', 'download', runId, '--repo', env.GITHUB_REPOSITORY,
      '--name', `integration-verification-${run.run_attempt}`, '--dir', directory]);
    const proof = JSON.parse(readFileSync(join(directory, 'integration-verification.json'), 'utf8'));
    return validateProof(proof, run, env.RELEASE_TAG);
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  if (process.argv[2] === 'record') {
    assert.equal(process.env.DEPLOY_ENVIRONMENT, 'integration');
    assert.match(process.env.MANIFEST_SHA256 ?? '', /^[a-f0-9]{64}$/);
    writeFileSync('integration-verification.json', JSON.stringify({
      schema: 1, environment: 'integration', repository: process.env.GITHUB_REPOSITORY,
      run_id: process.env.GITHUB_RUN_ID, run_attempt: Number(process.env.GITHUB_RUN_ATTEMPT),
      workflow_commit: process.env.GITHUB_SHA, release: process.env.RELEASE_TAG,
      manifest_sha256: process.env.MANIFEST_SHA256,
    }, null, 2));
    appendFileSync(process.env.GITHUB_STEP_SUMMARY,
      `\nIntegration verification run ID: \`${process.env.GITHUB_RUN_ID}\`. Use this with release \`${process.env.RELEASE_TAG}\` in a later manual production deployment.\n`);
  } else {
    const digest = prepareDeployment(process.env);
    appendFileSync(process.env.GITHUB_OUTPUT, `manifest_sha256=${digest}\n`);
  }
}
