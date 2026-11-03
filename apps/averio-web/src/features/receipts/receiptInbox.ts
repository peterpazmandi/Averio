import type { ReceiptScanResult } from '../../shared/api/generated';

export const receiptHistoryStatuses = new Set(['Posted', 'Discarded', 'Expired']);
export const receiptPageSize = 10;

export function receiptInboxRows(scans: ReceiptScanResult[], history: boolean, status = '') {
    // Offset pages can overlap when a new scan arrives during pagination.
    const unique = [...new Map(scans.map(scan => [scan.id, scan])).values()];
    const rank = (scan: ReceiptScanResult) => scan.status === 'Ready' ? 0 : scan.status === 'Failed' ? 1 : 2;
    return unique.filter(scan => receiptHistoryStatuses.has(scan.status) === history && (!status || scan.status === status))
        .sort((a, b) => (history ? 0 : rank(a) - rank(b)) || Date.parse(b.createdAt) - Date.parse(a.createdAt) || a.id.localeCompare(b.id));
}
