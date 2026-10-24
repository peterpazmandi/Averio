import assert from 'node:assert/strict';

export async function verifyLanding({ page, development }) {
    const home = 'http://localhost:5173/';
    let releaseAuth;
    const held = new Promise(resolve => { releaseAuth = resolve; });
    const holdAuth = async route => { await held; await route.abort('timedout'); };
    await page.route('**/api/v1/auth/me', holdAuth);
    try {
        const requested = page.waitForRequest('**/api/v1/auth/me');
        await page.goto(home);
        await requested;
        await page.getByRole('heading', { level: 1, name: /Your money/ }).waitFor({ timeout: 5_000 });
        assert.equal(new URL(page.url()).pathname, '/', 'A pending session cannot block public content');
    } finally {
        releaseAuth();
        await page.unrouteAll({ behavior: 'wait' });
    }

    const failAuth = route => route.fulfill({ status: 503, contentType: 'application/problem+json',
        headers: { 'access-control-allow-origin': 'http://localhost:5173', 'access-control-allow-credentials': 'true' },
        body: JSON.stringify({ title: 'Test API outage', status: 503 }) });
    await page.route('**/api/v1/auth/me', failAuth);
    try {
        // StrictMode can finish a previous document's real 401 while the next
        // document starts. Match the simulated outage, not that late response.
        const failed = page.waitForResponse(response =>
            response.url().endsWith('/api/v1/auth/me') && response.status() === 503);
        await page.goto(home);
        assert.equal((await failed).status(), 503);
        await page.getByRole('heading', { level: 1, name: /Your money/ }).waitFor();
        assert.equal(new URL(page.url()).pathname, '/');
    } finally {
        await page.unroute('**/api/v1/auth/me', failAuth);
    }

    for (const width of [320, 1440]) for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await page.setViewportSize({ width, height: 900 });
        await page.locator('header select').selectOption(language);
        if (await page.locator('html').evaluate(el => el.classList.contains('dark')) !== (theme === 'dark')) {
            await page.locator('header > div button').first().click();
        }
        await page.waitForFunction(dark => document.documentElement.classList.contains('dark') === dark, theme === 'dark');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);

        await page.evaluate(() => window.scrollTo({ top: 1000, behavior: 'instant' }));
        await page.waitForFunction(() => window.scrollY > 900);
        const header = await page.locator('header').boundingBox();
        assert.ok(Math.abs(header.y) <= 1, `Header must remain visible at ${width}px, ${language}, ${theme}`);

        await page.locator('button[aria-pressed]').nth(1).click();
        await page.getByText('€312.00', { exact: true }).waitFor();
        await page.locator('button[aria-pressed]').first().click();

        const ratios = await page.locator('a[href="/register"], button[aria-pressed="true"]').evaluateAll(elements => {
            const luminance = color => {
                const rgb = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => v / 255);
                return rgb.map(v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)
                    .reduce((sum, value, i) => sum + value * [0.2126, 0.7152, 0.0722][i], 0);
            };
            return elements.map(el => {
                const style = getComputedStyle(el);
                const a = luminance(style.color), b = luminance(style.backgroundColor);
                return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
            });
        });
        assert.ok(ratios.every(ratio => ratio >= 4.5), `CTA and selected-view contrast: ${ratios}`);
    }

    await page.setViewportSize({ width: 390, height: 900 });
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
    await page.locator('button[aria-controls="landing-mobile-nav"]').click();
    await page.locator('#landing-mobile-nav a[href="#features"]').click();
    await page.locator('#landing-mobile-nav').waitFor({ state: 'hidden' });
    await page.waitForFunction(() => {
        const top = document.getElementById('features').getBoundingClientRect().top;
        return top >= document.querySelector('header').getBoundingClientRect().bottom && top <= 100;
    });
    await page.locator('#faq summary').first().click();
    assert.equal(await page.locator('#faq details').first().evaluate(el => el.open), true);

    // Keep the shared session predictable for the remainder of the fixture.
    await page.locator('header select').selectOption('en');
    console.log(`PASS landing (${development ? 'development StrictMode' : 'production'}): pending/failed auth, sticky navigation, EN/DE/HU, 320px/desktop, themes, contrast and preview`);
}
