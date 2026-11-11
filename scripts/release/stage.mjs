import { createHash } from 'node:crypto';
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
const manifestBytes = readFileSync('release/manifest.json');
const manifestHash = createHash('sha256').update(manifestBytes).digest('hex');
const expectedHash = process.env.EXPECTED_MANIFEST_SHA256;
if (process.env.DEPLOY_ENVIRONMENT === 'production' && !/^[a-f0-9]{64}$/.test(expectedHash ?? '')) {
  throw new Error('Production requires the manifest digest verified by integration');
}
if (expectedHash && expectedHash !== manifestHash) throw new Error('Release manifest changed after integration');
const manifest = JSON.parse(manifestBytes);
if (manifest.schema !== 1 || manifest.tag !== process.env.RELEASE_TAG || !/^[a-f0-9]{40}$/.test(manifest.commit)) throw new Error('Invalid release manifest');
const registry = process.env.ECR_REGISTRY;
if (!/^\d+\.dkr\.ecr\.[a-z0-9-]+\.amazonaws\.com$/.test(registry ?? '')) throw new Error('Invalid ECR registry');
for (const [key, image] of Object.entries({ api: manifest.components.api.image, worker: manifest.components.worker.image, migrator: manifest.migrator })) {
  if (!image?.startsWith(`${registry}/averio-${key}@sha256:`) || !/sha256:[a-f0-9]{64}$/.test(image)) throw new Error(`Invalid ${key} image`);
  appendFileSync(process.env.GITHUB_OUTPUT, `${key}=${image}\n`);
}
const hash = createHash('sha256').update(readFileSync('release/frontend.tar.gz')).digest('hex');
if (hash !== manifest.components.frontend.sha256) throw new Error('Frontend archive hash mismatch');
const entries = execFileSync('tar', ['-tzf', 'release/frontend.tar.gz'], { encoding: 'utf8' }).split('\n').filter(Boolean);
if (entries.some(path => path.startsWith('/') || path.split('/').includes('..'))) throw new Error('Unsafe archive path');
execFileSync('tar', ['-xzf', 'release/frontend.tar.gz', '-C', 'dist']);
const environment = process.env.DEPLOY_ENVIRONMENT;
if (!['integration', 'production'].includes(environment)) throw new Error('Invalid environment');
const api = new URL(process.env.API_URL);
if (api.protocol !== 'https:' || api.username || api.password || api.pathname !== '/' || api.search || api.hash) throw new Error('API_URL must be an HTTPS origin');
writeFileSync('dist/runtime-config.js', `window.__AVERIO_CONFIG__ = ${JSON.stringify({ apiUrl: api.origin, environment })};\n`);
writeFileSync('dist/_headers', '/*\n  X-Content-Type-Options: nosniff\n' +
  (environment === 'integration' ? '  X-Robots-Tag: noindex\n' : '') +
  '/runtime-config.js\n  Cache-Control: no-store\n/version.json\n  Cache-Control: no-store\n/index.html\n  Cache-Control: no-cache\n');
// Exposed to the caller only after the entire release has passed staging validation.
appendFileSync(process.env.GITHUB_OUTPUT, `manifest_sha256=${manifestHash}\n`);
