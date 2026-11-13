import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { readFile, writeFile, mkdtemp, rm, appendFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export function configuration(env) {
  const patterns = {
    DEPLOY_ENVIRONMENT: /^(integration|production)$/,
    DEPLOY_INSTANCE_ID: /^i-([a-f0-9]{8}|[a-f0-9]{17})$/,
    AWS_REGION: /^[a-z]{2}-[a-z]+-\d$/,
    AWS_ROLE_ARN: /^arn:aws:iam::\d{12}:role\/[A-Za-z0-9+=,.@_/-]+$/,
    COMPOSE_PROJECT: /^[a-z0-9][a-z0-9_-]*$/,
    ECR_REGISTRY: /^\d{12}\.dkr\.ecr\.[a-z]{2}-[a-z]+-\d\.amazonaws\.com$/,
  };
  for (const [key, pattern] of Object.entries(patterns)) {
    if (!pattern.test(env[key] ?? '')) throw new Error('Invalid ' + key);
  }
  for (const key of ['COMPOSE_FILE', 'ENV_FILE', 'MIGRATION_ENV_FILE']) {
    if (!/^\/etc\/averio\/[a-zA-Z0-9._/-]+$/.test(env[key] ?? '') || env[key].includes('..')) throw new Error('Invalid ' + key);
  }
  const account = env.AWS_ROLE_ARN.split(':')[4];
  if (env.ECR_REGISTRY !== account + '.dkr.ecr.' + env.AWS_REGION + '.amazonaws.com') throw new Error('AWS account/region mismatch');
  for (const [key, repository] of [['API_IMAGE', 'averio-api'], ['WORKER_IMAGE', 'averio-worker'], ['MIGRATOR_IMAGE', 'averio-migrator']]) {
    const prefix = env.ECR_REGISTRY + '/' + repository + '@sha256:';
    if (!env[key]?.startsWith(prefix) || !/^[a-f0-9]{64}$/.test(env[key].slice(prefix.length))) throw new Error('Invalid ' + key);
  }
  return { ...env, account };
}

export function request(config, script) {
  const keys = ['DEPLOY_ENVIRONMENT', 'COMPOSE_PROJECT', 'COMPOSE_FILE', 'ENV_FILE', 'MIGRATION_ENV_FILE', 'AWS_REGION', 'ECR_REGISTRY', 'API_IMAGE', 'WORKER_IMAGE', 'MIGRATOR_IMAGE'];
  const quote = value => "'" + value.replaceAll("'", "'\\''") + "'";
  const encoded = Buffer.from(script.replaceAll('\r\n', '\n')).toString('base64');
  const command = [
    "bash <<'AVERIO_SSM_SCRIPT'",
    'set -euo pipefail',
    'umask 077',
    'script=$(mktemp /tmp/averio-deploy.XXXXXXXX)',
    'trap \'rm -f "$script"\' EXIT',
    "printf '%s' " + quote(encoded) + ' | base64 --decode > "$script"',
    'bash "$script" ' + keys.map(key => quote(config[key])).join(' '),
    'AVERIO_SSM_SCRIPT',
  ].join('\n');
  return {
    InstanceIds: [config.DEPLOY_INSTANCE_ID],
    DocumentName: 'AWS-RunShellScript',
    DocumentVersion: '1',
    TimeoutSeconds: 120,
    Comment: 'Averio ' + config.DEPLOY_ENVIRONMENT + ' release deployment',
    Parameters: { executionTimeout: ['1800'], commands: [command] },
  };
}

export async function deploy(env, script, aws, { sleep = ms => new Promise(r => setTimeout(r, ms)), now = Date.now, report = console.log, timeout = 40 * 60 * 1000 } = {}) {
  const config = configuration(env);
  const identity = await aws('sts', 'get-caller-identity', {});
  if (identity.Account !== config.account) throw new Error('Unexpected AWS caller account');
  const instances = await aws('ssm', 'describe-instance-information', { Filters: [{ Key: 'InstanceIds', Values: [config.DEPLOY_INSTANCE_ID] }] });
  const nodes = instances.InstanceInformationList ?? [];
  if (nodes.length !== 1 || nodes[0].InstanceId !== config.DEPLOY_INSTANCE_ID || nodes[0].PingStatus !== 'Online' || nodes[0].PlatformType !== 'Linux') {
    throw new Error('Deployment instance must be an online Linux SSM managed node');
  }
  let sent;
  try { sent = await aws('ssm', 'send-command', request(config, script)); }
  catch { throw new Error('SSM submission failed or its response was lost. Inspect Run Command before retrying; the command may already be running.'); }
  const id = sent.Command?.CommandId;
  if (!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(id ?? '')) throw new Error('Missing SSM command ID; inspect Run Command before retrying');
  await report('SSM command: ' + id + ' | instance: ' + config.DEPLOY_INSTANCE_ID + ' | region: ' + config.AWS_REGION);
  const deadline = now() + timeout;
  while (now() < deadline) {
    let result;
    try { result = await aws('ssm', 'get-command-invocation', { CommandId: id, InstanceId: config.DEPLOY_INSTANCE_ID }); }
    catch (error) {
      if (!String(error.stderr ?? '').includes('(InvocationDoesNotExist)')) throw new Error('Cannot monitor SSM command ' + id + '; inspect Run Command before retrying');
      await sleep(10000);
      continue;
    }
    if (result.CommandId !== id || result.InstanceId !== config.DEPLOY_INSTANCE_ID) throw new Error('Unexpected SSM invocation identity');
    if (result.Status === 'Success' && result.ResponseCode === 0) return id;
    if (!['Pending', 'InProgress', 'Delayed', 'Cancelling'].includes(result.Status)) throw new Error('SSM command ' + id + ' ended with ' + result.Status + ' (exit ' + result.ResponseCode + '). Inspect its output in AWS.');
    await sleep(10000);
  }
  throw new Error('Stopped waiting for SSM command ' + id + '. It may still be running; inspect AWS before retrying. No cancellation was sent.');
}

async function main() {
  const config = configuration(process.env);
  const dir = await mkdtemp(join(tmpdir(), 'averio-ssm-'));
  try {
    const aws = async (service, operation, payload) => {
      const input = join(dir, 'request.json');
      await writeFile(input, JSON.stringify(payload), { mode: 0o600 });
      const { stdout } = await promisify(execFile)('aws', [service, operation, '--cli-input-json', 'file://' + input, '--region', config.AWS_REGION, '--output', 'json', '--no-cli-pager'], {
        timeout: 60000, maxBuffer: 1024 * 1024,
        env: { ...process.env, AWS_PAGER: '', AWS_MAX_ATTEMPTS: operation === 'send-command' ? '1' : '3' },
      });
      return JSON.parse(stdout);
    };
    await deploy(config, await readFile('scripts/release/deploy-host.sh', 'utf8'), aws, {
      report: async message => {
        console.log(message);
        if (process.env.GITHUB_STEP_SUMMARY) await appendFile(process.env.GITHUB_STEP_SUMMARY, message + '\n\nIf the workflow is interrupted, inspect this command in AWS Systems Manager before retrying.\n');
      },
    });
  } finally { await rm(dir, { recursive: true, force: true }); }
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
