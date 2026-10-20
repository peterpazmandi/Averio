import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

const context = { exports: {} };
vm.runInNewContext(ts.transpileModule(fs.readFileSync(
    new URL('../src/features/personal-money/refundEligibility.ts', import.meta.url), 'utf8'),
{ compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, context);
const { nextRefundPage, refundEligibility, validRefundAmount } = context.exports;
const purchase = { id: 'purchase', accountId: 'account', currency: 'EUR', visibility: 'Private' };
const income = { ...purchase, id: 'income', status: 'Posted' };
const entry = (amount, transactionId = 'income') => ({
    kind: 'PrivateRefund', amount, transactionId, relatedTransactionId: 'purchase',
});
const wallet = entries => ({
    currency: 'EUR', charges: [{ transactionId: 'purchase', amount: '100.0000' }], entries,
});

test('partial refunds reduce the limit and exclude used incomes; reversals restore eligibility', () => {
    let result = refundEligibility(wallet([entry('60')]));
    assert.equal(result.remaining.get('purchase'), '40.0000');
    assert.equal(result.canUseIncome(income, purchase), false);
    assert.equal(validRefundAmount('60', '60', result.remaining.get('purchase')), false);
    assert.equal(validRefundAmount('40', '60', result.remaining.get('purchase')), true);
    result = refundEligibility(wallet([entry('60'), entry('-60')]));
    assert.equal(result.remaining.get('purchase'), '100.0000');
    assert.equal(result.canUseIncome(income, purchase), true);
    assert.equal(result.canUseIncome({ ...income, visibility: 'Household' }, purchase), false);
    assert.equal(result.canUseIncome({ ...income, accountId: 'other' }, purchase), false);
    assert.equal(refundEligibility(wallet([entry('60'), entry('40', 'second')])).remaining.size, 0);
});

test('shared refunds and decimal boundaries use exact amounts', () => {
    const result = refundEligibility(wallet([{ ...entry('0.0001'), kind: 'SharedRefund' }]));
    assert.equal(result.remaining.get('purchase'), '99.9999');
    assert.equal(validRefundAmount('100', '100', result.remaining.get('purchase')), false);
    assert.equal(validRefundAmount('99,9999', '100', result.remaining.get('purchase')), true);
    assert.equal(validRefundAmount('999999999999999.9999', '999999999999999.9999', '999999999999999.9998'), false);
    for (const value of ['0', '-1', 'NaN', '1e2', '1.00001']) assert.equal(validRefundAmount(value, '100', '100'), false);
});

test('pagination uses the last unfiltered record so older eligible transactions remain reachable', () => {
    const items = Array.from({ length: 100 }, (_, i) => ({
        id: `newer-${i}`, occurredOn: '2026-10-20', createdAt: `timestamp-${i}`,
    }));
    const cursor = nextRefundPage({ items, hasMore: true });
    assert.equal(cursor.cursorId, 'newer-99');
    assert.equal(cursor.cursorCreatedAt, 'timestamp-99');
    assert.equal(cursor.cursorDate, '2026-10-20');
    const pages = [{ items, hasMore: true }, { items: [purchase], hasMore: false }];
    const eligible = refundEligibility(wallet([]));
    assert.equal(pages.flatMap(page => page.items).filter(x => eligible.remaining.has(x.id)).length, 1);
    assert.equal(nextRefundPage(pages[1]), undefined);
    assert.equal(nextRefundPage({ items: [], hasMore: false }), undefined);
});
