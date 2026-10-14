import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
function load(file) {
    const context = { exports: {} };
    vm.runInNewContext(ts.transpileModule(fs.readFileSync(new URL('../src/features/transactions/' + file, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS },
    }).outputText, context);
    return context.exports;
}
const { resolveTransactionDates: dates, savedFilters } = load('transactionDates.ts');
const { transactionSearch } = load('transactionSearch.ts');
const clean = value => JSON.parse(JSON.stringify(value));
const at = value => new Date(value + 'T12:00:00Z');
test('relative calendar presets include leap days and cross year boundaries', () => {
    const expected = {
        'this-month': ['2028-03-01', '2028-03-15'], 'last-month': ['2028-02-01', '2028-02-29'],
        'previous-3-full-months': ['2027-12-01', '2028-02-29'], 'this-year': ['2028-01-01', '2028-03-15'],
        'last-year': ['2027-01-01', '2027-12-31'], 'last-12-months': ['2027-04-01', '2028-03-15'],
    };
    for (const [period, [from, to]] of Object.entries(expected)) assert.deepEqual(clean(dates({ period }, at('2028-03-15'))), { from, to });
    assert.deepEqual(clean(dates({ period: 'previous-3-full-months' }, at('2027-01-01'))), { from: '2026-10-01', to: '2026-12-31' });
    assert.deepEqual(clean(dates({ period: 'last-month' }, at('2026-03-01'))), { from: '2026-02-01', to: '2026-02-28' });
});
test('saved relative meaning survives reopening later; custom dates remain fixed and can be open ended', () => {
    const relative = clean(savedFilters({ period: 'this-month', from: '1999-01-01' }));
    assert.equal(relative.from, null);
    assert.deepEqual(clean(dates(transactionSearch(relative), at('2026-11-02'))), { from: '2026-11-01', to: '2026-11-02' });
    const fixed = transactionSearch(savedFilters({ period: 'custom', from: '2026-01-01', to: '2026-01-15' }));
    assert.deepEqual(clean(dates(fixed, at('2027-11-02'))), { from: '2026-01-01', to: '2026-01-15' });
    assert.deepEqual(clean(dates({ period: 'custom', from: '2026-01-01' }, at('2027-01-01'))), { from: '2026-01-01' });
    assert.deepEqual(clean(dates({ dates: 'all' }, at('2027-01-01'))), {});
    assert.deepEqual(clean(dates({ activity: 'id' }, at('2027-01-01'))), {});
    assert.deepEqual(clean(dates({}, new Date('2026-11-01T00:01:00Z'))), { from: '2026-11-01', to: '2026-11-01' });
});
test('URL labels are deduplicated, bounded and validated; save excludes transaction, create and draft data', () => {
    const id = '12345678-1234-1234-1234-123456789abc';
    const parsed = clean(transactionSearch({ period: 'previous-3-full-months', labels: [id, 'invalid', id], from: '2000-01-01' }));
    assert.deepEqual(parsed, { period: 'previous-3-full-months', labels: [id] });
    assert.equal(transactionSearch({ period: 'previous3' }).period, undefined);
    const saved = clean(savedFilters({ ...parsed, transaction: id, create: true, note: 'private' }));
    assert.equal(saved.transaction, undefined); assert.equal(saved.create, undefined); assert.equal(saved.note, undefined);
    assert.deepEqual(saved.labels, [id]);
});
