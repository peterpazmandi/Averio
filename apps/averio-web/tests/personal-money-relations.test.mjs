import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

function load() {
    const code = ts.transpileModule(fs.readFileSync(new URL('../src/features/personal-money/personalMoneyRelations.ts', import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS },
    }).outputText;
    const context = { exports: {} };
    vm.runInNewContext(code, context);
    return context.exports.personalMoneyRelations;
}
const event = (kind, amount, ids = {}) => ({
    id: crypto.randomUUID(), kind, amount, occurredOn: '2026-10-19', ...ids,
});
test('personal relation badges reflect only nonzero own wallet effects', () => {
    const relations = load();
    assert.equal(relations(null).transactions.size, 0);
    const snapshot = {
        currency: 'HUF',
        entries: [
            event('Grant', '300.0000', { transferId: 'create' }),
            event('Funding', '300.0000', { transferId: 'create' }),
            event('Funding', '40.0000', { transferId: 'fund' }),
            event('Funding', '-20.0000', { transferId: 'return' }),
            event('PrivateCharge', '-50.0000', { transactionId: 'private' }),
            event('SharedCharge', '-80.0000', { transactionId: 'shared' }),
            event('SharedCharge', '80.0000', { transactionId: 'shared' }),
            event('SharedCharge', '-60.0000', { transactionId: 'shared' }),
            event('SharedRefund', '10.0000', { transactionId: 'refund', relatedTransactionId: 'shared' }),
            event('PrivateRefund', '30.0000', { transactionId: 'void-refund' }),
            event('PrivateRefund', '-30.0000', { transactionId: 'void-refund' }),
            event('Surrender', '-10.0000'),
        ],
    };
    const result = relations(snapshot);
    assert.equal(result.transactions.get('private').kind, 'PrivateCharge');
    assert.equal(result.transactions.get('shared').amount, '60.0000');
    assert.equal(result.transactions.get('refund').relatedTransactionId, 'shared');
    assert.equal(result.transactions.has('void-refund'), false);
    assert.equal(result.transfers.get('create').kind, 'CreateAndFundGrant');
    assert.equal(result.transfers.get('fund').kind, 'AllowanceFunding');
    assert.equal(result.transfers.get('return').kind, 'FundingReturn');
});
test('a reverted or voided funding has no active relation', () => {
    const relations = load();
    const { transfers } = relations({ currency: 'EUR', entries: [
        event('Funding', '20.0000', { transferId: 'voided' }),
        event('Funding', '-20.0000', { transferId: 'voided' }),
    ] });
    assert.equal(transfers.size, 0);
});
