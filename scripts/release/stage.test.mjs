import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync, spawnSync } from 'node:child_process';
const script = fileURLToPath(new URL('./stage.mjs', import.meta.url));
const registry = '123456789012.dkr.ecr.eu-north-1.amazonaws.com';
function fixture() {
  const cwd = mkdtempSync(join(tmpdir(), 'averio-release-'));
  for (const dir of ['release', 'dist', 'source']) mkdirSync(join(cwd, dir));
  writeFileSync(join(cwd, 'source/app.js'), 'console.log("same compiled application")');
  execFileSync('tar', ['-czf', 'release/frontend.tar.gz', '-C', 'source', '.'], { cwd });
  const digest = 'a'.repeat(64);
  const manifest = { schema: 1, tag: 'release-1-1', commit: 'b'.repeat(40), components: {
    frontend: { sha256: createHash('sha256').update(readFileSync(join(cwd, 'release/frontend.tar.gz'))).digest('hex') },
    api: { image: `${registry}/averio-api@sha256:${digest}` },
    worker: { image: `${registry}/averio-worker@sha256:${digest}` },
  }, migrator: `${registry}/averio-migrator@sha256:${digest}` };
  const save = () => writeFileSync(join(cwd, 'release/manifest.json'), JSON.stringify(manifest));
  save();
  const run = (extra = {}) => spawnSync(process.execPath, [script], { cwd, encoding: 'utf8', env: {
    ...process.env, RELEASE_TAG: manifest.tag, ECR_REGISTRY: registry, GITHUB_OUTPUT: join(cwd, 'output'),
    DEPLOY_ENVIRONMENT: 'integration', API_URL: 'https://integration-api.averioapp.com', ...extra,
  } });
  return { cwd, manifest, save, run, cleanup: () => rmSync(cwd, { recursive: true, force: true }) };
}
test('promotion preserves compiled assets and switches only public settings', () => {
  const f = fixture();
  try {
    let result = f.run(); assert.equal(result.status, 0, result.stderr);
    const app = readFileSync(join(f.cwd, 'dist/app.js'), 'utf8');
    assert.match(readFileSync(join(f.cwd, 'dist/_headers'), 'utf8'), /X-Robots-Tag: noindex/);
    result = f.run({ DEPLOY_ENVIRONMENT: 'production', API_URL: 'https://api.averioapp.com' });
    assert.equal(result.status, 0, result.stderr);
    assert.equal(readFileSync(join(f.cwd, 'dist/app.js'), 'utf8'), app);
    assert.doesNotMatch(readFileSync(join(f.cwd, 'dist/_headers'), 'utf8'), /noindex/);
    assert.match(readFileSync(join(f.cwd, 'dist/runtime-config.js'), 'utf8'), /"environment":"production"/);
  } finally { f.cleanup(); }
});
test('corrupt frontend archive fails before extraction', () => {
  const f = fixture();
  try {
    writeFileSync(join(f.cwd, 'release/frontend.tar.gz'), 'corrupted');
    assert.notEqual(f.run().status, 0);
  } finally { f.cleanup(); }
});
test('unexpected registry, tag and insecure API origin are rejected', () => {
  const f = fixture();
  try {
    assert.notEqual(f.run({ RELEASE_TAG: 'release-2-1' }).status, 0);
    assert.notEqual(f.run({ API_URL: 'http://localhost:5180' }).status, 0);
    f.manifest.components.api.image = `attacker.example/averio-api@sha256:${'a'.repeat(64)}`;
    f.save();
    assert.notEqual(f.run().status, 0);
  } finally { f.cleanup(); }
});
