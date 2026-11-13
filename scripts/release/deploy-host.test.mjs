import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Run the real shell script with isolated paths and external commands substituted.
// No Docker daemon, AWS account, real /etc or live environment is used.
const linux = process.platform === 'linux';
function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'averio-host-'));
  const config = join(root, 'config'), state = join(config, 'deploy-integration');
  const bin = join(root, 'bin');
  mkdirSync(state, { recursive: true }); mkdirSync(bin);
  for (const name of ['compose.yml', 'app.env', 'migration.env']) writeFileSync(join(config, name), 'fixture');
  const registry = '123456789012.dkr.ecr.eu-north-1.amazonaws.com';
  const image = (component, version) => `${registry}/averio-${component}@sha256:${version.repeat(64)}`;
  const overlay = (api, worker) => `services:\n  webapi:\n    image: ${image('api', api)}\n  worker:\n    image: ${image('worker', worker)}\n`;
  writeFileSync(join(state, 'current.yml'), overlay('b', 'b'));
  writeFileSync(join(state, 'previous.yml'), overlay('a', 'a'));
  const script = readFileSync(new URL('./deploy-host.sh', import.meta.url), 'utf8')
    .replaceAll('/etc/averio/', config + '/').replaceAll('/var/lock/', root + '/');
  writeFileSync(join(root, 'deploy.sh'), script);
  const executable = (name, body) => writeFileSync(join(bin, name), '#!/bin/bash\nset -eu\n' + body, { mode: 0o755 });
  executable('aws', 'echo fake-token\n');
  executable('sleep', ':\n');
  executable('docker', `
printf '%s\\n' "$*" >> "$FIXTURE_ROOT/calls"
case "$1" in
  login) cat >/dev/null ;;
  pull) : ;;
  run) [[ "$FAIL_AT" != migration ]] ;;
  compose)
    if [[ " $* " == *" up "* ]]; then [[ "$FAIL_AT" != compose ]]; fi
    if [[ " $* " == *" ps -q "* ]]; then echo "container-\${@: -1}"; fi
    ;;
  inspect)
    case "$3" in
      '{{.Config.Image}}') if [[ "$4" == container-webapi ]]; then echo "$API_IMAGE"; else echo "$WORKER_IMAGE"; fi ;;
      '{{.State.Running}}') if [[ "$FAIL_AT" == startup ]]; then echo false; else echo true; fi ;;
      '{{.RestartCount}}') echo 3 ;;
    esac ;;
esac
`);
  const run = (api = 'c', worker = 'b', fail = '') => spawnSync('bash', [join(root, 'deploy.sh'),
    'integration', 'averio-integration', join(config, 'compose.yml'), join(config, 'app.env'),
    join(config, 'migration.env'), 'eu-north-1', registry, image('api', api), image('worker', worker), image('migrator', 'd')],
  { encoding: 'utf8', env: { ...process.env, PATH: bin + ':' + process.env.PATH, FIXTURE_ROOT: root,
    API_IMAGE: image('api', api), WORKER_IMAGE: image('worker', worker), FAIL_AT: fail } });
  return { root, state, overlay, run, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}
for (const phase of ['migration', 'compose', 'startup']) {
  test(`${phase} failure preserves both recovery references`, { skip: !linux }, () => {
    const f = fixture();
    try {
      const result = f.run('c', 'b', phase); assert.notEqual(result.status, 0);
      assert.equal(readFileSync(join(f.state, 'current.yml'), 'utf8'), f.overlay('b', 'b'));
      assert.equal(readFileSync(join(f.state, 'previous.yml'), 'utf8'), f.overlay('a', 'a'));
      if (phase === 'migration') assert.doesNotMatch(readFileSync(join(f.root, 'calls'), 'utf8'), / up /);
    } finally { f.cleanup(); }
  });
}
test('successful rollout rotates references; retry retains the previous release', { skip: !linux }, () => {
  const f = fixture();
  try {
    for (let attempt = 0; attempt < 2; attempt++) {
      const result = f.run(); assert.equal(result.status, 0, result.stderr);
      assert.equal(readFileSync(join(f.state, 'current.yml'), 'utf8'), f.overlay('c', 'b'));
      assert.equal(readFileSync(join(f.state, 'previous.yml'), 'utf8'), f.overlay('b', 'b'));
    }
  } finally { f.cleanup(); }
});
test('frontend-only rollout allows Compose to retain unchanged backends', { skip: !linux }, () => {
  const f = fixture();
  try {
    const result = f.run('b', 'b'); assert.equal(result.status, 0, result.stderr);
    const calls = readFileSync(join(f.root, 'calls'), 'utf8');
    assert.match(calls, /up -d --no-build --no-deps webapi worker/);
    assert.doesNotMatch(calls, /--force-recreate/);
    assert.equal(readFileSync(join(f.state, 'previous.yml'), 'utf8'), f.overlay('a', 'a'));
  } finally { f.cleanup(); }
});
