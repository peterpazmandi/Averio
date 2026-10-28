import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import vm from 'node:vm';

const root = process.cwd();
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5281', '--strictPort'], { cwd: root, stdio: 'ignore' });
const url = 'http://localhost:5281/tests/fixtures/receipts.html';
const artifacts = path.resolve(root, '../../test-results/receipts');
fs.mkdirSync(artifacts, { recursive: true });
const copyModule = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/receipts.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, copyModule);
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=', 'base64');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let browser;
try {
    for (let i = 0; i < 80; i++) { try { if ((await fetch(url)).ok) break; } catch {} await pause(100); }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) for (const width of [375, 1440]) {
        const copy = copyModule.exports['receipts' + language.toUpperCase()];
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [], uploads = [], posts = [];
        let failInbox = true, failUpload = true, failPost = true, enabled = true;
        let uploaded = false, discarded = false, recorded = false;
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        const ready = () => ({ id: 'scan', status: recorded ? 'Posted' : 'Ready', version: 'version', attempts: 1,
            createdAt: '2026-10-28T08:00:00Z', expiresAt: '2026-11-04T08:00:00Z', failureCode: null,
            postedTransactionId: recorded ? 'transaction' : null,
            draft: recorded ? null : { merchant: 'Shop <script>', date: null, currency: 'EUR', total: '9.00',
                lines: [{ description: 'Food Apples', amount: '10.00', kind: 'Item' }, { description: 'Discount', amount: '-1.00', kind: 'Discount' },
                    { description: 'Card', amount: '9.00', kind: 'Payment' }], warnings: ['review_required', 'date_missing'] } });
        const queued = () => ({ ...ready(), id: 'new-scan', status: discarded ? 'Discarded' : 'Queued', draft: null, postedTransactionId: null });
        await page.route('**/api/v1/**', async route => {
            const req = route.request(), pathname = new URL(req.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            const fail = status => route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Please retry', detail: 'Please retry', status }) });
            if (req.method() === 'OPTIONS') return route.fulfill({ status: 204 });
            if (pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (pathname.endsWith('/receipt-scans/capabilities')) return json({ enabled, maxUploadBytes: 10485760 });
            if (pathname.endsWith('/receipt-scans/scan/image')) return route.fulfill({ status: 200, contentType: 'image/png', body: png });
            if (pathname.endsWith('/receipt-scans/scan/transaction')) {
                posts.push(req.postDataJSON());
                if (failPost) { failPost = false; return fail(409); }
                recorded = true; return json({ id: 'transaction' });
            }
            if (pathname.endsWith('/receipt-scans/new-scan') && req.method() === 'DELETE') {
                assert.equal(new URL(req.url()).searchParams.get('expectedVersion'), 'version');
                discarded = true; return route.fulfill({ status: 204 });
            }
            if (pathname.endsWith('/receipt-scans')) {
                if (req.method() === 'POST') {
                    uploads.push(req.postDataBuffer().toString());
                    if (failUpload) { failUpload = false; return fail(503); }
                    uploaded = true; return json(queued());
                }
                if (failInbox) { failInbox = false; return fail(503); }
                return json([ready(), ...(uploaded ? [queued()] : [])]);
            }
            if (pathname.endsWith('/account-owner-options')) return json({ currentMemberId: 'member', members: [] });
            if (pathname.endsWith('/accounts')) return json([
                { id: 'eur', name: 'EUR bank', currency: 'EUR', scope: 'Household', visibility: 'Household', includeInHouseholdReports: true, status: 'Active', owners: [] },
                { id: 'huf', name: 'HUF bank', currency: 'HUF', scope: 'Household', visibility: 'Household', includeInHouseholdReports: true, status: 'Active', owners: [] }]);
            if (pathname.endsWith('/categories')) return json([
                { id: 'uncategorized', name: 'Other', systemKey: 'uncategorized', kind: 'Expense', isArchived: false },
                { id: 'food', name: 'Food', systemKey: null, kind: 'Expense', isArchived: false }]);
            if (pathname.endsWith('/personal-money/me')) return route.fulfill({ status: 204 });
            return json([]);
        });
        await page.goto(url);
        await page.getByRole('button', { name: copy.retry, exact: true }).click();
        await page.getByRole('button', { name: copy.review, exact: true }).click();
        const confirm = page.getByRole('button', { name: copy.confirm, exact: true });
        assert.equal(await confirm.isDisabled(), true);
        assert.equal(await page.locator('input[type="date"]').inputValue(), '', 'Missing dates must not silently become today.');
        const account = page.locator('select').filter({ has: page.locator('option[value="eur"]') });
        assert.equal(await account.inputValue(), '', 'Receipt review requires an explicit account selection.');
        assert.equal(await account.locator('option[value="huf"]').count(), 0, 'No implicit currency conversion.');
        await page.getByRole('checkbox', { name: copy.reviewConsent }).check();
        assert.equal(await confirm.isDisabled(), true, 'A mismatched item total cannot be confirmed.');
        await page.getByRole('button', { name: copy.suggestion.replace('{{category}}', 'Food'), exact: true }).click();
        await page.locator('input[id$="-amount"]').fill('9.00');
        await page.locator('input[type="date"]').fill('2026-10-28');
        await account.selectOption('eur');
        await page.getByText(copy.observations, { exact: true }).click();
        assert.ok((await page.locator('details').textContent()).includes('Discount'));
        await page.screenshot({ path: path.join(artifacts, `review-${language}-${dark}-${width}.png`), fullPage: true });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await confirm.click();
        await page.getByRole('alert').filter({ hasText: 'Please retry' }).waitFor();
        assert.equal(await page.locator('input[id$="-amount"]').inputValue(), '9.00', 'Posting failure retains the reviewed form.');
        await confirm.click();
        await page.getByRole('link', { name: copy.openTransaction, exact: true }).waitFor();
        assert.equal(posts.length, 2);
        assert.deepEqual(posts[0], posts[1], 'Retry uses the same draft and reviewed values.');
        assert.equal(posts[1].currency, 'EUR');
        assert.equal(posts[1].transaction.items.length, 1, 'Discount and payment lines cannot become extra expenses.');
        assert.equal(posts[1].transaction.items[0].categoryId, 'food');
        assert.equal(posts[1].reviewedTotal, '9.00');
        await page.locator('input[type="file"]').setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: png });
        await page.getByRole('button', { name: copy.upload, exact: true }).click();
        await page.getByRole('button', { name: copy.retryUpload, exact: true }).click();
        await page.getByText(copy.status.Queued, { exact: true }).waitFor();
        const operation = body => /name="operationId"\r\n\r\n([^\r]+)/.exec(body)?.[1];
        assert.equal(uploads.length, 2);
        assert.ok(operation(uploads[0]));
        assert.equal(operation(uploads[0]), operation(uploads[1]), 'Upload retry must reuse its operation ID.');
        await page.getByRole('button', { name: copy.discard, exact: true }).click();
        await page.getByRole('button', { name: copy.discard, exact: true }).last().click();
        await page.getByText(copy.status.Discarded, { exact: true }).waitFor();
        assert.equal(posts.length, 2, 'Upload and discard never post financial records.');
        enabled = false;
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByText(copy.disabled, { exact: true }).waitFor();
        assert.equal(await page.locator('input[type="file"]').isDisabled(), true);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS receipt inbox, reconciliation, categories, posting/upload retry, discard, disabled state and EN/DE/HU mobile/desktop themes');
} finally { await browser?.close(); server.kill(); }
