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
    const card = () => page.getByRole('region', { name: 'Spending by category', exact: true });
    const total = async expected => {
        await poll(async () => (await card().getByTestId('category-spending-total').textContent()).replace(/\s/g, '') === expected, `category total ${expected}`);
    };
    await page.goto('http://localhost:5173/app');
    await card().getByText('Your spending story starts here', { exact: true }).waitFor();
    // An owner can read their private expense, but it must never enter household reports.
    assert.equal(await card().getByTestId('category-spending-total').count(), 0);
    await card().getByRole('link', { name: 'Add transaction', exact: true }).click();
    const editor = page.getByRole('dialog', { name: 'Add transaction', exact: true });
    await editor.getByLabel('Account', { exact: true }).selectOption(f.sharedAccountId);
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
    await card().getByText('Your spending story starts here', { exact: true }).waitFor();

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
    await card().getByText('Other categories', { exact: false }).waitFor();
    assert.equal(await card().locator('dt').count(), 5);
    assert.equal(await card().getByText(categoryName, { exact: false }).count() > 0, true);
    await card().getByLabel('Spending currency', { exact: true }).selectOption('HUF');
    await total('HUF1,234.5678');
    await card().getByLabel('Spending currency', { exact: true }).selectOption('EUR');
    await total('€45.0003');
    await card().getByRole('link', { name: 'View report', exact: true }).click();
    await page.waitForURL('**/app/statistics');
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
        await localized.screenshot({ path: path.join(artifacts, `category-${language}-${theme}.png`) });
    }
    await page.evaluate(() => localStorage.setItem('i18nextLng', 'en'));
    const reportPattern = '**/statistics/category-cash-flow?*';
    let release;
    const blocked = new Promise(resolve => { release = resolve; });
    await page.route(reportPattern, async route => { await blocked; await route.continue(); });
    await page.reload();
    await card().getByRole('status').waitFor();
    assert.equal(await card().getByText('Your spending story starts here', { exact: true }).count(), 0);
    release(); await total('€45.0003');
    await page.unroute(reportPattern);
    await page.route(reportPattern, route => route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' }));
    await page.reload(); await card().getByRole('alert').waitFor();
    assert.equal(await card().getByTestId('category-spending-total').count(), 0);
    assert.equal(await card().getByText('Your spending story starts here', { exact: true }).count(), 0);
    await page.unroute(reportPattern);
    await card().getByRole('button', { name: 'Try again', exact: true }).click();
    await total('€45.0003');
    console.log('PASS category dashboard: real create/edit/void, precise amounts, UTC range, currencies, privacy/reporting exclusions, Other, locales/themes, loading/error/retry');
}
