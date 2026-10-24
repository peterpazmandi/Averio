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
        let reconciliation = false, failIncome = true, refunded = false;
        let itemLinked = false;
        const refundWrites = [];
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
            if (reconciliation && pathname.endsWith('/personal-money/me/refunds')) {
                refundWrites.push(request.postDataJSON()); refunded = true;
                return route.fulfill({ status: 204 });
            }
            if (reconciliation && pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR', available: '60', attributedPrivateFunding: '60', fundingDifference: '0',
                entries: [{ id: 'used-event', kind: 'PrivateRefund', amount: '60', transactionId: 'used-income', relatedTransactionId: 'purchase', occurredOn: '2026-10-01', recordedAt: '2026-10-01T00:00:00Z' },
                    ...(refunded ? [{ id: 'new-event', kind: 'PrivateRefund', amount: '40', transactionId: 'free-income', relatedTransactionId: 'purchase', occurredOn: '2026-10-01', recordedAt: '2026-10-01T00:00:00Z' }] : [])],
                charges: [{ transactionId: 'purchase', amount: '100', version: 1 }] });
            if (reconciliation && pathname.endsWith('/transactions')) {
                const params = new URL(request.url()).searchParams;
                const kind = params.get('kind');
                // These eligible records are archived: normal overview requests cannot see them.
                if (params.get('includeArchived') !== 'true') return json({ items: [], hasMore: false });
                if (kind === 'Income' && failIncome) { failIncome = false; return route.fulfill({ status: 503 }); }
                const record = (id, accountId = 'private') => ({ id, accountId, isArchived: true, visibility: 'Private', status: 'Posted', kind, amount: '60', currency: 'EUR', occurredOn: '2026-10-01', createdAt: '2026-10-01T00:00:00Z', merchant: id, items: [], labels: [] });
                if (!params.has('cursorId')) return json({ items: Array.from({length:100}, (_, i) => record('newer-' + i, 'other')), hasMore: true });
                assert.equal(params.get('cursorId'), 'newer-99');
                assert.equal(params.get('cursorCreatedAt'), '2026-10-01T00:00:00Z');
                return json({ items: kind === 'Expense' ? [record('purchase')] : [record('used-income'), record('free-income')], hasMore: false });
            }
            if (pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR', available: String(100 - Number(amount)), attributedPrivateFunding: '0', fundingDifference: '100', entries: itemLinked && amount !== '0' ? [{id:'item-charge',kind:'PrivateCharge',amount:'-' + amount,transactionId:'purchase',transactionItemId:'item'}] : [], charges: amount === '0' ? [] : [{ transactionId: 'purchase', amount, version }] });
            if (pathname.endsWith('/households/home')) {
                await pause(150); // Direct hashes must wait until the target is mounted.
                return json({ id: 'home', name: 'Test household', countryCode: 'HU', preferredCurrency: 'EUR', onboardingCompleted: true, role, version: '1', members: Array.from({ length: 6 }, (_, i) => ({ id: 'member' + i, displayName: 'Member ' + i, isCurrentUser: i === 0, role: i === 0 ? 'owner' : 'member', joinedAtUtc: '2026-01-01T00:00:00Z' })) });
            }
            if (pathname.endsWith('/accounts')) return json([{ id: 'private', status: itemLinked ? 'Archived' : 'Active', scope: 'Personal', visibility: 'Private', owners: [{ householdMemberId: 'member0' }] }]);
            if (pathname.endsWith('/transactions')) return json({ items: [{ id: 'new-purchase', accountId: 'private', visibility: 'Private', status: 'Posted', kind: 'Expense', amount: '20', currency: 'EUR', occurredOn: '2026-10-01', merchant: 'Retry purchase', items: [], labels: [] }], totalCount: 1, page: 1, pageSize: 50 });
            return json([]);
        });
        await page.goto(url);
        const link = page.locator('a[href="#household-personal-money"]');
        const openMoney = () => dark
            ? page.getByRole('combobox').first().selectOption('personalMoney')
            : link.click();
        const openTab = tab => dark
            ? page.getByRole('combobox', { name: copy.sections, exact: true }).selectOption(tab)
            : page.getByRole('button', { name: copy.tabs[tab], exact: true }).click();
        await openMoney();
        const checkPosition = async () => {
            await page.waitForFunction(() => {
                const section = document.getElementById('household-personal-money');
                const top = section?.getBoundingClientRect().top;
                // Single-panel navigation can have too little content to scroll to a fixed
                // sticky-header offset; it must open the right panel and bring it into view.
                return top !== undefined && top >= 0 && top < innerHeight * 0.8;
            });
        };
        await checkPosition();
        await page.evaluate(() => window.scrollTo(0, 0)); await openMoney(); await checkPosition();
        await page.reload(); await checkPosition();
        await page.getByRole('heading', { name: copy.myWallet, exact: true }).waitFor();
        await openTab('owner');
        assert.equal(await page.getByRole('heading', { name: copy.newGrant, exact: true }).count(), 1);
        await openTab('charges');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, `${language}-${dark ? 'dark-mobile' : 'light-desktop'}.png`), fullPage: true });
        await page.getByLabel(copy.changedAmount, { exact: true }).fill('10');
        await openTab('mine');
        await openTab('charges');
        assert.equal(await page.getByLabel(copy.changedAmount, { exact: true }).inputValue(), '10');
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
        itemLinked = true; amount = '15'; await page.reload();
        await openTab('charges');
        assert.equal(await page.getByLabel(copy.changedAmount, { exact: true }).count(), 0);
        await page.getByRole('button', { name: copy.removeCharge, exact: true }).click();
        await page.getByRole('button', { name: copy.confirmUnlink, exact: true }).click();
        await page.getByText(copy.noCharges, { exact: true }).waitFor();
        assert.equal(writes.at(-1).amount, '0', 'Item allocations can be removed from Personal Money while the source account is archived.');
        reconciliation = true; await page.reload();
        await openTab('reconcile');
        const form = page.locator('form').filter({ has: page.getByRole('heading', { name: copy.refundTitle, exact: true }) });
        await form.getByRole('alert').waitFor();
        await form.getByRole('button').nth(1).click(); // Retry the failed income page.
        await form.getByRole('alert').waitFor({ state: 'hidden' });
        await form.getByRole('button').nth(0).click(); // Older purchases.
        await form.getByLabel(copy.refundOriginal, { exact: true }).selectOption('purchase');
        await form.getByRole('button').nth(0).click(); // Older income transactions.
        const incomeSelect = form.getByLabel(copy.refundIncome, { exact: true });
        await incomeSelect.locator('option[value="free-income"]').waitFor({ state: 'attached' });
        assert.equal(await incomeSelect.locator('option[value="used-income"]').count(), 0);
        await incomeSelect.selectOption('free-income');
        const apply = form.getByRole('button', { name: copy.applyRefund, exact: true });
        assert.equal(await apply.isDisabled(), true); // €60 exceeds the €40 remainder.
        await form.getByLabel(copy.amount, { exact: true }).fill('40');
        assert.equal(await apply.isEnabled(), true);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, language + (dark ? '-dark' : '-light') + '-refund.png'), fullPage: true });
        await apply.click();
        await page.getByText(copy.refundSuccess, { exact: true }).waitFor();
        assert.equal(refundWrites.length, 1); assert.equal(refundWrites[0].amount, '40');
        assert.equal(await form.locator('option[value="purchase"]').count(), 0);
        role = 'member'; await page.reload();
        await page.getByRole('heading', { name: copy.myWallet, exact: true }).waitFor();
        assert.equal(await page.getByRole('heading', { name: copy.newGrant, exact: true }).count(), 0);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS personal money: anchor/reload/repeated click, sticky offsets, EN/DE/HU, desktop/mobile/light/dark, owner separation, correction and unlink');
} finally { await browser?.close(); server.kill(); }
