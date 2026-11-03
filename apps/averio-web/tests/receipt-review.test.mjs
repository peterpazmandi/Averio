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
const { receiptInboxRows, receiptPageSize } = load('../src/features/receipts/receiptInbox.ts');

test('receipt inbox separates history, keeps failures actionable and deduplicates overlapping pages', () => {
    const scans = ['Posted', 'Ready', 'Discarded', 'Failed', 'Queued', 'Expired', 'Processing', 'Uploading'].map((status, index) => ({
        id: String(index), status, createdAt: '2026-11-02T08:00:00Z',
    }));
    const active = receiptInboxRows([...scans, scans[1]], false);
    assert.deepEqual(Array.from(active, scan => scan.status), ['Ready', 'Failed', 'Queued', 'Processing', 'Uploading']);
    assert.deepEqual(Array.from(receiptInboxRows(scans, true), scan => scan.status), ['Posted', 'Discarded', 'Expired']);
    assert.equal(receiptInboxRows(scans, false, 'Queued').length, 1);
    assert.equal(receiptInboxRows(scans, true, 'Ready').length, 0);
    assert.equal(receiptPageSize, 10);
});
const { assignReceiptDiscounts, normalizeReceiptAmount, receiptItems, receiptReconciles, suggestReceiptCategory } = load('../src/features/receipts/receiptReview.ts', {
    '../../shared/money/decimalAmount': money,
});

test('receipt review retains each product and deposit price in source order', () => {
    const pairs = [
        ['Vegyes viragmez', '1999', 'Item'], ['Narancsle 100%', '599', 'Item'],
        ['Visszav.dij KF 1db', '50', 'Deposit'], ['Coca-Cola 0,5l', '419', 'Item'],
        ['Visszav.dij BF 1db', '50', 'Deposit'], ['Kap. juht. cs.mell', '1399', 'Item'],
    ];
    const draft = { total: '4516', lines: pairs.map(([description, amount, kind]) => ({ description, amount, kind })) };
    const items = receiptItems(draft);
    assert.deepEqual(Array.from(items, item => [item.originalDescription, item.amount]), pairs.map(([description, amount]) => [description, amount]));
    assert.equal(receiptReconciles(draft.total, items), true);
    assert.equal(items[2].canRememberCategory, false);
    draft.lines[1].amount = '1999';
    assert.equal(receiptReconciles(draft.total, receiptItems(draft)), false);
    assert.equal(receiptItems(draft)[1].amount, '1999', 'Do not invent a correction from the expected total.');
});

test('explicit discount assignment requires a complete exact positive proposal and preserves edits', () => {
    const items = [{ amount: '1999', description: 'Edited chocolate' }, { amount: '3198' }];
    const discounts = [{ amount: '-640' }, { amount: '-600' }];
    const result = assignReceiptDiscounts(items, discounts, [1, 0], '3957');
    assert.equal(result[0].description, 'Edited chocolate');
    assert.equal(result[0].amount, '1399');
    assert.equal(result[1].amount, '2558');
    assert.equal(result[1].originalAmount, '3198');
    assert.equal(result[1].discountAmount, '-640');
    assert.equal(items[0].amount, '1999');
    assert.equal(assignReceiptDiscounts(result, discounts, [1, 0], '3957'), null);
    assert.equal(assignReceiptDiscounts(items, discounts, [-1, 0], '3957'), null);
    assert.equal(assignReceiptDiscounts(items, discounts, [1], '3957'), null);
    assert.equal(assignReceiptDiscounts(items, discounts, [1, 0], '3956'), null);
    assert.equal(assignReceiptDiscounts([{ amount: '1' }, { amount: '10' }], [{ amount: '-2' }], [0], '9'), null);
    assert.equal(assignReceiptDiscounts(items, [{ amount: 'oops' }], [0], '3957'), null);
});

test('OCR-damaged product suffix still allocates the two Tesco discounts exactly once', () => {
    const draft = { total: '9497.00', lines: [
        { kind: 'Item', description: '0,602 KG * 484,00 Ft /KG ALMA GALA LEDIG', amount: '291.00' },
        { kind: 'Item', description: 'MILKA ALP.TEJCS.250g', amount: '1999.00' },
        { kind: 'Item', description: '3AS KAPCS.EL.5M ZOLD', amount: '5249.00' },
        { kind: 'Item', description: '2,000 DB * 1 599,00 Ft /DB LANDL A60 12.4/15W', amount: '3198.00' },
        { kind: 'Subtotal', description: 'RESZOSSZEG', amount: '10737.00' },
        { kind: 'Discount', description: 'CC LANDL A60 12.4/1Voysa miyoo', amount: '-640.00' },
        { kind: 'Discount', description: 'CC MILKA ALP.TEJCS.2', amount: '-600.00' },
        { kind: 'Subtotal', description: 'OSSZESEN', amount: '9497.00' },
        { kind: 'Payment', description: 'BANKKARTYA', amount: '9497.00' },
    ] };
    const original = JSON.stringify(draft);
    const items = receiptItems(draft);
    assert.equal(items.map(x => x.amount).join(), '291.00,1399,5249.00,2558');
    assert.equal(items[1].discountAmount, '-600');
    assert.equal(items[3].discountAmount, '-640');
    assert.equal(items[3].originalAmount, '3198.00');
    assert.equal(items[3].receiptLineIndex, 3);
    assert.equal(receiptReconciles(draft.total, items), true);
    assert.equal(JSON.stringify(draft), original);
    assert.equal(receiptItems(draft).map(x => x.amount).join(), items.map(x => x.amount).join());
    assert.equal(receiptItems({ ...draft, total: '9496' })[3].amount, '3198.00');
});

test('OCR fallback refuses ambiguous models, code-only matches and numeric prices', () => {
    const item = { kind: 'Item', description: 'LANDL A60 15W', amount: '10' };
    const discount = { kind: 'Discount', description: 'LANDL A60 damaged suffix extra words', amount: '-1' };
    assert.equal(receiptItems({ total: '19', lines: [item, { ...item, description: 'LANDL A60 20W' }, discount] })[0].amount, '10');
    for (const description of ['A60 damaged suffix extra words', 'LANDL 1599 damaged suffix extra words', 'LANDL A600 damaged suffix extra words']) {
        assert.equal(receiptItems({ total: '9', lines: [item, { ...discount, description }] })[0].amount, '10');
    }
    assert.equal(receiptItems({ total: '10', lines: [item, discount] })[0].discountAmount, '');
});
test('category suggestions remain unapplied and source indices survive non-item lines', () => {
    const draft = { lines: [
        { kind: 'Subtotal', description: 'Subtotal', amount: '5' },
        { kind: 'Item', description: 'KEFIR', amount: '4', suggestedCategoryId: 'dairy', categorySuggestionSource: 'HouseholdRule' },
        { kind: 'Deposit', description: 'Deposit', amount: '1' },
    ] };
    const items = receiptItems(draft);
    assert.equal(items[0].categoryId, '');
    assert.equal(items[0].suggestedCategoryId, 'dairy');
    assert.equal(items[0].categorySuggestionSource, 'HouseholdRule');
    assert.equal(items[0].receiptLineIndex, 1);
    assert.equal(items[0].rememberCategory, false);
    assert.equal(items[0].canRememberCategory, true);
    assert.equal(items[1].canRememberCategory, false);
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

