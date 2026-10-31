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
const moneyCopyModule = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/personalMoney.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, moneyCopyModule);
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aXioAAAAASUVORK5CYII=', 'base64');
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
let browser;
try {
    for (let i = 0; i < 80; i++) { try { if ((await fetch(url)).ok) break; } catch {} await pause(100); }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) for (const width of [375, 1440]) {
        console.log(`Receipt browser scenario: ${language}/${dark ? 'dark' : 'light'}/${width}`);
        const copy = copyModule.exports['receipts' + language.toUpperCase()];
        const moneyCopy = moneyCopyModule.exports['personalMoney' + language.toUpperCase()];
        const hasWallet = language === 'en' && width === 375 && !dark;
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        page.setDefaultTimeout(15000);
        const errors = [], uploads = [], posts = [], manualPosts = [];
        let showFailed = false;
        let failInbox = true, failUpload = true, failPost = true, enabled = true;
        let failAccounts = language === 'en' && width === 375 && !dark;
        let uploaded = false, discarded = false, recorded = false, photoDeleted = false, monthlyUsed = 1;
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        const ready = () => ({ id: 'scan', status: recorded ? 'Posted' : 'Ready', version: 'version', attempts: 1,
            createdAt: '2026-10-28T08:00:00Z', expiresAt: '2026-11-04T08:00:00Z', failureCode: null,
            postedTransactionId: recorded ? 'transaction' : null, imageAvailable: !photoDeleted, possibleDuplicate: true, matchingReceiptId: 'original',
            draft: recorded ? null : { merchant: 'Shop <script>', date: null, currency: 'EUR', total: '9.00',
                lines: [{ description: 'FOOD APPLES', suggestedDescription: 'Food Apples', suggestedCategoryId: 'food', categorySuggestionSource: dark ? 'HouseholdRule' : 'Model', amount: '10.00', kind: 'Item' }, { description: 'Food Apples Discount', amount: '-1.00', kind: 'Discount' },
                    { description: 'Card', amount: '9.00', kind: 'Payment' }], warnings: ['review_required', 'date_missing'] } });
        const queued = () => ({ ...ready(), id: 'new-scan', status: discarded ? 'Discarded' : 'Queued', draft: null, postedTransactionId: null });
        await page.route('**/api/v1/**', async route => {
            const req = route.request(), pathname = new URL(req.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            const fail = status => route.fulfill({ status, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Please retry', detail: 'Please retry', status }) });
            if (req.method() === 'OPTIONS') return route.fulfill({ status: 204 });
            if (pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (pathname.endsWith('/receipt-scans/capabilities')) return json({ enabled, maxUploadBytes: 10485760, monthlyLimit: 10, monthlyUsed, resetsAt: "2026-11-01T00:00:00Z", postedImageRetentionDays: 365 });
            if (pathname.endsWith('/receipt-scans/scan/image') && req.method() === 'DELETE') {
                assert.equal(new URL(req.url()).searchParams.get('expectedVersion'), 'version');
                photoDeleted = true; return route.fulfill({ status: 204 });
            }
            if (pathname.endsWith('/receipt-scans/scan')) return json(ready());
            if (pathname.endsWith('/receipt-scans/original')) return json({ ...ready(), id: 'original', status: 'Posted', draft: null, postedTransactionId: 'original-transaction' });
            if (pathname.endsWith('/receipt-scans/original/image') || pathname.endsWith('/receipt-scans/failed/image')) return route.fulfill({ status: 200, contentType: 'image/png', body: png });
            if (pathname.endsWith('/transactions') && req.method() === 'POST') { manualPosts.push(req.postDataJSON()); return json({ id: 'manual-transaction' }); }
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
                    if (failUpload) {
                        // The server accepted the operation, but its response was lost.
                        failUpload = false; uploaded = true; monthlyUsed++;
                        return fail(503);
                    }
                    // Replaying the same operation returns the accepted scan without charging again.
                    return json(queued());
                }
                if (failInbox) { failInbox = false; return fail(503); }
                return json([ready(), ...(uploaded ? [queued()] : []), ...(showFailed ? [{ ...ready(), id: 'failed', status: 'Failed', draft: null, postedTransactionId: null, failureCode: 'provider_unavailable', possibleDuplicate: false, imageAvailable: true }] : [])]);
            }
            if (pathname.endsWith('/account-owner-options')) return json({ currentMemberId: 'member', members: [] });
            if (pathname.endsWith('/accounts') && failAccounts) { failAccounts = false; return fail(503); }
            if (pathname.endsWith('/accounts')) return json([
                { id: 'eur', name: 'EUR bank', currency: 'EUR', scope: 'Household', visibility: 'Household', includeInHouseholdReports: true, status: 'Active', owners: [] },
                { id: 'private-eur', name: 'Private EUR', currency: 'EUR', scope: 'Personal', visibility: 'Private', includeInHouseholdReports: false, status: 'Active', owners: [{ householdMemberId: 'member' }] },
                { id: 'huf', name: 'HUF bank', currency: 'HUF', scope: 'Household', visibility: 'Household', includeInHouseholdReports: true, status: 'Active', owners: [] }]);
            if (pathname.endsWith('/categories')) return json([
                { id: 'uncategorized', name: 'Other', systemKey: 'uncategorized', kind: 'Expense', isArchived: false },
                { id: 'food', name: 'Food', systemKey: null, kind: 'Expense', isArchived: false }]);
            if (pathname.endsWith('/personal-money/me')) return hasWallet
                ? json({ walletId: 'wallet', currency: 'EUR', available: '100', entries: [], charges: [] })
                : route.fulfill({ status: 204 });
            return json([]);
        });
        await page.goto(url);
        await page.getByRole('button', { name: copy.retry, exact: true }).click();
        await page.getByRole('button', { name: copy.review, exact: true }).click();
        if (language === 'en' && width === 375 && !dark) {
            await page.getByRole('button', { name: copy.retry, exact: true }).click();
        }
        // Accidental dismissal must not silently erase reviewed values.
        await page.getByLabel(copy.reviewedTotal, { exact: true }).fill('8');
        await page.getByText(copy.itemsAbove.replace('{{amount}}', new Intl.NumberFormat(language, { style: 'currency', currency: 'EUR' }).format(1)), { exact: true }).waitFor();
        await page.keyboard.press('Escape');
        await page.getByRole('dialog', { name: copy.unsavedTitle, exact: true }).waitFor();
        await page.getByRole('button', { name: copy.keepEditing, exact: true }).click();
        assert.equal(await page.getByLabel(copy.reviewedTotal, { exact: true }).inputValue(), '8');
        await page.locator('input[id$="-amount"]').fill('0');
        assert.equal(await page.locator('input[id$="-amount"]').getAttribute('aria-invalid'), 'true');
        await page.getByText(copy.invalidItemAmount, { exact: true }).waitFor();
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: copy.discardEdits, exact: true }).click();
        await page.getByRole('button', { name: copy.review, exact: true }).click();
        assert.equal(await page.getByLabel(copy.reviewedTotal, { exact: true }).inputValue(), '9.00');
        // Photo inspection is reachable from the fixed editor footer.
        await page.getByRole('button', { name: copy.expandPhoto, exact: true }).click();
        const zoom = page.getByRole('slider', { name: copy.zoom, exact: true });
        await zoom.focus(); await page.keyboard.press('End');
        assert.equal(await zoom.inputValue(), '400');
        await page.keyboard.press('Escape');
        await page.getByRole('dialog', { name: copy.review, exact: true }).getByRole('button', { name: copy.viewMatch, exact: true }).click();
        const matchDialog = page.getByRole('dialog', { name: copy.matchTitle, exact: true });
        await matchDialog.getByRole('link', { name: copy.openTransaction, exact: true }).waitFor();
        assert.ok((await matchDialog.getByRole('link').getAttribute('href')).includes('original-transaction'));
        await page.keyboard.press('Escape');
        async function assertItemAlignment() {
            if (width < 640) return;
            const rows = await page.locator('input[id$="-description"]').evaluateAll(inputs => inputs.map(input => {
                const row = input.parentElement.parentElement;
                const controls = [input, row.children[1].querySelector('button'), row.querySelector('input[id$="-amount"]'), row.lastElementChild.querySelector('button')];
                return controls.map(control => control.getBoundingClientRect().top);
            }));
            for (const tops of rows) assert.ok(Math.max(...tops) - Math.min(...tops) <= 2,
                'Description, category, amount and remove must align even with suggestions, discounts and household-rule controls.');
        }
        const confirm = page.getByRole('button', { name: copy.confirm, exact: true });
        assert.equal(await confirm.isDisabled(), true);
        assert.equal(await page.locator('input[type="date"]').inputValue(), '', 'Missing dates must not silently become today.');
        const account = page.locator('select').filter({ has: page.locator('option[value="eur"]') });
        assert.equal(await account.inputValue(), '', 'Receipt review requires an explicit account selection.');
        const labelsToggle = page.getByRole('button').filter({ hasText: copy.optionalLabels });
        assert.equal(await labelsToggle.getAttribute('aria-expanded'), 'false');
        await labelsToggle.click();
        await page.getByText(copy.labelsChooseAccount, { exact: true }).waitFor();
        assert.equal(await page.locator('input[id$="-amount"]').inputValue(), '9', 'Unique matching discount prefills the net item amount.');
        await page.getByText(copy.discountsSuggested, { exact: true }).waitFor();
        await page.getByText(copy.originalLine.replace('{{description}}', 'FOOD APPLES'), { exact: true }).waitFor();
        assert.equal(await account.locator('option[value="huf"]').count(), 0, 'No implicit currency conversion.');
        await page.getByRole('checkbox', { name: copy.reviewConsent }).check();
        assert.equal(await confirm.isDisabled(), true, 'A mismatched item total cannot be confirmed.');
        await page.getByText(dark ? copy.householdRuleSuggestion : copy.modelCategorySuggestion, { exact: true }).waitFor();
        await assertItemAlignment();
        await page.getByRole('button', { name: copy.suggestion.replace('{{category}}', 'Food'), exact: true }).click();
        const remember = page.getByRole('checkbox', { name: new RegExp(copy.rememberCategory) });
        assert.equal(await remember.isChecked(), false, 'Household rule sharing is opt-in.');
        if (dark) await remember.check();
        await assertItemAlignment();
        await page.getByLabel(copy.reviewedTotal, { exact: true }).fill('0009,00');
        await page.getByRole('checkbox', { name: copy.reviewConsent }).check();
        await page.locator('input[id$="-amount"]').fill('0009,00');
        await page.locator('input[type="date"]').fill('2026-10-28');
        await account.selectOption('eur');
        await page.getByText(copy.labelsChooseAccount, { exact: true }).waitFor({ state: 'hidden' });
        if (hasWallet) {
            await page.getByRole('button', { name: moneyCopy.itemCharge + ' · Food Apples', exact: true }).click();
            const allocation = page.getByRole('dialog', { name: moneyCopy.itemAllocationTitle, exact: true });
            await allocation.getByRole('checkbox', { name: moneyCopy.itemCharge, exact: true }).check();
            await allocation.locator('input[inputmode="decimal"]').fill('0000,5000');
            await page.keyboard.press('Escape');
        }
        await page.getByRole('heading', { name: copy.confirmStep, exact: true }).scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(artifacts, `confirm-${language}-${dark}-${width}.png`), fullPage: true });
        await page.getByText(copy.observations, { exact: true }).click();
        await page.getByRole('checkbox', { name: copy.reviewConsent }).check();
        assert.ok((await page.locator('details').filter({ has: page.getByText(copy.observations, { exact: true }) }).textContent()).includes('Discount'));
        await page.getByText(copy.viewPhoto, { exact: true }).click();
        await page.getByAltText(copy.imageAlt).waitFor();
        await page.getByText(copy.viewPhoto, { exact: true }).click();
        await page.screenshot({ path: path.join(artifacts, `review-${language}-${dark}-${width}.png`), fullPage: true });
        await page.locator('input[id$="-description"]').scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(artifacts, `items-${language}-${dark}-${width}.png`), fullPage: true });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        const consent = page.getByRole('checkbox', { name: copy.reviewConsent });
        if (!hasWallet) {
            for (const accountId of ['private-eur', 'eur']) {
                await account.selectOption(accountId);
                assert.equal(await consent.isChecked(), false, 'Changing account/privacy invalidates the reviewed intent.');
                assert.equal(await confirm.isDisabled(), true, 'A changed account requires fresh acknowledgement.');
                await consent.check();
                assert.equal(await confirm.isEnabled(), true);
            }
        }
        await page.locator('input[type="date"]').fill('2026-10-29');
        assert.equal(await consent.isChecked(), false, 'Changing receipt details invalidates acknowledgement.');
        await page.locator('input[type="date"]').fill('2026-10-28');
        await consent.check();
        await confirm.click();
        await page.getByRole('alert').filter({ hasText: 'Please retry' }).waitFor();
        assert.equal(await page.locator('input[id$="-amount"]').inputValue(), '0009,00', 'Posting failure retains the reviewed form.');
        await confirm.click();
        await page.getByRole('link', { name: copy.openTransaction, exact: true }).waitFor();
        assert.equal(posts.length, 2);
        assert.deepEqual(posts[0], posts[1], 'Retry uses the same draft and reviewed values.');
        assert.equal(posts[1].currency, 'EUR');
        assert.equal(posts[1].transaction.items.length, 1, 'Discount and payment lines cannot become extra expenses.');
        assert.equal(posts[1].transaction.items[0].categoryId, 'food');
        assert.deepEqual(posts[1].rememberCategoryRules, dark ? [{ lineIndex: 0, itemIndex: 0 }] : [], 'Only explicit rule consent is submitted.');
        assert.equal(posts[1].reviewedTotal, '9');
        assert.equal(posts[1].transaction.items[0].amount, '9', 'Leading-zero/comma item amounts must use the same canonical API format.');
        if (hasWallet) assert.equal(posts[1].transaction.items[0].personalMoneyAmount, '0.5', 'Personal Money allocations must use canonical decimal strings too.');
        await page.screenshot({ path: path.join(artifacts, `inbox-${language}-${dark}-${width}.png`), fullPage: true });
        await page.locator('input[type="file"]').first().setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: png });
        await page.getByAltText(copy.selectedPhoto).waitFor();
        await page.getByRole('button', { name: copy.removePhoto, exact: true }).click();
        assert.equal(await page.getByAltText(copy.selectedPhoto).count(), 0);
        await page.locator('input[type="file"]').first().setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: png });
        await page.getByAltText(copy.selectedPhoto).waitFor();
        await page.screenshot({ path: path.join(artifacts, `photo-${language}-${dark}-${width}.png`), fullPage: true });
        await page.getByRole('button', { name: copy.upload, exact: true }).click();
        await page.getByRole('button', { name: copy.retryUpload, exact: true }).waitFor();
        const usedAfterAcceptance = monthlyUsed;
        enabled = false;
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByText(copy.disabled, { exact: true }).waitFor();
        assert.equal(await page.getByRole('button', { name: copy.chooseFile, exact: true }).isDisabled(), true);
        assert.equal(await page.getByRole('button', { name: copy.takePhoto, exact: true }).isDisabled(), true);
        assert.equal(await page.locator('input[type="file"]').first().isDisabled(), true);
        assert.equal(await page.getByRole('button', { name: copy.retryUpload, exact: true }).isEnabled(), true);
        await page.getByRole('button', { name: copy.retryUpload, exact: true }).click();
        await page.getByText(copy.status.Queued, { exact: true }).waitFor();
        const operation = body => /name="operationId"\r\n\r\n([^\r]+)/.exec(body)?.[1];
        assert.equal(uploads.length, 2);
        assert.equal(monthlyUsed, usedAfterAcceptance, 'Disabled-state recovery must not consume another scan.');
        enabled = true;
        assert.ok(operation(uploads[0]));
        assert.equal(operation(uploads[0]), operation(uploads[1]), 'Upload retry must reuse its operation ID.');
        await page.getByRole('button', { name: copy.discard, exact: true }).click();
        await page.getByRole('button', { name: copy.discard, exact: true }).last().click();
        await page.getByText(copy.status.Discarded, { exact: true }).waitFor();
        assert.equal(posts.length, 2, 'Upload and discard never post financial records.');
        monthlyUsed = 10;
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByText(copy.quotaExhausted, { exact: true }).waitFor();
        assert.equal(await page.getByRole('button', { name: copy.chooseFile, exact: true }).isDisabled(), true);
        await page.getByRole('button', { name: copy.viewPhoto, exact: true }).click();
        await page.getByRole('dialog').getByAltText(copy.imageAlt).waitFor();
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: copy.deletePhoto, exact: true }).click();
        await page.getByRole('dialog').getByText(copy.deletePhotoHelp, { exact: true }).waitFor();
        await page.getByRole('dialog').getByRole('button', { name: copy.deletePhoto, exact: true }).click();
        await page.getByRole('button', { name: copy.viewPhoto, exact: true }).waitFor({ state: 'hidden' });
        await page.getByRole('link', { name: copy.openTransaction, exact: true }).waitFor();
        assert.equal(posts.length, 2, 'Deleting a photo must not post or delete an expense.');
        enabled = false;
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByText(copy.disabled, { exact: true }).waitFor();
        assert.equal(await page.locator('input[type="file"]').first().isDisabled(), true);
        showFailed = true;
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByText(copy.providerFailed, { exact: true }).waitFor();
        await page.getByText(copy.newScanCharge, { exact: true }).waitFor();
        assert.equal(await page.getByRole('button', { name: copy.anotherPhoto, exact: true }).isDisabled(), true);
        const beforeManual = monthlyUsed;
        await page.getByRole('button', { name: copy.enterManually, exact: true }).click();
        await page.getByText(copy.manualHelp, { exact: true }).waitFor();
        await page.locator('input[id$="-description"]').fill('Manual groceries');
        await page.locator('input[id$="-amount"]').fill('4.50');
        await page.locator('button[type="submit"][form="transaction-editor"]').click();
        await page.getByText(copy.manualHelp, { exact: true }).waitFor({ state: 'hidden' });
        assert.equal(manualPosts.length, 1);
        assert.equal(manualPosts[0].items[0].amount, '4.50');
        assert.equal(monthlyUsed, beforeManual, 'Manual recovery must not consume scan quota.');
        assert.equal(uploads.length, 2);
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS receipt inbox, reconciliation, categories, posting/upload retry, discard, disabled state and EN/DE/HU mobile/desktop themes');
} finally { await browser?.close(); server.kill(); }


