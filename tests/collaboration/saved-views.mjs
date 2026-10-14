import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifySavedViews({ fixture: f, outsider, password, session, api, ok, poll, artifacts, localeFile }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const prefix = `/households/${f.householdId}`;
    const endpoint = prefix + '/saved-transaction-views';
    const label = async (name, scope = 'Household', accountId = null) => (await ok(page, prefix + '/labels', { method: 'POST', body: { name, scope, accountId } })).id;
    const essential = await label('Essentials'), reviewed = await label('Reviewed');
    const secret = await label('SECRET private label', 'Account', f.privateAccountId);
    const personal = await label('Personal label', 'Account', f.personalAccountId);
    const categories = await ok(page, prefix + '/categories?kind=Expense');
    const create = (merchant, labelIds) => ok(page, prefix + '/transactions', { method: 'POST', body: {
        accountId: f.sharedAccountId, kind: 'Expense', visibility: 'Household', merchant, occurredOn: f.date,
        note: null, labelIds, items: [{ description: merchant, categoryId: categories[0].id, amount: '12.3456' }],
    } });
    const target = await create('Saved view target', [essential, reviewed]);
    await create('Only one label', [essential]);
    // All-label matching is performed before pagination, not on the browser's current page.
    const both = await ok(page, prefix + '/transactions?pageSize=1&labelIds=' + essential + '&labelIds=' + reviewed);
    assert.equal(both.items.length, 1); assert.equal(both.items[0].id, target.id); assert.equal(both.hasMore, false);
    const link = 'http://localhost:5173/app/transactions?period=this-month&kind=Expense';
    await page.goto(link);
    const filterButton = page.locator('button.av-filter-toggle');
    const toolbarTop = (await filterButton.boundingBox()).y;
    await filterButton.click();
    await page.getByRole('dialog', { name: 'Filters', exact: true }).waitFor();
    assert.equal((await filterButton.boundingBox()).y, toolbarTop, 'opening filters must not move the page');
    // URL navigation commits controlled checkbox state asynchronously.
    for (const name of ['Essentials', 'Reviewed']) {
        const checkbox = page.getByRole('checkbox', { name, exact: true });
        await checkbox.click();
        await poll(() => checkbox.isChecked(), `${name} label selected`);
    }
    await poll(() => new URL(page.url()).searchParams.get('labels')?.includes(reviewed), 'labels in URL');
    await page.getByRole('button', { name: 'Show results', exact: true }).click();
    assert.equal(await filterButton.evaluate(el => el === document.activeElement), true, 'filter dialog restores focus');
    await page.getByRole('button').filter({ hasText: 'Saved view target' }).waitFor();
    await page.getByRole('button').filter({ hasText: 'Only one label' }).waitFor({ state: 'hidden' });
    await page.getByRole('button', { name: 'Save current view', exact: true }).click();
    let dialog = page.getByRole('dialog', { name: 'Save current view', exact: true });
    await dialog.getByLabel('View name', { exact: true }).fill('Monthly essentials');
    await dialog.getByRole('button', { name: 'Save current view', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    let views = await ok(page, endpoint), view = views.find(x => x.name === 'Monthly essentials');
    assert.equal(view.filters.period, 'this-month'); assert.equal(view.filters.from, null); assert.deepEqual([...view.filters.labels].sort(), [essential, reviewed].sort());
    await page.getByRole('button', { name: /^Filters/ }).click();
    await page.getByLabel('Date range', { exact: true }).selectOption('previous-3-full-months');
    assert.equal(new URL(page.url()).searchParams.get('period'), 'previous-3-full-months');
    assert.equal(new URL(page.url()).searchParams.has('from'), false);
    await page.getByRole('button', { name: 'Show results', exact: true }).click();
    await page.getByRole('status').filter({ hasText: 'Modified' }).waitFor();
    await page.getByRole('button', { name: 'Manage views', exact: true }).click();
    await page.getByRole('button', { name: 'Update view', exact: true }).click();
    await page.getByRole('dialog', { name: 'Update view', exact: true }).getByRole('button', { name: 'Update view', exact: true }).click();
    await poll(async () => (await ok(page, endpoint))[0].filters.period === 'previous-3-full-months', 'updated preset');
    await page.getByRole('button', { name: 'Manage views', exact: true }).click();
    await page.getByRole('button', { name: 'Rename view', exact: true }).click();
    dialog = page.getByRole('dialog', { name: 'Rename view', exact: true });
    await dialog.getByLabel('View name', { exact: true }).fill('Quarter review');
    await dialog.getByRole('button', { name: 'Rename view', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    view = (await ok(page, endpoint))[0];
    const before = view;
    const duplicate = await api(page, endpoint, { method: 'POST', body: { name: 'quarter REVIEW', filters: view.filters, expectedVersion: null } });
    assert.equal(duplicate.status, 409);
    await ok(page, endpoint + '/' + view.id, { method: 'PUT', body: { name: view.name, filters: view.filters, expectedVersion: view.version } });
    assert.equal((await api(page, endpoint + '/' + view.id, { method: 'PUT', body: { name: 'Stale overwrite', filters: view.filters, expectedVersion: before.version } })).status, 409);
    assert.equal((await api(page, endpoint + '/' + view.id + '?expectedVersion=' + before.version, { method: 'DELETE' })).status, 409);
    const malformed = await api(page, endpoint, { method: 'POST', body: { name: 'Invalid preset', filters: { ...view.filters, period: 'not-a-period' } } });
    assert.equal(malformed.status, 422);
    const second = await session(f.email, password);
    await second.clock.setFixedTime(new Date('2026-11-15T12:00:00Z'));
    await second.goto('http://localhost:5173/app/transactions');
    await second.getByRole('button', { name: 'Quarter review', exact: true }).click();
    await second.getByRole('button', { name: /^Filters/ }).click();
    assert.equal(await second.getByLabel('From', { exact: true }).inputValue(), '2026-08-01');
    assert.equal(await second.getByLabel('To', { exact: true }).inputValue(), '2026-10-31');
    await second.reload();
    await second.getByRole('button', { name: /^Filters/ }).click();
    assert.equal(await second.getByLabel('Date range', { exact: true }).inputValue(), 'previous-3-full-months');
    await second.getByLabel('Date range', { exact: true }).selectOption('last-year');
    await poll(async () => new URL(second.url()).searchParams.get('period') === 'last-year'
        && await second.getByLabel('Date range', { exact: true }).inputValue() === 'last-year', 'last-year navigation committed');
    await second.goBack();
    await poll(async () => new URL(second.url()).searchParams.get('period') === 'previous-3-full-months'
        && await second.getByLabel('Date range', { exact: true }).inputValue() === 'previous-3-full-months', 'previous preset restored by Back');
    await second.goForward();
    await poll(async () => new URL(second.url()).searchParams.get('period') === 'last-year'
        && await second.getByLabel('Date range', { exact: true }).inputValue() === 'last-year', 'last-year preset restored by Forward');

    const peer = await session(f.peerEmail, password);
    assert.deepEqual(await ok(peer, endpoint), []);
    assert.equal((await api(peer, endpoint + '/' + view.id, { method: 'PUT', body: { name: 'Cannot overwrite', filters: view.filters, expectedVersion: view.version } })).status, 404);
    assert.equal((await api(peer, endpoint + '/' + view.id + '?expectedVersion=' + view.version, { method: 'DELETE' })).status, 404);
    const options = await ok(peer, prefix + '/labels/filter-options');
    assert(!options.some(x => x.id === secret)); assert(options.some(x => x.id === personal));
    assert.equal((await ok(peer, prefix + '/transactions?labelIds=' + secret)).items.length, 0);
    const external = await session(outsider.email, password);
    assert.equal((await api(external, endpoint)).status, 404);
    assert.equal((await api(external, prefix + '/labels/filter-options')).status, 404);
    const peerFilters = { account: f.personalAccountId, kind: null, period: 'all', from: null, to: null, activity: null, labels: [personal] };
    const peerView = await ok(peer, endpoint, { method: 'POST', body: { name: 'Personal account review', filters: peerFilters } });
    const account = await ok(page, prefix + '/accounts/' + f.personalAccountId);
    await ok(page, prefix + '/accounts/' + f.personalAccountId, { method: 'PUT', body: {
        name: account.name, type: account.type, ownerHouseholdMemberIds: account.owners.map(x => x.householdMemberId),
        visibility: 'Private', includeInHouseholdReports: false, expectedUpdatedAt: account.updatedAt,
    } });
    const unavailable = (await ok(peer, endpoint))[0]; assert.equal(unavailable.available, false);
    assert(!(await ok(peer, prefix + '/labels/filter-options')).some(x => x.id === personal));
    await peer.goto('http://localhost:5173/app/transactions');
    await peer.getByRole('button', { name: 'Personal account review', exact: true }).click();
    await peer.getByRole('alert').filter({ hasText: 'This view includes an unavailable' }).waitFor();
    assert.equal(new URL(peer.url()).searchParams.get('account'), f.personalAccountId);
    assert(new URL(peer.url()).searchParams.get('labels').includes(personal));
    assert.equal((await api(peer, endpoint + '/' + peerView, { method: 'PUT', body: { name: unavailable.name, filters: peerFilters, expectedVersion: unavailable.version } })).status, 422);
    await ok(peer, endpoint + '/' + peerView + '?expectedVersion=' + unavailable.version, { method: 'DELETE' });

    // A custom range stays fixed, and deleting a view leaves its transactions intact.
    await page.goto(link); await page.getByRole('button', { name: 'Quarter review', exact: true }).click();
    await page.getByRole('button', { name: 'Manage views', exact: true }).click();
    await page.getByRole('button', { name: 'Delete view', exact: true }).click();
    await page.getByRole('dialog', { name: 'Delete view', exact: true }).getByRole('button', { name: 'Delete view', exact: true }).click();
    await poll(async () => (await ok(page, endpoint)).length === 0, 'view deleted');
    assert.equal((await ok(page, prefix + '/transactions/' + target.id)).id, target.id);
    await ok(page, endpoint, { method: 'POST', body: { name: 'Fixed January', filters: { ...view.filters, period: 'custom', from: '2026-01-01', to: '2026-01-31', labels: [] } } });
    await second.goto('http://localhost:5173/app/transactions'); await second.getByRole('button', { name: 'Fixed January', exact: true }).click();
    await second.getByRole('button', { name: /^Filters/ }).click();
    assert.equal(await second.getByLabel('From', { exact: true }).inputValue(), '2026-01-01');
    assert.equal(await second.getByLabel('To', { exact: true }).inputValue(), '2026-01-31');
    // Observe every rendered status, including transient changes during URL navigation.
    await ok(page, endpoint, { method: 'POST', body: { name: 'Relative year', filters: { ...view.filters, period: 'last-year', labels: [] } } });
    await second.goto('http://localhost:5173/app/transactions');
    await second.getByRole('button', { name: 'Fixed January', exact: true }).waitFor();
    await second.evaluate(() => {
        window.modifiedFlashes = [];
        window.modifiedObserver = new MutationObserver(() => {
            for (const status of document.querySelectorAll('[role="status"]')) {
                if (status.textContent.includes('Modified')) window.modifiedFlashes.push(status.textContent);
            }
        });
        window.modifiedObserver.observe(document.body, { subtree: true, childList: true, characterData: true });
    });
    for (let i = 0; i < 6; i++) {
        const name = i % 2 ? 'Relative year' : 'Fixed January';
        await second.getByRole('button', { name, exact: true }).click();
        await poll(() => new URL(second.url()).searchParams.get('period') === (i % 2 ? 'last-year' : 'custom'), 'view switch committed');
        await second.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    }
    assert.deepEqual(await second.evaluate(() => { window.modifiedObserver.disconnect(); return window.modifiedFlashes; }), []);
    await second.getByRole('button', { name: /^Filters/ }).click();
    await second.getByLabel('Date range', { exact: true }).selectOption('this-year');
    await second.getByRole('button', { name: 'Show results', exact: true }).click();
    await second.getByRole('status').filter({ hasText: 'Modified' }).waitFor();
    const copies = localeFile('savedViews');
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await second.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
        await second.setViewportSize({ width: theme === 'dark' ? 390 : 1440, height: 1000 });
        await second.goto('http://localhost:5173/app/transactions?period=previous-3-full-months');
        const copy = copies['savedViews' + language.toUpperCase()];
        await second.getByRole('button', { name: copy.save, exact: true }).click();
        await second.getByRole('dialog', { name: copy.save, exact: true }).getByLabel(copy.name).fill('Review');
        assert.equal(await second.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await second.screenshot({ path: path.join(artifacts, `saved-views-${language}-${theme}.png`), fullPage: true });
        await second.keyboard.press('Escape');
    }
    console.log('PASS saved views: relative dates, rollover, labels before pagination, persistent CRUD, cross-session restore, Back/Forward, optimistic concurrency, member/household privacy, lost account/label access, custom dates and localized layouts');
}

