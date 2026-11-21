import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyPaymentAccessRecovery({ fixture: f, password, session, api, ok, poll, localeFile, artifacts }) {
    const page = await session(f.email, password);
    const peer = await session(f.peerEmail, password);
    const base = '/households/' + f.householdId;
    const expenseCategory = (await ok(page, base + '/categories?kind=Expense'))[0].id;
    const incomeCategory = (await ok(page, base + '/categories?kind=Income'))[0].id;
    const account = await ok(peer, base + '/accounts', { method: 'POST', body: { name: 'Joint recovery account', type: 'Cash', scope: 'Personal', ownerHouseholdMemberIds: [f.memberId, f.peerMemberId], visibility: 'Household', includeInHouseholdReports: true, currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date } });
    const post = (kind, amount, name) => ok(page, base + '/transactions', { method: 'POST', body: { accountId: account.id, kind, visibility: 'Household', merchant: name, occurredOn: f.date, labelIds: [], items: [{ description: name, categoryId: kind === 'Expense' ? expenseCategory : incomeCategory, amount }] } });
    for (const committed of [true, false]) {
        const source = await post('Expense', '100', 'Private recovery source ' + committed);
        const credit = await post('Income', '10', 'Already matched credit ' + committed);
        const eligible = await post('Income', '5', 'Eligible refund credit ' + committed);
        const other = await post('Expense', '100', 'Refund picker source ' + committed);
        const sourceRecord = await ok(page, base + '/transactions/' + source.id);
        await ok(page, base + '/reimbursements', { method: 'POST', body: { transactionId: source.id, expectedUpdatedAt: sourceRecord.updatedAt, configuration: { provider: 'PRIVATE provider', calculation: 'Fixed', value: '10', destinationAccountId: account.id, paymentDelayDays: 0 } } });
        const claimFor = async () => (await ok(page, base + '/reimbursements?transactionId=' + source.id))[0];
        let claim = await claimFor();
        await ok(page, base + '/reimbursements/' + claim.id + '/status', { method: 'PUT', body: { expectedVersion: claim.version, status: 'Submitted', receiptSubmitted: true, submittedOn: f.date } });
        claim = await claimFor();
        const bankCredit = await ok(page, base + '/transactions/' + credit.id);
        await ok(page, base + '/reimbursements/' + claim.id + '/payments', { method: 'POST', body: { creditTransactionId: credit.id, operationId: crypto.randomUUID(), expectedVersion: claim.version, expectedCreditUpdatedAt: bankCredit.updatedAt, moneyReceived: true } });
        await page.goto('http://localhost:5173/app/transactions?transaction=' + other.id);
        await page.getByRole('region', { name: 'Item refunds', exact: true }).getByRole('button', { name: 'Add refund', exact: true }).click();
        let dialog = page.getByRole('dialog', { name: 'Add refund', exact: true });
        await dialog.getByRole('radio', { name: 'Link an existing incoming transaction', exact: true }).check();
        await dialog.locator('[data-credit-id="' + eligible.id + '"]').waitFor();
        assert.equal(await dialog.locator('[data-credit-id="' + credit.id + '"]').count(), 0);
        const accountRecord = (await ok(peer, base + '/accounts')).find(a => a.id === account.id);
        const changeAccess = async (hidden) => {
            const current = (await ok(peer, base + '/accounts')).find(a => a.id === account.id);
            await ok(peer, base + '/accounts/' + account.id, { method: 'PUT', body: { name: current.name, type: current.type, ownerHouseholdMemberIds: hidden ? [f.peerMemberId] : [f.memberId, f.peerMemberId], visibility: hidden ? 'Private' : 'Household', includeInHouseholdReports: !hidden, expectedUpdatedAt: current.updatedAt } });
        };
        await changeAccess(true);
        assert.deepEqual(await ok(page, base + '/reimbursements?transactionId=' + source.id), []);
        const links = await ok(page, base + '/reimbursements/payment-recovery');
        assert.equal(links.length, 1);
        assert.deepEqual(Object.keys(links[0]).sort(), ['claimId', 'paymentId']);
        assert.deepEqual(await ok(peer, base + '/reimbursements/payment-recovery'), []);
        assert.equal((await api(peer, base + '/reimbursements/payment-recovery', { method: 'POST', body: { links } })).status, 404);
        for (const language of (committed ? ['en', 'de', 'hu'] : ['en'])) {
            await page.evaluate(language => localStorage.setItem('i18nextLng', language), language);
            await page.setViewportSize({ width: language === 'en' ? 1280 : 390, height: 900 });
            await page.goto('http://localhost:5173/app');
            const copy = localeFile('reimbursements')['reimbursements' + language.toUpperCase()].recovery;
            await page.getByRole('button', { name: copy.review, exact: true }).click();
            dialog = page.getByRole('dialog', { name: copy.title, exact: true });
            assert.equal(await dialog.evaluate(el => el.scrollWidth <= el.clientWidth + 1), true);
            assert.equal(await page.getByText('PRIVATE provider', { exact: true }).count(), 0);
            await page.screenshot({ path: path.join(artifacts, 'payment-access-recovery-' + language + '.png') });
            await page.keyboard.press('Escape');
        }
        await page.evaluate(() => localStorage.setItem('i18nextLng', 'en'));
        await page.goto('http://localhost:5173/app');
        await page.getByRole('button', { name: 'Review link recovery', exact: true }).click();
        dialog = page.getByRole('dialog', { name: 'Recover inaccessible payment links', exact: true });
        assert.equal(await dialog.getByRole('button', { name: 'Remove listed links', exact: true }).isDisabled(), true);
        await dialog.getByRole('checkbox').check();
        const url = '**/reimbursements/payment-recovery';
        let intent;
        const lose = async route => {
            if (route.request().method() !== 'POST') return route.continue();
            intent = route.request().postDataJSON();
            if (committed) assert.equal((await route.fetch()).status(), 204);
            await route.abort('failed');
        };
        await page.route(url, lose);
        await dialog.getByRole('button', { name: 'Remove listed links', exact: true }).click();
        await dialog.getByRole('alert').waitFor();
        await page.unroute(url, lose);
        const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/payment-recovery'));
        await dialog.getByRole('button', { name: 'Try again', exact: true }).click();
        const retry = await response;
        assert.equal(retry.status(), 204); assert.deepEqual(retry.request().postDataJSON(), intent);
        await dialog.waitFor({ state: 'hidden' });
        await poll(async () => (await ok(page, base + '/reimbursements/payment-recovery')).length === 0, 'recovery completed');
        const after = (await ok(peer, base + '/accounts')).find(a => a.id === account.id);
        assert.equal(after.currentBalanceAmount, accountRecord.currentBalanceAmount);
        assert.deepEqual(await ok(peer, base + '/transactions/' + credit.id), bankCredit);
        await changeAccess(false);
        claim = await claimFor();
        assert.equal(claim.receivedAmount, '0'); assert.equal(claim.payments.length, 1); assert.ok(claim.payments[0].unlinkedAt);
        const candidates = await ok(page, base + '/transactions?accountId=' + account.id + '&refundEligibleOnly=true');
        assert.ok(candidates.items.some(t => t.id === credit.id));
    }
    console.log('PASS refund picker eligibility and access recovery: real ownership changes, privacy, lost responses, unchanged cash and EN/DE/HU layouts');
}
