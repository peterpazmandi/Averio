import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import ts from 'typescript';
import vm from 'node:vm';
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5287', '--strictPort'], { windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5287/tests/fixtures/import-tools.html';
const copies = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/importTools.ts', 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, copies);
let browser;
try {
    for (let i = 0; i < 80; i++) { try { if ((await fetch(url)).ok) break; } catch {} await new Promise(r => setTimeout(r, 100)); }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const language of ['en', 'de', 'hu']) for (const width of [375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 1000 } });
        const copy = copies.exports['importTools' + language.toUpperCase()], errors = [], writes = [];
        let stale = false, done = false;
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(language => { localStorage.setItem('i18nextLng', language); }, language);
        await page.route('**/api/v1/**', async route => {
            const req = route.request(), path = new URL(req.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (path.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (path.endsWith('/balances')) return json([{ accountId: 'a', name: 'Main', currency: 'EUR', anchorDate: '2026-01-01', anchorAmount: '100', currentBalance: '123.4567', asOf: '2026-11-05' }]);
            if (path.endsWith('/undo-plan')) return json({ token: stale ? 'fresh' : 'initial', eligible: done ? 0 : 1, retained: 2,
                records: [{ id: 'a', isTransfer: false, state: done ? 'Voided' : 'Eligible' }, { id: 'b', isTransfer: false, state: 'Edited' }, { id: 'c', isTransfer: false, state: 'Linked' }],
                effects: done ? [] : [{ accountId: 'a', name: 'Main', currency: 'EUR', before: '123.4567', after: '125.4567', change: '2' }] });
            if (path.endsWith('/undo')) {
                writes.push(req.postDataJSON());
                if (!stale) { stale = true; return route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ status: 409, detail: 'Review changed' }) }); }
                done = true; return json({ voided: 1, retained: 2 });
            }
            throw Error('Unexpected request ' + path);
        });
        await page.goto(url);
        await page.getByLabel(copy.expected, { exact: true }).fill('123,4567');
        await page.getByText(copy.matchesBalance, { exact: true }).waitFor();
        assert.equal(writes.length, 0, 'Reconciliation never changes money');
        await page.getByLabel(copy.expected, { exact: true }).fill('124.4567');
        assert.equal(await page.getByText(copy.matchesBalance, { exact: true }).count(), 0);
        const action = page.getByRole('button', { name: copy.undoAction, exact: true });
        assert.equal(await action.isDisabled(), true);
        await page.getByRole('checkbox').check(); await action.click();
        await page.getByText(copy.undoError, { exact: true }).waitFor();
        assert.equal(await page.getByRole('checkbox').isChecked(), false, 'A stale preview requires fresh confirmation');
        await page.getByRole('checkbox').check(); await action.click();
        await page.getByText(copy.undone.replace('{{count}}', '1'), { exact: true }).waitFor();
        assert.deepEqual(writes, [{ token: 'initial', confirm: true }, { token: 'fresh', confirm: true }]);
        assert.equal(await action.isDisabled(), true);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        assert.deepEqual(errors, []);
        console.log('PASS precise reconciliation and stale-safe undo: ' + language + ' ' + width);
        await page.close();
    }
} finally { await browser?.close(); server.kill(); }