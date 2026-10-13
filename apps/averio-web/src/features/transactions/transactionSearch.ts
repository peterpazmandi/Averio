export type TransactionSearch = {
    account?: string; kind?: 'Expense' | 'Income'; from?: string; to?: string;
    dates?: 'all'; activity?: string; transaction?: string;
    create?: boolean; createKind?: 'Expense' | 'Income';
};
const uuid = (value: unknown) => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) ? value : undefined;
const kind = (value: unknown) => value === 'Expense' || value === 'Income' ? value : undefined;
function date(value: unknown) {
    if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return undefined;
    const parsed = new Date(value + 'T00:00:00Z');
    return Number.isFinite(parsed.getTime()) && parsed.getUTCFullYear() > 0 && parsed.toISOString().slice(0, 10) === value ? value : undefined;
}
export function transactionSearch(search: Record<string, unknown>): TransactionSearch {
    const transaction = uuid(search.transaction);
    const create = !transaction && [true, 'true', '1'].includes(search.create as string | boolean) ? true : undefined;
    return { account: uuid(search.account), activity: uuid(search.activity), transaction,
        kind: kind(search.kind), from: date(search.from), to: date(search.to),
        dates: search.dates === 'all' ? 'all' : undefined, create,
        createKind: create ? kind(search.createKind) : undefined };
}
