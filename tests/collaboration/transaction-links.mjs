import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyTransactionLinks({ fixture: f, outsider, password, session, ok, poll, artifacts, localeFile }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const prefix = `/households/${f.householdId}`;
    const categories = await ok(page, prefix + '/categories?kind=Expense');
    const make = (merchant, accountId, occurredOn = f.date) => ok(page, prefix + '/transactions', { method: 'POST', body: {
        accountId, occurredOn, merchant, kind: 'Expense', visibility: 'Household', note: null, labelIds: [],
        items: [{ description: merchant, categoryId: categories[0].id, amount: '12.3456' }],
    } });
    const target = await make('Navigation target', f.sharedAccountId);
    await make('Navigation other account', f.personalAccountId);
    const old = await make('Navigation old date', f.sharedAccountId, '2020-01-02');
    const query = new URLSearchParams({ account: f.sharedAccountId, kind: 'Expense', from: f.date, to: f.date });
    const listUrl = 'http://localhost:5173/app/transactions?' + query;
    const targetRow = () => page.locator('button').filter({ hasText: 'Navigation target' });
    const editor = () => page.getByRole('dialog', { name: 'Edit transaction', exact: true });
    await page.goto(listUrl);
    await targetRow().waitFor();
    assert.equal(await page.getByRole('button').filter({ hasText: 'Navigation other account' }).count(), 0);
    assert.equal(await page.getByRole('button').filter({ hasText: 'Navigation old date' }).count(), 0);
    await page.reload(); await targetRow().waitFor();
    await page.getByRole('button', { name: /^Filters/ }).click();
    assert.equal(await page.getByLabel(/^Account/).inputValue(), f.sharedAccountId);
    assert.equal(await page.getByLabel('From', { exact: true }).inputValue(), f.date);
    await page.getByRole('button', { name: 'Income', exact: true }).click();
    await poll(() => new URL(page.url()).searchParams.get('kind') === 'Income', 'kind written to URL');
    await targetRow().waitFor({ state: 'hidden' });
    await page.goBack(); await targetRow().waitFor();
    assert.equal(await page.getByRole('button', { name: 'Expense', exact: true }).getAttribute('aria-pressed'), 'true');
    await page.getByLabel('From', { exact: true }).fill('2099-01-01');
    await page.getByRole('alert').waitFor();
    await page.goBack(); await targetRow().waitFor();
    await page.getByRole('button', { name: 'Show results', exact: true }).click();
    await targetRow().click(); await editor().waitFor();
    assert.equal(new URL(page.url()).searchParams.get('transaction'), target.id);
    await page.goBack(); await editor().waitFor({ state: 'hidden' }); await targetRow().waitFor();
    await page.goForward(); await editor().waitFor();
    await page.reload(); await editor().waitFor();
    assert.equal(await editor().getByLabel('Merchant / Store', { exact: true }).inputValue(), 'Navigation target');
    await editor().getByRole('button', { name: 'Close', exact: true }).click();
    await poll(() => !new URL(page.url()).searchParams.has('transaction'), 'detail closed');
    assert.equal(new URL(page.url()).searchParams.get('account'), f.sharedAccountId);

    // Direct lookup works even when the transaction cannot appear in the filtered page.
    await page.goto(listUrl + '&transaction=' + old.id); await editor().waitFor();
    assert.equal(await editor().getByLabel('Merchant / Store', { exact: true }).inputValue(), 'Navigation old date');
    await editor().getByRole('button', { name: 'Close', exact: true }).click();
    await page.goto('http://localhost:5173/app');
    await page.locator(`a[href*="transaction=${target.id}"]`).click(); await editor().waitFor();
    await page.goBack();
    await page.goto('http://localhost:5173/app/accounts');
    await page.getByRole('link', { name: 'View transactions', exact: true }).first().waitFor();
    await page.locator(`a[href*="account=${f.sharedAccountId}"]`).click(); await targetRow().waitFor();
    assert.equal(new URL(page.url()).searchParams.get('dates'), 'all');
    await page.getByRole('button').filter({ hasText: 'Navigation old date' }).waitFor();

    // Voided records remain inspectable but cannot reopen an editable form.
    const record = await ok(page, prefix + '/transactions/' + old.id);
    await ok(page, prefix + '/transactions/' + old.id + '?expectedUpdatedAt=' + encodeURIComponent(record.updatedAt), { method: 'DELETE' });
    await page.goto(listUrl + '&transaction=' + old.id);
    const details = page.getByRole('dialog', { name: 'Transaction details', exact: true });
    await details.getByText('This transaction is read-only.', { exact: true }).waitFor();
    await details.getByText('Voided', { exact: true }).waitFor();
    assert.equal(await details.getByRole('button', { name: 'Save transaction' }).count(), 0);

    // A valid ID does not bypass household authorization, and a missing ID is safe.
    for (const id of [outsider.privateTransactionId, '00000000-0000-0000-0000-000000000001']) {
        await page.goto(listUrl + '&transaction=' + id);
        await details.getByRole('alert').waitFor();
        assert.equal(await page.getByText('SECRET note', { exact: true }).count(), 0);
        await details.getByRole('button', { name: 'Close', exact: true }).first().click();
        await targetRow().waitFor();
    }
    const copies = localeFile('transactionLinks');
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await page.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
        await page.setViewportSize({ width: theme === 'dark' ? 390 : 1440, height: 1000 });
        await page.goto(listUrl + '&transaction=' + old.id);
        const localized = page.getByRole('dialog', { name: copies['transactionLinks' + language.toUpperCase()].details, exact: true });
        await localized.getByText(copies['transactionLinks' + language.toUpperCase()].readOnly, { exact: true }).waitFor();
        assert.equal(await localized.evaluate(el => el.scrollWidth > el.clientWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, `transaction-links-${language}-${theme}.png`), fullPage: true });
    }
    console.log('PASS transaction links: URL filters, refresh/Back/Forward, direct detail outside list, account/dashboard navigation, voided read-only, missing/foreign IDs, locales/layouts');
}
