import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

const code = ts.transpileModule(fs.readFileSync(new URL('../src/features/activities/activityChart.ts', import.meta.url), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const context = { exports: {} };
vm.runInNewContext(code, context);
const { activityChartRows } = context.exports;
const day = (date, currency, income, expenses) => ({ date, currency, income, expenses, transactionCount: 1 });

test('timeline sorts dates, separates currencies and preserves input', () => {
    const input = [day('2026-10-03', 'EUR', '10', '5'), day('2026-10-01', 'EUR', '0', '20'), day('2026-10-01', 'USD', '1000', '0')];
    const rows = activityChartRows(input, 'EUR');
    assert.equal(rows.map(x => x.date).join(), '2026-10-01,2026-10-03');
    assert.equal(rows[0].expenseHeight, 160);
    assert.equal(rows[1].incomeHeight, 80);
    assert.equal(rows[1].expenseHeight, 40);
    assert.equal(input[0].date, '2026-10-03');
});

test('timeline handles empty, zero and large precise decimals without nonfinite coordinates', () => {
    assert.equal(activityChartRows([], 'EUR').length, 0);
    const zero = activityChartRows([day('2026-10-03', 'EUR', '0', '0')], 'EUR')[0];
    assert.equal(zero.incomeHeight, 0);
    const huge = activityChartRows([day('2026-10-03', 'EUR', '99999999999999999999.9999', '0.0001')], 'EUR')[0];
    assert.equal(huge.incomeHeight, 160);
    assert.equal(huge.income, '99999999999999999999.9999');
    assert(Number.isFinite(huge.expenseHeight));
});
