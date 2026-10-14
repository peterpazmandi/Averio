import type { DatePreset, TransactionSearch } from './transactionSearch';

export function effectivePeriod(search: TransactionSearch): DatePreset {
    return search.period ?? (search.from || search.to ? 'custom' : search.dates === 'all' || search.activity ? 'all' : 'this-month');
}
// Match dashboard calendar boundaries: resolve presets in UTC, without serializing today's dates.
export function resolveTransactionDates(search: TransactionSearch, now: Date) {
    const period = effectivePeriod(search);
    const y = now.getUTCFullYear(), m = now.getUTCMonth();
    const iso = (date: Date) => date.toISOString().slice(0, 10);
    const day = (year: number, month: number, date = 1) => { const d = new Date(0); d.setUTCFullYear(year, month, date); return iso(d); };
    const today = iso(now);
    switch (period) {
        case 'this-month': return { from: day(y, m), to: today };
        case 'last-month': return { from: day(y, m - 1), to: day(y, m, 0) };
        case 'previous-3-full-months': return { from: day(y, m - 3), to: day(y, m, 0) };
        case 'this-year': return { from: day(y, 0), to: today };
        case 'last-year': return { from: day(y - 1, 0), to: day(y, 0, 0) };
        case 'last-12-months': return { from: day(y, m - 11), to: today };
        case 'all': return { from: undefined, to: undefined };
        default: return { from: search.from, to: search.to };
    }
}

export function savedFilters(search: TransactionSearch) {
    const period = effectivePeriod(search);
    return { account: search.account ?? null, kind: search.kind ?? null, period,
        from: period === 'custom' ? search.from ?? null : null, to: period === 'custom' ? search.to ?? null : null,
        activity: search.activity ?? null, labels: [...(search.labels ?? [])].sort() };
}
