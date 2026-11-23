import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyLinkedTransactionEdits({ fixture: f, password, session, api, ok, poll, localeFile, artifacts }) {
    const page = await session(f.email, password);
    const base = '/households/' + f.householdId;
    const ec = (await ok(page, base + '/categories?kind=Expense'))[0].id;
    const ic = (await ok(page, base + '/categories?kind=Income'))[0].id;
    const post = (kind, amount, name) => ok(page, base + '/transactions', { method: 'POST', body: { accountId: f.sharedAccountId, kind, visibility: 'Household', merchant: name, occurredOn: f.date, labelIds: [], items: [{ description: name, categoryId: kind === 'Expense' ? ec : ic, amount }] } });
    const source = await post('Expense', '100', 'Editable source');
    const credit = await post('Income', '10', 'Editable credit');
    const saved = await ok(page, base + '/transactions/' + source.id);
    await ok(page, base + '/reimbursements', { method: 'POST', body: { transactionId: source.id, expectedUpdatedAt: saved.updatedAt, configuration: { provider: 'Safe edit provider', calculation: 'Fixed', value: '20', destinationAccountId: f.sharedAccountId, paymentDelayDays: 0 } } });
    const claimFor = async () => (await ok(page, base + '/reimbursements?transactionId=' + source.id))[0];
    let claim = await claimFor();
    await ok(page, base + '/reimbursements/' + claim.id + '/status', { method: 'PUT', body: { expectedVersion: claim.version, status: 'Submitted', receiptSubmitted: true, submittedOn: f.date } });
    await page.goto('http://localhost:5173/app/transactions?transaction=' + source.id);
    let editor = page.getByRole('dialog', { name: 'Edit transaction', exact: true });
    await editor.getByLabel('Merchant / Store', { exact: true }).fill('Unsaved description');
    assert.equal(await editor.getByRole('button', { name: 'Link received payment', exact: true }).isDisabled(), true);
    await editor.getByLabel('Merchant / Store', { exact: true }).fill('Editable source');
    await editor.getByRole('button', { name: 'Link received payment', exact: true }).click();
    let dialog = page.getByRole('dialog', { name: 'Link received payment', exact: true });
    await dialog.getByLabel('Search received payments', { exact: true }).fill('Editable credit');
    await dialog.getByRole('radio').check(); await dialog.getByRole('checkbox').check();
    // Commit the link but fail the explicit editor refresh. Retry must be a read only.
    const detailUrl = '**/transactions/' + source.id;
    let block = true;
    const failRefresh = route => route.request().method() === 'GET' && block ? route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Temporary read failure', status: 503 }) }) : route.continue();
    await page.route(detailUrl, failRefresh);
    await dialog.getByRole('button', { name: 'Link received payment', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    await editor.getByRole('button', { name: 'Retry refresh', exact: true }).waitFor();
    assert.equal(await editor.getByRole('button', { name: 'Save transaction', exact: true }).isDisabled(), true);
    block = false;
    await editor.getByRole('button', { name: 'Retry refresh', exact: true }).click();
    await editor.getByText('Linked payment protection', { exact: true }).waitFor();
    await page.unroute(detailUrl, failRefresh);
    assert.equal((await claimFor()).payments.length, 1);
    const assertLocked = async () => {
        assert.equal(await editor.locator('input[type="date"]').isDisabled(), true);
        assert.equal(await editor.locator('input[id$="-amount"]').isDisabled(), true);
        assert.equal(await editor.getByRole('button', { name: '+ Add item', exact: true }).isDisabled(), true);
        assert.equal(await editor.getByRole('button', { name: 'Void transaction', exact: true }).isDisabled(), true);
        assert.equal(await editor.getByLabel('Merchant / Store', { exact: true }).isDisabled(), false);
        assert.equal(await editor.locator('input[id$="-description"]').isDisabled(), false);
        await poll(async () => !(await editor.getByRole('button', { name: 'Save transaction', exact: true }).isDisabled()), 'editable form data loaded');
    };
    await assertLocked();
    await editor.getByLabel('Merchant / Store', { exact: true }).fill('Corrected source');
    await editor.locator('input[id$="-description"]').fill('Corrected source item');
    await editor.getByRole('button', { name: 'Save transaction', exact: true }).click(); await editor.waitFor({ state: 'hidden' });
    assert.equal((await ok(page, base + '/transactions/' + source.id)).merchant, 'Corrected source');
    await page.goto('http://localhost:5173/app/transactions?transaction=' + credit.id);
    editor = page.getByRole('dialog', { name: 'Edit transaction', exact: true });
    await editor.getByText('Linked payment protection', { exact: true }).waitFor(); await assertLocked();
    await editor.getByLabel('Merchant / Store', { exact: true }).fill('Corrected income');
    await editor.getByRole('button', { name: 'Save transaction', exact: true }).click(); await editor.waitFor({ state: 'hidden' });
    const bank = await ok(page, base + '/transactions/' + credit.id);
    assert.equal(bank.merchant, 'Corrected income'); assert.equal(bank.amount, '10'); assert.equal(bank.paymentDetailsLocked, true);
    const current = await claimFor(); assert.equal(current.receivedAmount, '10'); assert.equal(current.outstandingAmount, '10');
    for (const language of ['en', 'de', 'hu']) {
        const copy = localeFile('transactionManagement')['transactionManagement' + language.toUpperCase()];
        await page.evaluate(language => localStorage.setItem('i18nextLng', language), language);
        await page.setViewportSize({ width: language === 'en' ? 1280 : 393, height: 900 });
        await page.goto('http://localhost:5173/app/transactions?transaction=' + source.id);
        editor = page.getByRole('dialog', { name: copy.editTitle, exact: true });
        // Use the protection heading to avoid depending on an editor-title translation key.
        await page.getByText(copy.paymentProtection.title, { exact: true }).waitFor();
        await page.screenshot({ path: path.join(artifacts, 'linked-edit-' + language + '.png') });
        assert.equal(await page.locator('dialog[open]').evaluate(el => el.scrollWidth <= el.clientWidth + 1), true);
    }
    await page.evaluate(() => localStorage.setItem('i18nextLng', 'en'));
    await page.goto('http://localhost:5173/app/transactions?transaction=' + source.id);
    editor = page.getByRole('dialog', { name: 'Edit transaction', exact: true });
    await editor.getByRole('button', { name: 'Unlink payment', exact: true }).click();
    dialog = page.getByRole('dialog', { name: 'Unlink payment', exact: true });
    await dialog.getByRole('button', { name: 'Unlink payment', exact: true }).click(); await dialog.waitFor({ state: 'hidden' });
    await poll(async () => !(await editor.locator('input[id$="-amount"]').isDisabled()), 'unlock without reopening');
    assert.equal(await editor.getByText('Linked payment protection', { exact: true }).count(), 0);
    assert.equal((await claimFor()).receivedAmount, '0');
    assert.equal((await ok(page, base + '/transactions/' + credit.id)).paymentDetailsLocked, false);

    // Retaining account ownership while making it private must not prevent safe edits.
    const privateAccount = await ok(page, base + '/accounts', { method: 'POST', body: { name: 'Linked visibility regression', type: 'Cash', scope: 'Personal', ownerHouseholdMemberIds: [f.memberId], visibility: 'Household', includeInHouseholdReports: true, currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date } });
    const privatePost = (kind, amount) => ok(page, base + '/transactions', { method: 'POST', body: { accountId: privateAccount.id, kind, visibility: 'Household', merchant: 'Before privacy change', occurredOn: f.date, labelIds: [], items: [{ description: kind, categoryId: kind === 'Expense' ? ec : ic, amount }] } });
    const privateSource = await privatePost('Expense', '100');
    const privateCredit = await privatePost('Income', '10');
    const sourceDetail = await ok(page, base + '/transactions/' + privateSource.id);
    await ok(page, base + '/reimbursements', { method: 'POST', body: { transactionId: privateSource.id, expectedUpdatedAt: sourceDetail.updatedAt, configuration: { provider: 'Privacy regression', calculation: 'Fixed', value: '10', destinationAccountId: privateAccount.id, paymentDelayDays: 0 } } });
    const privateClaim = async () => (await ok(page, base + '/reimbursements?transactionId=' + privateSource.id))[0];
    let pc = await privateClaim();
    await ok(page, base + '/reimbursements/' + pc.id + '/status', { method: 'PUT', body: { expectedVersion: pc.version, status: 'Submitted', receiptSubmitted: true, submittedOn: f.date } });
    pc = await privateClaim();
    const creditDetail = await ok(page, base + '/transactions/' + privateCredit.id);
    await ok(page, base + '/reimbursements/' + pc.id + '/payments', { method: 'POST', body: { creditTransactionId: privateCredit.id, operationId: crypto.randomUUID(), expectedVersion: pc.version, expectedCreditUpdatedAt: creditDetail.updatedAt, moneyReceived: true } });
    const coverageBefore = await privateClaim();
    const accountBefore = (await ok(page, base + '/accounts')).find(a => a.id === privateAccount.id);
    await ok(page, base + '/accounts/' + privateAccount.id, { method: 'PUT', body: { name: accountBefore.name, type: accountBefore.type, ownerHouseholdMemberIds: [f.memberId], visibility: 'Private', includeInHouseholdReports: false, expectedUpdatedAt: accountBefore.updatedAt } });
    const peer = await session(f.peerEmail, password);
    for (const record of [privateSource, privateCredit]) {
        await page.goto('http://localhost:5173/app/transactions?transaction=' + record.id);
        editor = page.getByRole('dialog', { name: 'Edit transaction', exact: true });
        await editor.getByText('Linked payment protection', { exact: true }).waitFor();
        await assertLocked();
        await editor.getByLabel('Merchant / Store', { exact: true }).fill('Saved after account became private');
        await editor.getByRole('button', { name: 'Save transaction', exact: true }).click();
        await editor.waitFor({ state: 'hidden' });
        const after = await ok(page, base + '/transactions/' + record.id);
        assert.equal(after.merchant, 'Saved after account became private');
        assert.equal(after.visibility, 'Household');
        assert.equal(after.paymentDetailsLocked, true);
        assert.equal((await api(peer, base + '/transactions/' + record.id)).status, 404);
    }
    assert.deepEqual(await privateClaim(), { ...coverageBefore, description: 'Saved after account became private' });
    const accountAfter = (await ok(page, base + '/accounts')).find(a => a.id === privateAccount.id);
    assert.equal(accountAfter.currentBalanceAmount, accountBefore.currentBalanceAmount);
    assert.equal(accountAfter.visibility, 'Private');
    console.log('PASS linked source and Income descriptive saves after account privacy change, preserved coverage/balance and private reads');
    console.log('PASS linked transaction edits: immediate protection, source/Income metadata saves, dirty draft guard, refresh failure/retry, unlink unlock and EN/DE/HU layouts');
}
