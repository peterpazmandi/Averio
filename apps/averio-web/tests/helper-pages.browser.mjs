import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
const port = 5287;
const origin = 'http://localhost:' + port;
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', String(port), '--strictPort'], { windowsHide: true, stdio: 'ignore' });
const artifacts = path.resolve('../../test-results/info-popover');
fs.mkdirSync(artifacts, { recursive: true });
let browser;
try {
    for (let i = 0; i < 100; i++) {
        try { if ((await fetch(origin)).ok) break; } catch {}
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const width of [375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        await page.route('**/api/v1/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            let json = [];
            if (pathname.endsWith('/auth/me')) json = { userId: '11111111-1111-4111-8111-111111111111', email: 'owner@example.test', displayName: 'Alex', emailVerified: true, onboardingRequired: false, household: { householdId: '22222222-2222-4222-8222-222222222222', role: 'Owner' } };
            else if (pathname.endsWith('/dashboard')) json = { displayName: 'Alex', reportingCurrency: 'EUR', householdBalances: [], accounts: [], cashFlow: [], latestTransactions: [] };
            else if (/statistics\/(cash-flow|category-cash-flow)$/.test(pathname)) json = { periods: [] };
            else if (pathname.endsWith('/transactions')) json = { items: [], totalCount: 0, page: 1, pageSize: 50 };
            else if (pathname.endsWith('/transfers')) json = { items: [], totalCount: 0, page: 1, pageSize: 50 };
            else if (pathname.endsWith('/account-owner-options')) json = { currentMemberId: 'member', members: [] };
            else if (pathname.endsWith('/personal-money/me')) json = null;
            else if (/\/households\/[^/]+$/.test(pathname)) json = { id: '22222222-2222-4222-8222-222222222222', name: 'Averio household', countryCode: 'HU', preferredCurrency: 'EUR', role: 'owner', version: '1', members: [] };
            await route.fulfill({ json });
        });
        for (const slug of ['', 'accounts', 'transactions', 'transfers', 'activities', 'categories', 'statistics', 'recurring', 'household']) {
            await page.goto(origin + '/app' + (slug ? '/' + slug : ''));
            const trigger = page.locator('button[popovertarget]').first();
            await trigger.waitFor();
            const heading = trigger.locator('xpath=ancestor::*[self::h2 or self::h3 or self::h4][1]');
            const headingName = await heading.getAttribute('aria-label');
            const headingLevel = await heading.evaluate(element => Number(element.tagName.slice(1)));
            assert.ok(headingName, slug + ' keeps a dedicated heading name');
            await page.getByRole('heading', { name: headingName, level: headingLevel, exact: true }).waitFor();
            await trigger.click();
            const popup = page.locator('[popover]:popover-open');
            await popup.waitFor();
            await page.getByRole('heading', { name: headingName, level: headingLevel, exact: true }).waitFor();
            await page.waitForTimeout(80);
            assert.ok((await popup.textContent()).trim().length > 10);
            const box = await popup.boundingBox();
            assert.ok(box.x >= 0 && box.x + box.width <= width + 1);
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, slug + ' page overflow');
            await page.screenshot({ path: path.join(artifacts, 'page-' + (slug || 'dashboard') + '-' + width + '.png'), fullPage: true });
            await page.keyboard.press('Escape');
            assert.equal(await popup.count(), 0);
            assert.deepEqual(errors, []);
        }
        await page.close();
    }
    console.log('PASS 9 application routes at mobile and desktop widths with mocked empty-data API responses.');
} finally { await browser?.close(); server.kill(); }
