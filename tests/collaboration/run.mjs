import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
import { verifyCategoryDashboard } from './category-dashboard.mjs';
import { verifyTransactionLinks } from './transaction-links.mjs';
import { verifyRefresh } from './refresh.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const web = path.join(root, 'apps/averio-web');
const require = createRequire(path.join(web, 'package.json'));
const { chromium } = require('playwright');
const ts = require('typescript');
const ui = 'http://localhost:5173';
const apiUrl = 'http://localhost:5180/api/v1';
const controlUrl = 'http://127.0.0.1:5181';
const artifacts = path.join(root, 'test-results/collaboration');
fs.mkdirSync(artifacts, { recursive: true });
const children = [];
const contexts = [];
const errors = [];
let browser;

function start(command, args, cwd, name, env = {}) {
    const log = fs.openSync(path.join(artifacts, name + '.log'), 'w');
    const child = spawn(command, args, { cwd, env: { ...process.env, ...env }, stdio: ['ignore', log, log], windowsHide: true });
    fs.closeSync(log);
    child.on('error', error => errors.push(String(error)));
    children.push(child);
    return child;
}
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function poll(action, description, timeout = 30_000) {
    const until = Date.now() + timeout;
    let last;
    do {
        try { const result = await action(); if (result) return result; }
        catch (error) { last = error; }
        await pause(100);
    } while (Date.now() < until);
    throw new Error(`Timed out: ${description}. ${last ?? ''}`);
}
async function control(route, data) {
    const response = await fetch(controlUrl + route, data === undefined ? {} : {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error(`Test control ${route}: ${response.status}`);
    return response.status === 204 ? undefined : response.json();
}
async function mail(email, kind) {
    return poll(() => control(`/mail?email=${encodeURIComponent(email)}&kind=${kind}`), `captured ${kind}`);
}
async function api(page, route, { method = 'GET', body, csrf = true } = {}) {
    return page.evaluate(async ({ url, method, body, csrf, apiUrl }) => {
        const headers = { 'Content-Type': 'application/json', 'Accept-Language': 'en' };
        if (csrf && method !== 'GET') {
            const token = await fetch(apiUrl + '/security/csrf', { credentials: 'include' }).then(r => r.json());
            headers['X-CSRF-TOKEN'] = token.token;
        }
        const response = await fetch(url, { method, credentials: 'include', headers,
            body: body === undefined ? undefined : JSON.stringify(body) });
        const text = await response.text();
        return { status: response.status, body: text ? JSON.parse(text) : null };
    }, { url: apiUrl + route, method, body, csrf, apiUrl });
}
async function ok(page, route, options) {
    const response = await api(page, route, options);
    assert.ok(response.status >= 200 && response.status < 300, `${route}: ${response.status} ${JSON.stringify(response.body)}`);
    return response.body;
}
async function denied(page, route, options) {
    const response = await api(page, route, options);
    assert.ok([403, 404].includes(response.status), `${route} must deny access: ${response.status}`);
}
async function session(email, password) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    contexts.push(context);
    await context.tracing.start({ screenshots: true, snapshots: true });
    const page = await context.newPage();
    page.setDefaultTimeout(15_000);
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(ui + '/login');
    if (email) await login(page, email, password);
    return page;
}
async function login(page, email, password) {
    await page.locator('input[type=email]').fill(email);
    await page.locator('input[type=password]').fill(password);
    await page.locator('button[type=submit]').click();
    await page.waitForURL(url => url.pathname === '/invitation' || url.pathname === '/app' || url.pathname.startsWith('/app/'));
}
async function invite(page, email) {
    await page.getByRole('button', { name: 'Invite a partner', exact: true }).click();
    await page.getByRole('dialog').getByLabel('Email address', { exact: true }).fill(email);
    await page.getByRole('button', { name: 'Send invitation', exact: true }).click();
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
}
async function review(page, url) {
    await page.goto(url);
    await page.getByRole('button', { name: 'Review invitation', exact: true }).click();
}
async function join(page) {
    const button = page.getByRole('button', { name: 'Accept and join', exact: true });
    assert.equal(await button.isDisabled(), true, 'Joining needs explicit consent');
    await page.getByRole('checkbox').check();
    await button.click();
    await page.waitForURL('**/app/household');
}
function localeFile(file) {
    const code = ts.transpileModule(fs.readFileSync(path.join(web, 'src/i18n/locales', file + '.ts'), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS },
    }).outputText;
    const module = { exports: {} };
    require('node:vm').runInNewContext(code, module);
    return module.exports;
}

try {
    if (!process.env.AVERIO_TEST_CONNECTION_STRING) throw new Error('AVERIO_TEST_CONNECTION_STRING is required; see docs/collaboration-verification.md.');
    start('dotnet', ['run', '--project', 'tests/Averio.Collaboration.Host', '--configuration', 'Release', '--no-build',
        '--no-launch-profile', '--', '--browser-fixture'], root, 'api', {
        ASPNETCORE_TEST_CONTENTROOT_AVERIO_API: path.join(root, 'apps/Averio.Api'),
    });
    const fixture = await poll(() => control('/ready'), 'migrated API and seeded fixture', 60_000);
    const preview = start(process.execPath, [path.join(web, 'node_modules/vite/bin/vite.js'), 'preview', '--host', 'localhost',
        '--port', '5173', '--strictPort'], web, 'web');
    await poll(async () => (await fetch(ui)).ok, 'built frontend');
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    const { owner: f, existing: old, password } = fixture;
    const owner = await session(f.email, password);
    await owner.goto(ui + '/app/household');
    await owner.getByRole('button', { name: 'Invite a partner', exact: true }).waitFor();
    const partnerEmail = 'partner@collaboration.test';
    await invite(owner, partnerEmail);
    const invitation = await mail(partnerEmail, 'invitation');
    const wrongEmail = await api(owner, '/invitations/preview', { method: 'POST', body: { token: new URL(invitation.url).hash.slice(1) } });
    assert.equal(wrongEmail.status, 409);
    assert.equal(wrongEmail.body.code, 'invitation_invalid');

    // A genuinely new user registers and verifies through the real cookie/CSRF API.
    const partner = await session();
    await partner.goto(invitation.url);
    await partner.locator('a[href="/register"]').click();
    await partner.locator('input[type=email]').fill(partnerEmail);
    await partner.locator('input[type=password]').nth(0).fill(password);
    await partner.locator('input[type=password]').nth(1).fill(password);
    await partner.locator('button[type=submit]').click();
    await partner.waitForURL('**/verify-email?*');
    const verification = await mail(partnerEmail, 'verification');
    const verified = partner.waitForResponse(r => r.url().endsWith('/auth/email-verification') && r.request().method() === 'POST');
    await partner.goto(verification.url);
    assert.equal((await verified).status(), 204);
    await partner.goto(ui + '/login');
    await login(partner, partnerEmail, password);
    assert.equal(new URL(partner.url()).pathname, '/invitation', 'Invitation precedes household creation onboarding');
    await partner.getByRole('button', { name: 'Review invitation', exact: true }).click();
    await partner.getByRole('heading', { name: 'You’re joining Collaboration home' }).waitFor();
    assert.equal(await partner.locator('a[href="/onboarding/household"]').count(), 0);
    await join(partner);
    const member = await ok(partner, '/auth/me');
    assert.equal(member.household.householdId, f.householdId);
    assert.equal(member.household.role.toLowerCase(), 'member');
    assert.equal(member.onboardingRequired, false);
    assert.equal(await partner.evaluate(() => sessionStorage.getItem('averio.pendingInvitation')), null);
    await partner.getByText('Collaboration home', { exact: true }).first().waitFor();
    assert.equal(await partner.getByRole('button', { name: 'Edit profile', exact: true }).count(), 0);
    assert.deepEqual(await ok(partner, '/household-history'), []);
    await partner.getByRole('heading', { name: 'Previous households' }).waitFor({ state: 'hidden' });
    assert.equal((await api(partner, `/households/${f.householdId}/invitations`)).status, 403);
    const noCsrf = await api(partner, '/invitations/preview', { method: 'POST', csrf: false, body: { token: 'A'.repeat(64) } });
    assert.equal(noCsrf.status, 400);

    const prefix = `/households/${f.householdId}`;
    const accounts = await ok(partner, prefix + '/accounts');
    assert.equal(accounts.some(a => a.id === f.privateAccountId), false);
    const holderAccounts = await ok(owner, prefix + '/accounts');
    assert.equal(Number(holderAccounts.find(a => a.id === f.personalAccountId).currentBalanceAmount), 73);
    assert.equal(Number(accounts.find(a => a.id === f.personalAccountId).currentBalanceAmount), 80.0001);
    for (const route of ['/transactions', '/transactions?search=SECRET', '/transfers', '/activities',
        `/statistics/cash-flow?from=${f.date}&to=${f.date}`, `/statistics/category-cash-flow?from=${f.date}&to=${f.date}`,
        '/dashboard']) {
        const result = await ok(partner, prefix + route);
        assert.equal(JSON.stringify(result).includes('SECRET'), false, `Private data leaked in ${route}`);
    }
    const cashFlow = await ok(partner, prefix + `/statistics/cash-flow?from=${f.date}&to=${f.date}`);
    assert.equal(Number(cashFlow.periods[0].incomeAmount), 230.1235);
    await denied(partner, prefix + `/accounts/${f.privateAccountId}`);
    await denied(partner, prefix + `/transactions/${f.privateTransactionId}`);
    const redacted = await ok(partner, prefix + `/transfers/${f.privateTransferId}`);
    assert.ok(redacted.source, 'The shared transfer leg remains visible');
    assert.equal(redacted.destination, null);
    assert.equal(redacted.note, null);
    assert.equal(redacted.canModify, false);
    assert.equal((await ok(partner, prefix + `/transfers?accountId=${f.privateAccountId}`)).items.length, 0);
    await denied(partner, prefix + `/transfers/${f.privateTransferId}?expectedUpdatedAt=${encodeURIComponent(redacted.updatedAt)}`, { method: 'DELETE' });
    await denied(partner, prefix + `/activities/${f.privateActivityId}/timeline`);
    const privateRow = await ok(owner, prefix + `/transactions/${f.privateTransactionId}`);
    await denied(partner, prefix + `/transactions/${f.privateTransactionId}?expectedUpdatedAt=${encodeURIComponent(privateRow.updatedAt)}`, { method: 'DELETE' });
    console.log('PASS real registration/verification/invitation, current membership, owner controls, CSRF and privacy across financial reads/writes');

    // Existing member: revoked link, failed delivery, cooldown, expiry, rotation, cutover.
    const existing = await session(old.email, password);
    await existing.goto(ui + '/app/accounts'); // Populate source financial caches before joining.
    await existing.getByRole('heading', { name: 'Previous home personal', exact: true }).waitFor();
    await invite(owner, old.email);
    const revoked = await mail(old.email, 'invitation');
    let row = owner.locator('li').filter({ hasText: old.email });
    await row.getByRole('button', { name: 'Revoke invitation', exact: true }).click();
    await owner.getByRole('dialog').getByRole('button', { name: 'Revoke invitation', exact: true }).click();
    await owner.getByRole('dialog').waitFor({ state: 'hidden' });
    await review(existing, revoked.url);
    await existing.getByRole('alert').waitFor();
    assert.equal(await existing.getByRole('button', { name: 'Accept and join', exact: true }).count(), 0);
    await control('/delivery', { failedEmail: old.email });
    await invite(owner, old.email);
    row = owner.locator('li').filter({ hasText: 'Email delivery failed' });
    await row.waitFor();
    await control('/advance', { seconds: 61 });
    await control('/delivery', { failedEmail: null });
    await row.getByRole('button', { name: 'Resend', exact: true }).click();
    await poll(async () => (await mail(old.email, 'invitation')).url !== revoked.url, 'successful retry');
    const expired = await mail(old.email, 'invitation');
    await control('/advance', { seconds: 8 * 86400 });
    await review(existing, expired.url);
    await existing.getByRole('alert').waitFor();
    await owner.reload();
    row = owner.locator('li').filter({ hasText: old.email }).filter({ hasText: 'Expired' });
    await row.getByRole('button', { name: 'Resend', exact: true }).click();
    const fresh = await poll(async () => { const m = await mail(old.email, 'invitation'); return m.url !== expired.url && m; }, 'rotated expired link');
    assert.equal((await api(existing, '/invitations/preview', { method: 'POST', body: { token: new URL(expired.url).hash.slice(1) } })).status, 409);
    await review(existing, fresh.url);
    await existing.getByRole('heading', { name: 'Previous home personal', exact: true }).waitFor();
    await existing.getByRole('heading', { name: 'Previous home personal', exact: true }).locator('..').getByRole('combobox').selectOption('shared');
    await existing.getByRole('heading', { name: 'SECRET private account', exact: true }).locator('..').getByRole('combobox').selectOption('private');
    const reviewed = await ok(existing, '/invitations/preview', { method: 'POST', body: { token: new URL(fresh.url).hash.slice(1) } });
    await existing.getByText('You are the last active member.', { exact: false }).waitFor();
    await join(existing);
    assert.equal((await ok(existing, '/auth/me')).household.householdId, f.householdId);
    const copied = (await ok(existing, prefix + '/accounts')).filter(a => a.name === 'Previous home personal');
    assert.equal(copied.length, 1);
    assert.notEqual(copied[0].id, old.personalAccountId);
    assert.equal(Number(copied[0].balanceAnchorAmount), 73);
    assert.equal((await ok(existing, prefix + `/transactions?accountId=${copied[0].id}`)).items.length, 0);
    assert.equal((await ok(existing, prefix + `/transfers?accountId=${copied[0].id}`)).items.length, 0);
    await denied(existing, `/households/${old.householdId}/accounts`);
    await denied(partner, `/households/${old.householdId}/transactions/${old.sharedTransactionId}`);
    assert.deepEqual(await ok(existing, prefix + `/statistics/cash-flow?from=${f.date}&to=${f.date}`), cashFlow, 'Copied opening balances do not become income');
    const history = await ok(existing, '/household-history');
    assert.equal(history.length, 1);
    const snapshot = await ok(existing, `/household-history/${history[0].id}`);
    assert.ok(snapshot.transactions.some(t => t.id === old.sharedTransactionId));
    await denied(partner, `/household-history/${history[0].id}`);
    await existing.getByRole('button', { name: 'View history', exact: true }).click();
    await existing.getByText('This is your personal snapshot at departure.', { exact: false }).waitFor();
    const retry = { token: new URL(fresh.url).hash.slice(1), confirmed: true, expectedSourceVersion: reviewed.departure.version,
        accounts: reviewed.departure.accounts.map(a => ({ accountId: a.id, expectedVersion: a.version, openingBalance: a.balance,
            handling: a.currency === 'HUF' ? 'private' : 'shared' })) };
    await Promise.all([ok(existing, '/invitations/acceptances', { method: 'POST', body: retry }),
        ok(existing, '/invitations/acceptances', { method: 'POST', body: retry })]);
    assert.equal((await ok(existing, prefix + '/accounts')).filter(a => a.name === 'Previous home personal').length, 1);
    console.log('PASS revoked/expired links, delivery retry, rotation, existing accounts, archive/history, no imported ledger, simultaneous idempotent retries');

    // Exercise desktop/mobile/light/dark localized states with real authenticated data.
    const invitations = localeFile('invitations');
    const householdCopy = localeFile('householdSettings');
    const concepts = localeFile('concept');
    for (const language of ['en', 'de', 'hu']) for (const theme of ['light', 'dark']) {
        await partner.evaluate(({ language, theme }) => {
            localStorage.setItem('i18nextLng', language); localStorage.setItem('averio.theme', theme);
        }, { language, theme });
        await partner.setViewportSize({ width: theme === 'dark' ? 390 : 1440, height: 900 });
        await partner.goto(ui + '/app/household');
        await partner.getByRole('heading', { name: householdCopy['householdSettings' + language.toUpperCase()].profile, exact: true }).waitFor();
        await partner.getByRole('heading', { name: invitations['invitations' + language.toUpperCase()].history.title }).waitFor({ state: 'hidden' });
        assert.equal(await partner.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        await partner.goto(ui + '/app');
        await partner.getByRole('heading', { name: concepts['concept' + language.toUpperCase()].balanceTitle, exact: true }).waitFor();
        assert.equal(await partner.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.equal(await partner.evaluate(() => document.documentElement.classList.contains('dark')), theme === 'dark');
        await partner.screenshot({ path: path.join(artifacts, `balance-${language}-${theme}.png`), fullPage: true });
    }
    await existing.goto(ui + '/app');
    await existing.getByRole('button', { name: 'Sign out', exact: true }).click();
    await existing.waitForURL('**/login');
    await login(existing, partnerEmail, password);
    await existing.goto(ui + '/app/household');
    await existing.getByText('Collaboration home', { exact: true }).first().waitFor();
    await existing.getByRole('heading', { name: 'Previous households', exact: true }).waitFor({ state: 'hidden' });
    assert.equal(await existing.getByRole('heading', { name: 'Previous home', exact: true }).count(), 0);
    assert.deepEqual(await ok(existing, '/household-history'), []);
    assert.deepEqual(errors, []);
    console.log('PASS EN/DE/HU, desktop/mobile, light/dark balances and household states, same-tab logout/login isolation');
    await verifyCategoryDashboard({ fixture: fixture.dashboard, password, session, ok, poll, localeFile, artifacts, outsider: partner });
    await verifyTransactionLinks({ fixture: fixture.navigation, outsider: fixture.owner, password, session, ok, poll, artifacts, localeFile });
    await verifyRefresh({ session, email: fixture.dashboard.email, password, development: false });
    // Production React omits StrictMode effect replay. Exercise the actual dev
    // entry point too, using the same API, cookies and allowed frontend origin.
    const previewStopped = new Promise(resolve => preview.once('exit', resolve));
    preview.kill();
    await previewStopped;
    start(process.execPath, [path.join(web, 'node_modules/vite/bin/vite.js'), '--host', 'localhost',
        '--port', '5173', '--strictPort'], web, 'web-development');
    await poll(async () => (await fetch(ui)).ok, 'development frontend');
    await verifyRefresh({ session, email: fixture.dashboard.email, password, development: true });
    assert.deepEqual(errors, []);
} catch (error) {
    for (const name of ['api', 'web', 'web-development']) {
        const log = path.join(artifacts, name + '.log');
        if (fs.existsSync(log)) console.error(`${name} fixture log:\n${fs.readFileSync(log, 'utf8').split('\n').slice(-80).join('\n')}`);
    }
    for (let i = 0; i < contexts.length; i++) {
        const page = contexts[i].pages()[0];
        if (page) await page.screenshot({ path: path.join(artifacts, `failure-${i}.png`), fullPage: true }).catch(() => {});
    }
    console.error(error);
    process.exitCode = 1;
} finally {
    for (let i = 0; i < contexts.length; i++) {
        await contexts[i].tracing.stop({ path: path.join(artifacts, `session-${i}.zip`) }).catch(() => {});
    }
    await browser?.close();
    await control('/shutdown', {}).catch(() => {});
    // Give the test host time to dispose the real API and remove its isolated database.
    if (children[0]) {
        await poll(() => children[0].exitCode !== null, 'fixture cleanup', 10_000).catch(() => { process.exitCode = 1; });
        if (children[0].exitCode !== 0) process.exitCode = 1;
    }
    for (const child of children) if (child.exitCode === null) child.kill();
}
