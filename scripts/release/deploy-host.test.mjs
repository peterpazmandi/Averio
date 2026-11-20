import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Run the real shell script with isolated paths and external commands substituted.
// No Docker daemon, AWS account, real /etc or live environment is used.
const linux = process.platform === 'linux';
function fixture(floor = '1') {
  const root = mkdtempSync(join(tmpdir(), 'averio-host-'));
  const config = join(root, 'config'), state = join(config, 'deploy-integration');
  const bin = join(root, 'bin');
  mkdirSync(state, { recursive: true }); mkdirSync(bin);
  if (floor !== null) writeFileSync(join(state, 'minimum-schema-epoch'), floor + '\n');
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
  executable('curl', `
printf 'curl %s\\n' "$*" >> "$FIXTURE_ROOT/calls"
if [[ "$FAIL_AT" == readiness ]]; then echo 503; else echo 200; fi
`);
  executable('docker', `
printf '%s\\n' "$*" >> "$FIXTURE_ROOT/calls"
case "$1" in
  login) cat >/dev/null ;;
  pull) : ;;
  image)
    while IFS='|' read -r reference epoch; do
      if [[ "$reference" == "\${@: -1}" ]]; then
        [[ "$epoch" != unavailable ]] || exit 1
        printf '%s\\n' "$epoch"; exit 0
      fi
    done <<< "$IMAGE_EPOCHS"
    exit 1 ;;
  run) [[ "$FAIL_AT" != migration ]] ;;
  compose)
    if [[ " $* " == *" stop webapi worker "* ]]; then [[ "$FAIL_AT" != stop ]]; fi
    if [[ " $* " == *" rm -f webapi worker "* ]]; then [[ "$FAIL_AT" != remove ]]; fi
    if [[ " $* " == *"candidate.yml up "* ]]; then
      [[ "$FAIL_AT" != compose ]]
      if [[ "\${@: -1}" == worker ]]; then [[ "$FAIL_AT" != worker ]]; fi
    fi
    if [[ " $* " == *" ps -q "* ]]; then echo "container-\${@: -1}"; fi
    if [[ " $* " == *" port webapi 8080 "* ]]; then echo "127.0.0.1:8080"; fi
    ;;
  inspect)
    case "$3" in
      '{{.Config.Image}}') if [[ "$4" == container-webapi ]]; then echo "$API_IMAGE"; else echo "$WORKER_IMAGE"; fi ;;
      '{{.State.Running}}') if [[ "$FAIL_AT" == startup ]]; then echo false; else echo true; fi ;;
      '{{.RestartCount}}') echo 3 ;;
    esac ;;
esac
`);
  const run = (api = 'c', worker = 'b', fail = '', epochs = {}) => spawnSync('bash', [join(root, 'deploy.sh'),
    'integration', 'averio-integration', join(config, 'compose.yml'), join(config, 'app.env'),
    join(config, 'migration.env'), 'eu-north-1', registry, image('api', api), image('worker', worker), image('migrator', 'd')],
  { encoding: 'utf8', env: { ...process.env, PATH: bin + ':' + process.env.PATH, FIXTURE_ROOT: root,
    API_IMAGE: image('api', api), WORKER_IMAGE: image('worker', worker), FAIL_AT: fail,
    IMAGE_EPOCHS: ['api', 'worker', 'migrator'].flatMap(component => ['a', 'b', 'c', 'd'].map(version =>
      `${image(component, version)}|${epochs[`${component}:${version}`] ?? epochs[version] ?? '1'}`)).join('\n') } });
  return { root, state, overlay, run, cleanup: () => rmSync(root, { recursive: true, force: true }) };
}
for (const phase of ['migration', 'compose', 'startup', 'readiness', 'worker']) {
  test(`${phase} failure preserves both recovery references`, { skip: !linux }, () => {
    const f = fixture();
    try {
      const result = f.run('c', 'b', phase); assert.notEqual(result.status, 0);
      assert.equal(readFileSync(join(f.state, 'current.yml'), 'utf8'), f.overlay('b', 'b'));
      assert.equal(readFileSync(join(f.state, 'previous.yml'), 'utf8'), f.overlay('a', 'a'));
      const calls = readFileSync(join(f.root, 'calls'), 'utf8');
      if (phase === 'migration') assert.doesNotMatch(calls, / up /);
      else assert.match(calls, /current\.yml up -d --no-build --no-deps webapi worker/);
      if (phase === 'readiness') {
        assert.match(calls, /curl .*system\/health/);
        assert.doesNotMatch(calls, /candidate\.yml up -d --no-build --no-deps worker/);
      }
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

for (const phase of ['', 'migration', 'compose', 'startup', 'readiness', 'worker']) {
  test(`epoch upgrade ${phase || 'success'} stops old writers and never restores them`, { skip: !linux }, () => {
    const f = fixture(null);
    try {
      const result = f.run('c', 'c', phase, { b: '' });
      assert.equal(result.status === 0, phase === '', result.stderr);
      const calls = readFileSync(join(f.root, 'calls'), 'utf8');
      assert.match(calls, / stop webapi worker\n[\s\S]*run --rm/);
      assert.match(calls, / rm -f webapi worker\n[\s\S]*run --rm/);
      assert.doesNotMatch(calls, /current\.yml up /);
      assert.equal(readFileSync(join(f.state, 'minimum-schema-epoch'), 'utf8'), '1\n');
      if (phase && phase !== 'migration') assert.match(calls, /stop webapi worker\n$/);
      if (phase) assert.equal(readFileSync(join(f.state, 'current.yml'), 'utf8'), f.overlay('b', 'b'));
    } finally { f.cleanup(); }
  });
}

test('failed migration fences old releases and a retry still stops incompatible current images', { skip: !linux }, () => {
  const f = fixture(null);
  try {
    assert.notEqual(f.run('c', 'c', 'migration', { b: '' }).status, 0);
    writeFileSync(join(f.root, 'calls'), '');
    const rejected = f.run('b', 'b', '', { b: '', d: '' });
    assert.notEqual(rejected.status, 0);
    assert.match(rejected.stderr, /cannot run the selected older/);
    assert.doesNotMatch(readFileSync(join(f.root, 'calls'), 'utf8'), /run --rm| stop | up /);
    const retried = f.run('c', 'c', '', { b: '' });
    assert.equal(retried.status, 0, retried.stderr);
    assert.match(readFileSync(join(f.root, 'calls'), 'utf8'), / stop webapi worker\n[\s\S]*run --rm/);
  } finally { f.cleanup(); }
});

for (const [name, epochs, marker, failure] of [
  ['mixed backend epochs', { 'worker:c': '0' }, null, ''],
  ['mixed migrator epoch', { d: '0' }, null, ''],
  ['invalid image label', { c: '01' }, null, ''],
  ['unreadable image metadata', { c: 'unavailable' }, null, ''],
  ['corrupt persisted marker', {}, 'invalid', ''],
  ['failed maintenance stop', { b: '' }, null, 'stop'],
  ['failed obsolete container removal', { b: '' }, null, 'remove'],
]) {
  test(`${name} prevents migration and startup`, { skip: !linux }, () => {
    const f = fixture();
    try {
      if (marker !== null) writeFileSync(join(f.state, 'minimum-schema-epoch'), marker);
      assert.notEqual(f.run('c', 'c', failure, epochs).status, 0);
      assert.doesNotMatch(readFileSync(join(f.root, 'calls'), 'utf8'), /run --rm| up /);
      assert.equal(readFileSync(join(f.state, 'current.yml'), 'utf8'), f.overlay('b', 'b'));
    } finally { f.cleanup(); }
  });
}

for (const missing of ['overlay', 'image']) {
  test(`missing recovery ${missing} keeps failed rollout in maintenance`, { skip: !linux }, () => {
    const f = fixture();
    try {
      if (missing === 'overlay') rmSync(join(f.state, 'current.yml'));
      const result = f.run('c', 'c', 'worker', { b: 'unavailable' });
      assert.notEqual(result.status, 0);
      const calls = readFileSync(join(f.root, 'calls'), 'utf8');
      assert.match(calls, / stop webapi worker\n[\s\S]*run --rm/);
      assert.doesNotMatch(calls, /current\.yml up /);
      assert.match(calls, /stop webapi worker\n$/);
    } finally { f.cleanup(); }
  });
}

test('all shipped backend images declare the refund provenance compatibility epoch', () => {
  for (const component of ['api', 'worker', 'migrator']) {
    const dockerfile = readFileSync(new URL(`../../Dockerfile.${component}`, import.meta.url), 'utf8');
    assert.match(dockerfile.slice(dockerfile.lastIndexOf('\nFROM ')), /LABEL io\.averio\.schema-epoch="1"/);
  }
});
test('frontend-only rollout allows Compose to retain unchanged backends', { skip: !linux }, () => {
  const f = fixture();
  try {
    const result = f.run('b', 'b'); assert.equal(result.status, 0, result.stderr);
    const calls = readFileSync(join(f.root, 'calls'), 'utf8');
    assert.match(calls, /up -d --no-build --no-deps webapi\n[\s\S]*curl .*system\/health\n[\s\S]*up -d --no-build --no-deps worker/);
    assert.doesNotMatch(calls, /--force-recreate/);
    assert.doesNotMatch(calls, / stop | rm -f /);
    assert.equal(readFileSync(join(f.state, 'previous.yml'), 'utf8'), f.overlay('a', 'a'));
  } finally { f.cleanup(); }
});
