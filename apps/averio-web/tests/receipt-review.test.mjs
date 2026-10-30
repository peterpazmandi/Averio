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
const { normalizeReceiptAmount, receiptItems, receiptReconciles, suggestReceiptCategory } = load('../src/features/receipts/receiptReview.ts', {
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
test('receipt monetary request values share the reconciliation canonical form', () => {
    for (const input of ['09.00', '0009,00', ' 00009.0000 ']) {
        const wire = normalizeReceiptAmount(input);
        assert.equal(wire, '9');
        assert.equal(receiptReconciles(input, [{ amount: '09.00' }]), true);
        assert.match(wire, /^(?:0|[1-9][0-9]{0,14})(?:\.[0-9]{1,4})?$/);
    }
    assert.equal(normalizeReceiptAmount('0000,5000'), '0.5');
    assert.equal(normalizeReceiptAmount('0000.0000'), '0');
    assert.equal(normalizeReceiptAmount('1e2'), null);
    assert.equal(normalizeReceiptAmount('1000000000000000'), null);
});
test('category suggestions require a unique complete category-name match and support Unicode', () => {
    const categories = [{ id: 'a', displayName: 'Élelmiszer' }, { id: 'b', displayName: 'Food' }];
    assert.equal(suggestReceiptCategory('Friss ÉLELMISZER, 1 kg', categories)?.id, 'a');
    assert.equal(suggestReceiptCategory('Seafood', categories), undefined);
    assert.equal(suggestReceiptCategory('Élelmiszer Food', categories), undefined);
    assert.equal(suggestReceiptCategory('Food', [...categories, { id: 'c', displayName: 'Food' }]), undefined);
});

test('discount suggestions reconcile the Tesco receipt and preserve original evidence', () => {
    const lines = [
        ['TOR MEX TORT', '3078'], ['ZEWA DEL LEV 3R24T', '4329'], ['GMO M S DAR', '3398'],
        ['TS S-M DHUS', '1839'], ['LARA HY TOR', '929'], ['MAGY TEJF', '537'], ['TS KEFIR', '357'],
        ['VENUSZ SOS', '426'], ['GOUDAS STANGLI', '438'], ['BANAN LEDIG', '403'], ['GAGA BABY WIPES 72DB', '1436'], ['Tesco bag', '19'],
    ].map(([description, amount]) => ({ description, amount, kind: 'Item' }));
    lines.push({ description: 'CC ZEWA DEL LEV 3R24', amount: '-860', kind: 'Discount' },
        { description: '11866 GAGA BABY WIPE', amount: '-150', kind: 'Discount' });
    const draft = { total: '16179', lines };
    const original = JSON.stringify(draft);
    const items = receiptItems(draft);
    assert.equal(items[1].amount, '3469');
    assert.equal(items[10].amount, '1286');
    assert.equal(items[1].discountAmount, '-860');
    assert.equal(receiptReconciles('16179', items), true);
    assert.equal(JSON.stringify(draft), original);
});
test('discount matching leaves ambiguous, unmatched, invalid and already-net amounts for review', () => {
    const item = { description: 'Food Apples', amount: '10', kind: 'Item' };
    const discount = { description: 'Food Apples discount', amount: '-1', kind: 'Discount' };
    assert.equal(receiptItems({ total: '9', lines: [item, discount] })[0].amount, '9');
    assert.equal(receiptItems({ total: '19', lines: [item, item, discount] })[0].amount, '10');
    assert.equal(receiptItems({ total: '9', lines: [item, { ...discount, description: 'Coupon' }] })[0].amount, '10');
    assert.equal(receiptItems({ total: '8', lines: [item, discount] })[0].amount, '10');
    assert.equal(receiptItems({ total: '10', lines: [item, discount] })[0].discountAmount, '');
    assert.equal(receiptItems({ total: '9', lines: [item, { ...discount, amount: 'oops' }] })[0].amount, '10');
    assert.equal(receiptItems({ total: '0', lines: [item, { ...discount, amount: '-10' }] })[0].amount, '10');
});
test('AI names prefill edit fields without replacing original evidence or breaking old drafts', () => {
    const item = { description: 'TS DHUS', suggestedDescription: 'Darált hús', kind: 'Item', amount: '100' };
    const result = receiptItems({ total: '100', lines: [item] })[0];
    assert.equal(result.description, 'Darált hús');
    assert.equal(result.originalDescription, 'TS DHUS');
    assert.equal(receiptItems({ lines: [{ ...item, suggestedDescription: null }] })[0].description, 'TS DHUS');
});
