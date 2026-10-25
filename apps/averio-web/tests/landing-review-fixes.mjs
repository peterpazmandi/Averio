import assert from 'node:assert/strict';

export async function verifyLandingReviewFixes(browser, url, artifacts) {
    for (const language of ['en', 'de', 'hu']) {
        const page = await browser.newPage({ viewport: { width: 568, height: 320 } });
        await page.addInitScript(language => localStorage.setItem('i18nextLng', language), language);
        await page.route('**/api/v1/**', route => route.fulfill({ status: 401, body: '{}' }));
        for (const path of ['/', '/contact']) {
            await page.goto(url + path);
            await page.locator('h1').waitFor();
            await page.locator('[aria-controls="landing-mobile-nav"]').click();
            const nav = page.locator('#landing-mobile-nav');
            assert.ok(await nav.evaluate(el => el.scrollHeight > el.clientHeight), 'Landscape menu must scroll');
            await nav.locator('a[href="/register"]').scrollIntoViewIfNeeded();
            const bounds = await nav.locator('a[href="/register"]').boundingBox();
            assert.ok(bounds.y >= 0 && bounds.y + bounds.height <= 320, 'Registration is visible after scrolling');
            await page.screenshot({ path: `${artifacts}/landscape-${path === '/' ? 'landing' : 'contact'}-${language}.png` });
            await nav.locator('a[href="/register"]').click();
            await page.waitForURL('**/register');
        }
        await page.close();
    }

    for (const onboardingRequired of [false, true]) {
        const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
        await page.route('**/api/v1/**', route => route.fulfill({
            status: 200, contentType: 'application/json',
            body: JSON.stringify(route.request().url().endsWith('/auth/me') ? {
                userId: 'review', email: 'review@example.com', displayName: 'Review', emailVerified: true,
                onboardingRequired, household: { householdId: 'household', name: 'Review', role: 'Owner' },
            } : []),
        }));
        const session = page.waitForResponse('**/api/v1/auth/me');
        await page.goto(url + '/contact');
        await session;
        await page.locator('main a[href="/#faq"]').click();
        await page.locator('#faq summary').first().click();
        assert.equal(new URL(page.url()).pathname, '/');
        assert.equal(new URL(page.url()).hash, '#faq');
        assert.ok(await page.locator('#faq details').first().evaluate(el => el.open));
        await page.reload();
        await page.locator('#faq').waitFor();
        await page.locator('header a[href="/#features"]').click();
        await page.waitForURL('**/#features');
        await page.locator('#features').waitFor();
        // The normal root entry point must still redirect signed-in users.
        await page.goto(url + '/');
        await page.waitForURL(onboardingRequired ? '**/onboarding' : '**/app');
        await page.close();
    }

    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.addInitScript(() => {
        localStorage.setItem('averio.theme', 'light');
        localStorage.setItem('i18nextLng', 'en');
    });
    await page.route('**/api/v1/**', route => route.fulfill({ status: 401, body: '{}' }));
    await page.goto(url + '/');
    await page.locator('h1').waitFor();
    const ratios = await page.evaluate(() => {
        const ctx = document.createElement('canvas').getContext('2d');
        const rgba = color => {
            ctx.clearRect(0, 0, 1, 1);
            ctx.fillStyle = color;
            ctx.fillRect(0, 0, 1, 1);
            return [...ctx.getImageData(0, 0, 1, 1).data];
        };
        const luminance = rgb => rgb.slice(0, 3).map(v => v / 255)
            .map(v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)
            .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
        return [...document.querySelectorAll('main span, main p, main strong')]
            .filter(el => ['This month', '€5,480', 'Finance that makes sense', 'app.averio · Illustrative preview'].includes(el.textContent))
            .map(el => {
                const parents = []; for (let node = el; node; node = node.parentElement) parents.unshift(node);
                let background = [255, 255, 255];
                for (const node of parents) {
                    const [r, g, b, alpha] = rgba(getComputedStyle(node).backgroundColor);
                    background = [r, g, b].map((v, i) => v * alpha / 255 + background[i] * (1 - alpha / 255));
                }
                const fg = luminance(rgba(getComputedStyle(el).color));
                const bg = luminance(background);
                return { text: el.textContent, ratio: (Math.max(fg, bg) + 0.05) / (Math.min(fg, bg) + 0.05) };
            });
    });
    assert.equal(ratios.length, 4, 'All reviewed informational text samples must be checked');
    for (const { text, ratio } of ratios) assert.ok(ratio >= 4.5, `${text}: ${ratio.toFixed(2)} must meet 4.5:1`);
    console.log('PASS review fixes: landscape registration, authenticated FAQ/root redirects, text contrast', ratios);
    await page.close();
}
