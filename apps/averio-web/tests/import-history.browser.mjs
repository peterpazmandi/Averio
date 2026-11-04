import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import ts from 'typescript';
import vm from 'node:vm';
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5285', '--strictPort'], { stdio: 'ignore' });
const url = 'http://localhost:5285/tests/fixtures/import-history.html';
const copyModule = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/importHistory.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, copyModule);
let browser;
try {
    for (let i = 0; i < 80; i++) {
        try { if ((await fetch(url)).ok) break; } catch { /* Vite is starting. */ }
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const language of ['en', 'de', 'hu']) for (const width of [375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        page.setDefaultTimeout(15000);
        const copy = copyModule.exports['importHistory' + language.toUpperCase()];
        const errors = [], requests = [];
        let fail = true, legacy = false, empty = false;
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark: width === 375 });
        const batch = { id: '11111111-1111-1111-1111-111111111111', sourceName: 'Original.xlsx', completedAt: '2026-11-04T10:00:00Z', trackingAvailable: true,
            summary: { count: 52, from: '2026-09-01', to: '2026-10-30', totals: [{ kind: 'Expense', currency: 'EUR', count: 51, amount: '1234.5678' }, { kind: 'Transfer', currency: 'HUF', count: 1, amount: '5000' }] } };
        const row = { source: { sheet: 'Expenses', rowNumber: 3, kind: 'Expense', date: '2026-09-01', account: 'Old bank', destination: null, category: 'Food', amount: '12.3456', currency: 'EUR', receivedAmount: null, receivedCurrency: null, tags: '<script>shop</script>', note: 'Original note' },
            recordId: '22222222-2222-2222-2222-222222222222', state: 'Edited', changed: true, canModify: true, importedAccount: 'My bank', importedDestination: null, importedCategory: 'Food', categoryIsBase: false };
        await page.route('**/api/v1/**', async route => {
            const req = route.request(), u = new URL(req.url()); requests.push(req.method());
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (u.pathname.endsWith('/imports')) {
                if (fail) { fail = false; return route.fulfill({ status: 503 }); }
                return json({ items: empty ? [] : [{ ...batch, trackingAvailable: !legacy }], hasMore: false });
            }
            const next = u.searchParams.get('page') === '1';
            return json({ batch: { ...batch, trackingAvailable: !legacy },
                records: Array.from({ length: next ? 2 : 50 }, (_, i) => ({ ...row, source: { ...row.source, rowNumber: (next ? 53 : 3) + i },
                    state: legacy ? 'Untracked' : i === 1 ? 'Unavailable' : i === 2 ? 'Voided' : i === 3 ? 'Unchanged' : 'Edited',
                    recordId: legacy || i === 1 ? null : row.recordId, changed: legacy || i === 1 ? null : i !== 3 })),
                review: { unchanged: 1, edited: 49, voided: 1, unavailable: 1, untracked: legacy ? 52 : 0, readOnly: 0 },
                checkedAt: '2026-11-04T11:00:00Z', hasMore: !next });
        });
        await page.goto(url);
        await page.getByRole('alert').waitFor();
        await page.getByRole('button').click();
        await page.getByRole('button', { name: copy.records, exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('ol > li').length === 50);
        assert.equal(await page.locator('ol script').count(), 0);
        assert.equal(await page.locator('ol > li').nth(1).getByRole('link').count(), 0, 'Inaccessible row has no current record link');
        await page.getByRole('button', { name: copy.preview, exact: true }).click();
        await page.getByRole('heading', { name: copy.undo, exact: true }).waitFor();
        await page.getByText(copy.undoHelp, { exact: true }).waitFor();
        await page.getByRole('button', { name: copy.next, exact: true }).click();
        await page.waitForFunction(() => document.querySelectorAll('ol > li').length === 2);
        await page.getByRole('button', { name: copy.refresh, exact: true }).click();
        await page.getByRole('button', { name: copy.back, exact: true }).click();
        legacy = true;
        await page.getByRole('button', { name: copy.records, exact: true }).click();
        await page.getByText(copy.legacy, { exact: true }).waitFor();
        assert.equal(await page.locator('ol').getByRole('link').count(), 0);
        assert.deepEqual([...new Set(requests)], ['GET'], 'History and preview are read-only');
        assert.doesNotMatch(await page.locator('body').innerText(), /importHistory\.[a-zA-Z]/);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        empty = true;
        await page.reload();
        await page.getByRole('heading', { name: copy.empty, exact: true }).waitFor();
        assert.deepEqual(errors, []);
        await page.close();
        console.log('PASS import history ' + language + ' ' + width);
    }
} finally { await browser?.close(); server.kill(); }
