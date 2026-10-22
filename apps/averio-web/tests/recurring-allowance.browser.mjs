import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import vm from 'node:vm';

const root = process.cwd();
const server = spawn(process.execPath,
    ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5276', '--strictPort'],
    { cwd: root, windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5276/tests/fixtures/personal-money.html';
const artifactDir = path.resolve(root, '../../test-results/recurring-allowance');
fs.mkdirSync(artifactDir, { recursive: true });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const translations = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync('src/i18n/locales/personalMoney.ts', 'utf8'),
    { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, translations);

let browser;
try {
    for (let attempt = 0; attempt < 80; attempt++) {
        try { if ((await fetch(url)).ok) break; } catch { /* Server startup */ }
        await pause(100);
    }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const language of ['en', 'de', 'hu']) for (const width of [375, 1440]) {
        const copy = translations.exports['personalMoney' + language.toUpperCase()];
        const mobile = width < 1024;
        const page = await browser.newPage({ viewport: { width, height: 720 },
            timezoneId: mobile ? 'Pacific/Kiritimati' : 'America/Los_Angeles' });
        // At these instants, the local date is respectively a day ahead/behind UTC.
        // Both must still follow the API's UTC calendar-day contract.
        const mockedUtc = mobile ? '2026-10-15T23:30:00Z' : '2026-10-15T00:30:00Z';
        await page.clock.install({ time: new Date(mockedUtc) });
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.addInitScript(language => localStorage.setItem('i18nextLng', language), language);
        let role = 'owner', failFirstCreate = true, conflictNextUpdate = false, failScheduleLoad = false;
        let migrationNotReady = language === 'en' && mobile;
        let schedule;
        const creationAttempts = [], updates = [];
        let historyRequests = 0, dashboardLedgerRequests = 0, summaryRequests = 0;
        let dashboard = false, walletAvailable = '250', walletEmpty = true;
        await page.route('**/api/v1/**', async route => {
            const request = route.request();
            const pathname = new URL(request.url()).pathname;
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (request.method() === 'OPTIONS') return route.fulfill({ status: 204 });
            if (pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (pathname.endsWith('/households/home'))
                return json({ id: 'home', name: 'Family', countryCode: 'HU', preferredCurrency: 'EUR',
                    role, onboardingCompleted: true, version: '1',
                    members: [{ id: 'member0', displayName: 'Owner', isCurrentUser: true, role, joinedAtUtc: '2026-01-01T00:00:00Z' },
                        { id: 'member1', displayName: 'Partner', isCurrentUser: false, role: 'member', joinedAtUtc: '2026-01-01T00:00:00Z' }] });
            if (pathname.endsWith('/personal-money/me/summary')) {
                summaryRequests++;
                if (walletEmpty) return route.fulfill({ status: 204 });
                return json({ walletId: 'wallet', currency: 'EUR', available: walletAvailable,
                    attributedPrivateFunding: '50', fundingDifference: '200' });
            }
            if (dashboard && pathname.endsWith('/personal-money/me')) {
                dashboardLedgerRequests++;
                if (walletEmpty) return route.fulfill({ status: 204 });
                return json({ walletId: 'wallet', currency: 'EUR', available: walletAvailable,
                    attributedPrivateFunding: '50', fundingDifference: '200', charges: [],
                    entries: Array.from({ length: 25 }, (_, index) => ({
                        id: 'entry-' + index, kind: index === 0 ? 'Grant' : 'PrivateCharge',
                        amount: index === 0 ? '300' : '-3.1234', occurredOn: '2026-10-01',
                        transactionId: index === 0 ? null : 'purchase-' + index,
                        transactionName: index === 0 ? null : 'Purchase ' + index,
                        transferId: null, relatedTransactionId: null, recurringAllowanceRuleId: null,
                    })),
                });
            }
            if (pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR',
                available: '250', attributedPrivateFunding: '50', fundingDifference: '200',
                entries: [], charges: [] });
            if (pathname.endsWith('/personal-money/schedules') && request.method() === 'POST') {
                const data = request.postDataJSON(); creationAttempts.push(data);
                if (failFirstCreate) {
                    failFirstCreate = false;
                    return route.fulfill({ status: 503, contentType: 'application/problem+json',
                        body: JSON.stringify({ title: 'Retry me' }) });
                }
                schedule = { id: 'scheduled-1', recipientMemberId: data.recipientMemberId,
                    currency: data.currency, amount: data.amount, startsOn: data.startsOn,
                    endsOn: data.endsOn ?? null, nextDueOn: data.startsOn,
                    isPaused: false, isStopped: false, isBlocked: false, version: 'version1' };
                return json(schedule);
            }
            if (pathname.endsWith('/personal-money/schedules') && request.method() === 'GET') {
                if (migrationNotReady) return route.fulfill({ status: 503, contentType: 'application/problem+json',
                    body: JSON.stringify({ code: 'recurring_allowance_database_update_required' }) });
                if (failScheduleLoad) return route.fulfill({ status: 500, contentType: 'application/problem+json', body: '{}' });
                return json(schedule ? [schedule] : []);
            }
            if (pathname.includes('/personal-money/schedules/scheduled-1/history')) {
                historyRequests++;
                return json([
                { dueOn: '2026-10-01', amount: '100000000000000.01' },
                ]);
            }
            if (pathname.endsWith('/personal-money/schedules/scheduled-1') && request.method() === 'PUT') {
                const data = request.postDataJSON(); updates.push(data);
                assert.equal(data.expectedVersion, schedule.version);
                if (conflictNextUpdate) {
                    conflictNextUpdate = false;
                    schedule = { ...schedule, amount: '999999999999999.9999', version: 'concurrent-version' };
                    return route.fulfill({ status: 409, contentType: 'application/problem+json',
                        body: JSON.stringify({ title: 'Schedule changed. Review the latest values.',
                            code: 'recurring_allowance_conflict' }) });
                }
                schedule = { ...schedule, amount: data.amount, endsOn: data.endsOn ?? null,
                    isPaused: data.isPaused, isStopped: data.isStopped,
                    nextDueOn: data.isStopped ? null : schedule.nextDueOn,
                    version: 'version' + (updates.length + 1) };
                return route.fulfill({ status: 204 });
            }
            if (pathname.endsWith('/accounts')) return json([]);
            if (pathname.endsWith('/transactions')) return json({ items: [], hasMore: false });
            if (pathname.endsWith('/statistics/boundary-flow')) return json({ months: [] });
            return json([]);
        });
        await page.goto(url);
        if (mobile) {
            await page.getByRole('combobox').first().selectOption('personalMoney');
            await page.getByRole('combobox', { name: copy.sections }).selectOption('owner');
        } else {
            await page.locator('a[href="#household-personal-money"]').click();
            await page.getByRole('button', { name: copy.tabs.owner, exact: true }).click();
        }
        // The new compact UI hides the creation form until the owner asks for it.
        const form = page.getByRole('form', { name: copy.recurring.create, exact: true });
        assert.equal(await form.count(), 0, 'Schedule form should be collapsed by default.');
        if (migrationNotReady) {
            await page.getByText(copy.recurring.schemaUnavailable, { exact: true })
                .waitFor({ timeout: 8000 }).catch(async cause => {
                    const text = (await page.locator('body').innerText()).slice(-4500);
                    await page.screenshot({ path: path.join(artifactDir, 'schema-failure-' + language + '-' + width + '.png'), fullPage: true });
                    throw new Error('Missing schema-ready message. Rendered body: ' + text +
                        '; browser errors: ' + errors.join(' | '), { cause });
                });
            assert.equal(await page.getByRole('button', { name: copy.recurring.create, exact: true }).isDisabled(), true);
            migrationNotReady = false;
            await page.getByRole('button', { name: copy.recurring.retry, exact: true }).click();
            await page.getByText(copy.recurring.none, { exact: true }).waitFor();
        }
        await page.getByRole('button', { name: copy.recurring.create, exact: true }).click();
        await form.waitFor({ timeout: 8000 }).catch(async () => {
            const visible = (await page.locator('body').innerText()).slice(0, 3000);
            await page.screenshot({ path: path.join(artifactDir, 'failure-' + language + '-' + width + '.png'), fullPage: true });
            throw new Error('Recurring owner form was not rendered: ' + visible + '; page errors: ' + errors.join(' | '));
        });
        // The controls are nested inside reusable labels, and the generated select text
        // may contribute to its accessible name in Chromium. Scope to this single form.
        await form.locator('select').first().selectOption('member1');
        await form.locator('input[inputmode="decimal"]').first().fill('220');
        const start = form.getByLabel(copy.recurring.start, { exact: true });
        assert.equal(await start.getAttribute('min'), '2026-10-15',
            'Minimum due date must use UTC even when the browser is in another date.');
        const due = '2026-11-01';
        await start.fill(due);
        const create = form.getByRole('button', { name: copy.recurring.create, exact: true });
        await Promise.all([page.waitForResponse(response =>
            response.url().endsWith('/personal-money/schedules') && response.status() === 503), create.click()]);
        await page.getByText(copy.recurring.createError, { exact: true }).waitFor();
        await Promise.all([page.waitForResponse(response =>
            response.url().endsWith('/personal-money/schedules') && response.status() === 200
            && response.request().method() === 'POST'), create.click()]);
        await page.getByText(copy.recurring.created, { exact: true }).waitFor();
        assert.equal(await form.count(), 0, 'Form should collapse after successful creation.');
        assert.equal(creationAttempts.length, 2);
        assert.deepEqual(creationAttempts[0], creationAttempts[1], 'Create retries must reuse the same operation ID.');
        assert.equal(historyRequests, 0, 'Collapsed grant histories must not trigger API requests.');
        await page.getByText(copy.recurring.history, { exact: true }).click();
        await page.waitForFunction(() => document.body.textContent.includes('2026-10-01'));
        assert.equal(historyRequests, 1, 'Expanding one grant history should request only that schedule.');
        const historyAmount = language === 'en' ? '€100,000,000,000,000.01'
            : language === 'de' ? '100.000.000.000.000,01 €' : '100 000 000 000 000,01 EUR';
        await page.getByText(historyAmount, { exact: true }).waitFor();
        await page.getByRole('button', { name: copy.recurring.manage, exact: true }).click();
        await page.getByRole('button', { name: copy.recurring.pause, exact: true }).click();
        await page.getByText(copy.recurring.status.paused, { exact: true }).waitFor();
        assert.equal(updates[0].isPaused, true);
        await page.getByRole('button', { name: copy.recurring.manage, exact: true }).click();
        await page.getByRole('button', { name: copy.recurring.resume, exact: true }).click();
        await page.getByText(copy.recurring.status.active, { exact: true }).waitFor();
        assert.equal(updates[1].isPaused, false);
        // A competing tab wins. Refetch must reset the stale editor but retain the explanation.
        conflictNextUpdate = true;
        await page.getByRole('button', { name: copy.recurring.manage, exact: true }).click();
        await page.locator('#recurring-edit-scheduled-1 input[inputmode="decimal"]').fill('123.4567');
        await page.getByRole('button', { name: copy.recurring.save, exact: true }).click();
        const exactAmount = language === 'en' ? '€999,999,999,999,999.9999'
            : language === 'de' ? '999.999.999.999.999,9999 €' : '999 999 999 999 999,9999 EUR';
        await page.getByText(exactAmount, { exact: false }).waitFor();
        await page.getByRole('alert').filter({ hasText: 'Schedule changed. Review the latest values.' }).waitFor();
        assert.equal(schedule.amount, '999999999999999.9999', 'Rejected draft must not be saved.');
        await page.getByRole('button', { name: copy.recurring.manage, exact: true }).click();
        assert.equal(await page.locator('#recurring-edit-scheduled-1 input[inputmode="decimal"]').inputValue(), schedule.amount);
        await page.getByRole('button', { name: copy.recurring.hide, exact: true }).click();
        await page.screenshot({ path: path.join(artifactDir, language + '-' + width + '.png'), fullPage: true });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.getByRole('button', { name: copy.recurring.manage, exact: true }).click();
        await page.getByRole('button', { name: copy.recurring.stop, exact: true }).click();
        await page.getByRole('button', { name: copy.recurring.confirmStop, exact: true }).click();
        await page.getByText(copy.recurring.status.stopped, { exact: true }).waitFor();
        // Recipients have no owner panel to recover a failed schedule request.
        role = 'member';
        schedule = { ...schedule, recipientMemberId: 'member0', isStopped: false, nextDueOn: due };
        for (const schemaFailure of [true, false]) {
            migrationNotReady = schemaFailure;
            failScheduleLoad = !schemaFailure;
            await page.goto(url);
            if (mobile) await page.getByRole('combobox').first().selectOption('personalMoney');
            else await page.locator('a[href="#household-personal-money"]').click();
            await page.getByText(schemaFailure ? copy.recurring.schemaUnavailable : copy.recurring.loadError,
                { exact: true }).waitFor();
            migrationNotReady = false; failScheduleLoad = false;
            await page.getByRole('button', { name: copy.recurring.retry, exact: true }).click();
            await page.getByText(exactAmount, { exact: false }).waitFor();
            assert.equal(await page.getByRole('alert').count(), 0);
            // Exercise both the upcoming and paused recipient rendering paths.
            schedule = { ...schedule, isPaused: true };
        }
        dashboard = true;
        await page.goto(url + '?dashboard=1');
        await page.getByText(copy.empty, { exact: true }).waitFor();
        const invalidate = page.getByRole('button', { name: 'Simulate wallet mutation', exact: true });
        walletEmpty = false;
        await invalidate.click();
        await page.locator('dd').first().waitFor();
        const initialBalance = await page.locator('dd').first().innerText();
        walletAvailable = '220.1234';
        await invalidate.click();
        await page.waitForFunction(previous => document.querySelector('dd')?.textContent !== previous, initialBalance);
        assert.match(await page.locator('dd').first().innerText(), /220[.,]1234/);
        assert.equal(summaryRequests, 0, 'Full-history dashboard needs no redundant summary request.');
        assert.equal(dashboardLedgerRequests, 3, 'Wallet mutations must refresh balance and history together.');
        const history = page.getByRole('region', { name: copy.history, exact: true });
        assert.equal(await history.getByRole('listitem').count(), 25, 'Show the complete history beyond 20 entries.');
        await history.getByText('Purchase 24', { exact: true }).waitFor();
        assert.equal(await page.getByText(copy.funded, { exact: true }).count(), 0);
        await page.getByText(copy.available, { exact: true }).waitFor();
        const credit = history.locator('strong').first();
        assert.match(await credit.innerText(), /^\+/);
        assert.match(await credit.getAttribute('class'), /text-emerald-700/);
        const debit = history.locator('strong').nth(1);
        assert.match(await debit.innerText(), /-/);
        assert.match(await debit.getAttribute('class'), /text-red-700/);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await page.screenshot({ path: path.join(artifactDir, 'dashboard-' + language + '-' + width + '.png'), fullPage: true });
        assert.equal(await page.getByRole('link', { name: copy.dashboardManage }).getAttribute('href'),
            '/app/household#household-personal-money');
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS recurring allowance browser: EN/DE/HU mobile desktop, retry, lifecycle, screenshots');
} finally {
    await browser?.close();
    server.kill();
}
