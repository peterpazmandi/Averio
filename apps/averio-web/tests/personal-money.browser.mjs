import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import vm from 'node:vm';
const root = process.cwd();
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5273', '--strictPort'], { cwd: root, windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5273/tests/fixtures/personal-money.html';
const artifacts = path.resolve(root, '../../test-results/personal-money');
fs.mkdirSync(artifacts, { recursive: true });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const context = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/personalMoney.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, context);
let browser;
try {
    for (let i = 0; i < 80; i++) { try { if ((await fetch(url)).ok) break; } catch {} await pause(100); }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) {
        const copy = context.exports['personalMoney' + language.toUpperCase()];
        const page = await browser.newPage({ viewport: { width: dark ? 390 : 1440, height: 900 } });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        let amount = '25', version = 1, role = 'owner';
        const writes = [], creations = [];
        await page.route('**/api/v1/**', async route => {
            const request = route.request(); const pathname = new URL(request.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (request.method() === 'OPTIONS') return route.fulfill({ status: 204 });
            if (pathname.endsWith('/security/csrf')) return json({ token: 'test' });
            if (pathname.endsWith('/personal-money/me/charges/purchase') && request.method() === 'PUT') {
                const body = request.postDataJSON(); writes.push(body);
                assert.equal(body.expectedAmount, amount); assert.equal(body.expectedVersion, version);
                amount = body.amount; version++;
                return route.fulfill({ status: 204 });
            }
            if (pathname.endsWith('/personal-money/me/charges') && request.method() === 'POST') {
                creations.push(request.postDataJSON());
                return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Retry test' }) });
            }
            if (pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR', available: String(100 - Number(amount)), attributedPrivateFunding: '0', fundingDifference: '100', entries: [], charges: amount === '0' ? [] : [{ transactionId: 'purchase', amount, version }] });
            if (pathname.endsWith('/households/home')) {
                await pause(150); // Direct hashes must wait until the target is mounted.
                return json({ id: 'home', name: 'Test household', countryCode: 'HU', preferredCurrency: 'EUR', onboardingCompleted: true, role, version: '1', members: Array.from({ length: 6 }, (_, i) => ({ id: 'member' + i, displayName: 'Member ' + i, isCurrentUser: i === 0, role: i === 0 ? 'owner' : 'member', joinedAtUtc: '2026-01-01T00:00:00Z' })) });
            }
            if (pathname.endsWith('/accounts')) return json([{ id: 'private', scope: 'Personal', visibility: 'Private', owners: [{ householdMemberId: 'member0' }] }]);
            if (pathname.endsWith('/transactions')) return json({ items: [{ id: 'new-purchase', accountId: 'private', visibility: 'Private', status: 'Posted', kind: 'Expense', amount: '20', currency: 'EUR', occurredOn: '2026-10-01', merchant: 'Retry purchase', items: [], labels: [] }], totalCount: 1, page: 1, pageSize: 50 });
            return json([]);
        });
        await page.goto(url);
        const link = page.locator('a[href="#household-personal-money"]');
        await link.click();
        const checkPosition = async () => {
            await page.waitForFunction(() => {
                const section = document.getElementById('household-personal-money');
                const top = section?.getBoundingClientRect().top;
                return top !== undefined && top >= 78 && top < 120;
            });
        };
        await checkPosition();
        await page.evaluate(() => window.scrollTo(0, 0)); await link.click(); await checkPosition();
        await page.reload(); await checkPosition();
        await page.getByRole('heading', { name: copy.myWallet, exact: true }).waitFor();
        await page.getByRole('button', { name: copy.tabs.owner, exact: true }).click();
        assert.equal(await page.getByRole('heading', { name: copy.newGrant, exact: true }).count(), 1);
        await page.getByRole('button', { name: copy.tabs.charges, exact: true }).click();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, `${language}-${dark ? 'dark-mobile' : 'light-desktop'}.png`), fullPage: true });
        await page.getByLabel(copy.changedAmount, { exact: true }).fill('10');
        await page.getByRole('button', { name: copy.saveCharge, exact: true }).click();
        await page.waitForFunction(() => document.querySelector('input[inputmode="decimal"]') !== null);
        await page.getByRole('button', { name: copy.removeCharge, exact: true }).click();
        await page.getByRole('button', { name: copy.confirmUnlink, exact: true }).click();
        await page.getByText(copy.noCharges, { exact: true }).waitFor();
        assert.equal(writes.length, 2); assert.equal(writes[1].amount, '0');
        assert.notEqual(writes[0].operationId, writes[1].operationId);
        const picker = page.locator('select').filter({ has: page.locator('option[value="new-purchase"]') });
        await picker.selectOption('new-purchase');
        const submit = page.getByRole('button', { name: copy.charge, exact: true });
        for (let i = 0; i < 2; i++) {
            await Promise.all([page.waitForResponse(r => r.url().endsWith('/personal-money/me/charges') && r.status() === 503), submit.click()]);
            await submit.waitFor({ state: 'visible' });
        }
        assert.equal(creations.length, 2);
        assert.ok(creations[0].operationId);
        assert.deepEqual(creations[0], creations[1]);
        await picker.locator('xpath=ancestor::form').locator('input').fill('15');
        await Promise.all([page.waitForResponse(r => r.url().endsWith('/personal-money/me/charges') && r.status() === 503), submit.click()]);
        assert.notEqual(creations[1].operationId, creations[2].operationId);
        role = 'member'; await page.reload();
        await page.getByRole('heading', { name: copy.myWallet, exact: true }).waitFor();
        assert.equal(await page.getByRole('heading', { name: copy.newGrant, exact: true }).count(), 0);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS personal money: anchor/reload/repeated click, sticky offsets, EN/DE/HU, desktop/mobile/light/dark, owner separation, correction and unlink');
} finally { await browser?.close(); server.kill(); }
