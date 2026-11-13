import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { verifyPublicRelease } from './smoke-checks.mjs';
function fixture() {
  const dist = mkdtempSync(join(tmpdir(), 'averio-public-smoke-'));
  mkdirSync(join(dist, 'assets'));
  const html = '<html><div id="root"></div><script src="/runtime-config.js"></script><script type="module" src="/assets/app.js"></script><link href="/assets/app.css" rel="stylesheet"></html>';
  writeFileSync(join(dist, 'index.html'), html);
  writeFileSync(join(dist, 'assets/app.js'), 'window.application = true;');
  writeFileSync(join(dist, 'assets/app.css'), 'body { color: black; }');
  const api = 'https://api.example.test', frontend = 'https://app.example.test', commit = 'a'.repeat(40);
  const manifest = { tag: 'release-1-1', components: { api: { version: '0.1.0', commit }, frontend: { version: '0.1.0', commit } } };
  const routes = new Map([
    [`${api}/api/v1/system/health`, () => new Response(JSON.stringify({ status: 'healthy', databaseConnected: true }), { headers: { 'X-Averio-Version': `0.1.0+${commit}` } })],
    [`${api}/api/v1/auth/me`, () => new Response('', { status: 401 })],
    [`${frontend}/version.json?release=release-1-1`, () => Response.json({ version: '0.1.0', commit })],
    [`${frontend}/`, () => new Response(html, { headers: { 'Content-Type': 'text/html' } })],
    [`${frontend}/runtime-config.js`, () => new Response(`window.__AVERIO_CONFIG__ = ${JSON.stringify({ apiUrl: api, environment: 'production' })};\n`)],
    [`${frontend}/assets/app.js`, () => new Response('window.application = true;')],
    [`${frontend}/assets/app.css`, () => new Response('body { color: black; }')],
  ]);
  return { frontend, routes, run: () => verifyPublicRelease({ api, frontend, manifest, dist, environment: 'production',
    request: async url => routes.get(url)?.() ?? new Response('', { status: 404 }) }),
  cleanup: () => rmSync(dist, { recursive: true, force: true }) };
}
test('accepts a fully served release with matching public runtime settings and assets', async () => {
  const f = fixture(); try { await f.run(); } finally { f.cleanup(); }
});
for (const file of ['/', '/runtime-config.js', '/assets/app.js', '/assets/app.css']) {
  test(`rejects unavailable ${file} even when version.json and API health pass`, async () => {
    const f = fixture();
    try { f.routes.set(f.frontend + file, () => new Response('', { status: 503 })); await assert.rejects(f.run()); }
    finally { f.cleanup(); }
  });
}
test('rejects stale assets and an integration API configured on production', async () => {
  const f = fixture();
  try {
    const correctRuntime = f.routes.get(f.frontend + '/runtime-config.js');
    f.routes.set(f.frontend + '/runtime-config.js', () => new Response('window.__AVERIO_CONFIG__ = {"apiUrl":"https://integration-api.averioapp.com","environment":"integration"};'));
    await assert.rejects(f.run(), /Wrong deployed API/);
    f.routes.set(f.frontend + '/runtime-config.js', correctRuntime);
    f.routes.set(f.frontend + '/assets/app.js', () => new Response('old application'));
    await assert.rejects(f.run(), /asset differs/);
  } finally { f.cleanup(); }
});
