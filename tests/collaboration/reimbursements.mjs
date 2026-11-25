import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyReimbursements({ fixture: f, password, session, api, ok, artifacts, localeFile }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const base = '/households/' + f.householdId;
    const name = 'Mortgage reimbursement ' + Date.now();
    await page.goto('http://localhost:5173/app/recurring');
    const startNew = async type => {
        await page.getByRole('button', { name: 'New recurring', exact: true }).first().click();
        const chooser = page.getByRole('dialog', { name: 'Choose a recurring transaction', exact: true });
        await chooser.getByRole('button', { name: new RegExp('^' + type) }).click();
        return page.getByRole('dialog', { name: 'New recurring', exact: true });
    };
    for (const type of ['Recurring expense', 'Recurring income']) {
        const basic = await startNew(type);
        assert.equal(await basic.getByLabel(type === 'Recurring income' ? 'Income' : 'Expense', { exact: true }).isChecked(), true);
        await basic.getByRole('button', { name: 'Cancel', exact: true }).click();
    }
    const dialog = await startNew('Expense with reimbursement');
    assert.equal(await dialog.getByLabel('Expense', { exact: true }).isChecked(), true);
    assert.equal(await dialog.getByLabel('Track reimbursement for this payment', { exact: true }).isChecked(), true);
    await dialog.getByLabel('Account', { exact: true }).selectOption(f.sharedAccountId);
    await dialog.getByLabel('Description', { exact: true }).fill('Mortgage');
    await dialog.getByLabel(/^Amount/).first().fill('150');
    await dialog.getByLabel('Name', { exact: true }).fill(name);
    await dialog.getByLabel('First date', { exact: true }).fill(f.date);
    await dialog.getByRole('button', { name: 'About How much can you claim?', exact: true }).click();
    await page.getByRole('dialog', { name: 'How much can you claim?', exact: true }).waitFor();
    await page.keyboard.press('Escape');
    assert.equal(await dialog.isVisible(), true, 'Closing help must preserve the transaction dialog');
    await dialog.getByRole('textbox', { name: 'Reimbursement provider', exact: true }).fill('Browser employer');
    await dialog.getByLabel(/^Reimbursement estimate/).fill('30');
    await dialog.getByLabel('Destination account', { exact: true }).selectOption(f.sharedAccountId);
    await dialog.getByLabel('Days after recording the expense', { exact: true }).fill('0');
    await dialog.getByLabel('Calculation', { exact: true }).selectOption('Percentage');
    await dialog.getByLabel(/^Percentage of payment/).fill('20');
    await dialog.getByRole('status').filter({ hasText: 'Potential reimbursement per payment:' }).waitFor();
    await dialog.getByLabel('Remind me to submit the receipt', { exact: true }).uncheck();
    assert.equal(await dialog.getByLabel('Days after recording the expense', { exact: true }).count(), 0);
    await dialog.getByLabel('Remind me to submit the receipt', { exact: true }).check();
    await dialog.getByLabel('Days after recording the expense', { exact: true }).fill('0');
    await dialog.getByRole('button', { name: 'Save schedule', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    const rule = (await ok(page, base + '/recurring')).find(r => r.name === name);
    assert.equal(rule.reimbursement.value, '20');
    assert.equal(rule.reimbursement.calculation, 'Percentage');
    assert.equal(rule.reimbursement.submissionReminderDays, 0);
    assert.deepEqual(await ok(page, base + '/reimbursements'), []);
    const record = { method: 'POST', body: { expectedVersion: rule.version, dueOn: f.date } };
    const results = await Promise.all([ok(page, base + '/recurring/' + rule.id + '/occurrences', record), ok(page, base + '/recurring/' + rule.id + '/occurrences', record)]);
    assert.equal(results[0].transactionId, results[1].transactionId);
    const claim = (await ok(page, base + '/reimbursements'))[0];
    assert.equal(claim.expectedAmount, '0'); assert.equal(claim.submissionReminderDue, true);
    const beforeAccounts = await ok(page, base + '/accounts');
    const noConsent = await api(page, base + '/reimbursements/' + claim.id + '/status', { method: 'PUT', body: {
        expectedVersion: claim.version, status: 'Submitted', submittedOn: f.date, receiptSubmitted: false,
    } });
    assert.equal(noConsent.status, 422);
    await page.goto('http://localhost:5173/app');
    await page.getByRole('link', { name: 'Money back', exact: true }).click();
    const overviewCard = () => page.locator('div.overflow-hidden').filter({ has: page.getByRole('heading', { name: 'Mortgage', exact: true }) });
    await overviewCard().getByText('Needs submission', { exact: true }).waitFor();
    await page.getByRole('link', { name: 'Dashboard', exact: true }).click();
    const panel = page.getByRole('region', { name: 'Reimbursements', exact: true });
    await panel.getByText('Needs submission', { exact: true }).waitFor();
    await panel.getByRole('button', { name: 'Mark receipt as submitted', exact: true }).click();
    const submission = page.getByRole('dialog', { name: 'Mark receipt as submitted', exact: true });
    assert.equal(await submission.getByRole('button', { name: 'Confirm', exact: true }).isDisabled(), true);
    await submission.getByLabel('Claim reference (optional)', { exact: true }).fill('BROWSER-CLAIM');
    await submission.getByLabel('I confirm that I submitted this receipt to the reimbursement provider outside Averio.', { exact: true }).check();
    await submission.getByRole('button', { name: 'Confirm', exact: true }).click();
    await submission.waitFor({ state: 'hidden' });
    await panel.getByText('Submitted', { exact: true }).waitFor();
    const overviewReload = page.waitForResponse(r => r.request().method() === 'GET' && r.url().includes('/money-back?'));
    await page.getByRole('link', { name: 'Money back', exact: true }).click();
    assert.equal((await overviewReload).status(), 200);
    await overviewCard().getByText('Submitted', { exact: true }).waitFor();
    await page.getByRole('link', { name: 'Dashboard', exact: true }).click();
    const submitted = (await ok(page, base + '/reimbursements'))[0];
    assert.equal(submitted.expectedAmount, '30'); assert.equal(submitted.receivedAmount, '0');
    assert.equal(submitted.reference, 'BROWSER-CLAIM'); assert.equal(submitted.events.length, 2);
    assert.deepEqual(await ok(page, base + '/accounts'), beforeAccounts, 'Claim submission changed account balances');
    await panel.getByRole('link', { name: 'View original payment', exact: true }).click();
    await page.getByRole('dialog').waitFor();
    await page.getByRole('region', { name: 'Reimbursements', exact: true }).getByText('BROWSER-CLAIM', { exact: true }).waitFor();
    await page.getByRole('dialog').getByRole('button', { name: 'Close', exact: true }).first().click();
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await page.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
        await page.goto('http://localhost:5173/app');
        for (const width of [375, 1440]) {
            await page.setViewportSize({ width, height: 1000 });
            const copy = localeFile('reimbursements')['reimbursements' + language.toUpperCase()];
            const name = copy.title;
            const region = page.getByRole('region', { name, exact: true });
            await region.waitFor();
            assert.ok((await region.boundingBox()).width >= 300, 'Reimbursement section collapsed in dashboard grid');
            assert.equal(await region.evaluate(el => el.scrollWidth > el.clientWidth + 1), false, 'Claim content overflow');
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, language + ' overflow');
            await region.screenshot({ path: path.join(artifacts, 'reimbursements-' + language + '-' + theme + '-' + width + '.png') });
            await page.goto('http://localhost:5173/app/recurring');
            const editName = { en: 'Edit schedule', de: 'Zeitplan bearbeiten', hu: 'Ütemezés szerkesztése' }[language];
            await page.getByRole('article', { name: rule.name, exact: true }).getByRole('button', { name: editName, exact: true }).click();
            const editing = page.getByRole('dialog', { name: editName, exact: true });
            const settingsName = copy.settings;
            const settings = editing.getByRole('heading', { name: settingsName, exact: true }).locator('../..');
            const about = localeFile(language)[language].common.infoAbout;
            for (const [subject, explanation] of [[copy.provider, copy.providerHelp], [copy.edit.paymentTiming, copy.edit.paymentTimingHelp]]) {
                await editing.getByRole('button', { name: about.replace('{{subject}}', subject), exact: true }).click();
                const help = page.getByRole('dialog', { name: subject, exact: true });
                await help.getByText(explanation, { exact: true }).waitFor();
                assert.equal(await help.evaluate(el => el.scrollWidth > el.clientWidth + 1), false, 'Reimbursement help overflow');
                await page.keyboard.press('Escape');
                assert.equal(await editing.isVisible(), true, 'Help dismissal closed the editor');
            }
            assert.equal(await settings.evaluate(el => el.scrollWidth > el.clientWidth + 1), false, 'Reimbursement editor overflow');
            await settings.screenshot({ path: path.join(artifacts, 'reimbursement-editor-' + language + '-' + theme + '-' + width + '.png') });
            await editing.getByRole('button', { name: { en: 'Cancel', de: 'Abbrechen', hu: 'Mégse' }[language], exact: true }).click();
            await page.goto('http://localhost:5173/app');

        }
    }
    await page.evaluate(() => localStorage.setItem('i18nextLng', 'en'));
    const peer = await session(f.peerEmail, password);
    const destination = await ok(peer, base + '/accounts', { method: 'POST', body: { name: 'Reimbursement access loss', type: 'Cash',
        scope: 'Personal', ownerHouseholdMemberIds: [f.memberId, f.peerMemberId], visibility: 'Household', includeInHouseholdReports: true,
        currency: 'EUR', balanceAnchorAmount: '0', balanceAnchorDate: f.date } });
    const pauseName = name + ' access loss';
    const created = await ok(page, base + '/recurring', { method: 'POST', body: { ...rule, name: pauseName, expectedVersion: null,
        reimbursement: { ...rule.reimbursement, destinationAccountId: destination.id } } });
    const account = await ok(peer, base + '/accounts/' + destination.id);
    await ok(peer, base + '/accounts/' + destination.id, { method: 'PUT', body: { name: account.name, type: account.type,
        ownerHouseholdMemberIds: [f.peerMemberId], visibility: 'Household', includeInHouseholdReports: true, expectedUpdatedAt: account.updatedAt } });
    await page.goto('http://localhost:5173/app/recurring');
    await page.getByRole('article', { name: pauseName, exact: true }).getByRole('button', { name: 'Edit schedule', exact: true }).click();
    const pausedEditor = page.getByRole('dialog', { name: 'Edit schedule', exact: true });
    assert.equal(await pausedEditor.getByLabel('Destination account', { exact: true }).evaluate(el => el.checkValidity()), false);
    await pausedEditor.getByLabel('Pause schedule', { exact: true }).check();
    assert.equal(await pausedEditor.getByLabel('Destination account', { exact: true }).evaluate(el => el.checkValidity()), true);
    await pausedEditor.getByRole('button', { name: 'Save schedule', exact: true }).click();
    await pausedEditor.waitFor({ state: 'hidden' });
    assert.equal((await ok(page, base + '/recurring')).find(r => r.id === created.id).isPaused, true);
    console.log('PASS reimbursement editor, mandatory consent, concurrent occurrence retry, dashboard/details, no balance changes and EN/DE/HU mobile/desktop');
}
