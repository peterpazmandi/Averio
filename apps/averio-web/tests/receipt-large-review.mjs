import assert from 'node:assert/strict';
import path from 'node:path';

// Exercise receipt-sized lists, not just the single-item happy path.
export async function verifyLargeReceipt(browser, url, artifacts) {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
        localStorage.setItem('i18nextLng', 'en');
        document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.add('dark'));
    });
    const categories = Array.from({ length: 180 }, (_, index) => ({
        id: `category-${index}`, name: `Category ${index}`, kind: 'Expense',
        parentId: index >= 20 ? `category-${index % 20}` : null,
        systemKey: null, icon: 'folder', isArchived: false, isPinned: false,
    }));
    const receipt = { id: 'large', status: 'Ready', version: 'v1', attempts: 1,
        createdAt: '2026-11-01T08:00:00Z', expiresAt: '2026-11-08T08:00:00Z', imageAvailable: false,
        draft: { merchant: 'Weekly groceries', date: '2026-11-01', currency: 'EUR', total: '600', warnings: [],
            lines: Array.from({ length: 60 }, (_, index) => ({ description: `Product ${index + 1}`, amount: '10', kind: 'Item' })) } };
    await page.route('**/api/v1/**', route => {
        const pathname = new URL(route.request().url()).pathname;
        const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
        if (pathname.endsWith('/receipt-scans/capabilities')) return json({ enabled: true, monthlyLimit: 100, monthlyUsed: 1, maxUploadBytes: 10485760 });
        if (pathname.endsWith('/receipt-scans')) return json([receipt]);
        if (pathname.endsWith('/receipt-scans/large')) return json(receipt);
        if (pathname.endsWith('/categories')) return json(categories);
        if (pathname.endsWith('/owner-options')) return json({ currentMemberId: 'member', members: [] });
        if (pathname.endsWith('/personal-money/me')) return route.fulfill({ status: 204 });
        return json([]);
    });
    try {
        await page.goto(url);
        await page.getByRole('button', { name: 'Review receipt', exact: true }).click();
        const cards = page.locator('[data-receipt-item]');
        assert.equal(await cards.count(), 60);
        assert.equal(await cards.locator('input[id$="-amount"]').count(), 0, 'Compact items do not mount hidden editors.');
        await cards.nth(0).getByRole('button', { name: 'Edit', exact: true }).click();
        await cards.nth(1).getByRole('button', { name: 'Edit', exact: true }).click();
        assert.equal(await cards.nth(0).locator('div[role=group]').isVisible(), true);
        assert.equal(await cards.nth(1).locator('div[role=group]').isVisible(), true);
        assert.equal(await cards.nth(2).locator('div[role=group]').isVisible(), false);
        assert.equal(await cards.locator('input[id$="-amount"]').count(), 2, 'Only the two expanded items mount their controls.');
        await cards.nth(0).locator('input[id$="-description"]').fill('Edited groceries');
        assert.equal(await cards.nth(1).locator('input[id$="-description"]').inputValue(), 'Product 2');
        await cards.nth(0).getByRole('button', { name: 'Category', exact: true }).click();
        const picker = page.getByRole('dialog', { name: 'Category', exact: true });
        await picker.locator('input[type="search"]').fill('Category 179');
        await picker.getByRole('button', { name: /Category 179/ }).click();
        assert.match(await cards.nth(0).innerText(), /Category 179/);
        assert.equal(await cards.nth(1).locator('div[role=group]').isVisible(), true);
        await cards.nth(0).getByRole('button', { name: 'Done', exact: true }).click();
        await cards.nth(0).getByRole('button', { name: 'Edit', exact: true }).click();
        assert.equal(await cards.nth(0).locator('input[id$="-description"]').inputValue(), 'Edited groceries');
        const bounds = await cards.evaluateAll(nodes => nodes.slice(0, 3).map(node => {
            const box = node.getBoundingClientRect();
            return { top: box.top, bottom: box.bottom, border: getComputedStyle(node).borderLeftWidth };
        }));
        assert.ok(bounds[1].top - bounds[0].bottom >= 12 && bounds[2].top - bounds[1].bottom >= 12, 'Adjacent items have clear space between their enclosing cards.');
        assert.ok(bounds.every(box => parseFloat(box.border) >= 3));
        // Measure input-to-next-paint with a larger list. Report timings rather
        // than enforce a hardware-dependent benchmark threshold in CI.
        const durations = await cards.nth(0).locator('input[id$="-amount"]').evaluate(async input => {
            const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
            const times = [];
            for (const value of ['1', '12', '12.', '12.5', '12.50']) {
                const start = performance.now();
                setValue.call(input, value);
                input.dispatchEvent(new Event('input', { bubbles: true }));
                await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                times.push(performance.now() - start);
            }
            return times;
        });
        assert.equal(await cards.nth(0).locator('input[id$="-amount"]').inputValue(), '12.50');
        assert.match(await page.getByRole('dialog', { name: 'Review receipt', exact: true }).locator('footer').innerText(), /602\.50/, 'Typing updates React state and the exact total, not only the input DOM value.');
        console.log('Large receipt input-to-paint (60 items / 180 categories), ms:', durations.map(Math.round));
        await cards.nth(0).scrollIntoViewIfNeeded();
        await page.screenshot({ path: path.join(artifacts, 'large-receipt-item-cards.png') });
        assert.deepEqual(errors, []);
    } finally { await page.close(); }
}
