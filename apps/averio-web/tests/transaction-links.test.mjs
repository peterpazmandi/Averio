import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

const code = ts.transpileModule(fs.readFileSync(new URL('../src/features/transactions/transactionSearch.ts', import.meta.url), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS },
}).outputText;
const context = { exports: {} };
vm.runInNewContext(code, context);
const { transactionSearch } = context.exports;
const id = '12345678-1234-1234-1234-123456789abc';
const clean = value => JSON.parse(JSON.stringify(value));

test('transaction filter links round-trip without including free text or draft values', () => {
    const search = { account: id, activity: id, kind: 'Expense', from: '2028-02-29', to: '2028-03-01', transaction: id };
    assert.deepEqual(clean(transactionSearch({ ...search, note: 'private', amount: '100', search: 'private merchant' })), search);
    assert.deepEqual(clean(transactionSearch({ dates: 'all' })), { dates: 'all' });
});
test('malformed IDs, dates and enum values never reach generated API parameters', () => {
    assert.deepEqual(clean(transactionSearch({ account: '../secret', transaction: ['bad'], activity: 123,
        kind: 'Transfer', from: '2026-02-30', to: '0000-01-01', dates: 'anything' })), {});
    assert.deepEqual(clean(transactionSearch({ from: '2026-01-01' })), { from: '2026-01-01' });
});
test('a detail link takes precedence over creation and creation kind is a one-shot intent', () => {
    assert.deepEqual(clean(transactionSearch({ transaction: id, create: true, createKind: 'Income' })), { transaction: id });
    assert.deepEqual(clean(transactionSearch({ create: 'true', createKind: 'Income' })), { create: true, createKind: 'Income' });
    assert.deepEqual(clean(transactionSearch({ createKind: 'Income' })), {});
});
