import assert from 'node:assert/strict';

export async function verifyBrowser({ page, frontend, api, environment, email, password, timeout = 30000 }) {
  assert.ok(['integration', 'production'].includes(environment));
  const errors = [], apiRequests = [];
  page.setDefaultTimeout(timeout);
  await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/v1/')) apiRequests.push(request.url());
  });
  page.on('response', response => {
    if (new URL(response.url()).origin === frontend && response.url().includes('/assets/') && response.status() >= 400) {
      errors.push('Failed frontend asset: ' + response.status());
    }
  });
  const anonymousResponse = page.waitForResponse(response => response.url() === `${api}/api/v1/auth/me`, { timeout });
  // Attach a handler immediately so an early navigation failure cannot leave an unhandled rejection.
  anonymousResponse.catch(() => {});
  const navigation = await page.goto(`${frontend}/login`, { waitUntil: 'load', timeout });
  assert.equal(navigation?.status(), 200, 'Login page unavailable');
  await page.locator('input[type="email"]').waitFor({ state: 'visible' });
  await page.locator('input[type="password"]').waitFor({ state: 'visible' });
  assert.deepEqual(await page.evaluate(() => window.__AVERIO_CONFIG__), { apiUrl: api, environment }, 'Browser loaded the wrong runtime configuration');
  assert.equal((await anonymousResponse).status(), 401, 'Application did not use the expected API anonymously');

  if (environment === 'integration') {
    assert.ok(email && password, 'Integration requires smoke credentials');
    await page.locator('input[type="email"]').fill(email);
    await page.locator('input[type="password"]').fill(password);
    await page.locator('input[type="checkbox"]').uncheck();
    const [login, me] = await Promise.all([
      page.waitForResponse(response => response.url() === `${api}/api/v1/auth/login` && response.request().method() === 'POST'),
      page.waitForResponse(response => response.url() === `${api}/api/v1/auth/me` && response.status() === 200),
      page.locator('form button[type="submit"]').click(),
    ]);
    assert.equal(login.status(), 204, 'Login form did not authenticate');
    const identity = await me.json();
    assert.equal(identity.email.toLowerCase(), email.toLowerCase(), 'Unexpected authenticated identity');
    assert.equal(identity.onboardingRequired, false, 'Smoke account must complete onboarding');
    await page.waitForURL(url => url.pathname === '/app' || url.pathname === '/app/', { timeout });
    const [logout] = await Promise.all([
      page.waitForResponse(response => response.url() === `${api}/api/v1/auth/logout` && response.request().method() === 'POST'),
      page.getByRole('button', { name: 'Sign out', exact: true }).click(),
    ]);
    assert.equal(logout.status(), 204, 'Logout UI failed');
    await page.waitForURL(url => url.pathname === '/login', { timeout });
    const [afterLogout] = await Promise.all([
      page.waitForResponse(response => response.url() === `${api}/api/v1/auth/me`),
      page.reload({ waitUntil: 'load' }),
    ]);
    assert.equal(afterLogout.status(), 401, 'Session remained authenticated after logout');
    await page.locator('input[type="password"]').waitFor({ state: 'visible' });
  }
  assert.ok(apiRequests.length > 0, 'No application API traffic observed');
  assert.ok(apiRequests.every(url => new URL(url).origin === api), 'Application called an unexpected API origin');
  assert.deepEqual(errors, [], 'Browser runtime/asset errors');
}
