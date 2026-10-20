import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import vm from 'node:vm';

const root = process.cwd();
const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5275', '--strictPort'],
    { cwd: root, windowsHide: true, stdio: 'ignore' });
const url = 'http://localhost:5275/tests/fixtures/personal-money.html';
const artifacts = path.resolve(root, '../../test-results/household-navigation');
fs.mkdirSync(artifacts, { recursive: true });
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
function translations(file) {
    const context = { exports: {} };
    vm.runInNewContext(ts.transpileModule(fs.readFileSync(`src/i18n/locales/${file}.ts`, 'utf8'),
        { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, context);
    return context.exports;
}
const householdCopy = translations('householdSettings');
const moneyCopy = translations('personalMoney');
let browser;
try {
    for (let i = 0; i < 80; i++) {
        try { if ((await fetch(url)).ok) break; } catch { /* Vite is starting. */ }
        await pause(100);
    }
    browser = await chromium.launch({ channel: 'chrome', headless: true });
    for (const width of [320, 375, 390, 768, 1024, 1440]) for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) {
        const h = householdCopy['householdSettings' + language.toUpperCase()];
        const m = moneyCopy['personalMoney' + language.toUpperCase()];
        const page = await browser.newPage({ viewport: { width, height: 667 } });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        let role = 'owner';
        await page.route('**/api/v1/**', async route => {
            const pathname = new URL(route.request().url()).pathname;
            const json = data => route.fulfill({ contentType: 'application/json', body: JSON.stringify(data) });
            if (pathname.endsWith('/households/home')) {
                await pause(100);
                return json({ id: 'home', name: 'Averio household', countryCode: 'HU', preferredCurrency: 'EUR', role, version: '1',
                    members: [{ id: 'member', displayName: 'Alex', isCurrentUser: true, role, joinedAtUtc: '2026-01-01T00:00:00Z' }] });
            }
            if (pathname.endsWith('/personal-money/me')) return json({ walletId: 'wallet', currency: 'EUR', available: '100', attributedPrivateFunding: '0', fundingDifference: '100', entries: [], charges: [] });
            if (pathname.endsWith('/accounts')) return json([{ id: 'private', scope: 'Personal', visibility: 'Private', owners: [{ householdMemberId: 'member' }] }]);
            if (pathname.endsWith('/transactions')) return json({ items: [{ id: 'purchase', accountId: 'private', visibility: 'Private', status: 'Posted', kind: 'Expense', amount: '20', currency: 'EUR', occurredOn: '2026-10-01', merchant: 'Purchase', items: [], labels: [] }], totalCount: 1, page: 1, pageSize: 50 });
            if (pathname.endsWith('/statistics/boundary-flow')) return json({ months: [] });
            return json([]);
        });
        const householdNav = page.getByRole('navigation', { name: h.sections, exact: true });
        const moneyNav = page.getByRole('navigation', { name: m.sections, exact: true });
        const hash = section => '#household-' + (section === 'personalMoney' ? 'personal-money' : section);
        const openSection = async section => {
            if (width < 1024) await householdNav.getByRole('combobox').selectOption(section);
            else await householdNav.locator(`a[href="${hash(section)}"]`).click();
            await page.waitForFunction(expected => location.hash === expected, hash(section));
        };
        const openTab = async tab => {
            if (width < 1024) await moneyNav.getByRole('combobox').selectOption(tab);
            else await moneyNav.getByRole('button', { name: m.tabs[tab], exact: true }).click();
        };
        const assertLayout = async nav => {
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'No page overflow');
            const issues = await nav.evaluate(element => {
                const failures = [];
                for (const control of element.querySelectorAll('a, button, select')) {
                    if (!control.checkVisibility()) continue;
                    const rect = control.getBoundingClientRect();
                    if (rect.height < 44 || rect.left < 0 || rect.right > innerWidth) failures.push('tap target/overflow');
                    if (control instanceof HTMLSelectElement) {
                        const style = getComputedStyle(control);
                        const canvas = document.createElement('canvas').getContext('2d');
                        canvas.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
                        const available = rect.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight) - 24;
                        for (const option of control.options) {
                            if (canvas.measureText(option.text).width > available) failures.push(`clipped option: ${option.text}`);
                        }
                    } else {
                        // Full desktop labels may wrap at spaces, but never character-by-character.
                        for (const span of control.querySelectorAll('span')) {
                            if (span.children.length || !span.textContent.trim()) continue;
                            const style = getComputedStyle(span);
                            if (span.getBoundingClientRect().width < 90) failures.push('cramped label');
                            if (style.wordBreak === 'break-all' || style.overflowWrap === 'anywhere') failures.push('character wrapping');
                        }
                    }
                }
                return failures;
            });
            assert.deepEqual(issues, [], `${width}/${language}/${dark}`);
        };
        const screenshot = async name => {
            await page.evaluate(() => window.scrollTo(0, 0));
            await page.screenshot({ path: path.join(artifacts, `${width}-${language}-${dark ? 'dark' : 'light'}-${name}.png`), fullPage: true });
        };

        await page.goto(url);
        await householdNav.waitFor();
        await assertLayout(householdNav);
        assert.equal(await householdNav.getByRole('combobox').isVisible(), width < 1024);
        if (width >= 1024) {
            assert.equal(await householdNav.evaluate(element => getComputedStyle(element).position), 'sticky');
            assert.equal(await householdNav.locator('[aria-current="page"]').getAttribute('href'), hash('profile'));
        }
        await screenshot('profile');
        // Real local editor state must survive moving away and browser history.
        await page.getByRole('button', { name: h.edit, exact: true }).click();
        await page.getByLabel(h.name, { exact: true }).fill('Unsaved household draft');
        await openSection('members');
        await page.getByRole('heading', { name: h.members, exact: true }).waitFor();
        await openSection('personalMoney');
        await page.getByRole('heading', { name: m.myWallet, exact: true }).waitFor();
        await assertLayout(moneyNav);
        await screenshot('personal-money');
        await page.goBack();
        await page.getByRole('heading', { name: h.members, exact: true }).waitFor();
        await page.goBack();
        assert.equal(await page.getByLabel(h.name, { exact: true }).inputValue(), 'Unsaved household draft');
        await page.goForward();
        await page.getByRole('heading', { name: h.members, exact: true }).waitFor();
        await page.goForward();
        await page.getByRole('heading', { name: m.myWallet, exact: true }).waitFor();
        await openTab('charges');
        const purchase = page.locator('select').filter({ has: page.locator('option[value="purchase"]') });
        await purchase.selectOption('purchase');
        const amount = purchase.locator('xpath=ancestor::form').locator('input');
        await amount.fill('12');
        await openTab('mine');
        await openTab('charges');
        assert.equal(await amount.inputValue(), '12');
        await openSection('privacy');
        await openSection('personalMoney');
        assert.equal(await amount.inputValue(), '12', 'Personal Money tab and draft survive household navigation');
        await openTab('reconcile');
        await assertLayout(moneyNav);
        const surrender = page.getByRole('heading', { name: m.surrenderTitle, exact: true })
            .locator('xpath=ancestor::form').locator('input[inputmode="decimal"]');
        await surrender.fill('7');
        await openTab('owner');
        await openTab('reconcile');
        assert.equal(await surrender.inputValue(), '7', 'Adjustment drafts survive internal navigation');
        await screenshot('adjustments');
        // Reload and delayed data still honor direct links.
        await page.reload();
        await page.getByRole('heading', { name: m.myWallet, exact: true }).waitFor();
        await page.goto(url + hash('members'));
        await page.getByRole('heading', { name: h.members, exact: true }).waitFor();
        await openSection('invitations');
        assert.equal(await page.locator('#household-invitations').isVisible(), true);
        await page.locator('#household-invitations').getByRole('button').click();
        const inviteEmail = page.getByRole('dialog').locator('input[type="email"]');
        await inviteEmail.fill('draft@example.test');
        await page.keyboard.press('Escape');
        await openSection('privacy');
        await openSection('invitations');
        await page.locator('#household-invitations').getByRole('button').click();
        assert.equal(await inviteEmail.inputValue(), 'draft@example.test');
        await page.keyboard.press('Escape');
        role = 'member';
        await page.reload();
        await page.getByRole('heading', { name: h.profile, exact: true }).waitFor();
        assert.equal(await householdNav.locator('option[value="invitations"], a[href="#household-invitations"]').count(), 0);
        assert.equal(await page.locator('#household-invitations').count(), 0);
        // Keyboard selection uses the same section state and hash path.
        if (width < 1024) {
            await householdNav.getByRole('combobox').focus();
            await page.keyboard.press('ArrowDown');
            await page.keyboard.press('Enter');
        } else {
            await householdNav.locator(`a[href="${hash('members')}"]`).focus();
            await page.keyboard.press('Enter');
        }
        await page.getByRole('heading', { name: h.members, exact: true }).waitFor();
        assert.equal(new URL(page.url()).hash, hash('members'));
        assert.deepEqual(errors, []);
        await page.close();
        console.log(`PASS household navigation ${width}px ${language} ${dark ? 'dark' : 'light'}`);
    }
} finally {
    await browser?.close();
    server.kill();
}
