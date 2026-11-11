import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';

const origin = 'http://localhost:5298';
// Test the already built production assets, not Vite's development transforms.
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', 'localhost', '--port', '5298', '--strictPort'], { windowsHide: true, stdio: 'ignore' });
let browser;
try {
    for (let attempt = 0; attempt < 100; attempt++) {
        try { if ((await fetch(origin)).ok) break; } catch {}
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const environment of ['integration', 'production']) {
        const page = await browser.newPage();
        const api = `https://${environment}-api.example.test`;
        const requested = [];
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        await page.route('**/runtime-config.js', route => route.fulfill({
            contentType: 'application/javascript',
            body: `window.__AVERIO_CONFIG__ = ${JSON.stringify({ apiUrl: api, environment })};`,
        }));
        await page.route('**/api/v1/**', route => {
            requested.push(route.request().url());
            return route.fulfill({ status: 401, json: {} });
        });
        await page.goto(origin + '/login');
        await page.locator('input[type="password"]').waitFor();
        assert.ok(requested.some(url => url.startsWith(api + '/')), 'Runtime API origin was not used');
        assert.ok(requested.every(url => url.startsWith(api + '/')), 'Build-time API origin leaked into requests');
        assert.equal(await page.getByRole('status').filter({ hasText: /^Integration$/ }).count(), environment === 'integration' ? 1 : 0);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS same production build uses each runtime API origin and toggles integration banner');
} finally { await browser?.close(); server.kill(); }
