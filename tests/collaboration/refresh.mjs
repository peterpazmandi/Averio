import assert from 'node:assert/strict';

export async function verifyRefresh({ session, email, password, development }) {
    const page = await session(email, password);
    // Delay the real API response so the session check overlaps StrictMode replay.
    // No identity or authorization response is mocked.
    await page.route('**/api/v1/auth/me', async route => {
        const response = await route.fetch();
        await new Promise(resolve => setTimeout(resolve, 150));
        await route.fulfill({ response });
    });
    for (const route of ['/app', '/app/accounts', '/app/statistics', '/']) {
        await page.goto('http://localhost:5173' + route);
        await page.getByRole('button', { name: 'Sign out', exact: true }).waitFor();
        await page.reload();
        await page.getByRole('button', { name: 'Sign out', exact: true }).waitFor();
        assert.equal(await page.getByText('Something went wrong!', { exact: true }).count(), 0);
        assert.equal(await page.getByText('CancelledError', { exact: true }).count(), 0);
    }
    assert.equal(await page.locator('script[src*="/src/main.tsx"]').count(), development ? 1 : 0);
    await page.getByRole('button', { name: 'Sign out', exact: true }).click();
    await page.waitForURL('**/login');
    for (const route of ['/app', '/']) {
        await page.goto('http://localhost:5173' + route);
        await page.waitForURL(url => url.pathname === '/login');
        await page.locator('input[type=email]').waitFor();
        assert.equal(await page.getByText('CancelledError', { exact: true }).count(), 0);
    }
    await page.unroute('**/api/v1/auth/me');
    console.log(`PASS refresh (${development ? 'development StrictMode' : 'production'}): signed-in cold loads/reloads, root redirect, signed-out redirect`);
}
