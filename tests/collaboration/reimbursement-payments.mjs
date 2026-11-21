import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyReimbursementPayments({ fixture: f, password, session, api, ok, poll, localeFile, artifacts }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const base = '/households/' + f.householdId;
    const expenseCategory = (await ok(page, base + '/categories?kind=Expense'))[0].id;
    const incomeCategory = (await ok(page, base + '/categories?kind=Income'))[0].id;
    const account = await ok(page, base + '/accounts', { method: 'POST', body: { name: 'Reimbursement settlement', type: 'Cash', scope: 'Household', ownerHouseholdMemberIds: [], visibility: 'Household', includeInHouseholdReports: true, currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date } });
    const post = (kind, amount, name) => ok(page, base + '/transactions', { method: 'POST', body: { accountId: account.id, kind, visibility: 'Household', merchant: name, note: 'Original bank details', occurredOn: f.date, labelIds: [], items: [{ description: name, categoryId: kind === 'Expense' ? expenseCategory : incomeCategory, amount }] } });
    const claimFor = async id => (await ok(page, base + '/reimbursements?transactionId=' + id))[0];
    const open = async id => { await page.goto('http://localhost:5173/app/transactions?transaction=' + id); return page.getByRole('region', { name: 'Reimbursements', exact: true }); };
    for (const committed of [true, false]) {
        const source = await post('Expense', '100', 'Settlement source ' + committed);
        const credit = await post('Income', '10', 'Provider first ' + committed);
        const second = await post('Income', '20', 'Provider final ' + committed);
        const sourceRecord = await ok(page, base + '/transactions/' + source.id);
        await ok(page, base + '/reimbursements', { method: 'POST', body: { transactionId: source.id, expectedUpdatedAt: sourceRecord.updatedAt,
            configuration: { provider: 'Settlement fund', calculation: 'Fixed', value: '30', maximumAmount: null, destinationAccountId: account.id, paymentDelayDays: 0, submissionReminderDays: null } } });
        let claim = await claimFor(source.id);
        await ok(page, base + '/reimbursements/' + claim.id + '/status', { method: 'PUT', body: { expectedVersion: claim.version, status: 'Submitted', receiptSubmitted: true, submittedOn: f.date } });
        const beforeAccounts = await ok(page, base + '/accounts');
        const beforeCredit = await ok(page, base + '/transactions/' + credit.id);
        let panel = await open(source.id);
        await panel.getByRole('button', { name: 'Link received payment', exact: true }).click();
        let dialog = page.getByRole('dialog', { name: 'Link received payment', exact: true });
        await dialog.getByLabel('Search received payments', { exact: true }).fill('Provider first ' + committed);
        await dialog.getByRole('radio').check();
        assert.equal(await dialog.getByRole('button', { name: 'Link received payment', exact: true }).isDisabled(), true);
        await dialog.getByRole('checkbox').check();
        const url = '**/reimbursements/' + claim.id + '/payments';
        let firstRequest;
        const lose = async route => { firstRequest = route.request().postDataJSON(); if (committed) { const result = await route.fetch(); assert.equal(result.status(), 200); } await route.abort('failed'); };
        await page.route(url, lose);
        await dialog.getByRole('button', { name: 'Link received payment', exact: true }).click();
        await dialog.getByRole('alert').waitFor();
        await dialog.getByRole('status').filter({ hasText: 'The result is not yet known' }).waitFor();
        assert.equal(await dialog.getByRole('checkbox').isDisabled(), true);
        await page.unroute(url, lose);
        await page.evaluate(() => { window.dispatchEvent(new Event('offline')); window.dispatchEvent(new Event('online')); });
        const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/reimbursements/' + claim.id + '/payments'));
        await dialog.getByRole('button', { name: 'Try again', exact: true }).click();
        const retried = await response;
        assert.equal(retried.status(), 200); assert.deepEqual(retried.request().postDataJSON(), firstRequest);
        await dialog.waitFor({ state: 'hidden' });
        claim = await claimFor(source.id);
        assert.equal(claim.status, 'PartiallyReceived'); assert.equal(claim.receivedAmount, '10'); assert.equal(claim.outstandingAmount, '20');
        const firstLink = claim.payments[0];
        panel = await open(source.id);
        await panel.getByRole('button', { name: 'Link received payment', exact: true }).click();
        dialog = page.getByRole('dialog', { name: 'Link received payment', exact: true });
        await dialog.getByLabel('Search received payments', { exact: true }).fill('Provider final ' + committed);
        await dialog.getByRole('radio').check(); await dialog.getByRole('checkbox').check();
        await dialog.getByRole('button', { name: 'Link received payment', exact: true }).click(); await dialog.waitFor({ state: 'hidden' });
        claim = await claimFor(source.id);
        assert.equal(claim.status, 'Received'); assert.equal(claim.outstandingAmount, '0'); assert.equal(claim.paymentFollowUpDue, false);
        assert.deepEqual(await ok(page, base + '/accounts'), beforeAccounts);
        assert.deepEqual(await ok(page, base + '/transactions/' + credit.id), beforeCredit);
        assert.equal((await api(page, base + '/transactions/' + credit.id + '?expectedUpdatedAt=' + encodeURIComponent(beforeCredit.updatedAt), { method: 'DELETE' })).status, 422);
        panel = await open(source.id);
        await panel.locator('[data-payment-id="' + firstLink.id + '"]').getByRole('button', { name: 'Unlink payment', exact: true }).click();
        const unlinkDialog = page.getByRole('dialog', { name: 'Unlink payment', exact: true });
        const unlinkUrl = '**/reimbursements/' + claim.id + '/payments/*/unlink';
        const lostUnlink = async route => { const result = await route.fetch(); assert.equal(result.status(), 204); await route.abort('failed'); };
        await page.route(unlinkUrl, lostUnlink);
        await unlinkDialog.getByRole('button', { name: 'Unlink payment', exact: true }).click(); await unlinkDialog.getByRole('alert').waitFor();
        await page.unroute(unlinkUrl, lostUnlink);
        await unlinkDialog.getByRole('button', { name: 'Try again', exact: true }).click(); await unlinkDialog.waitFor({ state: 'hidden' });
        claim = await claimFor(source.id);
        assert.equal(claim.status, 'PartiallyReceived'); assert.equal(claim.outstandingAmount, '10');
        const relinked = await ok(page, base + '/reimbursements/' + claim.id + '/payments', { method: 'POST', body: { ...firstRequest, operationId: crypto.randomUUID(), expectedVersion: claim.version } });
        assert.notEqual(relinked.id, firstLink.id);
        await ok(page, base + '/reimbursements/' + claim.id + '/payments/' + firstLink.id + '/unlink', { method: 'POST', body: { expectedVersion: claim.version } });
        assert.equal((await claimFor(source.id)).status, 'Received');
        assert.deepEqual(await ok(page, base + '/accounts'), beforeAccounts);
        // Leave one partially settled claim to exercise matching layouts in every language/theme.
        claim = await claimFor(source.id);
        await ok(page, base + '/reimbursements/' + claim.id + '/payments/' + relinked.id + '/unlink', { method: 'POST', body: { expectedVersion: claim.version } });
        if (committed) {
            for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
                const copy = localeFile('reimbursements')['reimbursements' + language.toUpperCase()];
                await page.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
                await page.setViewportSize({ width: theme === 'dark' ? 375 : 1440, height: 1000 });
                await page.goto('http://localhost:5173/app/transactions?transaction=' + source.id);
                await page.getByRole('region', { name: copy.title, exact: true }).getByRole('button', { name: copy.payments.link, exact: true }).click();
                const localized = page.getByRole('dialog', { name: copy.payments.link, exact: true });
                await localized.getByRole('radio').first().waitFor();
                assert.equal(await localized.evaluate(el => el.scrollWidth <= el.clientWidth + 1), true);
                await page.screenshot({ path: path.join(artifacts, 'reimbursement-payment-' + language + '-' + theme + '.png'), fullPage: false });
                await page.keyboard.press('Escape');
            }
            await page.evaluate(() => { localStorage.setItem('i18nextLng', 'en'); localStorage.setItem('averio.theme', 'light'); });
        }
        assert.equal((await ok(page, base + '/transactions/' + second.id)).kind, 'Income');
    }
    await poll(async () => !(await page.getByRole('alert').count()), 'no lingering settlement errors');
    console.log('PASS reimbursement payments: partial/full receipt, matching and unlink lost responses, unchanged cash/credits, relinking and EN/DE/HU responsive layouts');
}
