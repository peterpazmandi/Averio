import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { configuration, request, deploy } from './deploy-ssm.mjs';

const registry = '678745735110.dkr.ecr.eu-north-1.amazonaws.com';
const env = {
  DEPLOY_ENVIRONMENT: 'integration', DEPLOY_INSTANCE_ID: 'i-0d54ed79d21e72d8f',
  AWS_REGION: 'eu-north-1', AWS_ROLE_ARN: 'arn:aws:iam::678745735110:role/averio-deploy-integration',
  COMPOSE_PROJECT: 'averio-integration', COMPOSE_FILE: '/etc/averio/compose.integration.yml',
  ENV_FILE: '/etc/averio/.env.integration', MIGRATION_ENV_FILE: '/etc/averio/migration.integration.env',
  ECR_REGISTRY: registry,
  ...Object.fromEntries([['API_IMAGE','api'],['WORKER_IMAGE','worker'],['MIGRATOR_IMAGE','migrator']].map(([key, name]) => [key, registry + '/averio-' + name + '@sha256:' + 'a'.repeat(64)])),
};
const id = '12345678-abcd-abcd-abcd-123456789012';
function fixture(overrides = {}) {
  const calls = [];
  let clock = 0;
  const aws = async (service, operation, input) => {
    calls.push({ service, operation, input });
    if (overrides[operation]) return overrides[operation](input);
    if (operation === 'get-caller-identity') return { Account: '678745735110' };
    if (operation === 'describe-instance-information') return { InstanceInformationList: [{ InstanceId: env.DEPLOY_INSTANCE_ID, PingStatus: 'Online', PlatformType: 'Linux' }] };
    if (operation === 'send-command') return { Command: { CommandId: id } };
    return { CommandId: id, InstanceId: env.DEPLOY_INSTANCE_ID, Status: 'Success', ResponseCode: 0 };
  };
  return { calls, aws, options: { now: () => clock, sleep: async ms => { clock += ms; }, report: () => {}, timeout: 30000 } };
}

test('targets one instance with pinned document, exact host script and immutable image arguments', () => {
  const payload = request(configuration(env), '#!/bin/bash\r\necho ok\r\n');
  assert.deepEqual(payload.InstanceIds, [env.DEPLOY_INSTANCE_ID]);
  assert.equal(payload.DocumentVersion, '1');
  assert.equal(payload.DocumentName, 'AWS-RunShellScript');
  assert.deepEqual(payload.Parameters.executionTimeout, ['1800']);
  const command = payload.Parameters.commands[0];
  assert.ok(command.includes(Buffer.from('#!/bin/bash\necho ok\n').toString('base64')));
  assert.ok(command.includes("'" + env.MIGRATION_ENV_FILE + "'"));
  assert.ok(command.includes("'" + env.API_IMAGE + "'"));
  assert.ok(command.includes('trap'));
});

test('rejects malformed targets, paths, mutable images and mismatched account before AWS calls', async () => {
  for (const patch of [
    { DEPLOY_INSTANCE_ID: 'i-bad;echo oops' }, { COMPOSE_FILE: '/etc/averio/../passwd' },
    { API_IMAGE: registry + '/averio-api:latest' }, { AWS_REGION: 'eu-west-1' },
    { AWS_ROLE_ARN: 'arn:aws:iam::111111111111:role/deploy' }, { DEPLOY_ENVIRONMENT: 'staging' },
    { COMPOSE_PROJECT: "x'$(whoami)" },
  ]) {
    const f = fixture();
    await assert.rejects(deploy({ ...env, ...patch }, 'true', f.aws, f.options));
    assert.equal(f.calls.length, 0);
  }
});

test('requires matching AWS account and online Linux target before submission', async () => {
  for (const overrides of [
    { 'get-caller-identity': () => ({ Account: '111111111111' }) },
    { 'describe-instance-information': () => ({ InstanceInformationList: [] }) },
    { 'describe-instance-information': () => ({ InstanceInformationList: [{ InstanceId: env.DEPLOY_INSTANCE_ID, PingStatus: 'ConnectionLost', PlatformType: 'Linux' }] }) },
    { 'describe-instance-information': () => ({ InstanceInformationList: [{ InstanceId: 'i-12345678', PingStatus: 'Online', PlatformType: 'Linux' }] }) },
  ]) {
    const f = fixture(overrides);
    await assert.rejects(deploy(env, 'true', f.aws, f.options));
    assert.ok(!f.calls.some(c => c.operation === 'send-command'));
  }
});

test('waits through eventual consistency and pending status, submits exactly once', async () => {
  let count = 0;
  const f = fixture({ 'get-command-invocation': () => {
    if (count++ === 0) throw Object.assign(new Error('not ready'), { stderr: '(InvocationDoesNotExist)' });
    return { CommandId: id, InstanceId: env.DEPLOY_INSTANCE_ID, Status: count === 2 ? 'InProgress' : 'Success', ResponseCode: count === 2 ? -1 : 0 };
  } });
  assert.equal(await deploy(env, 'true', f.aws, f.options), id);
  assert.equal(f.calls.filter(c => c.operation === 'send-command').length, 1);
});

test('fails closed for terminal failures, nonzero success, unknown status and wrong invocation', async () => {
  for (const patch of [{ Status: 'Failed' }, { Status: 'TimedOut' }, { Status: 'Cancelled' }, { Status: 'Success', ResponseCode: 1 }, { Status: 'Unknown' }, { InstanceId: 'i-12345678' }, { CommandId: 'wrong' }]) {
    const f = fixture({ 'get-command-invocation': () => ({ CommandId: id, InstanceId: env.DEPLOY_INSTANCE_ID, Status: 'Success', ResponseCode: 0, ...patch }) });
    await assert.rejects(deploy(env, 'true', f.aws, f.options));
  }
});

test('timeout and monitoring errors never cancel or resubmit a migration', async () => {
  for (const handler of [
    () => ({ CommandId: id, InstanceId: env.DEPLOY_INSTANCE_ID, Status: 'InProgress', ResponseCode: -1 }),
    () => { throw new Error('network failure'); },
  ]) {
    const f = fixture({ 'get-command-invocation': handler });
    await assert.rejects(deploy(env, 'true', f.aws, f.options), /inspect/i);
    assert.equal(f.calls.filter(c => c.operation === 'send-command').length, 1);
    assert.ok(!f.calls.some(c => c.operation === 'cancel-command'));
  }
});

test('ambiguous submission fails without retry or exposing AWS error payload', async () => {
  const f = fixture({ 'send-command': () => { throw new Error('SENSITIVE remote details'); } });
  await assert.rejects(deploy(env, 'true', f.aws, f.options), error => /may already be running/.test(error.message) && !error.message.includes('SENSITIVE'));
  assert.equal(f.calls.filter(c => c.operation === 'send-command').length, 1);
});

test('SSM shell wrapper executes the script with all ten arguments and preserves failure exit code', { skip: process.platform !== 'linux' }, () => {
  const script = 'printf "%s\\n" "$@"\nexit 17\n';
  const command = request(configuration(env), script).Parameters.commands[0];
  const result = spawnSync('sh', ['-c', command], { encoding: 'utf8' });
  assert.equal(result.status, 17, result.stderr);
  assert.deepEqual(result.stdout.trimEnd().split('\n'), [
    env.DEPLOY_ENVIRONMENT, env.COMPOSE_PROJECT, env.COMPOSE_FILE, env.ENV_FILE,
    env.MIGRATION_ENV_FILE, env.AWS_REGION, env.ECR_REGISTRY, env.API_IMAGE,
    env.WORKER_IMAGE, env.MIGRATOR_IMAGE,
  ]);
});
