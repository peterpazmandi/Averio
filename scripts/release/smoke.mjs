import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const manifest = JSON.parse(readFileSync('release/manifest.json', 'utf8'));
const api = process.env.API_URL.replace(/\/$/, '');
const frontend = process.env.FRONTEND_URL.replace(/\/$/, '');
const request = (url, options = {}) => fetch(url, { ...options, signal: AbortSignal.timeout(15000), redirect: 'error' });
let lastError;
for (let attempt = 0; attempt < 12; attempt++) {
  try {
    const health = await request(`${api}/api/v1/system/health`);
    assert.equal(health.status, 200);
    const data = await health.json();
    assert.equal(data.status, 'healthy');
    assert.equal(data.databaseConnected, true);
    assert.ok(health.headers.get('x-averio-version')?.startsWith(`${manifest.components.api.version}+${manifest.components.api.commit}`), 'API release identity mismatch');
    const version = await request(`${frontend}/version.json?release=${manifest.tag}`);
    assert.equal(version.status, 200);
    assert.deepEqual(await version.json(), { version: manifest.components.frontend.version, commit: manifest.components.frontend.commit });
    const me = await request(`${api}/api/v1/auth/me`);
    assert.equal(me.status, 401, 'Private endpoint must reject anonymous access');
    console.log('API, database, frontend identity and anonymous access checks passed');
    lastError = undefined;
    break;
  } catch (error) { lastError = error; await new Promise(resolve => setTimeout(resolve, 5000)); }
}
if (lastError) throw lastError;
