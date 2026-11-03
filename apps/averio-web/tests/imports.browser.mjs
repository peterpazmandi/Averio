import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import ts from 'typescript';
import vm from 'node:vm';

const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5284', '--strictPort'], { stdio: 'ignore' });
const url = 'http://localhost:5284/tests/fixtures/imports.html';
const copyModule = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/imports.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, copyModule);
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
        const copy = copyModule.exports['imports' + language.toUpperCase()];
        const errors = [], confirmations = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark: width === 375 });
        const rows = Array.from({ length: 52 }, (_, i) => ({ sheet: 'Kiad�sok', rowNumber: i + 3, kind: 'Expense', date: '2026-09-21', account: 'Main', destination: null, category: 'Food', amount: '12.3456', currency: 'EUR', receivedAmount: null, receivedCurrency: null, tags: '<script>shop</script>', note: 'Original note' }));
        rows.push({ ...rows[0], sheet: '�tutal�s', kind: 'Transfer', destination: 'Savings', category: '', amount: '10' });
        const preview = { id: 'batch', status: 'Draft', count: rows.length, from: '2026-09-21', to: '2026-09-21', accounts: ['Main', 'Savings'], categories: [{ kind: 'Expense', name: 'Food' }], totals: [{ kind: 'Expense', currency: 'EUR', amount: '641.9712', count: 52 }], repeatedRows: 51, rows };
        await page.route('**/api/v1/**', async route => {
            const req = route.request(), pathname = new URL(req.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (req.method() === 'OPTIONS') return route.fulfill({ status: 204 });
            if (pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (pathname.endsWith('/account-owner-options')) return json({ currentMemberId: 'member', members: [] });
            if (pathname.endsWith('/accounts')) return json([
                { id: 'main', name: 'Main', currency: 'EUR', scope: 'Household', visibility: 'Household', status: 'Active', owners: [], balanceAnchorDate: '2026-09-01', balanceAnchorAmount: '1000' },
                { id: 'savings', name: 'Savings', currency: 'EUR', scope: 'Personal', visibility: 'Private', status: 'Active', owners: [{ householdMemberId: 'member' }], balanceAnchorDate: '2026-09-01', balanceAnchorAmount: '500' },
                { id: 'other-private', name: 'Other private', currency: 'EUR', scope: 'Personal', visibility: 'Private', status: 'Active', owners: [{ householdMemberId: 'someone-else' }] }]);
            if (pathname.endsWith('/categories')) return json([]);
            if (pathname.endsWith('/imports')) { assert.match(req.postDataBuffer().toString(), /fixture.xlsx/); return json(preview); }
            if (pathname.endsWith('/confirmation')) {
                confirmations.push(req.postDataJSON());
                if (confirmations.length === 1) return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ status: 503, detail: 'Retry test' }) });
                return json({ id: 'batch', status: 'Completed', transactions: 52, transfers: 1 });
            }
            throw new Error(`Unexpected request ${pathname}`);
        });
        await page.goto(url);
        await page.locator('input[type=file]').setInputFiles({ name: 'fixture.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: Buffer.from('synthetic; API is mocked') });
        await page.getByText(copy.accountsHelp, { exact: true }).waitFor();
        assert.equal(await page.locator('select option[value="other-private"]').count(), 0);
        assert.equal(confirmations.length, 0, 'Preview must not commit');
        await page.locator('footer button').last().click();
        await page.getByText(copy.categoriesHelp, { exact: true }).waitFor();
        await page.locator('footer button').last().click();
        await page.getByRole('table').waitFor();
        assert.equal(await page.locator('tbody tr').count(), 50);
        assert.equal(await page.locator('footer button').last().isDisabled(), true);
        assert.equal(await page.locator('tbody script').count(), 0, 'Source tags are rendered as text');
        await page.getByRole('checkbox').check();
        await page.locator('footer button').last().click();
        await page.getByRole('alert').waitFor();
        await page.locator('footer button').last().click();
        await page.getByRole('heading', { name: copy.complete, exact: true }).waitFor();
        assert.equal(confirmations.length, 2);
        assert.deepEqual(confirmations[0], confirmations[1], 'Retry preserves reviewed mappings');
        assert.deepEqual(confirmations[0].accounts, [{ source: 'Main', accountId: 'main', visibility: 'Household' }, { source: 'Savings', accountId: 'savings', visibility: 'Private' }]);
        assert.equal(confirmations[0].categories[0].categoryId, null);
        assert.equal(confirmations[0].acknowledgeBalancesAndOverlap, true);
        assert.deepEqual(errors, []);
        assert.doesNotMatch(await page.locator('body').innerText(), /imports\.[a-zA-Z]/);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        await page.close();
    }
    console.log('PASS import review, localization, privacy defaults, pagination and retry browser flows');
} finally { await browser?.close(); server.kill(); }
