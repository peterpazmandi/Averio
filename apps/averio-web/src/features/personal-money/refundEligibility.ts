import type { PersonalMoneySnapshotResponse, TransactionPageResponse, TransactionResponse } from '../../shared/api/generated';

export type RefundCursor = { cursorDate: string; cursorCreatedAt: string; cursorId: string };

export function nextRefundPage(page: TransactionPageResponse): RefundCursor | undefined {
    const last = page.items.at(-1);
    return page.hasMore && last
        ? { cursorDate: last.occurredOn, cursorCreatedAt: last.createdAt, cursorId: last.id }
        : undefined;
}

// Match the ledger's decimal(19,4) arithmetic, including signed reversals.
const units = (value: string) => {
    const match = /^(-?)(\d+)(?:[.,](\d{1,4}))?$/.exec(value.trim());
    if (!match) return null;
    return (match[1] ? -1n : 1n) * (BigInt(match[2]) * 10000n + BigInt((match[3] ?? '').padEnd(4, '0')));
};
const decimal = (value: bigint) => `${value / 10000n}.${String(value % 10000n).padStart(4, '0')}`;

export function refundEligibility(wallet: PersonalMoneySnapshotResponse) {
    const refundedPurchases = new Map<string, bigint>();
    const usedIncomes = new Map<string, bigint>();
    for (const entry of wallet.entries) {
        if (entry.kind !== 'PrivateRefund' && entry.kind !== 'SharedRefund') continue;
        const amount = units(entry.amount) ?? 0n;
        if (entry.relatedTransactionId)
            refundedPurchases.set(entry.relatedTransactionId, (refundedPurchases.get(entry.relatedTransactionId) ?? 0n) + amount);
        if (entry.transactionId)
            usedIncomes.set(entry.transactionId, (usedIncomes.get(entry.transactionId) ?? 0n) + amount);
    }
    const remaining = new Map<string, string>();
    for (const charge of wallet.charges) {
        const amount = (units(charge.amount) ?? 0n) - (refundedPurchases.get(charge.transactionId) ?? 0n);
        if (amount > 0n) remaining.set(charge.transactionId, decimal(amount));
    }
    return {
        remaining,
        canUseIncome: (income: TransactionResponse, purchase: TransactionResponse | undefined) =>
            !!purchase && income.status === 'Posted' && income.accountId === purchase.accountId
            && income.currency === wallet.currency && income.visibility === purchase.visibility
            && (usedIncomes.get(income.id) ?? 0n) === 0n,
    };
}

export function validRefundAmount(amount: string, incomeAmount: string, remainingAmount: string) {
    const value = units(amount);
    return value !== null && value > 0n
        && value <= (units(incomeAmount) ?? 0n) && value <= (units(remainingAmount) ?? 0n);
}
