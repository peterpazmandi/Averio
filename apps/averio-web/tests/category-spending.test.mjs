import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

function load(file, imports = {}) {
    const code = ts.transpileModule(fs.readFileSync(new URL(file, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    }).outputText;
    const context = { exports: {}, require: name => {
        if (!(name in imports)) throw new Error(`Unexpected import ${name}`);
        return imports[name];
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}
const money = load('../src/shared/money/decimalAmount.ts');
const { categorySpending, utcMonthToDate } = load('../src/features/dashboard/categorySpending.ts', {
    '../../shared/money/decimalAmount': money,
});
const row = (id, amount, currency = 'EUR') => ({ categoryId: id, categorySystemKey: null,
    categoryName: id, categoryIcon: 'wallet', currency, periodStart: '2026-10-12',
    expenseAmount: amount, incomeAmount: '0', itemCount: 1 });

test('daily category rows sum exactly and keep currencies and direct parent/child assignments separate', () => {
    const input = [row('parent', '0.1'), row('parent', '0.2001'), row('child', '1'), row('parent', '999', 'HUF')];
    const before = JSON.stringify(input);
    const result = categorySpending(input, 'EUR');
    assert.equal(result.total, '1.3001');
    assert.equal(result.segments.find(x => x.id === 'parent').amount, '0.3001');
    assert.equal(result.segments.find(x => x.id === 'child').amount, '1');
    assert.equal(JSON.stringify(input), before);
});
test('top four and Other retain every expense, including decimal differences above Number precision', () => {
    const input = [row('a', '99999999999999999999.9998'), row('b', '99999999999999999999.9999'),
        row('c', '3'), row('d', '2'), row('e', '0.0001'), row('f', '0.0002')];
    const result = categorySpending(input, 'EUR');
    assert.equal(result.segments.map(x => x.id).join(), 'b,a,c,d,other');
    assert.equal(result.segments[4].amount, '0.0003');
    assert.equal(result.total, '200000000000000000005');
    assert.equal(money.sumDecimalAmounts(result.segments.map(x => x.amount)), result.total);
    assert.ok(result.segments.every(x => Number.isFinite(x.percentage) && Number.isFinite(x.offset)));
});
test('empty, income-only and reversed zero rows have no chart or percentages', () => {
    assert.equal(categorySpending([], 'EUR').total, '0');
    assert.equal(categorySpending([{ ...row('income', '0'), incomeAmount: '1000' }, row('voided', '0.0000')], 'EUR').segments.length, 0);
});
test('month-to-date uses UTC across local midnight, month and year boundaries', () => {
    assert.equal(JSON.stringify(utcMonthToDate(new Date('2026-11-01T00:30:00+02:00'))), JSON.stringify({ from: '2026-10-01', to: '2026-10-31' }));
    assert.equal(JSON.stringify(utcMonthToDate(new Date('2026-12-31T23:30:00-02:00'))), JSON.stringify({ from: '2027-01-01', to: '2027-01-01' }));
});
test('account changes invalidate household reporting as well as balances', async () => {
    const keys = [];
    const all = () => ['feature'];
    const { useAccountMutations } = load('../src/features/accounts/api/useAccountMutations.ts', {
        '../../transfers/transferQueries': { transferKeys: { all } },
        '../../activities/activityQueries': { activityKeys: { all } },
        '@tanstack/react-query': { useMutation: options => options,
            useQueryClient: () => ({ invalidateQueries: ({ queryKey }) => { keys.push(queryKey); } }) },
        '../../../shared/api/ApiError': { ApiError: Error },
        './accountsQueryKeys': { accountsQueryKeys: { household: all } },
        '../../dashboard/api/dashboardQueryKeys': { dashboardQueryKeys: { household: all } },
        './accountsApi': {},
    });
    await useAccountMutations('user', 'home').update.onSuccess();
    assert.ok(keys.some(key => key.join() === 'statistics,user,home'));
});

test('income ranks independently, sums daily decimals and groups the remainder without expenses', () => {
    const input = Array.from({ length: 6 }, (_, i) => ({ ...row(String(i), '999'), incomeAmount: String(i + 1) }));
    input.push({ ...row('5', '999'), incomeAmount: '0.0001' });
    input.push({ ...row('foreign', '999', 'HUF'), incomeAmount: '10000' });
    const result = categorySpending(input, 'EUR', 'Income');
    assert.equal(result.total, '21.0001');
    assert.equal(result.segments.map(x => x.id).join(), '5,4,3,2,other');
    assert.equal(result.segments[0].amount, '6.0001');
    assert.equal(result.segments[4].amount, '3');
    assert.equal(categorySpending([row('expense', '100')], 'EUR', 'Income').segments.length, 0);
});

const { categoryReportSearch } = load('../src/features/dashboard/categoryReportSearch.ts');
test('report links validate dates, currency, type and category without trusting URL input', () => {
    const valid = { from: '2026-10-01', to: '2026-10-12', currency: 'HUF', kind: 'Income', category: '12345678-1234-1234-1234-123456789abc' };
    assert.equal(JSON.stringify(categoryReportSearch(valid)), JSON.stringify(valid));
    for (const override of [{ from: '2026-02-30' }, { from: '0000-01-01', to: '0000-01-02' }, { to: '2025-10-01' }, { to: '2028-10-01' },
        { currency: 'EURO' }, { kind: 'Transfer' }, { from: ['2026-10-01'] }, { to: undefined }]) {
        assert.equal(Object.keys(categoryReportSearch({ ...valid, ...override })).length, 0);
    }
    assert.equal(categoryReportSearch({ ...valid, category: '<script>' }).category, undefined);
    assert.equal(categoryReportSearch({ ...valid, from: '2028-02-29', to: '2028-02-29' }).from, '2028-02-29');
});
test('full report preserves every category and reconciles to the dashboard including Other', () => {
    const rows = Array.from({ length: 7 }, (_, i) => row(String(i), `${i}.0001`));
    const dashboard = categorySpending(rows, 'EUR');
    const report = categorySpending(rows, 'EUR', 'Expense', Infinity);
    assert.equal(report.segments.length, 7);
    assert.equal(report.total, dashboard.total);
    assert.equal(report.segments.some(segment => segment.id === 'other'), false);
    assert.equal(money.sumDecimalAmounts(report.segments.slice(4).map(x => x.amount)), dashboard.segments[4].amount);
});
