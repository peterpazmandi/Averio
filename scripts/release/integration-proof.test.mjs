import test from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { prepareDeployment, validateRun, validateProof } from './integration-proof.mjs';

const repository = 'peterpazmandi/Averio';
const run = { id: 123, run_attempt: 2, repository: { full_name: repository }, head_repository: { full_name: repository },
  path: '.github/workflows/deploy.yml', event: 'workflow_dispatch', head_branch: 'develop',
  status: 'completed', conclusion: 'success', head_sha: 'a'.repeat(40) };
const proof = { schema: 1, environment: 'integration', repository, run_id: '123', run_attempt: 2,
  workflow_commit: run.head_sha, release: 'release-10-1', manifest_sha256: 'b'.repeat(64) };
const env = { GITHUB_REPOSITORY: repository, RELEASE_TAG: proof.release, DEPLOY_ENVIRONMENT: 'production', INTEGRATION_RUN_ID: '123' };

test('integration deploys independently without prior evidence or GitHub requests', () => {
  assert.equal(prepareDeployment({ ...env, DEPLOY_ENVIRONMENT: 'integration', INTEGRATION_RUN_ID: '' }, () => assert.fail()), '');
});
test('later production run uses the exact manifest from selected successful integration attempt', () => {
  const calls = [];
  const digest = prepareDeployment(env, args => {
    calls.push(args);
    if (args[0] === 'api') return JSON.stringify(run);
    assert.equal(args[args.indexOf('--name') + 1], 'integration-verification-2');
    writeFileSync(join(args.at(-1), 'integration-verification.json'), JSON.stringify(proof));
    return '';
  });
  assert.equal(digest, proof.manifest_sha256);
  assert.equal(calls.length, 2);
});
test('production fails closed without evidence or with invalid selections', () => {
  for (const changes of [{ INTEGRATION_RUN_ID: '' }, { INTEGRATION_RUN_ID: '../123' }, { RELEASE_TAG: 'latest' }, { DEPLOY_ENVIRONMENT: 'other' }]) {
    assert.throws(() => prepareDeployment({ ...env, ...changes }, () => assert.fail('No GitHub call expected')));
  }
  assert.throws(() => prepareDeployment(env, args => {
    if (args[0] === 'api') return JSON.stringify(run);
    throw new Error('Artifact expired or deleted');
  }), /expired or deleted/);
});
test('failed, running, foreign or untrusted workflow runs cannot authorize production', () => {
  for (const changes of [{ conclusion: 'failure' }, { conclusion: 'cancelled' }, { status: 'in_progress' },
    { head_branch: 'feature/test' }, { event: 'pull_request' }, { path: '.github/workflows/verify.yml' },
    { repository: { full_name: 'other/repo' } }, { head_repository: { full_name: 'other/repo' } },
    { id: 124 }, { run_attempt: 0 }]) {
    assert.throws(() => validateRun({ ...run, ...changes }, repository, '123'));
  }
});
test('evidence binds release, environment, repository, commit, run and current attempt', () => {
  for (const changes of [{ release: 'release-11-1' }, { environment: 'production' }, { repository: 'other/repo' },
    { workflow_commit: 'c'.repeat(40) }, { run_id: '124' }, { run_attempt: 1 }, { manifest_sha256: '' }, { schema: 2 }]) {
    assert.throws(() => validateProof({ ...proof, ...changes }, run, env.RELEASE_TAG));
  }
});
