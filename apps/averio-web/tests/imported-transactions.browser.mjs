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
    for (const width of [320, 375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [], filters = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        const batch = '11111111-1111-1111-1111-111111111111';
        const viewId = '22222222-2222-4222-8222-222222222222';
        const labelA = '33333333-3333-4333-8333-333333333333', labelB = '44444444-4444-4444-8444-444444444444';
        const base = { accountId: 'account', accountName: 'Main', kind: 'Expense', visibility: 'Household', amount: '12', currency: 'EUR', occurredOn: '2026-11-04', status: 'Posted', createdAt: '2026-11-04T10:00:00Z', updatedAt: '2026-11-04T10:00:00Z', canModify: true, items: [], labels: [] };
        const rows = [{ ...base, id: 'imported', description: 'Imported purchase', isImported: true, importBatchId: batch },
            { ...base, id: 'shared', kind: 'Income', visibility: 'Private', accountName: 'A very long account name for the private income transaction', description: 'Shared purchase', isImported: true, importBatchId: null },
            { ...base, id: 'manual', description: 'Manual purchase', isImported: false, importBatchId: null },
            { ...base, id: 'refund', kind: 'Refund', description: 'Refund purchase', isImported: false, importBatchId: null },
            { ...base, id: 'voided', status: 'Voided', description: 'Voided purchase', isImported: false, importBatchId: null }];
        await page.route('**/api/v1/**', async route => {
            const u = new URL(route.request().url());
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (u.pathname.endsWith('/transactions')) {
                const imported = u.searchParams.get('importedOnly') === 'true'; filters.push(imported);
                return json({ items: imported ? rows.slice(0, 2) : rows, hasMore: false });
            }
            if (u.pathname.endsWith('/saved-transaction-views')) return json([{ id: viewId, name: 'Monthly view', filters: { period: 'this-month', labels: [] }, version: batch, available: false }]);
            if (u.pathname.endsWith('/labels/filter-options')) return json([
                { id: labelA, name: 'Family', scope: 'Household', accountId: null },
                { id: labelB, name: 'Work', scope: 'Household', accountId: null },
            ]);
            if (u.pathname.includes('/personal-money/')) return route.fulfill({ status: 204 });
            return json([]);
        });
        await page.goto(url);
        const own = page.locator('[data-transaction-id="imported"]');
        await own.waitFor();
        const more = page.getByLabel('More actions', { exact: true });
        await more.click();
        const menu = page.getByRole('link', { name: 'Import transactions' });
        const menuBox = await menu.boundingBox();
        assert.ok(menuBox.x >= 0 && menuBox.x + menuBox.width <= width, 'More actions remains inside the viewport');
        await page.keyboard.press('Escape');
        assert.equal(await menu.isVisible(), false);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        assert.equal(await own.getByRole('link', { name: 'Imported', exact: true }).getAttribute('href'), '/app/import-history?batch=' + batch);
        assert.equal(await page.locator('[data-transaction-id="shared"]').getByRole('link').count(), 0);
        assert.equal(await page.locator('[data-transaction-id="manual"]').getByText('Imported', { exact: true }).count(), 0);
        assert.equal(await own.locator('button a').count(), 0, 'Badge is not nested in the editor button');
        const filterButton = page.getByRole('button', { name: /^Filters,/ });
        assert.match(await filterButton.getAttribute('aria-label'), /0 active filters/);
        assert.equal(await filterButton.getByText('Filters', { exact: true }).isVisible(), width >= 640);
        assert.equal(await more.getByText('More actions', { exact: true }).isVisible(), width >= 640);
        assert.equal(await page.getByRole('button', { name: 'Select transactions', exact: true }).getByText('Select transactions', { exact: true }).isVisible(), width >= 640);
        assert.equal(await own.getByRole('checkbox').count(), 0);
        await page.getByRole('button', { name: 'Select transactions', exact: true }).click();
        const expenseCheckbox = own.getByRole('checkbox', { name: 'Select: Imported purchase' });
        const income = page.locator('[data-transaction-id="shared"]');
        const incomeCheckbox = income.getByRole('checkbox', { name: 'Select: Shared purchase' });
        for (const [card, checkbox] of [[own, expenseCheckbox], [income, incomeCheckbox]]) {
            const cardBox = await card.boundingBox();
            const target = checkbox.locator('..');
            const targetBox = await target.boundingBox();
            assert.ok(cardBox.x >= 0 && cardBox.x + cardBox.width <= width, 'Transaction card remains inside the viewport');
            assert.ok(targetBox.width >= 44 && targetBox.height >= 44, 'Selection has a large touch target');
            assert.ok(targetBox.x >= cardBox.x && targetBox.y >= cardBox.y
                && targetBox.x + targetBox.width <= cardBox.x + cardBox.width
                && targetBox.y + targetBox.height <= cardBox.y + cardBox.height, 'Selection target is inside its card');
            assert.equal(await card.locator('button input').count(), 0, 'Checkbox is separate from the details button');
        }
        const privateBadgeBox = await income.locator('.av-private-badge').boundingBox();
        const incomeTargetBox = await incomeCheckbox.locator('..').boundingBox();
        assert.ok(privateBadgeBox.x + privateBadgeBox.width <= incomeTargetBox.x, 'Private badge does not overlap the selection control');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Selection controls do not cause horizontal overflow');
        assert.equal(await page.locator('[data-transaction-id="refund"]').getByRole('checkbox').count(), 0);
        assert.equal(await page.locator('[data-transaction-id="voided"]').getByRole('checkbox').count(), 0);
        await expenseCheckbox.locator('..').click({ position: { x: 4, y: 4 } });
        assert.equal(await expenseCheckbox.isChecked(), true);
        assert.equal(await page.getByText('Selected transactions: 1 / 100').isVisible(), true);
        assert.equal(await page.getByRole('dialog').count(), 0, 'Selecting does not open transaction details');
        await page.getByRole('button', { name: 'Assign activity', exact: true }).click();
        const assignmentDialog = page.getByRole('dialog', { name: 'Assign activity' });
        await assignmentDialog.waitFor();
        await page.mouse.click(5, 5);
        assert.equal(await assignmentDialog.isVisible(), true, 'Other dialogs retain their existing backdrop behavior');
        await page.keyboard.press('Escape');
        await assignmentDialog.waitFor({ state: 'detached' });
        await incomeCheckbox.focus();
        await page.keyboard.press('Space');
        assert.equal(await incomeCheckbox.isChecked(), true);
        assert.equal(await page.getByText('Selected transactions: 2 / 100').isVisible(), true);
        await page.keyboard.press('Space');
        assert.equal(await incomeCheckbox.isChecked(), false);
        assert.equal(await page.getByText('Selected transactions: 1 / 100').isVisible(), true);
        await page.getByRole('button', { name: 'Select loaded eligible transactions', exact: true }).click();
        assert.equal(await page.getByText('Selected transactions: 3 / 100').isVisible(), true);
        assert.equal(await incomeCheckbox.isChecked(), true);
        assert.equal(await page.locator('[data-transaction-id="manual"]').getByRole('checkbox').isChecked(), true);
        assert.equal(await page.getByRole('button', { name: 'Select loaded eligible transactions', exact: true }).isDisabled(), true);
        await filterButton.click();
        await page.getByText('More filters', { exact: true }).click();
        await page.getByRole('checkbox', { name: 'Imported only', exact: true }).check();
        assert.equal(await page.getByText('Selected transactions: 0 / 100').isVisible(), true);
        assert.match(await filterButton.getAttribute('aria-label'), /1 active filter/);
        assert.equal(await page.getByLabel('Active transaction filters').getByText('Imported only').isVisible(), true);
        await filterButton.click();
        await page.locator('[data-transaction-id="manual"]').waitFor({ state: 'detached' });
        assert.equal(filters.at(-1), true);
        await filterButton.click();
        await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
        assert.equal(await page.getByRole('checkbox', { name: 'Imported only', exact: true }).isChecked(), false);
        assert.match(await filterButton.getAttribute('aria-label'), /1 active filter/);
        assert.equal(await page.getByLabel('Active transaction filters').getByText('This month').isVisible(), true);
        await page.getByRole('searchbox', { name: 'Search transactions' }).fill('purchase');
        assert.match(await filterButton.getAttribute('aria-label'), /2 active filters/);
        assert.equal(await page.getByLabel('Active transaction filters').getByText('Search: purchase').isVisible(), true);
        const warning = 'Search and the archived, imported and Personal Money filters are not saved.';
        assert.equal(await page.getByRole('status').filter({ hasText: warning }).isVisible(), true);
        await page.getByRole('button', { name: 'Save current view', exact: true }).click();
        const saveDialog = page.getByRole('dialog', { name: 'Save current view', exact: true });
        assert.equal(await saveDialog.getByRole('status').filter({ hasText: warning }).isVisible(), true);
        await page.keyboard.press('Escape');
        await saveDialog.waitFor({ state: 'detached' });
        await page.getByRole('checkbox', { name: 'Imported only', exact: true }).check();
        await page.getByRole('combobox', { name: 'Saved views' }).selectOption(viewId);
        await page.waitForFunction(() => document.querySelector('#transaction-search')?.value === '');
        assert.equal(await page.getByRole('searchbox', { name: 'Search transactions' }).inputValue(), '');
        assert.equal(await page.getByRole('checkbox', { name: 'Imported only', exact: true }).isChecked(), true);
        await page.getByRole('checkbox', { name: 'Imported only', exact: true }).uncheck();
        await page.getByRole('combobox', { name: 'Date range' }).selectOption('last-month');
        assert.equal(await page.getByText('Monthly view · Modified').isVisible(), true);
        assert.equal(await page.getByLabel('Active transaction filters').getByText('Last month').isVisible(), true);
        await page.getByRole('checkbox', { name: 'Family', exact: true }).check();
        await page.getByRole('checkbox', { name: 'Work', exact: true }).check();
        await page.getByRole('button', { name: 'Refund', exact: true }).click();
        await page.getByRole('checkbox', { name: 'Include archived transactions' }).check();
        await page.waitForFunction(() => document.querySelector('[aria-controls="transaction-filters"]')?.getAttribute('aria-label')?.includes('5 active filters'));
        const summary = page.getByLabel('Active transaction filters');
        for (const value of ['Last month', 'Labels: Family', 'Labels: Work', 'Type: Refund', 'Include archived transactions']) {
            assert.equal(await summary.getByText(value, { exact: true }).isVisible(), true, value);
        }
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Expanded filters do not overflow');
        await filterButton.click();
        assert.equal(await summary.getByText('Labels: Work').isVisible(), true, 'Filters remain visible when collapsed');
        await summary.getByRole('button', { name: 'Remove filter: Labels: Work', exact: true }).click();
        assert.equal(await summary.getByText('Labels: Work').count(), 0);
        assert.equal(await filterButton.evaluate(el => el === document.activeElement), true, 'Removing a chip returns focus to filters');
        await summary.getByRole('button', { name: 'Remove filter: Last month', exact: true }).click();
        await filterButton.click();
        assert.equal(await page.getByRole('combobox', { name: 'Date range' }).inputValue(), 'all');
        assert.equal(await page.getByRole('checkbox', { name: 'Work', exact: true }).isChecked(), false);
        await page.getByRole('combobox', { name: 'Date range' }).selectOption('custom');
        await page.getByLabel('From', { exact: true }).fill('2026-12-10');
        await page.getByLabel('To', { exact: true }).fill('2026-12-01');
        await filterButton.click();
        assert.equal(await page.getByText('Monthly view · Modified').isVisible(), true);
        assert.equal(await page.getByRole('alert').filter({ hasText: 'unavailable account' }).isVisible(), true);
        assert.equal(await page.getByRole('alert').filter({ hasText: 'start date' }).isVisible(), true);
        await filterButton.click();
        await page.getByRole('button', { name: 'Reset filters', exact: true }).click();
        await filterButton.click();
        await page.getByRole('button', { name: 'Cancel', exact: true }).click();
        assert.equal(await page.getByRole('checkbox', { name: 'Select: Imported purchase' }).count(), 0);
        await page.locator('[data-transaction-id="manual"]').waitFor();
        await own.getByRole('link', { name: 'Imported', exact: true }).click();
        await page.waitForURL('**/app/import-history?batch=' + batch);
        assert.equal(new URL(page.url()).searchParams.has('transaction'), false);
        assert.deepEqual(errors, []);
        console.log('PASS transaction header, selection, saved-view warnings and imported filtering at ' + width);
        await page.close();
    }
    for (const [language, filterName, resetName, monthName, warning] of [
        ['de', 'Filter, 0 aktive Filter', 'Filter zurücksetzen', 'Dieser Monat', 'werden nicht gespeichert'],
        ['hu', 'Szűrők, 0 aktív szűrő', 'Szűrők törlése', 'Ez a hónap', 'szűrőket nem mentjük'],
    ]) {
        const page = await browser.newPage({ viewport: { width: 320, height: 900 } });
        await page.addInitScript(lang => localStorage.setItem('i18nextLng', lang), language);
        await page.route('**/api/v1/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            if (pathname.endsWith('/personal-money/me')) return route.fulfill({ status: 204 });
            return route.fulfill({ json: pathname.endsWith('/transactions') ? { items: [], hasMore: false } : [] });
        });
        await page.goto(url);
        await page.getByRole('button', { name: filterName, exact: true }).click();
        await page.getByRole('button', { name: resetName, exact: true }).click();
        assert.equal(await page.getByRole('button').getByText(monthName, { exact: true }).isVisible(), true);
        await page.locator('#transaction-search').fill('test');
        assert.equal(await page.getByRole('status').filter({ hasText: warning }).isVisible(), true);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, language + ' header overflow');
        await page.close();
        console.log('PASS localized transaction filters at 320: ' + language);
    }
    {
        const page = await browser.newPage({ viewport: { width: 375, height: 900 } });
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        const base = { accountId: 'account', accountName: 'Main', kind: 'Expense', visibility: 'Household', amount: '12', currency: 'EUR', occurredOn: '2026-11-04', status: 'Posted', createdAt: '2026-11-04T10:00:00Z', updatedAt: '2026-11-04T10:00:00Z', canModify: true, items: [], labels: [] };
        const rows = Array.from({ length: 102 }, (_, i) => ({ ...base, id: `row-${i}`, description: `Purchase ${i}` }));
        rows.push({ ...base, id: 'refund', description: 'Refund', kind: 'Refund' }, { ...base, id: 'voided', description: 'Voided', status: 'Voided' });
        await page.route('**/api/v1/**', async route => {
            const u = new URL(route.request().url());
            if (u.pathname.includes('/personal-money/')) return route.fulfill({ status: 204 });
            if (u.pathname.endsWith('/transactions')) {
                const cursor = u.searchParams.get('cursorId');
                const start = cursor ? rows.findIndex(row => row.id === cursor) + 1 : 0;
                return route.fulfill({ json: { items: rows.slice(start, start + 40), hasMore: start + 40 < rows.length } });
            }
            return route.fulfill({ json: [] });
        });
        await page.goto(url);
        await page.getByRole('button', { name: 'Select transactions', exact: true }).click();
        await page.getByRole('button', { name: 'Select loaded eligible transactions', exact: true }).click();
        assert.equal(await page.getByText('Selected transactions: 40 / 100').isVisible(), true);
        assert.equal(await page.locator('[data-transaction-id]').count(), 40, 'Selecting loaded transactions does not fetch another page');
        await page.getByRole('button', { name: 'Load older transactions', exact: true }).click();
        await page.locator('[data-transaction-id="row-79"]').waitFor();
        await page.getByRole('button', { name: 'Load older transactions', exact: true }).click();
        await page.locator('[data-transaction-id="row-101"]').waitFor();
        await page.getByRole('checkbox', { name: 'Select: Purchase 101', exact: true }).check();
        await page.getByRole('button', { name: 'Select loaded eligible transactions', exact: true }).click();
        assert.equal(await page.getByText('Selected transactions: 100 / 100').isVisible(), true);
        assert.equal(await page.getByRole('checkbox', { name: 'Select: Purchase 101', exact: true }).isChecked(), true);
        assert.equal(await page.locator('[data-transaction-id] input:checked').count(), 100);
        assert.equal(await page.locator('[data-transaction-id="refund"] input, [data-transaction-id="voided"] input').count(), 0);
        assert.equal(await page.getByRole('status').filter({ hasText: 'some loaded transactions remain unselected' }).isVisible(), true);
        assert.equal(await page.getByRole('button', { name: 'Select loaded eligible transactions', exact: true }).isDisabled(), true);
        await page.close();
        console.log('PASS bulk selection eligibility and 100-item limit');
    }
    for (const existingHistory of [false, true]) {
        const page = await browser.newPage({ viewport: { width: 375, height: 900 } });
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        let historyRequests = 0;
        await page.route('**/api/v1/**', async route => {
            const u = new URL(route.request().url());
            if (u.pathname.includes('/personal-money/')) return route.fulfill({ status: 204 });
            if (u.pathname.endsWith('/transactions')) {
                if (u.searchParams.get('pageSize') === '1') {
                    historyRequests++;
                    return route.fulfill({ json: { items: existingHistory ? [{ id: 'older', items: [], labels: [] }] : [], hasMore: false } });
                }
                return route.fulfill({ json: { items: [], hasMore: false } });
            }
            return route.fulfill({ json: [] });
        });
        await page.goto(url);
        await page.getByRole('button', { name: /^Filters,/ }).click();
        await page.getByRole('combobox', { name: 'Date range' }).selectOption('this-month');
        if (existingHistory) {
            await page.getByRole('heading', { name: 'No matching transactions' }).waitFor();
            await page.getByRole('button', { name: 'Clear all filters', exact: true }).click();
            assert.equal(await page.getByRole('combobox', { name: 'Date range' }).inputValue(), 'all');
        } else {
            await page.getByRole('heading', { name: 'Start your transaction history' }).waitFor();
            assert.equal(await page.getByRole('button', { name: 'Add transaction', exact: true }).count(), 2);
        }
        assert.ok(historyRequests > 0);
        await page.close();
        console.log('PASS transaction empty state: ' + (existingHistory ? 'older history' : 'first use'));
    }
    {
        const page = await browser.newPage({ viewport: { width: 375, height: 900 } });
        await page.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
        await page.route('**/api/v1/**', async route => {
            const u = new URL(route.request().url());
            if (u.pathname.includes('/personal-money/')) return route.fulfill({ status: 204 });
            if (u.pathname.endsWith('/transactions') && u.searchParams.get('pageSize') === '1') {
                return route.fulfill({ status: 500, json: { title: 'History unavailable', status: 500 } });
            }
            return route.fulfill({ json: u.pathname.endsWith('/transactions') ? { items: [], hasMore: false } : [] });
        });
        await page.goto(url);
        await page.getByRole('alert').getByRole('button', { name: 'Try again' }).waitFor();
        assert.equal(await page.getByRole('heading', { name: 'Start your transaction history' }).count(), 0);
        assert.equal(await page.getByRole('heading', { name: 'No matching transactions' }).count(), 0);
        await page.close();
        console.log('PASS transaction history failure is not an empty state');
    }
    const shell = await browser.newPage({ viewport: { width: 1440, height: 900 }, hasTouch: true });
    await shell.addInitScript(() => localStorage.setItem('i18nextLng', 'en'));
    await shell.route('**/api/v1/**', async route => {
        const pathname = new URL(route.request().url()).pathname;
        if (pathname.endsWith('/auth/me')) return route.fulfill({ json: {
            userId: '11111111-1111-4111-8111-111111111111', email: 'owner@example.test',
            displayName: 'Alex', emailVerified: true, onboardingRequired: false,
            household: { householdId: '22222222-2222-4222-8222-222222222222', role: 'Owner' },
        } });
        if (pathname.endsWith('/personal-money/me')) return route.fulfill({ status: 204 });
        if (pathname.endsWith('/transactions')) return route.fulfill({ json: { items: [], hasMore: false } });
        if (pathname.endsWith('/dashboard')) return route.fulfill({ json: { displayName: 'Alex', reportingCurrency: 'EUR', householdBalances: [], accounts: [], cashFlow: [], latestTransactions: [] } });
        return route.fulfill({ json: [] });
    });
    await shell.goto('http://localhost:5286/app/transactions');
    await shell.locator('.av-tx-page > header').getByRole('button', { name: 'Add transaction', exact: true }).waitFor();
    const wideTitleFont = await shell.addStyleTag({ content: '@media (max-width: 600px) { .av-ui .av-topbar h1 { font-family: Arial, sans-serif !important; font-size: 19px !important; } }' });
    assert.equal(await shell.locator('.av-tx-page > header').getByRole('button', { name: 'Add transaction', exact: true }).count(), 1);
    assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'Add transaction', exact: true }).count(), 0);
    for (const width of [320, 359, 360, 361, 375, 400, 479, 480, 481, 600, 601, 1440]) {
        await shell.setViewportSize({ width, height: 900 });
        const heading = shell.locator('.av-topbar').getByRole('heading', { name: 'Transactions', exact: true });
        const help = shell.locator('.av-topbar').getByRole('button', { name: 'About Transactions', exact: true });
        assert.equal(await shell.getByRole('button', { name: 'About Transactions', exact: true }).count(), 1);
        assert.equal(await shell.getByRole('region', { name: 'Transactions', exact: true }).count(), 1);
        const titleBox = await heading.boundingBox(), helpBox = await help.boundingBox();
        assert.ok(helpBox.x >= titleBox.x + titleBox.width && helpBox.x - (titleBox.x + titleBox.width) < 10, 'Help is beside the page title');
        assert.ok(helpBox.width >= 44 && helpBox.height >= 44 && helpBox.x + helpBox.width <= width, 'Help remains a visible 44px target');
        assert.equal(await heading.evaluate(element => element.scrollWidth <= element.clientWidth), true, 'Page title is not truncated at ' + width);
        assert.equal(await shell.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Top bar does not overflow');
        if (width <= 600) {
            assert.ok((await shell.locator('.av-topbar').boundingBox()).height <= 68, 'Mobile top bar stays on one line');
            assert.equal(await shell.locator('.av-topbar').getByRole('combobox', { name: 'Language' }).count(), 0);
            assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'Display and language' }).count(), 1);
            const signOutBox = await shell.locator('.av-topbar').getByRole('button', { name: 'Sign out', exact: true }).boundingBox();
            assert.ok(signOutBox.x >= helpBox.x + helpBox.width && signOutBox.x + signOutBox.width <= width, 'Sign out remains visible without overlapping help at ' + width);
            if (width < 480) assert.ok(signOutBox.width >= 44 && signOutBox.height >= 44, 'Compact sign out has a 44px target');
        } else {
            assert.equal(await shell.locator('.av-topbar').getByRole('combobox', { name: 'Language' }).count(), 1);
            assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'Display and language' }).count(), 0);
        }
        await help.focus();
        await shell.keyboard.press('Enter');
        const popup = shell.locator('[popover]:popover-open');
        await popup.waitFor();
        const popupBox = await popup.boundingBox();
        assert.ok(popupBox.x >= 0 && popupBox.x + popupBox.width <= width, 'Help remains inside the viewport');
        await shell.keyboard.press('Escape');
        assert.equal(await shell.locator('[popover]:popover-open').count(), 0);
        assert.equal(await help.evaluate(element => element === document.activeElement), true);
    }
    await shell.setViewportSize({ width: 320, height: 900 });
    await shell.evaluate(() => {
        window.sheetTransitions = [];
        for (const type of ['transitionrun', 'transitionend']) {
            document.addEventListener(type, event => {
                if (event.target instanceof HTMLDialogElement) {
                    window.sheetTransitions.push({ type, property: event.propertyName, pseudo: event.pseudoElement, elapsed: event.elapsedTime });
                }
            });
        }
    });
    const preferences = shell.locator('.av-topbar').getByRole('button', { name: 'Display and language' });
    await preferences.focus();
    await shell.keyboard.press('Enter');
    let preferencesDialog = shell.getByRole('dialog', { name: 'Display and language' });
    await preferencesDialog.waitFor();
    await shell.waitForFunction(() => window.sheetTransitions.some(event => event.type === 'transitionend' && event.property === 'translate' && !event.pseudo));
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionrun' && event.property === 'translate' && !event.pseudo)), 'Sheet animates into view');
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionend' && event.property === 'translate' && event.elapsed >= 0.15 && event.elapsed <= 0.3)), 'Opening has a visible animation duration');
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionrun' && event.property === 'opacity' && event.pseudo === '::backdrop')), 'Backdrop fades in');
    const sheetBox = await preferencesDialog.boundingBox();
    assert.ok(sheetBox.x === 0 && sheetBox.width === 320 && Math.abs(sheetBox.y + sheetBox.height - 900) < 1, 'Preferences open as a bottom sheet');
    assert.equal(await preferences.getAttribute('aria-expanded'), 'true');
    assert.equal(await shell.evaluate(() => document.body.style.overflow), 'hidden');
    const closePreferences = preferencesDialog.getByRole('button', { name: 'Close', exact: true });
    assert.equal(await closePreferences.count(), 1, 'Only the header close button is shown');
    assert.equal(await preferencesDialog.locator('footer').count(), 0, 'No empty footer remains');
    await closePreferences.focus();
    await shell.keyboard.press('Shift+Tab');
    assert.equal(await preferencesDialog.getByRole('combobox', { name: 'Theme' }).evaluate(element => element === document.activeElement), true);
    await shell.keyboard.press('Tab');
    assert.equal(await closePreferences.evaluate(element => element === document.activeElement), true);
    assert.equal(await preferencesDialog.getByRole('combobox', { name: 'Language' }).inputValue(), 'en');
    await preferencesDialog.getByRole('combobox', { name: 'Theme' }).selectOption('dark');
    await shell.waitForFunction(() => document.documentElement.classList.contains('dark') && localStorage.getItem('averio.theme') === 'dark');
    await shell.evaluate(() => { window.sheetTransitions = []; });
    await shell.keyboard.press('Escape');
    await preferencesDialog.waitFor({ state: 'detached' });
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionrun' && event.property === 'translate' && !event.pseudo)), 'Sheet animates out before it is removed');
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionend' && event.property === 'translate' && event.elapsed >= 0.15 && event.elapsed <= 0.3)), 'Closing has a visible animation duration');
    assert.ok(await shell.evaluate(() => window.sheetTransitions.some(event => event.type === 'transitionrun' && event.property === 'opacity' && event.pseudo === '::backdrop')), 'Backdrop fades out');
    assert.equal(await preferences.evaluate(element => element === document.activeElement), true);
    assert.equal(await preferences.getAttribute('aria-expanded'), 'false');
    assert.equal(await shell.evaluate(() => document.body.style.overflow), '');
    await preferences.click();
    preferencesDialog = shell.getByRole('dialog', { name: 'Display and language' });
    assert.equal(await preferencesDialog.getByRole('combobox', { name: 'Theme' }).inputValue(), 'dark');
    await preferencesDialog.getByRole('combobox', { name: 'Theme' }).selectOption('light');
    await shell.waitForFunction(() => !document.documentElement.classList.contains('dark') && localStorage.getItem('averio.theme') === 'light');
    await preferencesDialog.getByRole('button', { name: 'Close', exact: true }).click();
    await preferencesDialog.waitFor({ state: 'detached' });
    await preferences.click();
    preferencesDialog = shell.getByRole('dialog', { name: 'Display and language' });
    await preferencesDialog.waitFor();
    await preferencesDialog.getByRole('heading', { name: 'Display and language' }).click();
    assert.equal(await preferencesDialog.isVisible(), true, 'Clicking inside the sheet does not close it');
    const dragBox = await preferencesDialog.boundingBox();
    await shell.mouse.move(160, dragBox.y + 15);
    await shell.mouse.down();
    await shell.mouse.move(5, 100);
    await shell.mouse.up();
    assert.equal(await preferencesDialog.isVisible(), true, 'Dragging from the sheet to the backdrop does not close it');
    await shell.mouse.click(5, 100);
    await preferencesDialog.waitFor({ state: 'detached' });
    assert.equal(await preferences.evaluate(element => element === document.activeElement), true, 'Backdrop dismissal restores focus');
    assert.equal(await shell.evaluate(() => document.body.style.overflow), '', 'Backdrop dismissal restores scrolling');
    await preferences.click();
    preferencesDialog = shell.getByRole('dialog', { name: 'Display and language' });
    await preferencesDialog.waitFor();
    await shell.touchscreen.tap(5, 100);
    await preferencesDialog.waitFor({ state: 'detached' });
    await shell.emulateMedia({ reducedMotion: 'reduce' });
    await shell.evaluate(() => { window.sheetTransitions = []; });
    await preferences.click();
    preferencesDialog = shell.getByRole('dialog', { name: 'Display and language' });
    await preferencesDialog.waitFor();
    await shell.keyboard.press('Escape');
    await preferencesDialog.waitFor({ state: 'detached' });
    assert.equal(await shell.evaluate(() => window.sheetTransitions.filter(event => event.property === 'translate').length), 0, 'Reduced motion skips the sheet animation');
    await shell.emulateMedia({ reducedMotion: 'no-preference' });
    for (const [language, title] of [['de', 'Transaktionen'], ['hu', 'Tranzakciók']]) {
        await shell.setViewportSize({ width: 320, height: 900 });
        await shell.getByRole('button', { name: 'Display and language' }).click();
        await shell.getByRole('dialog', { name: 'Display and language' }).getByRole('combobox', { name: 'Language' }).selectOption(language);
        preferencesDialog = shell.getByRole('dialog');
        await shell.keyboard.press('Escape');
        await preferencesDialog.waitFor({ state: 'detached' });
        for (const width of [320, 359, 360, 361, 375, 400, 479, 480, 481, 600]) {
            await shell.setViewportSize({ width, height: 900 });
            const heading = shell.locator('.av-topbar').getByRole('heading', { name: title, exact: true });
            await heading.waitFor();
            assert.equal(await heading.evaluate(element => element.scrollWidth <= element.clientWidth), true, language + ' title is not truncated at ' + width);
            assert.equal(await shell.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, language + ' top bar does not overflow at ' + width);
            assert.ok((await shell.locator('.av-topbar').boundingBox()).height <= 68, language + ' top bar stays on one line at ' + width);
        }
        await shell.setViewportSize({ width: 320, height: 900 });
        await shell.getByRole('button', { name: language === 'de' ? 'Anzeige und Sprache' : 'Nyelv és megjelenés' }).click();
        await shell.getByRole('dialog').getByRole('combobox').first().selectOption('en');
        preferencesDialog = shell.getByRole('dialog');
        await shell.keyboard.press('Escape');
        await preferencesDialog.waitFor({ state: 'detached' });
    }
    await wideTitleFont.evaluate(element => element.remove());
    await shell.goto('http://localhost:5286/app/transactions/');
    await shell.locator('.av-tx-page > header').getByRole('button', { name: 'Add transaction', exact: true }).waitFor();
    assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'Add transaction', exact: true }).count(), 0);
    assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'About Transactions', exact: true }).count(), 1);
    await shell.setViewportSize({ width: 1440, height: 900 });
    await shell.goto('http://localhost:5286/app');
    await shell.locator('.av-topbar').getByRole('button', { name: 'Add transaction', exact: true }).waitFor();
    assert.equal(await shell.getByRole('button', { name: 'About Transactions', exact: true }).count(), 0);
    await shell.locator('.av-topbar').getByRole('button', { name: 'Dark', exact: true }).click();
    await shell.waitForFunction(() => document.documentElement.classList.contains('dark'));
    await shell.locator('.av-topbar').getByRole('button', { name: 'Light', exact: true }).click();
    await shell.waitForFunction(() => !document.documentElement.classList.contains('dark'));
    await shell.setViewportSize({ width: 320, height: 900 });
    assert.equal(await shell.locator('.av-topbar').getByRole('button', { name: 'Sign out', exact: true }).isVisible(), true);
    await shell.locator('.av-topbar').getByRole('button', { name: 'Display and language' }).click();
    await shell.getByRole('dialog', { name: 'Display and language' }).waitFor();
    await shell.keyboard.press('Escape');
    await shell.close();
    console.log('PASS responsive shell preferences, localized titles and transaction actions');
} finally { await browser?.close(); server.kill(); }
