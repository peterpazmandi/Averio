import type { PersonalMoneySnapshotResponse } from '../../shared/api/generated';

// Only the signed-in member's personal ledger is used. Never derive other members'
// private relationships from shared transaction or transfer attributes.
export type PersonalMoneyRelationKind =
    | 'PrivateCharge' | 'SharedCharge' | 'PrivateRefund' | 'SharedRefund'
    | 'AllowanceFunding' | 'FundingReturn' | 'CreateAndFundGrant';

export interface PersonalMoneyRelation {
    kind: PersonalMoneyRelationKind;
    amount: string;
    currency: string;
    relatedTransactionId?: string;
}

type LinkedEntry = PersonalMoneySnapshotResponse['entries'][number] & {
    transactionId?: string | null;
    transferId?: string | null;
    relatedTransactionId?: string | null;
};

// All ledger amounts are numeric(19,4). Aggregate display-only ledger links exactly,
// including signed corrective events, so edited/voided relations disappear.
const units = (amount: string): bigint => {
    const [, sign, whole, fraction = ''] =
        /^(-?)(\d+)(?:\.(\d{1,4}))?$/.exec(amount) ?? [];
    if (!whole) return 0n;
    const scaled = BigInt(whole) * 10000n + BigInt(fraction.padEnd(4, '0'));
    return sign === '-' ? -scaled : scaled;
};
const formatUnits = (amount: bigint) => {
    const abs = amount < 0n ? -amount : amount;
    return `${abs / 10000n}.${String(abs % 10000n).padStart(4, '0')}`;
};

export function personalMoneyRelations(snapshot: PersonalMoneySnapshotResponse | null | undefined) {
    const transactions = new Map<string, PersonalMoneyRelation>();
    const transfers = new Map<string, PersonalMoneyRelation>();
    if (!snapshot) return { transactions, transfers };

    const entries = snapshot.entries as LinkedEntry[];
    const groups = new Map<string, { kind: string; id: string; sum: bigint; relatedId?: string }>();
    for (const entry of entries) {
        const isTransfer = entry.kind === 'Funding' || entry.kind === 'Grant';
        const id = isTransfer ? entry.transferId : entry.transactionId;
        if (!id) continue;
        if (!isTransfer && !['PrivateCharge', 'SharedCharge', 'PrivateRefund', 'SharedRefund'].includes(entry.kind)) continue;
        const key = [isTransfer ? 'transfer' : 'transaction', entry.kind, id].join(':');
        const group = groups.get(key) ?? { kind: entry.kind, id, sum: 0n, relatedId: entry.relatedTransactionId ?? undefined };
        group.sum += units(entry.amount);
        groups.set(key, group);
    }
    const hasGrant = new Set([...groups.values()].filter(g => g.kind === 'Grant' && g.sum > 0n).map(g => g.id));
    for (const g of groups.values()) {
        if (g.sum === 0n) continue;
        if (g.kind === 'Funding') {
            transfers.set(g.id, {
                kind: g.sum < 0n ? 'FundingReturn' : hasGrant.has(g.id) ? 'CreateAndFundGrant' : 'AllowanceFunding',
                amount: formatUnits(g.sum), currency: snapshot.currency,
            });
        } else if (g.kind === 'PrivateCharge' || g.kind === 'SharedCharge') {
            if (g.sum >= 0n) continue;
            transactions.set(g.id, { kind: g.kind, amount: formatUnits(g.sum), currency: snapshot.currency });
        } else if (g.kind === 'PrivateRefund' || g.kind === 'SharedRefund') {
            if (g.sum <= 0n) continue;
            transactions.set(g.id, {
                kind: g.kind, amount: formatUnits(g.sum),
                currency: snapshot.currency, relatedTransactionId: g.relatedId,
            });
        }
    }
    return { transactions, transfers };
}
