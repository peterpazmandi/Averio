import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

function fixture() {
    const entries = [];
    class ApiError extends Error { constructor() { super('PRIVATE merchant and API key'); this.status = 503; this.code = 'receipt_storage_unavailable'; this.traceId = '00-abc-123-01'; } }
    const context = { exports: {}, require: () => ({ ApiError }), performance, DOMException,
        console: Object.fromEntries(['info', 'debug', 'warn'].map(level => [level, (...args) => entries.push({ level, args })])) };
    const source = fs.readFileSync(new URL('../src/features/receipts/receiptDiagnostics.ts', import.meta.url), 'utf8');
    vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, context);
    return { ...context.exports, entries, ApiError, context };
}

test('receipt diagnostics allow metadata but never DTO contents, filenames or exception messages', async () => {
    const f = fixture();
    f.receiptLog('upload', { operationId: 'operation-1', bytes: 123, merchant: 'PRIVATE', file: { name: 'PRIVATE' }, draft: { total: 'PRIVATE' }, status: 'PRIVATE\nforged' });
    assert.equal(f.entries[0].args[1].operationId, 'operation-1');
    const error = new f.ApiError();
    await assert.rejects(f.receiptRequest('upload', {}, () => Promise.reject(error)), e => e === error);
    const failure = f.entries.at(-1).args[1];
    assert.equal(failure.httpStatus, 503);
    assert.equal(failure.traceId, '00-abc-123-01');
    assert.equal(failure.failureCode, 'receipt_storage_unavailable');
    assert.ok(!JSON.stringify(f.entries).includes('PRIVATE'));
});

test('receipt status changes and attempts log once; stalled polls warn at most every two minutes', () => {
    const f = fixture(), observe = f.createReceiptObserver();
    const now = Date.parse('2026-11-01T12:00:00Z');
    const scan = { id: 'scan-1', status: 'Queued', attempts: 0, failureCode: null, createdAt: new Date(now - 180000).toISOString(), draft: { merchant: 'PRIVATE' } };
    observe([scan], now); observe([scan], now + 3000);
    assert.equal(f.entries.length, 2);
    assert.equal(f.entries[1].args[1].event, 'scan.waiting');
    observe([scan], now + 120000);
    assert.equal(f.entries.length, 3);
    observe([{ ...scan, status: 'Ready', attempts: 1 }], now + 123000);
    assert.equal(f.entries.at(-1).args[1].previousStatus, 'Queued');
    observe([{ ...scan, status: 'Ready', attempts: 1 }], now + 240000);
    assert.equal(f.entries.length, 4);
    assert.ok(!JSON.stringify(f.entries).includes('PRIVATE'));
});

test('broken console logging cannot break a successful receipt request', async () => {
    const f = fixture();
    f.context.console.info = () => { throw new Error('console disabled'); };
    assert.equal(await f.receiptRequest('upload', {}, async () => 42), 42);
});
