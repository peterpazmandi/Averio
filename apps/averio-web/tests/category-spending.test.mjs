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
