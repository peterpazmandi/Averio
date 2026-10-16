import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyRecurring({ fixture: f, password, session, api, ok, poll, artifacts, localeFile }) {
    const page = await session(f.email, password);
    await page.clock.setFixedTime(new Date(f.date + 'T12:00:00Z'));
    const prefix = '/households/' + f.householdId + '/recurring';
    const url = 'http://localhost:5173/app/recurring';

    // Create through the ordinary transaction popup, with every supported detail.
    const label = await ok(page, '/households/' + f.householdId + '/labels', { method: 'POST', body: { name: 'Recurring browser label', scope: 'Household' } });
    const activity = await ok(page, '/households/' + f.householdId + '/activities', { method: 'POST', body: { name: 'Recurring browser activity', startsOn: null, endsOn: null } });
    await page.goto('http://localhost:5173/app/transactions');
    await page.getByRole('button', { name: /Add transaction/, exact: false }).first().click();
    const editor = () => page.getByRole('dialog', { name: /Add transaction|Edit schedule/, exact: true });
    assert.equal(await editor().getByLabel('Recurring transaction', { exact: true }).isChecked(), false);
    await editor().getByLabel('Account', { exact: true }).selectOption(f.sharedAccountId);
    await editor().getByLabel('Description', { exact: true }).fill('Broadband service');
    await editor().getByLabel(/^Amount/).fill('10.3456');
    await editor().getByLabel('Merchant / Store', { exact: true }).fill('Browser provider');
    await editor().getByLabel('Note', { exact: true }).fill('Repeat these details');
    await editor().getByRole('button', { name: 'Recurring browser label', exact: true }).click();
    await editor().getByRole('button', { name: /^Activity / }).click();
    await page.getByRole('dialog').last().getByRole('button', { name: 'Recurring browser activity', exact: true }).click();
    await editor().getByRole('button', { name: /Add item/ }).click();
    await editor().getByLabel('Description', { exact: true }).nth(1).fill('Equipment rental');
    await editor().getByLabel(/^Amount/).nth(1).fill('2');
    await editor().getByLabel('Recurring transaction', { exact: true }).check();
    await editor().getByLabel('Name', { exact: true }).fill('Daily broadband');
    await editor().getByLabel('Frequency', { exact: true }).selectOption('Daily');
    await editor().getByLabel('First date', { exact: true }).fill(f.date);
    assert.equal(await editor().getByLabel('Record automatically', { exact: true }).isChecked(), false);
    // Switching mode retains the draft instead of discarding fields.
    await editor().getByLabel('Recurring transaction', { exact: true }).uncheck();
    assert.equal(await editor().getByLabel('Note', { exact: true }).inputValue(), 'Repeat these details');
    await editor().getByLabel('Recurring transaction', { exact: true }).check();
    await editor().getByRole('button', { name: 'Save schedule', exact: true }).click();
    await editor().waitFor({ state: 'hidden' });
    let rules = await ok(page, prefix);
    assert.equal(rules.length, 1);
    const initial = rules[0];
    assert.equal(initial.automatic, false);
    assert.equal(initial.frequency, 'Daily');
    assert.equal(initial.amount, '12.3456');
    assert.equal(initial.items.length, 2);
    assert.equal(initial.merchant, 'Browser provider');
    assert.equal(initial.note, 'Repeat these details');
    assert.deepEqual(initial.labelIds, [label.id]);
    assert.equal(initial.activityId, activity.id);
    assert.deepEqual(await ok(page, prefix + '/' + initial.id + '/occurrences'), []);
    await page.goto(url);
    await page.getByRole('button', { name: 'Review and record', exact: true }).click();
    const review = page.getByRole('dialog', { name: 'Review occurrence', exact: true });
    await review.getByLabel(/^Amount/).first().fill('15.1234');
    await review.getByRole('button', { name: 'Record transaction', exact: true }).click();
    await review.waitFor({ state: 'hidden' });
    const receipt = await ok(page, prefix + '/' + initial.id + '/occurrences');
    assert.equal(receipt.length, 1);
    const tomorrow = new Date(f.date + 'T12:00:00Z'); tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
    assert.equal((await ok(page, prefix))[0].nextDueOn, tomorrow.toISOString().slice(0, 10));
    const retry = await ok(page, prefix + '/' + initial.id + '/occurrences', { method: 'POST',
        body: { expectedVersion: initial.version, dueOn: initial.nextDueOn, skip: false, itemAmounts: ['15.1234', '2'] } });
    assert.equal(retry.transactionId, receipt[0].transactionId);
    assert.equal((await ok(page, '/households/' + f.householdId + '/transactions/' + retry.transactionId)).amount, '17.1234');
    const posted = await ok(page, '/households/' + f.householdId + '/transactions/' + retry.transactionId);
    assert.equal(posted.merchant, initial.merchant);
    assert.equal(posted.note, initial.note);
    assert.equal(posted.activityId, activity.id);
    assert.deepEqual(posted.labels.map(x => x.id), [label.id]);
    assert.deepEqual(posted.items.map(x => x.description), ['Broadband service', 'Equipment rental']);
    await page.getByRole('button', { name: 'Recent occurrences', exact: true }).click();
    await page.getByRole('link', { name: 'View transaction', exact: true }).click();
    await page.getByRole('dialog', { name: 'Edit transaction', exact: true }).waitFor();
    await page.goto(url);
    await page.getByRole('button', { name: 'Edit schedule', exact: true }).click();
    await editor().getByLabel('Pause schedule', { exact: true }).check();
    await editor().getByRole('button', { name: 'Save schedule', exact: true }).click();
    await editor().waitFor({ state: 'hidden' });
    assert.equal((await ok(page, prefix))[0].isPaused, true);
    // Keep a draft open while another tab changes the version.
    await page.getByRole('button', { name: 'Edit schedule', exact: true }).click();
    const current = (await ok(page, prefix))[0];
    await ok(page, prefix + '/' + current.id, { method: 'PUT', body: { ...current, expectedVersion: current.version, name: 'Changed elsewhere' } });
    await editor().getByLabel('Name', { exact: true }).fill('My retained draft');
    await editor().getByRole('button', { name: 'Save schedule', exact: true }).click();
    await editor().getByRole('alert').waitFor();
    assert.equal(await editor().getByLabel('Name', { exact: true }).inputValue(), 'My retained draft');
    await editor().getByRole('button', { name: 'Cancel', exact: true }).last().click();
    const second = await ok(page, prefix, { method: 'POST', body: { ...initial, name: 'Skip example' } });
    await page.reload();
    await page.getByRole('button', { name: 'Skip occurrence', exact: true }).click();
    const skip = page.getByRole('dialog', { name: 'Skip occurrence', exact: true });
    await skip.getByRole('button', { name: 'Skip occurrence', exact: true }).click();
    await skip.waitFor({ state: 'hidden' });
    assert.equal((await ok(page, prefix + '/' + second.id + '/occurrences'))[0].transactionId, null);
    const missingCsrf = await api(page, prefix, { method: 'POST', csrf: false, body: { ...initial, name: 'Rejected' } });
    assert.equal(missingCsrf.status, 400);
    // Recover a schedule after its personal account switches from shared to private.
    const recovery = await ok(page, prefix, { method: 'POST', body: { ...initial, accountId: f.personalAccountId, name: 'Privacy recovery' } });
    const account = await ok(page, '/households/' + f.householdId + '/accounts/' + f.personalAccountId);
    await ok(page, '/households/' + f.householdId + '/accounts/' + account.id, { method: 'PUT', body: {
        name: account.name, type: account.type, ownerHouseholdMemberIds: account.owners.map(x => x.householdMemberId),
        visibility: 'Private', includeInHouseholdReports: false, expectedUpdatedAt: account.updatedAt,
    } });
    await page.reload();
    const recoveryCard = () => page.getByRole('article', { name: 'Privacy recovery', exact: true });
    await recoveryCard().getByRole('button', { name: 'Edit schedule', exact: true }).click();
    assert.equal(await editor().getByLabel('Visibility', { exact: true }).isEnabled(), true);
    await editor().getByLabel('Pause schedule', { exact: true }).check();
    await editor().getByRole('button', { name: 'Save schedule', exact: true }).click();
    await editor().waitFor({ state: 'hidden' });
    assert.equal((await ok(page, prefix)).find(r => r.id === recovery.id).isPaused, true);
    await recoveryCard().getByRole('button', { name: 'Edit schedule', exact: true }).click();
    await editor().getByLabel('Visibility', { exact: true }).selectOption('Private');
    await editor().getByLabel('Pause schedule', { exact: true }).uncheck();
    await editor().getByRole('button', { name: 'Save schedule', exact: true }).click();
    await editor().waitFor({ state: 'hidden' });
    const repaired = (await ok(page, prefix)).find(r => r.id === recovery.id);
    assert.equal(repaired.visibility, 'Private');
    assert.equal(repaired.isPaused, false);
    assert.equal(repaired.canRecord, true);
    const copy = localeFile('recurring');
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        const labels = copy['recurring' + language.toUpperCase()];
        await page.evaluate(({ language, theme }) => { localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme); }, { language, theme });
        await page.setViewportSize({ width: theme === 'dark' ? 390 : 1440, height: 900 });
        await page.goto(url);
        await page.getByRole('heading', { name: labels.title, exact: true, level: 2 }).waitFor();
        await poll(() => page.getByRole('heading', { name: 'Changed elsewhere', exact: true }).isVisible(), 'recurring cards');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, 'recurring-' + language + '-' + theme + '.png'), fullPage: true });
        await page.getByRole('button', { name: labels.edit, exact: true }).first().click();
        await page.getByRole('dialog', { name: labels.edit, exact: true }).waitFor();
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifacts, 'recurring-editor-' + language + '-' + theme + '.png'), fullPage: true });
        await page.getByRole('dialog').getByRole('button', { name: labels.cancel, exact: true }).last().click();
    }
    console.log('PASS recurring creation, confirmation, exact amount, retry, history, pause, stale draft, skip, CSRF and localized responsive layouts');
}
