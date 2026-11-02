import assert from 'node:assert/strict';
import path from 'node:path';

export async function verifyReceiptOverview(browser, url, artifacts) {
    for (const width of [375, 1440]) {
        const page = await browser.newPage({ viewport: { width, height: 900 } });
        const errors = [];
        page.on('pageerror', error => errors.push(error.message));
        await page.addInitScript(() => {
            localStorage.setItem('i18nextLng', 'en');
            document.addEventListener('DOMContentLoaded', () => document.documentElement.classList.add('dark'));
        });
        const scans = Array.from({ length: 65 }, (_, index) => ({
            id: `scan-${index}`, status: index < 15 ? 'Ready' : index < 35 ? 'Posted' : index < 55 ? 'Discarded' : 'Queued',
            version: 'v1', createdAt: new Date(Date.UTC(2026, 10, 2, 12, -index)).toISOString(),
            expiresAt: '2026-11-09T12:00:00Z', attempts: 1, imageAvailable: index < 35,
            postedTransactionId: index >= 15 && index < 35 ? `transaction-${index}` : null,
            draft: index < 15 ? { merchant: `Shop ${index}`, total: '10', currency: 'EUR', lines: [], warnings: [] } : null,
        }));
        let uploaded = null;
        await page.route('**/api/v1/**', route => {
            const request = new URL(route.request().url());
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (request.pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (request.pathname.endsWith('/receipt-scans/capabilities')) return json({ enabled: true, monthlyLimit: 100, monthlyUsed: 65, maxUploadBytes: 10485760 });
            if (request.pathname.endsWith('/receipt-scans')) {
                if (route.request().method() === 'POST') {
                    uploaded = { ...scans[64], id: 'latest', createdAt: new Date().toISOString() };
                    return json(uploaded);
                }
                const skip = Number(request.searchParams.get('skip') || 0);
                return json((uploaded ? [uploaded, ...scans] : scans).slice(skip, skip + 50));
            }
            return json([]);
        });
        try {
            await page.goto(url);
            const list = page.getByRole('list', { name: 'Receipts', exact: true });
            await list.getByRole('button', { name: 'Review receipt', exact: true }).first().waitFor();
            assert.equal(await list.locator(':scope > li').count(), 10);
            assert.equal(await page.getByRole('button', { name: 'Choose image', exact: true }).isVisible(), false);
            await page.getByRole('button', { name: 'Next', exact: true }).click();
            assert.equal(await list.locator(':scope > li').count(), 5);
            await page.getByRole('button', { name: 'History', exact: true }).click();
            assert.equal(await list.locator(':scope > li').count(), 10);
            assert.equal(await list.getByRole('link').count(), 0, 'History actions stay out of the way until details are opened.');
            await list.getByText('Receipt details', { exact: true }).first().click();
            await list.getByRole('link', { name: 'Open transaction', exact: true }).waitFor();
            await page.getByLabel('Status', { exact: true }).selectOption('Discarded');
            assert.equal(await list.getByText('Recorded', { exact: true }).count(), 0);
            await page.getByRole('button', { name: 'To review', exact: true }).click();
            await page.getByLabel('Status', { exact: true }).selectOption('Queued');
            await page.getByText('No matching receipts loaded', { exact: true }).waitFor();
            await page.getByRole('button', { name: /Load more|Load older/i }).click();
            await list.getByText('Waiting to start', { exact: true }).first().waitFor();
            assert.equal(await list.locator(':scope > li').count(), 10, 'Older unfinished scans remain reachable through paging.');
            await page.getByRole('button', { name: 'Scan receipt', exact: true }).click();
            await page.getByRole('button', { name: 'Take photo', exact: true }).waitFor();
            await page.getByRole('button', { name: 'Scan receipt', exact: true }).click();
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
            await page.screenshot({ path: path.join(artifacts, `overview-${width}.png`), fullPage: true });
            await page.locator('input[type="file"]').first().setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: Buffer.from('fixture') });
            await page.getByRole('button', { name: 'Scan this photo', exact: true }).click();
            const latest = page.getByLabel('Latest scan', { exact: true });
            await latest.getByText('Waiting to start', { exact: true }).waitFor();
            uploaded.status = 'Processing';
            await page.getByRole('button', { name: 'Refresh', exact: true }).click();
            await latest.getByText('Reading receipt', { exact: true }).waitFor();
            uploaded.status = 'Ready'; uploaded.draft = scans[0].draft;
            await page.getByRole('button', { name: 'Refresh', exact: true }).click();
            await latest.getByRole('button', { name: 'Review this receipt', exact: true }).waitFor();
            await page.screenshot({ path: path.join(artifacts, `latest-scan-${width}.png`), fullPage: true });
            assert.deepEqual(errors, []);
        } finally { await page.close(); }
    }
}
