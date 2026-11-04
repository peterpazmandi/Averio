import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5286', '--strictPort'], { windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5286/tests/fixtures/imported-transactions.html';
let browser;
try {
    for (let i = 0; i < 80; i++) {
        try { if ((await fetch(url)).ok) break; } catch { /* Starting Vite. */ }
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const width of [375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [], filters = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        const batch = '11111111-1111-1111-1111-111111111111';
        const base = { accountId: 'account', accountName: 'Main', kind: 'Expense', visibility: 'Household', amount: '12', currency: 'EUR', occurredOn: '2026-11-04', status: 'Posted', createdAt: '2026-11-04T10:00:00Z', updatedAt: '2026-11-04T10:00:00Z', canModify: true, items: [], labels: [] };
        const rows = [{ ...base, id: 'imported', description: 'Imported purchase', isImported: true, importBatchId: batch },
            { ...base, id: 'shared', description: 'Shared purchase', isImported: true, importBatchId: null },
            { ...base, id: 'manual', description: 'Manual purchase', isImported: false, importBatchId: null }];
        await page.route('**/api/v1/**', async route => {
            const u = new URL(route.request().url());
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (u.pathname.endsWith('/transactions')) {
                const imported = u.searchParams.get('importedOnly') === 'true'; filters.push(imported);
                return json({ items: imported ? rows.slice(0, 2) : rows, hasMore: false });
            }
            if (u.pathname.includes('/personal-money/')) return route.fulfill({ status: 204 });
            return json([]);
        });
        await page.goto(url);
        const own = page.locator('[data-transaction-id="imported"]');
        await own.waitFor();
        assert.equal(await own.getByRole('link', { name: 'Imported', exact: true }).getAttribute('href'), '/app/import-history?batch=' + batch);
        assert.equal(await page.locator('[data-transaction-id="shared"]').getByRole('link').count(), 0);
        assert.equal(await page.locator('[data-transaction-id="manual"]').getByText('Imported', { exact: true }).count(), 0);
        assert.equal(await own.locator('button a').count(), 0, 'Badge is not nested in the editor button');
        await page.getByRole('button', { name: /Filters/ }).click();
        await page.getByRole('checkbox', { name: 'Imported only', exact: true }).check();
        await page.getByRole('button', { name: 'Show results', exact: true }).click();
        await page.locator('[data-transaction-id="manual"]').waitFor({ state: 'detached' });
        assert.equal(filters.at(-1), true);
        await page.getByRole('button', { name: /Filters/ }).click();
        await page.getByRole('dialog').getByRole('button', { name: /Reset/ }).click();
        assert.equal(await page.getByRole('checkbox', { name: 'Imported only', exact: true }).isChecked(), false);
        await page.getByRole('button', { name: 'Show results', exact: true }).click();
        await page.locator('[data-transaction-id="manual"]').waitFor();
        await own.getByRole('link', { name: 'Imported', exact: true }).click();
        await page.waitForURL('**/app/import-history?batch=' + batch);
        assert.equal(new URL(page.url()).searchParams.has('transaction'), false);
        assert.deepEqual(errors, []);
        console.log('PASS imported badge, private link, server filter and reset at ' + width);
        await page.close();
    }
} finally { await browser?.close(); server.kill(); }
