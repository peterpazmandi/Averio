import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', 'localhost', '--port', '5286', '--strictPort'], {
    cwd: process.cwd(), windowsHide: true, stdio: 'ignore',
});
const url = 'http://localhost:5286/tests/fixtures/info-popover.html';
const artifacts = path.resolve('../../test-results/info-popover');
fs.mkdirSync(artifacts, { recursive: true });
let browser;
try {
    for (let i = 0; i < 100; i++) {
        try { if ((await fetch(url)).ok) break; } catch {}
        await new Promise(resolve => setTimeout(resolve, 100));
    }
    browser = await chromium.launch({ ...(process.argv[2] ? { executablePath: process.argv[2] } : { channel: 'chrome' }), headless: true });
    for (const language of ['en', 'de', 'hu']) for (const dark of [false, true]) for (const width of [320, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 800 }, hasTouch: width === 320 });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(({ language, dark }) => {
            localStorage.setItem('i18nextLng', language);
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.toggle('dark', dark));
        }, { language, dark });
        await page.goto(url);
        const buttons = page.locator('main > div button[popovertarget]');
        const trigger = buttons.first();
        assert.match(await trigger.getAttribute('aria-label'), { en: /^About /, de: /^Informationen zu /, hu: /^Tudnivalók: / }[language]);
        assert.equal(await trigger.getAttribute('aria-expanded'), 'false');
        await trigger.focus();
        await page.keyboard.press('Enter');
        const popup = page.locator('[popover]:popover-open');
        await popup.waitFor({ state: 'visible' });
        await page.waitForFunction(() => document.querySelector('button[popovertarget]')?.getAttribute('aria-expanded') === 'true');
        assert.equal(await trigger.getAttribute('aria-expanded'), 'true');
        assert.equal(await popup.evaluate(el => el === document.activeElement), true);
        await page.keyboard.press('Escape');
        assert.equal(await popup.count(), 0);
        assert.equal(await trigger.evaluate(el => el === document.activeElement), true);
        if (width === 320) await trigger.tap(); else await trigger.click();
        await popup.waitFor({ state: 'visible' });
        await trigger.click();
        await popup.waitFor({ state: 'hidden' });
        await trigger.click();
        await buttons.nth(1).click();
        assert.equal(await popup.count(), 1, 'Only one help popover stays open.');
        await page.getByRole('combobox', { name: 'Language' }).click();
        assert.equal(await popup.count(), 0, 'Outside click dismisses help.');
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: 'Open editor', exact: true }).click();
        const editor = page.locator('dialog[open]');
        const innerTrigger = editor.locator('button[popovertarget]');
        await innerTrigger.click();
        await popup.waitFor({ state: 'visible' });
        await page.keyboard.press('Escape');
        assert.equal(await popup.count(), 0);
        assert.equal(await editor.count(), 1, 'Escape must not dismiss the parent editor.');
        assert.equal(await page.locator('output').textContent(), '0', 'Help must never submit a form.');
        assert.equal(await innerTrigger.evaluate(el => el === document.activeElement), true);
        await page.keyboard.press('Space');
        await popup.waitFor({ state: 'visible' });
        await page.screenshot({ path: path.join(artifacts, 'editor-' + language + '-' + dark + '-' + width + '.png') });
        await page.keyboard.press('Escape');
        await page.getByRole('button', { name: 'Close editor', exact: true }).click();
        const edge = page.getByRole('button', { name: /Edge$/ });
        await edge.click();
        await popup.waitFor({ state: 'visible' });
        await page.waitForTimeout(50);
        const box = await popup.boundingBox();
        assert.ok(box.x >= 0 && box.x + box.width <= width + 1, 'Help fits horizontally.');
        assert.ok(box.y >= 0 && box.y + box.height <= 801, 'Help fits vertically.');
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
        await page.screenshot({ path: path.join(artifacts, 'edge-' + language + '-' + dark + '-' + width + '.png') });
        await page.keyboard.press('Escape');
        await page.getByRole('combobox', { name: 'Language' }).selectOption(language === 'hu' ? 'de' : 'hu');
        assert.ok(!(await trigger.getAttribute('aria-label')).includes('common.infoAbout'), 'Language changes resolve translated labels.');
        assert.deepEqual(errors, []);
        await page.close();
    }
    console.log('PASS InfoPopover: 12 locale/theme/viewport combinations; keyboard, touch, light dismissal, single-open behavior, nested editor Escape, no submit, viewport bounds and language switching.');
} finally {
    await browser?.close();
    server.kill();
}
