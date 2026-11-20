import assert from 'node:assert/strict';

export async function verifyRefundRecovery({ fixture: f, password, session, ok, poll }) {
    const page = await session(f.email, password);
    const base = '/households/' + f.householdId;
    const account = await ok(page, base + '/accounts', { method: 'POST', body: {
        name: 'Refund retry account', type: 'Cash', scope: 'Household', ownerHouseholdMemberIds: [],
        visibility: 'Household', includeInHouseholdReports: true, currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date,
    } });
    const category = (await ok(page, base + '/categories?kind=Expense'))[0];
    const incomeCategory = (await ok(page, base + '/categories?kind=Income'))[0];
    const post = kind => ok(page, base + '/transactions', { method: 'POST', body: {
        accountId: account.id, kind, visibility: 'Household', merchant: 'Refund recovery shop', note: null,
        occurredOn: f.date, labelIds: [], items: [{ description: 'Returned item', categoryId: kind === 'Income' ? incomeCategory.id : category.id, amount: '100' }],
    } });
    const source = id => ok(page, base + '/transactions/' + id);
    const open = async id => {
        await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
        await page.goto('http://localhost:5173/app/transactions?transaction=' + id);
        await page.getByRole('region', { name: 'Item refunds', exact: true }).getByRole('button', { name: 'Add refund', exact: true }).click();
        return page.getByRole('dialog', { name: 'Add refund', exact: true });
    };
    const reconnect = async id => {
        const response = page.waitForResponse(r => r.request().method() === 'GET' && new URL(r.url()).pathname.endsWith('/transactions/' + id));
        await page.clock.setFixedTime(new Date(f.date + 'T12:01:00Z'));
        await page.evaluate(() => { window.dispatchEvent(new Event('offline')); window.dispatchEvent(new Event('online')); });
        await response;
    };
    for (const scenario of [
        { mode: 'new', committed: true },
        { mode: 'existing', committed: true },
        { mode: 'new', committed: false },
        { mode: 'new', committed: true, serverError: true },
    ]) {
        const purchase = await post('Expense');
        const incoming = scenario.mode === 'existing' ? await post('Income') : null;
        const dialog = await open(purchase.id);
        if (incoming) {
            await dialog.getByLabel('Link an existing incoming transaction', { exact: true }).check();
            await dialog.locator('[data-credit-id="' + incoming.id + '"] input[type=radio]').check();
        } else await dialog.getByLabel(/^Refund amount/).fill('100');
        await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
        const refundUrl = '**/api/v1/households/' + f.householdId + '/transactions/' + purchase.id + '/refunds';
        let firstPayload, savedRefundId;
        const loseResponse = async route => {
            if (route.request().method() !== 'POST') return route.continue();
            firstPayload = route.request().postDataJSON();
            if (scenario.committed) {
                const response = await route.fetch();
                assert.equal(response.status(), 200);
                savedRefundId = (await response.json()).id;
            }
            if (scenario.serverError) return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Temporary response failure', status: 503 }) });
            await route.abort('failed');
        };
        await page.route(refundUrl, loseResponse);
        await dialog.getByRole('button', { name: incoming ? 'Link refund' : 'Record refund', exact: true }).click();
        await dialog.getByRole('alert').waitFor();
        await dialog.getByRole('status').filter({ hasText: 'Averio could not confirm' }).waitFor();
        await page.unroute(refundUrl, loseResponse);
        assert.equal((await source(purchase.id)).amount, scenario.committed ? '0' : '100');
        await reconnect(purchase.id);
        if (scenario.committed) {
            await poll(async () => (await page.getByTestId('refund-net-expense').textContent()).replace(/\s/g, '') === '€0.00', 'saved refund version refetched after reconnect');
            assert.notEqual(firstPayload.expectedUpdatedAt, (await source(purchase.id)).updatedAt);
        }
        assert.equal(await dialog.getByRole('combobox', { name: 'Receipt item', exact: true }).isDisabled(), true);
        assert.equal(await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).isDisabled(), true);
        if (!incoming) assert.equal(await dialog.getByLabel(/^Refund amount/).isDisabled(), true);
        if (scenario.serverError) {
            // Later access/business rejection cannot prove the earlier lost write failed.
            const rejectRetry = route => route.request().method() === 'POST'
                ? route.fulfill({ status: 403, contentType: 'application/problem+json', body: JSON.stringify({ title: 'Access changed after the original write', status: 403 }) })
                : route.continue();
            await page.route(refundUrl, rejectRetry);
            const rejection = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/transactions/' + purchase.id + '/refunds'));
            await dialog.getByRole('button', { name: 'Retry refund', exact: true }).click();
            assert.equal((await rejection).status(), 403);
            await dialog.getByRole('alert').filter({ hasText: 'Access changed' }).waitFor();
            assert.equal(await dialog.getByLabel(/^Refund amount/).isDisabled(), true);
            await page.unroute(refundUrl, rejectRetry);
        }
        const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/transactions/' + purchase.id + '/refunds'));
        await dialog.getByRole('button', { name: 'Retry refund', exact: true }).click();
        const retried = await response;
        assert.equal(retried.status(), 200, 'A stable submitted request recovers its idempotent result');
        assert.deepEqual(retried.request().postDataJSON(), firstPayload, 'Keep both concurrency versions and the complete payload across reconnects');
        if (savedRefundId) assert.equal((await retried.json()).id, savedRefundId);
        await dialog.waitFor({ state: 'hidden' });
        const records = await ok(page, base + '/transactions/' + purchase.id + '/refunds');
        assert.equal(records.length, 1);
        assert.equal((await source(purchase.id)).amount, '0');
        assert.equal((await source(records[0].creditTransactionId)).kind, 'Refund');
        await poll(() => page.getByRole('button', { name: 'Save transaction', exact: true }).isEnabled(), 'editor remains usable after recovered refund');
        const creditBeforeReversal = await source(records[0].creditTransactionId);
        const cashBefore = Number((await ok(page, base + '/accounts')).find(a => a.id === account.id).currentBalanceAmount);
        await page.locator('[data-refund-id="' + records[0].id + '"]').getByRole('button', { name: 'Reverse refund', exact: true }).click();
        const reversal = page.getByRole('dialog', { name: 'Reverse this refund?', exact: true });
        await reversal.getByText(incoming ? /account balance stays unchanged/ : /removes the returned-money credit/).waitFor();
        const cancelUrl = refundUrl + '/' + records[0].id + '/cancellation';
        let originalCancel;
        const loseCancellation = async route => {
            originalCancel = route.request().postDataJSON();
            if (scenario.committed) assert.equal((await route.fetch()).status(), 204);
            await route.abort('failed');
        };
        await page.route(cancelUrl, loseCancellation);
        await reversal.getByRole('button', { name: 'Reverse refund', exact: true }).click();
        await reversal.getByRole('alert').waitFor();
        await page.unroute(cancelUrl, loseCancellation);
        // The same refund ID remains safe even when the first cancellation already committed.
        await reversal.getByRole('button', { name: 'Reverse refund', exact: true }).click();
        await reversal.waitFor({ state: 'hidden' });
        await ok(page, base + '/transactions/' + purchase.id + '/refunds/' + records[0].id + '/cancellation',
            { method: 'POST', body: originalCancel });
        const afterCredit = await source(creditBeforeReversal.id);
        assert.equal(afterCredit.status, incoming ? 'Posted' : 'Voided');
        assert.equal(afterCredit.kind, incoming ? 'Income' : 'Refund');
        assert.equal((await source(purchase.id)).amount, '100');
        assert.equal(Number((await ok(page, base + '/accounts')).find(a => a.id === account.id).currentBalanceAmount),
            cashBefore - (incoming ? 0 : 100));
        if (incoming) {
            assert.equal(afterCredit.items[0].categoryId, incomeCategory.id);
            assert.equal(afterCredit.items[0].description, 'Returned item');
            const relinkSource = await source(purchase.id);
            const relink = await ok(page, base + '/transactions/' + purchase.id + '/refunds', { method: 'POST', body: {
                transactionItemId: relinkSource.items[0].id, amount: '100', receivedOn: f.date, operationId: crypto.randomUUID(),
                expectedUpdatedAt: relinkSource.updatedAt, moneyReceived: true, creditTransactionId: incoming.id,
                expectedCreditUpdatedAt: afterCredit.updatedAt,
            } });
            await ok(page, base + '/transactions/' + purchase.id + '/refunds/' + records[0].id + '/cancellation',
                { method: 'POST', body: originalCancel });
            assert.equal((await source(incoming.id)).kind, 'Refund', 'A late old cancellation cannot undo a new link');
            assert.notEqual(relink.id, records[0].id);
        }
    }

    // A definitive 409 must release the snapshot and require a new confirmation.
    const rejected = await post('Expense');
    const dialog = await open(rejected.id);
    await dialog.getByLabel(/^Refund amount/).fill('20');
    await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
    const before = await source(rejected.id);
    await ok(page, base + '/transactions/' + rejected.id, { method: 'PUT', body: {
        accountId: before.accountId, kind: before.kind, visibility: before.visibility, merchant: 'Changed in another session', note: null,
        occurredOn: before.occurredOn, labelIds: [], expectedUpdatedAt: before.updatedAt,
        items: before.items.map(item => ({ existingItemId: item.id, description: item.description, categoryId: item.categoryId, amount: item.amount })),
    } });
    const conflict = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/transactions/' + rejected.id + '/refunds'));
    await dialog.getByRole('button', { name: 'Record refund', exact: true }).click();
    const conflictResponse = await conflict;
    assert.equal(conflictResponse.status(), 409);
    await dialog.getByRole('alert').waitFor();
    assert.equal(await dialog.getByLabel(/^Refund amount/).isEnabled(), true);
    assert.equal(await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).isChecked(), false);
    await reconnect(rejected.id);
    await dialog.getByLabel(/^Refund amount/).fill('30');
    await dialog.getByLabel('I confirm that this money was actually refunded.', { exact: true }).check();
    const accepted = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/transactions/' + rejected.id + '/refunds'));
    await dialog.getByRole('button', { name: 'Record refund', exact: true }).click();
    const acceptedResponse = await accepted;
    assert.equal(acceptedResponse.status(), 200);
    assert.notEqual(acceptedResponse.request().postDataJSON().operationId, conflictResponse.request().postDataJSON().operationId);
    await dialog.waitFor({ state: 'hidden' });
    assert.equal((await source(rejected.id)).amount, '70');
    assert.equal((await ok(page, base + '/transactions/' + rejected.id + '/refunds')).length, 1);
    console.log('PASS refund recovery: both reversal modes with lost committed/uncommitted responses, exact cash, late cancellation after relink; lost responses after commit for manual/full and existing-credit refunds, pre-write network failure, post-commit 503, stable payloads across reconnect, exactly one credit, and editable/reconfirmed 409 recovery');
}
