import assert from 'node:assert/strict';
import path from 'node:path';

// Uses the same real API, authentication and isolated PostgreSQL as collaboration.
// Only the explicit loading/failure checks intercept reporting requests.
export async function verifyCategoryDashboard({ fixture: f, password, session, ok, poll, localeFile, artifacts, outsider }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const prefix = `/households/${f.householdId}`;
    const start = f.date.slice(0, 8) + '01';
    const previous = new Date(start + 'T12:00:00Z'); previous.setUTCDate(0);
    const tomorrow = new Date(f.date + 'T12:00:00Z'); tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
    const categories = (await ok(page, prefix + '/categories?kind=Expense')).slice(0, 6);
    assert.equal(categories.length, 6);
    const categoryNames = localeFile('systemCategories').systemCategoriesEN;
    const categoryName = categoryNames[categories[0].systemKey] ?? categories[0].name;
    const make = async (amount, category = categories[0], extra = {}) => {
        const input = { accountId: f.sharedAccountId, kind: 'Expense', visibility: 'Household',
            merchant: 'Dashboard fixture', note: null, occurredOn: f.date,
            items: [{ description: 'Dashboard item', categoryId: category.id, amount }], labelIds: [], ...extra };
        return { ...(await ok(page, prefix + '/transactions', { method: 'POST', body: input })), input };
    };
    const dashboard = async () => {
        await page.locator('a[href="/app"]').first().click();
        await page.getByRole('region', { name: 'Spending by category', exact: true }).waitFor();
    };
    const card = () => page.getByRole('region', { name: /^(Spending|Income) by category$/ });
    const total = async expected => {
        await poll(async () => (await card().getByTestId('category-spending-total').textContent()).replace(/\s/g, '') === expected, `category total ${expected}`);
    };
    await page.goto('http://localhost:5173/app');
    await card().getByText('No expenses in EUR this month', { exact: true }).waitFor();
    // An owner can read their private expense, but it must never enter household reports.
    assert.equal(await card().getByTestId('category-spending-total').count(), 0);
    await card().getByRole('link', { name: 'Add expense', exact: true }).click();
    const editor = page.getByRole('dialog', { name: 'Add transaction', exact: true });
    assert.equal(await editor.getByRole('radio', { name: 'Expense', exact: true }).isChecked(), true);
    await editor.getByLabel(/^Account/).selectOption(f.sharedAccountId);
    await editor.getByLabel('Date', { exact: true }).fill(f.date);
    await editor.getByLabel('Merchant / Store', { exact: true }).fill('Dashboard browser purchase');
    await editor.getByLabel('Description', { exact: true }).fill('Browser expense');
    await editor.getByLabel(/^Amount/).fill('12.3456');
    await editor.getByRole('button', { name: 'Category', exact: true }).click();
    await page.getByRole('searchbox', { name: 'Search categories', exact: true }).fill(categoryName);
    await page.getByRole('dialog', { name: 'Category', exact: true }).getByRole('button').filter({ hasText: categoryName }).first().click();
    await editor.getByRole('button', { name: 'Save transaction', exact: true }).click();
    await editor.waitFor({ state: 'hidden' });
    await dashboard();
    await total('€12.3456');

    // Navigate within the SPA so this also exercises cached query invalidation.
    await page.locator('a[href="/app/transactions"]').first().click();
    await page.getByRole('button').filter({ hasText: 'Dashboard browser purchase' }).click();
    const edit = page.getByRole('dialog', { name: 'Edit transaction', exact: true });
    await edit.getByLabel(/^Amount/).fill('20.0001');
    await edit.getByRole('button', { name: 'Save transaction', exact: true }).click();
    await edit.waitFor({ state: 'hidden' });
    await dashboard(); await total('€20.0001');
    await page.locator('a[href="/app/transactions"]').first().click();
    await page.getByRole('button').filter({ hasText: 'Dashboard browser purchase' }).click();
    page.once('dialog', dialog => dialog.accept());
    await edit.getByRole('button', { name: 'Void transaction', exact: true }).click();
    await edit.waitFor({ state: 'hidden' });
    await dashboard();
    await card().getByText('No expenses in EUR this month', { exact: true }).waitFor();

    // Exact first-day inclusion and previous/future exclusion using daily aggregates.
    await make('10.0001', categories[0], { occurredOn: start });
    await make('20.0002');
    await make('999', categories[0], { occurredOn: previous.toISOString().slice(0, 10) });
    await make('888', categories[0], { occurredOn: tomorrow.toISOString().slice(0, 10) });
    await make('777', categories[0], { accountId: f.personalAccountId, visibility: 'Private' });
    for (let i = 1; i < 6; i++) await make(String(i), categories[i]);
    const account = await ok(page, prefix + `/accounts/${f.personalAccountId}`);
    const huf = await ok(page, prefix + '/accounts', { method: 'POST', body: {
        name: 'Dashboard HUF', type: 'Cash', scope: 'Household', ownerHouseholdMemberIds: [],
        visibility: 'Household', includeInHouseholdReports: true, currency: 'HUF',
        balanceAnchorAmount: '0', balanceAnchorDate: f.date,
    } });
    await make('1234.5678', categories[0], { accountId: huf.id });
    const excluded = await make('666', categories[0], { accountId: f.personalAccountId });
    await ok(page, prefix + `/accounts/${f.personalAccountId}`, { method: 'PUT', body: {
        name: account.name, type: account.type, ownerHouseholdMemberIds: account.owners.map(x => x.householdMemberId),
        visibility: 'Household', includeInHouseholdReports: false, expectedUpdatedAt: account.updatedAt,
    } });
    assert.ok(excluded.id);
    await page.reload(); await total('€45.0003');
    await card().locator('[data-category="other"] dt').filter({ hasText: /^Other/ }).waitFor();
    assert.equal(await card().locator('dt').count(), 5);
    assert.equal(await card().getByText(categoryName, { exact: false }).count() > 0, true);
    await card().getByRole('combobox', { name: 'Currency', exact: true }).selectOption('HUF');
    await total('HUF1,234.5678');
    await card().getByRole('combobox', { name: 'Currency', exact: true }).selectOption('EUR');
    await total('€45.0003');
    assert.deepEqual(await card().locator('[data-category]').evaluateAll(rows => rows.map(row => row.dataset.category)),
        [categories[0].id, categories[5].id, categories[4].id, categories[3].id, 'other']);
    assert.equal(await card().locator('[data-category="other"]').evaluate(el => getComputedStyle(el).borderTopWidth), '1px');
    await card().getByText('This month so far', { exact: true }).waitFor();
    await card().getByRole('combobox', { name: 'Transaction type', exact: true }).selectOption('Income');
    await total('€150.1234');
    await card().getByText('Total income', { exact: true }).waitFor();
    await card().getByRole('combobox', { name: 'Currency', exact: true }).selectOption('HUF');
    await card().getByText('No income in HUF this month', { exact: true }).waitFor();
    assert.equal(await card().getByTestId('category-spending-total').count(), 0);
    await card().getByRole('link', { name: 'Add income', exact: true }).click();
    await editor.waitFor();
    assert.equal(await editor.getByRole('radio', { name: 'Income', exact: true }).isChecked(), true);
    await editor.getByRole('button', { name: 'Close', exact: true }).click();
    await poll(() => !new URL(page.url()).searchParams.has('createKind'), 'creation intent cleared');
    await dashboard();
    await card().getByRole('combobox', { name: 'Currency', exact: true }).selectOption('HUF');
    await total('HUF1,234.5678');
    await card().getByRole('combobox', { name: 'Currency', exact: true }).selectOption('EUR');
    await total('€45.0003');
    await card().getByRole('link', { name: 'View report', exact: true }).click();
    await page.waitForURL(url => url.pathname === '/app/statistics');
    assert.equal(new URL(page.url()).searchParams.get('from'), start);
    assert.equal(new URL(page.url()).searchParams.get('to'), f.date);
    assert.equal(new URL(page.url()).searchParams.get('currency'), 'EUR');
    assert.equal(new URL(page.url()).searchParams.get('kind'), 'Expense');
    const reportTotal = async expected => poll(async () => (await page.getByTestId('category-report-total').textContent()).replace(/\s/g, '') === expected, `report total ${expected}`);
    await reportTotal('€45.0003');
    assert.equal(await page.getByTestId('category-report-list').locator('dt').count(), 6);
    await page.getByTestId('category-report-list').getByRole('link', { name: categoryName, exact: true }).click();
    await reportTotal('€30.0003');
    await page.getByTestId('category-daily').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('category'), categories[0].id);
    await page.reload(); await reportTotal('€30.0003');
    await page.goBack(); await reportTotal('€45.0003');
    await page.getByLabel(/^Currency/).selectOption('HUF');
    await page.getByLabel(/^Transaction type/).selectOption('Income');
    await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
    await page.getByTestId('category-report-empty').waitFor();
    assert.equal(await page.getByTestId('category-report-total').count(), 0);
    await page.getByLabel(/^Transaction type/).selectOption('Expense');
    await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
    await reportTotal('HUF1,234.5678');
    await page.getByLabel('From', { exact: true }).fill(tomorrow.toISOString().slice(0, 10));
    await page.getByRole('button', { name: 'Apply filters', exact: true }).click();
    await page.getByRole('alert').waitFor();
    await page.getByRole('link', { name: 'All statistics', exact: true }).click();
    await page.getByRole('heading', { name: 'Statistics', exact: true }).waitFor();
    await dashboard();
    await card().getByRole('link', { name: categoryName, exact: true }).click();
    await reportTotal('€30.0003');
    await dashboard();
    await card().getByRole('link', { name: 'Other', exact: true }).click();
    await reportTotal('€45.0003');
    assert.equal(await page.getByTestId('category-report-list').locator('dt').count(), 6);
    await dashboard();

    // An unrelated real household must not be able to request this report.
    const outsiderStatus = await outsider.evaluate(async url => (await fetch(url, { credentials: 'include' })).status,
        `http://localhost:5180/api/v1${prefix}/statistics/category-cash-flow?from=${start}&to=${f.date}&period=Day`);
    assert.ok([403, 404].includes(outsiderStatus));

    const copy = localeFile('categorySpending');
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await page.evaluate(({ language, theme }) => {
            localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme);
        }, { language, theme });
        await page.setViewportSize({ width: theme === 'dark' ? 390 : 1440, height: 1000 });
        await page.reload();
        const localized = page.getByRole('region', { name: copy['categorySpending' + language.toUpperCase()].title, exact: true });
        await localized.getByTestId('category-spending-total').waitFor();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.equal(await localized.evaluate(el => el.scrollWidth > el.clientWidth + 1), false);
        assert.ok(await page.locator('.av-bento > div').evaluateAll(cards => cards.length >= 8 && cards.every(card => {
            const style = getComputedStyle(card);
            return style.borderLeftWidth === '3px' && style.borderLeftColor !== style.borderRightColor;
        })));
        await localized.screenshot({ path: path.join(artifacts, `category-${language}-${theme}.png`) });
        await localized.getByRole('link', { name: copy['categorySpending' + language.toUpperCase()].report, exact: true }).click();
        await page.getByTestId('category-report-total').waitFor();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, `category-report-${language}-${theme}.png`), fullPage: true });
        await page.goBack();
        await localized.waitFor();
    }
    await page.evaluate(() => localStorage.setItem('i18nextLng', 'en'));
    const reportPattern = '**/statistics/category-cash-flow?*';
    let release;
    const blocked = new Promise(resolve => { release = resolve; });
    await page.route(reportPattern, async route => { await blocked; await route.continue(); });
    await page.reload();
    await card().getByRole('status').waitFor();
    assert.equal(await card().getByText('No expenses in EUR this month', { exact: true }).count(), 0);
    release(); await total('€45.0003');
    await page.unroute(reportPattern);
    await page.route(reportPattern, route => route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' }));
    await page.reload(); await card().getByRole('alert').waitFor();
    assert.equal(await card().getByTestId('category-spending-total').count(), 0);
    assert.equal(await card().getByText('No expenses in EUR this month', { exact: true }).count(), 0);
    await page.unroute(reportPattern);
    await card().getByRole('button', { name: 'Try again', exact: true }).click();
    await total('€45.0003');
    const nextMonth = new Date(start + 'T12:00:00Z');
    nextMonth.setUTCMonth(nextMonth.getUTCMonth() + 1);
    const nextDate = nextMonth.toISOString().slice(0, 10);
    await make('7', categories[0], { occurredOn: nextDate });
    await verifyCategoryCurrencyRollover(page, nextDate);
    console.log('PASS category dashboard: real create/edit/void, precise amounts, UTC range, currencies, expense/income switching and empty recovery and typed creation, report context/reload/back/daily drill-down, privacy/reporting exclusions, ranked top four/Other, consistent card accents, locales/themes, loading/error/retry');
}

// Keep the card mounted across a UTC month boundary. Delay the real response
// to verify the report link also retains the selection while data is loading.
export async function verifyCategoryCurrencyRollover(page, nextDate) {
    const card = page.getByRole('region', { name: 'Spending by category', exact: true });
    const currency = card.getByRole('combobox', { name: 'Currency', exact: true });
    await currency.selectOption('HUF');
    const assertReport = async () => {
        const url = new URL(await card.getByRole('link', { name: 'View report', exact: true }).getAttribute('href'), page.url());
        assert.equal(url.searchParams.get('currency'), 'HUF');
        assert.equal(url.searchParams.get('from'), nextDate);
        assert.equal(url.searchParams.get('to'), nextDate);
        assert.equal(url.searchParams.get('kind'), 'Expense');
    };
    let release;
    const blocked = new Promise(resolve => { release = resolve; });
    const pattern = '**/statistics/category-cash-flow?*';
    const handler = async route => { await blocked; await route.fallback(); };
    await page.route(pattern, handler);
    try {
        await page.clock.setFixedTime(new Date(nextDate + 'T12:00:00Z'));
        await page.evaluate(() => window.dispatchEvent(new Event('focus')));
        await card.getByRole('status').waitFor();
        await assertReport();
        assert.equal(await card.getByTestId('category-empty').count(), 0);
        release();
        await card.getByText('No expenses in HUF this month', { exact: true }).waitFor();
        assert.equal(await currency.inputValue(), 'HUF');
        assert.equal(await card.getByTestId('category-spending-total').count(), 0);
        await assertReport();
        await currency.selectOption('EUR');
        await card.getByTestId('category-spending-total').waitFor();
        assert.equal(await currency.inputValue(), 'EUR');
    } finally {
        release();
        await page.unroute(pattern, handler);
    }
    console.log('PASS category currency remains HUF during month rollover/loading/empty results; explicit EUR selection still works');
}
