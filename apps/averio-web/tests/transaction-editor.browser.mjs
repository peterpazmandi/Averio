import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import vm from 'node:vm';
const root = process.cwd();
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5274', '--strictPort'], { cwd: root, windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5274/tests/fixtures/transaction-editor.html';
const artifacts = path.resolve(root, '../../test-results/personal-money');
fs.mkdirSync(artifacts, { recursive: true });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const context = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/personalMoney.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, context);
let browser;
try {
    for (let i = 0; i < 80; i++) { try { if ((await fetch(url)).ok) break; } catch {} await pause(100); }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) for (const width of [320, 375, 1440]) {
        const copy = context.exports['personalMoney' + language.toUpperCase()];
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [];
        let editing = false;
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        await page.route('**/api/v1/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (pathname.endsWith('/account-owner-options')) return json({ currentMemberId: 'member', members: [] });
            if (pathname.endsWith('/accounts')) return json([
                { id: 'private', name: 'Private', currency: 'EUR', scope: 'Personal', visibility: 'Private', includeInHouseholdReports: false, status: 'Active', owners: [{ householdMemberId: 'member' }] },
                { id: 'shared', name: 'Shared', currency: 'EUR', scope: 'Household', visibility: 'Household', includeInHouseholdReports: true, status: 'Active', owners: [] }]);
            if (pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR', available: '100',
                entries: editing ? ['-0.3', '0.1'].map((amount, i) => ({ id: 'entry-' + i, amount,
                    kind: 'SharedCharge', transactionId: 'receipt', transactionItemId: 'stable-item' })) : [], charges: [] });
            if (pathname.endsWith('/categories')) return json([{ id: 'category', name: 'Other', systemKey: 'uncategorized', kind: 'Expense', isArchived: false, children: [] }]);
            return json([]);
        });
        await page.goto(url);
        const disclosures = page.locator('button[aria-controls][aria-expanded]');
        assert.equal(await disclosures.count(), 3);
        for (const d of await disclosures.all()) assert.equal(await d.getAttribute('aria-expanded'), 'false');
        const account = page.locator('select').filter({ has: page.locator('option[value="shared"]') });
        await account.selectOption('shared');
        await page.locator('input[required][maxlength="160"]').fill('Mixed receipt');
        await page.locator('input[inputmode="decimal"]').first().fill('12.50');
        const checkbox = page.getByRole('checkbox', { name: copy.itemCharge, exact: true });
        const allowanceButton = page.getByRole('button', { name: copy.itemCharge + ' · Mixed receipt', exact: true });
        assert.equal(await checkbox.count(), 0, 'Allowance options take no extra form row while closed.');
        await allowanceButton.click();
        await checkbox.waitFor();
        assert.equal(await checkbox.isDisabled(), false, 'Shared household purchases must support Personal Money item allocation.');
        await checkbox.check();
        await page.locator('input[inputmode="decimal"]').nth(1).fill('5.00');
        await page.screenshot({ path: path.join(artifacts, 'allowance-popup-' + language + '-' + dark + '-' + width + '.png'), fullPage: true });
        await page.keyboard.press('Escape');
        assert.equal(await allowanceButton.evaluate(button => button === document.activeElement), true, 'Closing allowance settings restores keyboard focus.');
        await page.locator('button[type="submit"][form="transaction-editor"]').click();
        await page.waitForFunction(() => !!document.body.dataset.saved);
        const saved = await page.evaluate(() => JSON.parse(document.body.dataset.saved));
        assert.equal(saved.chargePersonalMoney, false, 'Item allocations must not create whole-transaction double charges.');
        assert.equal(saved.items[0].personalMoneyAmount, '5.00');
        assert.equal(saved.items[0].amount, '12.50');
        assert.equal(saved.visibility, 'Household');
        await account.selectOption('private');
        await allowanceButton.click();
        assert.equal(await checkbox.isDisabled(), false, 'Private account purchases must support the same item controls.');
        await checkbox.check();
        await page.locator('input[inputmode="decimal"]').nth(1).fill('2.00');
        await page.keyboard.press('Escape');
        await page.locator('button[type="submit"][form="transaction-editor"]').click();
        await page.waitForFunction(() => JSON.parse(document.body.dataset.saved).items[0].personalMoneyAmount === '2.00');
        const privateCharge = await page.evaluate(() => JSON.parse(document.body.dataset.saved));
        assert.equal(privateCharge.items[0].personalMoneyAmount, '2.00');
        assert.equal(privateCharge.visibility, 'Private');
        await disclosures.nth(2).click();
        await page.locator('textarea').fill('Keep my note');
        await disclosures.nth(2).click();
        assert.ok((await disclosures.nth(2).textContent()).includes('Keep my note'));
        await page.screenshot({ path: path.join(artifacts, 'editor-compact-' + language + '-' + dark + '-' + width + '.png'), fullPage: true });
        await disclosures.nth(0).click();
        await page.locator('input[type="checkbox"]:visible').last().check();
        assert.equal(await checkbox.count(), 0, 'Recurring templates must not offer posted-item wallet charges.');
        assert.equal(await allowanceButton.count(), 0);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, 'editor-' + language + '-' + dark + '-' + width + '.png'), fullPage: true });
        editing = true;
        await page.addInitScript(() => {
            window.editorFixture = { id: 'receipt', accountId: 'shared', accountName: 'Shared', currency: 'EUR',
                kind: 'Expense', visibility: 'Household', amount: '12.50', description: 'Receipt', merchant: 'Shop',
                note: null, occurredOn: '2026-10-20', status: 'Posted', createdAt: '2026-10-20T12:00:00Z',
                updatedAt: '2026-10-20T12:00:00Z', canModify: true, labels: [], activityId: null,
                activityName: null, activityAssignmentVersion: '00000000-0000-0000-0000-000000000000',
                items: [{ id: 'stable-item', position: 0, description: 'Existing item', categoryId: 'category',
                    categoryName: 'Other', categoryIcon: 'receipt', amount: '12.50' }] };
        });
        await page.reload();
        const existingAllowanceButton = page.getByRole('button', { name: copy.itemCharge + ' · Existing item', exact: true });
        await existingAllowanceButton.click();
        await page.waitForFunction(() => [...document.querySelectorAll('input[inputmode="decimal"]')].some(i => i.value === '0.2'));
        assert.equal(await checkbox.isChecked(), true, 'Only the current member ledger supplies the existing allocation.');
        await page.keyboard.press('Escape');
        await page.locator('button[type="submit"][form="transaction-editor"]').click();
        await page.waitForFunction(() => !!document.body.dataset.saved);
        const retained = await page.evaluate(() => JSON.parse(document.body.dataset.saved));
        assert.equal(retained.items[0].existingItemId, 'stable-item');
        assert.equal(retained.items[0].personalMoneyAmount, null, 'Untouched allocations must remain server-owned.');
        await existingAllowanceButton.click();
        await checkbox.uncheck();
        await page.keyboard.press('Escape');
        await page.locator('button[type="submit"][form="transaction-editor"]').click();
        await page.waitForFunction(() => JSON.parse(document.body.dataset.saved).items[0].personalMoneyAmount === '0');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log("PASS item-level Personal Money on shared/private receipts: EN/DE/HU, desktop/mobile, light/dark");
} finally { await browser?.close(); server.kill(); }
