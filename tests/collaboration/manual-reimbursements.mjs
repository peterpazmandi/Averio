import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyManualReimbursements({ fixture: f, password, session, api, ok, artifacts }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const base = '/households/' + f.householdId;
    await page.goto('http://localhost:5173/app/transactions');
    await page.getByRole('button', { name: /Add transaction/ }).first().click();
    const editor = page.getByRole('dialog', { name: 'Add transaction', exact: true });
    await editor.getByLabel('Account', { exact: true }).selectOption(f.sharedAccountId);
    await editor.getByLabel('Description', { exact: true }).fill('Manual pharmacy');
    await editor.getByLabel(/^Amount/).first().fill('100');
    await editor.getByLabel('Track reimbursement for this payment', { exact: true }).check();
    await editor.getByLabel('Reimbursement provider', { exact: true }).fill('Manual health fund');
    await editor.getByLabel(/^Reimbursement estimate/).fill('25');
    await editor.getByRole('button', { name: 'Save transaction', exact: true }).click();
    await editor.waitFor({ state: 'hidden' });
    let claims = await ok(page, base + '/reimbursements');
    assert.equal(claims.length, 1);
    assert.equal(claims[0].potentialAmount, '25'); assert.equal(claims[0].expectedAmount, '0');
    assert.equal(claims[0].status, 'NeedsSubmission');
    assert.equal((await ok(page, base + '/recurring')).length, 0, 'Manual claim created a schedule');

    const categories = await ok(page, base + '/categories?kind=Expense');
    const ordinary = await ok(page, base + '/transactions', { method: 'POST', body: { accountId: f.sharedAccountId, kind: 'Expense', visibility: 'Household', merchant: 'Manual dental',
        note: null, occurredOn: f.date, items: [{ description: 'Dental visit', categoryId: categories.find(c => !c.isArchived).id, amount: '80' }], labelIds: [] } });
    const existing = await ok(page, base + '/transactions/' + ordinary.id);
    const before = await ok(page, base + '/accounts');
    await page.goto('http://localhost:5173/app/transactions?transaction=' + existing.id);
    const detail = page.getByRole('dialog');
    await detail.getByRole('button', { name: 'Track reimbursement', exact: true }).click();
    const create = page.getByRole('dialog', { name: 'Track reimbursement', exact: true });
    await create.getByLabel('Reimbursement provider', { exact: true }).fill('Manual insurer');
    await create.getByLabel(/^Reimbursement estimate/).fill('10');
    const sent = page.waitForRequest(r => r.method() === 'POST' && r.url().endsWith('/reimbursements'));
    await create.getByRole('button', { name: 'Create claim', exact: true }).click();
    const payload = (await sent).postDataJSON();
    await create.waitFor({ state: 'hidden' });
    await detail.getByText('Manual insurer', { exact: false }).waitFor();
    const retries = await Promise.all([ok(page, base + '/reimbursements', { method: 'POST', body: payload }), ok(page, base + '/reimbursements', { method: 'POST', body: payload })]);
    assert.equal(retries[0].id, retries[1].id);
    const perPayment = await ok(page, base + '/reimbursements?transactionId=' + existing.id);
    assert.equal(perPayment.length, 1); assert.equal(perPayment[0].potentialAmount, '10');
    assert.deepEqual(await ok(page, base + '/accounts'), before, 'Tracking an existing expense changed balances');
    assert.equal((await api(page, base + '/reimbursements', { method: 'POST', body: { ...payload, expectedUpdatedAt: '2000-01-01T00:00:00Z' } })).status, 409);
    await page.setViewportSize({ width: 375, height: 1000 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
    await page.screenshot({ path: path.join(artifacts, 'manual-reimbursement-mobile.png'), fullPage: true });
    console.log('PASS manual claims from new/recorded ordinary expenses, shared form, duplicate retries, unchanged balances and no recurring schedule');
}
