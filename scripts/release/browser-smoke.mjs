import assert from 'node:assert/strict';
// Import from the repository's locked frontend dependency tree.
import { chromium } from '../../apps/averio-web/node_modules/playwright/index.mjs';
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(process.env.FRONTEND_URL, { waitUntil: 'networkidle', timeout: 60000 });
  assert.ok(await page.locator('#root').innerText(), 'React app did not render');
  const result = await page.evaluate(async ({ api, email, password }) => {
    const call = (path, options = {}) => fetch(`${api}${path}`, { credentials: 'include', ...options });
    const csrfResponse = await call('/api/v1/security/csrf');
    const csrf = await csrfResponse.json();
    const login = await call('/api/v1/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token }, body: JSON.stringify({ email, password, rememberMe: false }) });
    if (login.status !== 204) return { login: login.status };
    const me = await call('/api/v1/auth/me');
    // Refresh CSRF after authentication because the identity is part of the token.
    const authenticatedCsrf = await (await call('/api/v1/security/csrf')).json();
    const logout = await call('/api/v1/auth/logout', { method: 'POST', headers: { 'X-CSRF-TOKEN': authenticatedCsrf.token } });
    return { login: login.status, me: me.status, logout: logout.status };
  }, { api: process.env.API_URL.replace(/\/$/, ''), email: process.env.SMOKE_EMAIL, password: process.env.SMOKE_PASSWORD });
  assert.deepEqual(result, { login: 204, me: 200, logout: 204 }, 'Cross-origin cookie/CSRF authentication failed');
  assert.deepEqual(errors, [], 'Browser runtime errors');
  console.log('Deployed browser, CORS, secure cookie, CSRF, login and logout checks passed');
} finally { await browser.close(); }
