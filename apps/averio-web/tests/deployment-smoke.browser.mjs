import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import { verifyBrowser } from '../../../scripts/release/browser-checks.mjs';

const frontend = 'http://localhost:5299', api = 'https://api.example.test';
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', 'preview', '--host', 'localhost', '--port', '5299', '--strictPort'], { windowsHide: true, stdio: 'ignore' });
let browser;
try {
    for (let attempt = 0; attempt < 100; attempt++) {
        try { if ((await fetch(frontend)).ok) break; } catch {}
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const scenario of ['production', 'integration', 'wrong-runtime', 'broken-entry']) {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const environment = scenario === 'integration' ? 'integration' : 'production';
        const posts = [];
        let signedIn = false;
        await page.route('**/runtime-config.js', route => route.fulfill({ contentType: 'application/javascript',
            body: `window.__AVERIO_CONFIG__ = ${JSON.stringify({ apiUrl: scenario === 'wrong-runtime' ? 'https://wrong.example.test' : api, environment })};` }));
        if (scenario === 'broken-entry') await page.route('**/assets/*.js', route => route.fulfill({ status: 503, body: 'unavailable' }));
        await page.route('**/api/v1/**', async route => {
            const request = route.request(), path = new URL(request.url()).pathname;
            if (request.method() === 'POST') posts.push(path);
            if (path.endsWith('/auth/login')) {
                assert.equal(request.postDataJSON().email, 'smoke@example.test');
                assert.ok(request.headers()['x-csrf-token']);
                signedIn = true; return route.fulfill({ status: 204 });
            }
            if (path.endsWith('/auth/logout')) { signedIn = false; return route.fulfill({ status: 204 }); }
            if (path.endsWith('/auth/me')) return route.fulfill(signedIn ? { json: {
                userId: '11111111-1111-4111-8111-111111111111', email: 'smoke@example.test', displayName: 'Smoke',
                emailVerified: true, onboardingRequired: false, household: { householdId: '22222222-2222-4222-8222-222222222222', role: 'owner' },
            } } : { status: 401, json: {} });
            let json = [];
            if (path.endsWith('/security/csrf')) json = { token: 'test-token' };
            else if (path.endsWith('/onboarding')) json = { completed: true, currentStep: 'completed', displayName: 'Smoke', household: { name: 'Test', preferredCurrency: 'EUR' } };
            else if (path.endsWith('/dashboard')) json = { displayName: 'Smoke', reportingCurrency: 'EUR', householdBalances: [], accounts: [], cashFlow: [], latestTransactions: [] };
            else if (/statistics\/(cash-flow|category-cash-flow)$/.test(path)) json = { periods: [] };
            else if (path.endsWith('/personal-money/me')) json = null;
            await route.fulfill({ json });
        });
        const check = () => verifyBrowser({ page, frontend, api, environment, email: 'smoke@example.test', password: 'fixture-only', timeout: 10000 });
        if (scenario === 'wrong-runtime' || scenario === 'broken-entry') await assert.rejects(check());
        else await check();
        assert.deepEqual(posts, scenario === 'integration' ? ['/api/v1/auth/login', '/api/v1/auth/logout'] : []);
        await page.close();
    }
    console.log('PASS real login/logout UI, anonymous production rendering, and negative runtime/entry asset cases');
} finally { await browser?.close(); server.kill(); }
