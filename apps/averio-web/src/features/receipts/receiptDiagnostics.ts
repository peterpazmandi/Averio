import { ApiError } from '../../shared/api/ApiError';

// Diagnostics stay in this browser's console. Never serialize a request, File,
// receipt draft, response body, merchant, amount, filename or Error object.
type Fields = {
    scanId?: string; operationId?: string; traceId?: string; status?: string;
    previousStatus?: string; failureCode?: string; attempt?: number; elapsedMs?: number;
    count?: number; skip?: number; httpStatus?: number; bytes?: number;
    enabled?: boolean; ageSeconds?: number;
};
type Level = 'info' | 'debug' | 'warn';
export function receiptLog(event: string, fields: Fields = {}, level: Level = 'info') {
    try {
        const data: Record<string, string | number | boolean> = { event, timestamp: new Date().toISOString() };
        // Runtime allowlist also protects against accidentally passing whole DTOs.
        for (const key of ['scanId', 'operationId', 'traceId', 'status', 'previousStatus', 'failureCode'] as const) {
            const value = fields[key];
            if (typeof value === 'string' && /^[a-zA-Z0-9_.:-]{1,128}$/.test(value)) data[key] = value;
        }
        for (const key of ['attempt', 'elapsedMs', 'count', 'skip', 'httpStatus', 'bytes', 'ageSeconds'] as const) {
            const value = fields[key];
            if (typeof value === 'number' && Number.isFinite(value)) data[key] = value;
        }
        if (typeof fields.enabled === 'boolean') data.enabled = fields.enabled;
        console[level]('[Averio receipts]', data);
    } catch { /* Diagnostics must never prevent uploading or recording an expense. */ }
}
export async function receiptRequest<T>(event: string, fields: Fields, action: () => Promise<T>, verbose = false): Promise<T> {
    const started = performance.now();
    receiptLog(event + '.started', fields, verbose ? 'debug' : 'info');
    try {
        const result = await action();
        receiptLog(event + '.completed', { ...fields, elapsedMs: Math.round(performance.now() - started) }, verbose ? 'debug' : 'info');
        return result;
    } catch (error) {
        receiptLog(event + '.failed', {
            ...fields, elapsedMs: Math.round(performance.now() - started),
            ...(error instanceof ApiError ? { httpStatus: error.status, failureCode: error.code, traceId: error.traceId } : {}),
        }, error instanceof DOMException && error.name === 'AbortError' ? 'debug' : 'warn');
        throw error;
    }
}

type ScanState = { id: string; status: string; attempts: number; failureCode: string | null; createdAt: string };
// One observer per mounted inbox: unchanged polls stay quiet. A two-minute
// heartbeat exposes a stalled queue even when the UI keeps receiving HTTP 200.
export function createReceiptObserver() {
    const previous = new Map<string, { signature: string; status: string; warnedAt: number }>();
    return (scans: readonly ScanState[], now = Date.now()) => {
        for (const scan of scans) {
            const signature = JSON.stringify([scan.status, scan.attempts, scan.failureCode]);
            const old = previous.get(scan.id);
            const fields = { scanId: scan.id, status: scan.status, attempt: scan.attempts, failureCode: scan.failureCode ?? undefined };
            const changed = old?.signature !== signature;
            if (changed) receiptLog('scan.state', { ...fields, previousStatus: old?.status }, scan.status === 'Failed' ? 'warn' : 'info');
            let warnedAt = old?.warnedAt ?? 0;
            const age = (now - Date.parse(scan.createdAt)) / 1000;
            if (['Uploading', 'Queued', 'Processing'].includes(scan.status) && age >= 120 && (changed || now - warnedAt >= 120_000)) {
                receiptLog('scan.waiting', { ...fields, ageSeconds: Math.round(age) }, 'warn');
                warnedAt = now;
            }
            previous.set(scan.id, { signature, status: scan.status, warnedAt });
        }
        for (const id of previous.keys()) if (!scans.some(scan => scan.id === id)) previous.delete(id);
    };
}
