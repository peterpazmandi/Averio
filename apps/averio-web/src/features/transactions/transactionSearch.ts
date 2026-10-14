export const datePresets = ['this-month', 'last-month', 'previous-3-full-months', 'this-year', 'last-year', 'last-12-months', 'all', 'custom'] as const;
export type DatePreset = typeof datePresets[number];
export type TransactionSearch = {
    account?: string; kind?: 'Expense' | 'Income'; from?: string; to?: string;
    period?: DatePreset; labels?: string[]; dates?: 'all'; activity?: string; transaction?: string;
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
    const period = datePresets.includes(search.period as DatePreset) ? search.period as DatePreset : undefined;
    const labels = [...new Set((Array.isArray(search.labels) ? search.labels : typeof search.labels === 'string' ? search.labels.split(',') : []).map(uuid).filter((id): id is string => Boolean(id)))].sort().slice(0, 20);
    const create = !transaction && [true, 'true', '1'].includes(search.create as string | boolean) ? true : undefined;
    return { period, labels: labels.length ? labels : undefined, account: uuid(search.account), activity: uuid(search.activity), transaction,
        kind: kind(search.kind), from: period && period !== 'custom' ? undefined : date(search.from), to: period && period !== 'custom' ? undefined : date(search.to),
        dates: search.dates === 'all' ? 'all' : undefined, create,
        createKind: create ? kind(search.createKind) : undefined };
}
