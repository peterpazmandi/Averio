import { readFileSync } from 'node:fs';
import { verifyPublicRelease } from './smoke-checks.mjs';
const manifest = JSON.parse(readFileSync('release/manifest.json', 'utf8'));
const api = process.env.API_URL.replace(/\/$/, '');
const frontend = process.env.FRONTEND_URL.replace(/\/$/, '');
const request = (url, options = {}) => fetch(url, { ...options, signal: AbortSignal.timeout(15000), redirect: 'error' });
let lastError;
for (let attempt = 0; attempt < 12; attempt++) {
  try {
    await verifyPublicRelease({ manifest, api, frontend, environment: process.env.DEPLOY_ENVIRONMENT, request });
    console.log('API, database, frontend HTML, runtime configuration, entry assets and identity checks passed');
    lastError = undefined;
    break;
  } catch (error) { lastError = error; await new Promise(resolve => setTimeout(resolve, 5000)); }
}
if (lastError) throw lastError;
