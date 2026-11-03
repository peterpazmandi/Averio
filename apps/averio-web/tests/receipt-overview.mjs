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
        let releaseUpload;
        const uploadResponse = new Promise(resolve => { releaseUpload = resolve; });
        await page.route('**/api/v1/**', async route => {
            const request = new URL(route.request().url());
            const json = data => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
            if (request.pathname.endsWith('/security/csrf')) return json({ token: 'csrf' });
            if (request.pathname.endsWith('/receipt-scans/capabilities')) return json({ enabled: true, monthlyLimit: 100, monthlyUsed: 65, maxUploadBytes: 10485760 });
            if (request.pathname.endsWith('/receipt-scans')) {
                if (route.request().method() === 'POST') {
                    uploaded = { ...scans[64], id: 'latest', createdAt: new Date().toISOString() };
                    await uploadResponse;
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
            const capture = page.locator('#receipt-upload-panel');
            assert.equal(await page.getByRole('progressbar').count(), 0, 'Selecting a photo does not start scanning.');
            await page.getByRole('button', { name: 'Scan this photo', exact: true }).click();
            await capture.locator('[aria-current="step"]').filter({ hasText: 'Uploading' }).waitFor();
            const progress = capture.getByRole('progressbar');
            const originalBar = await progress.elementHandle();
            releaseUpload();
            const latest = capture;
            assert.equal(await capture.isVisible(), true, 'Capture stays open after upload acceptance.');
            assert.equal(await capture.getByRole('img').count(), 1, 'The selected photo remains visible.');
            assert.equal(await page.getByLabel('Latest scan', { exact: true }).count(), 0, 'Progress never moves to another card.');
            await latest.locator('[aria-current="step"]').filter({ hasText: 'Waiting to start' }).waitFor();
            assert.equal(await latest.getByRole('list', { name: 'Scanning stages' }).getByRole('listitem').count(), 3);
            uploaded.status = 'Processing';
            await page.getByRole('button', { name: 'Refresh', exact: true }).click();
            await latest.locator('[aria-current="step"]').filter({ hasText: 'Reading receipt' }).waitFor();
            assert.equal(await originalBar.evaluate(node => node.isConnected), true, 'The same progress bar stays mounted through reading.');
            assert.equal(await capture.getByRole('button', { name: 'Choose image', exact: true }).isDisabled(), true);
            assert.equal(await capture.getByRole('button', { name: 'Remove photo', exact: true }).isDisabled(), true);
            await page.screenshot({ path: path.join(artifacts, `scan-progress-${width}.png`), fullPage: true });
            uploaded.status = 'Ready'; uploaded.draft = scans[0].draft;
            await page.getByRole('button', { name: 'Refresh', exact: true }).click();
            await list.getByRole('button', { name: 'Review receipt', exact: true }).first().waitFor();
            await capture.getByRole('img').waitFor({ state: 'hidden' });
            assert.equal(await capture.isVisible(), true, 'Capture stays open for the next photo.');
            assert.equal(await capture.getByRole('button', { name: 'Remove photo', exact: true }).count(), 0);
            assert.equal(await capture.getByRole('button', { name: 'Review this receipt', exact: true }).count(), 0);
            assert.equal(await capture.getByRole('button', { name: 'Take photo', exact: true }).isEnabled(), true);
            assert.equal(await list.getByText('Shop 0', { exact: true }).count() > 0, true, 'Completed receipt remains in the review inbox.');
            assert.equal(await page.getByRole('progressbar').count(), 0, 'Ready for review removes the progress bar.');
            await page.screenshot({ path: path.join(artifacts, `latest-scan-${width}.png`), fullPage: true });
            assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
            for (const [status, label] of [['Failed', 'Could not scan'], ['Discarded', 'Discarded'], ['Expired', 'Expired'], ['Posted', 'Recorded']]) {
                uploaded.status = status;
                await page.getByRole('button', { name: 'Refresh', exact: true }).click();
                await list.getByText(label, { exact: true }).first().waitFor({ state: ['Discarded', 'Expired', 'Posted'].includes(status) ? 'hidden' : 'visible' });
                assert.equal(await page.getByRole('progressbar').count(), 0, 'Terminal states do not show scan progress.');
            }
            await page.getByRole('button', { name: 'History', exact: true }).click();
            assert.equal(await page.getByRole('progressbar').count(), 0, 'History has no progress display.');
            await page.locator('input[type="file"]').first().setInputFiles({ name: 'next-receipt.png', mimeType: 'image/png', buffer: Buffer.from('next fixture') });
            await capture.getByText('next-receipt.png', { exact: true }).waitFor();
            assert.equal(await capture.getByRole('button', { name: 'Scan this photo', exact: true }).isEnabled(), true, 'A fresh photo can be scanned after the previous one is read.');
            assert.deepEqual(errors, []);
        } finally { await page.close(); }
    }
}


