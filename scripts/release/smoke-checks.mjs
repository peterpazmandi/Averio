import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
export async function verifyPublicRelease({ manifest, api, frontend, environment, dist = 'dist', request }) {
  const health = await request(`${api}/api/v1/system/health`);
  assert.equal(health.status, 200, 'API health failed');
  const data = await health.json();
  assert.equal(data.status, 'healthy');
  assert.equal(data.databaseConnected, true);
  assert.ok(health.headers.get('x-averio-version')?.startsWith(`${manifest.components.api.version}+${manifest.components.api.commit}`), 'API release identity mismatch');
  const version = await request(`${frontend}/version.json?release=${manifest.tag}`);
  assert.equal(version.status, 200);
  assert.deepEqual(await version.json(), { version: manifest.components.frontend.version, commit: manifest.components.frontend.commit });

  const htmlResponse = await request(`${frontend}/`);
  assert.equal(htmlResponse.status, 200, 'Frontend HTML is unavailable');
  assert.match(htmlResponse.headers.get('content-type') ?? '', /text\/html/i);
  const html = await htmlResponse.text();
  assert.match(html, /id=["']root["']/, 'Frontend root is missing');
  assert.match(html, /src=["']\/runtime-config\.js["']/, 'Runtime configuration script is missing');
  const runtimeResponse = await request(`${frontend}/runtime-config.js`);
  assert.equal(runtimeResponse.status, 200, 'Runtime configuration is unavailable');
  const runtimeText = await runtimeResponse.text();
  const runtime = runtimeText.match(/^window\.__AVERIO_CONFIG__ = (.+);\s*$/);
  assert.ok(runtime, 'Invalid deployed runtime configuration');
  assert.deepEqual(JSON.parse(runtime[1]), { apiUrl: api, environment }, 'Wrong deployed API origin or environment');

  // Verify the entry JS/CSS actually referenced by this release, not just version.json.
  const localHtml = readFileSync(resolve(dist, 'index.html'), 'utf8');
  const assets = [...localHtml.matchAll(/(?:src|href)=["'](\/assets\/[a-zA-Z0-9._/-]+\.(?:js|css))["']/g)].map(match => match[1]);
  assert.ok(assets.some(asset => asset.endsWith('.js')), 'Release has no application entry script');
  for (const asset of new Set(assets)) {
    assert.ok(!asset.split('/').includes('..'));
    assert.ok(html.includes(asset), 'Frontend HTML references a different release');
    const response = await request(`${frontend}${asset}`);
    assert.equal(response.status, 200, `Frontend asset unavailable: ${asset}`);
    assert.equal(hash(Buffer.from(await response.arrayBuffer())), hash(readFileSync(resolve(dist, '.' + asset))), `Frontend asset differs: ${asset}`);
  }
  const me = await request(`${api}/api/v1/auth/me`);
  assert.equal(me.status, 401, 'Private endpoint must reject anonymous access');
}
