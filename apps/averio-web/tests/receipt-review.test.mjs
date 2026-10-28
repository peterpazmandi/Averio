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
const { receiptItems, receiptReconciles, suggestReceiptCategory } = load('../src/features/receipts/receiptReview.ts', {
    '../../shared/money/decimalAmount': money,
});
test('only item and deposit observations prefill expenses, with discounts and payment lines retained in evidence', () => {
    const draft = { lines: ['Item', 'Deposit', 'Discount', 'Tax', 'Subtotal', 'Payment', 'Other']
        .map(kind => ({ kind, description: kind, amount: kind === 'Discount' ? '-1' : '2' })) };
    const original = JSON.stringify(draft);
    assert.equal(receiptItems(draft).map(x => x.description).join(), 'Item,Deposit');
    assert.equal(JSON.stringify(draft), original);
    assert.equal(receiptItems({ lines: Array.from({ length: 101 }, () => draft.lines[0]) }).length, 0);
    assert.equal(receiptItems({ lines: [{ kind: 'Item', description: 'Unreadable', amount: null }] })[0].amount, '');
});
test('reconciliation is exact, bounded, positive and rejects missing or invalid values', () => {
    assert.equal(receiptReconciles('0.3', [{ amount: '0.1' }, { amount: '0.2' }]), true);
    assert.equal(receiptReconciles('999999999999999.9999', [{ amount: '999999999999999.9998' }, { amount: '0.0001' }]), true);
    assert.equal(receiptReconciles('1,25', [{ amount: '1.25' }]), true);
    for (const total of ['', '-1', '0', '10.00111', '1e2', '1000000000000000'])
        assert.equal(receiptReconciles(total, [{ amount: total }]), false);
    assert.equal(receiptReconciles('9', [{ amount: '10' }]), false);
    assert.equal(receiptReconciles('9', [{ amount: '10' }, { amount: '-1' }]), false);
    assert.equal(receiptReconciles('1', [{ amount: '1' }, { amount: '' }]), false);
    assert.equal(receiptReconciles('1', []), false);
});
test('category suggestions require a unique complete category-name match and support Unicode', () => {
    const categories = [{ id: 'a', displayName: 'Élelmiszer' }, { id: 'b', displayName: 'Food' }];
    assert.equal(suggestReceiptCategory('Friss ÉLELMISZER, 1 kg', categories)?.id, 'a');
    assert.equal(suggestReceiptCategory('Seafood', categories), undefined);
    assert.equal(suggestReceiptCategory('Élelmiszer Food', categories), undefined);
    assert.equal(suggestReceiptCategory('Food', [...categories, { id: 'c', displayName: 'Food' }]), undefined);
});
