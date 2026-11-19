import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyItemRefunds({ fixture: f, password, session, api, ok, poll, localeFile, artifacts }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const base = '/households/' + f.householdId;
    const account = await ok(page, base + '/accounts', { method: 'POST', body: {
        name: 'Returns account', type: 'Cash', scope: 'Household', ownerHouseholdMemberIds: [], visibility: 'Household',
        includeInHouseholdReports: true, currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date,
    } });
    const categories = await ok(page, base + '/categories?kind=Expense');
    const incomeCategory = (await ok(page, base + '/categories?kind=Income'))[0];
    const categoryA = categories[0], categoryB = categories[1];
    const stats = () => ok(page, base + `/statistics/category-cash-flow?from=${f.date}&to=${f.date}&period=Day`);
    const categoryAmount = (s, id) => s.periods.filter(x => x.categoryId === id).reduce((sum, x) => sum + Number(x.expenseAmount), 0);
    const incomeTotal = s => s.periods.reduce((sum, x) => sum + Number(x.incomeAmount), 0);
    const beforeStats = await stats();
    const post = (kind, items) => ok(page, base + '/transactions', { method: 'POST', body: {
        accountId: account.id, kind, visibility: 'Household', merchant: 'Refund browser shop', note: null, occurredOn: f.date, items, labelIds: [],
    } });
    const original = await post('Expense', [{ description: 'Returned shoes', categoryId: categoryA.id, amount: '120' }, { description: 'Returned shirt', categoryId: categoryB.id, amount: '80' }]);
    const source = () => ok(page, base + '/transactions/' + original.id);
    const balance = async () => (await ok(page, base + '/accounts')).find(a => a.id === account.id).currentBalanceAmount;
    const refundList = () => ok(page, base + '/transactions/' + original.id + '/refunds');
    let saved = await source();
    const a = saved.items[0].id, b = saved.items[1].id;
    await page.goto('http://localhost:5173/app/transactions?transaction=' + original.id);
    const region = () => page.getByRole('region', { name: 'Item refunds', exact: true });
    const merchant = page.getByRole('dialog', { name: 'Edit transaction', exact: true }).getByLabel('Merchant / Store', { exact: true });
    await merchant.fill('Unsaved receipt edit');
    assert.equal(await region().getByRole('button', { name: 'Add refund', exact: true }).isDisabled(), true, 'Save the editing draft before changing its refund revision');
    await merchant.fill('Refund browser shop');
    await region().getByRole('button', { name: 'Add refund', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Add refund', exact: true });
    await dialog.getByLabel('Receipt item', { exact: true }).selectOption(a);
    await dialog.getByLabel(/^Refund amount/).fill('20');
    assert.equal(await dialog.getByRole('button', { name: 'Record refund', exact: true }).isDisabled(), true, 'Actual receipt confirmation is required');
    await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
    const sent = page.waitForRequest(r => r.method() === 'POST' && r.url().endsWith('/transactions/' + original.id + '/refunds'));
    await dialog.getByRole('button', { name: 'Record refund', exact: true }).click();
    const payload = (await sent).postDataJSON();
    await dialog.waitFor({ state: 'hidden' });
    await poll(async () => (await region().getByTestId('refund-net-expense').textContent()).replace(/\s/g, '') === '€180.00', 'net receipt after first item refund');
    assert.equal(await page.getByRole('button', { name: 'Save transaction', exact: true }).isDisabled(), true, 'An old editing draft must not overwrite a refund');
    const initialRefund = (await refundList())[0];
    const retried = await ok(page, base + '/transactions/' + original.id + '/refunds', { method: 'POST', body: payload });
    assert.equal(retried.id, initialRefund.id); assert.equal((await refundList()).length, 1);
    assert.equal((await ok(page, base + '/transactions/' + initialRefund.creditTransactionId)).kind, 'Refund');
    assert.equal(await balance(), '-180');
    const incoming = await post('Income', [{ description: 'Recorded bank return', categoryId: incomeCategory.id, amount: '15' }]);
    const beforeLink = await balance();
    await region().getByRole('button', { name: 'Add refund', exact: true }).click();
    await dialog.getByLabel('Receipt item', { exact: true }).selectOption(b);
    await dialog.getByLabel('Link an existing incoming transaction', { exact: true }).check();
    await dialog.getByLabel('Incoming transaction', { exact: true }).selectOption(incoming.id);
    await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
    await dialog.getByRole('button', { name: 'Link refund', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    await poll(async () => (await source()).amount === '165', 'linked return net expense');
    assert.equal(await balance(), beforeLink, 'Existing credit was counted twice');
    assert.equal((await ok(page, base + '/transactions/' + incoming.id)).kind, 'Refund');
    const after = await stats();
    assert.equal(categoryAmount(after, categoryA.id) - categoryAmount(beforeStats, categoryA.id), 100);
    assert.equal(categoryAmount(after, categoryB.id) - categoryAmount(beforeStats, categoryB.id), 65);
    assert.equal(incomeTotal(after), incomeTotal(beforeStats), 'Refund inflated income');
    for (const [itemId, amount] of [[a, '100'], [b, '65']]) {
        await region().getByRole('button', { name: 'Add refund', exact: true }).click();
        await dialog.getByLabel('Receipt item', { exact: true }).selectOption(itemId);
        await dialog.getByLabel(/^Refund amount/).fill(String(Number(amount) + 0.01));
        await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
        await dialog.getByRole('alert').filter({ hasText: /remaining cost/ }).waitFor();
        assert.equal(await dialog.getByRole('button', { name: 'Record refund', exact: true }).isDisabled(), true);
        await dialog.getByLabel(/^Refund amount/).fill(amount);
        await dialog.getByRole('button', { name: 'Record refund', exact: true }).click();
        await dialog.waitFor({ state: 'hidden' });
        await poll(async () => (await source()).items.find(i => i.id === itemId).amount === '0', 'full item return');
    }
    saved = await source();
    assert.equal(saved.amount, '0'); assert.equal(await balance(), '0');
    assert.deepEqual(saved.items.map(i => i.refundedAmount), ['120', '80']);
    assert.equal(await region().getByRole('button', { name: 'Add refund', exact: true }).isDisabled(), true);
    const records = await refundList();
    assert.equal(records.length, 4); assert.equal(new Set(records.map(r => r.creditTransactionId)).size, 4);
    const linked = records.find(r => r.creditTransactionId === incoming.id);
    await region().locator('[data-refund-id="' + linked.id + '"]').getByRole('button', { name: 'Reverse refund', exact: true }).click();
    const reversal = page.getByRole('dialog', { name: 'Reverse this refund?', exact: true });
    await reversal.getByRole('button', { name: 'Reverse refund', exact: true }).click();
    await reversal.waitFor({ state: 'hidden' });
    await poll(async () => (await source()).amount === '15', 'reversal restores original item cost');
    assert.equal(await balance(), '-15');
    assert.equal((await ok(page, base + '/transactions/' + incoming.id)).status, 'Voided');
    await region().locator('[data-refund-id="' + initialRefund.id + '"]').getByRole('link', { name: 'View returned money', exact: true }).click();
    await page.getByRole('link', { name: 'View original expense', exact: true }).waitFor();
    assert.equal(await page.getByRole('dialog').getByText('Income', { exact: true }).count(), 0);
    await page.getByRole('link', { name: 'View original expense', exact: true }).click();
    await region().waitFor();
    const final = await stats();
    assert.equal(categoryAmount(final, categoryA.id) - categoryAmount(beforeStats, categoryA.id), 0);
    assert.equal(categoryAmount(final, categoryB.id) - categoryAmount(beforeStats, categoryB.id), 15);
    assert.equal(incomeTotal(final), incomeTotal(beforeStats));

    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) for (const width of [375, 1440]) {
        await page.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
        await page.setViewportSize({ width, height: 1000 });
        await page.goto('http://localhost:5173/app/transactions?transaction=' + original.id);
        const copy = localeFile('itemRefunds')['itemRefunds' + language.toUpperCase()];
        const panel = page.getByRole('region', { name: copy.title, exact: true });
        await panel.getByRole('button', { name: copy.add, exact: true }).click();
        const adding = page.getByRole('dialog', { name: copy.add, exact: true });
        await adding.getByLabel(new RegExp('^' + copy.amount)).fill('1');
        await adding.getByLabel(copy.received, { exact: true }).check();
        assert.equal(await adding.getByRole('button', { name: copy.record, exact: true }).isEnabled(), true);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false, 'Item refund form overflow');
        await page.screenshot({ path: path.join(artifacts, `item-refund-${language}-${theme}-${width}.png`), fullPage: true });
        await adding.getByRole('button', { name: { en: 'Cancel', de: 'Abbrechen', hu: 'Mégse' }[language], exact: true }).click();
    }
    console.log('PASS item refund UI: manual/link existing credit, actual-money confirmation, different receipt items, caps, full zero expense, net categories/no income, exact balances, duplicate retry, reversal, origin navigation and EN/DE/HU responsive themes');
}
