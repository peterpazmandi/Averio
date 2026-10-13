export type CategoryReportSearch = {
    from?: string;
    to?: string;
    currency?: string;
    kind?: 'Expense' | 'Income';
    category?: string;
};

function validDate(value: unknown): value is string {
    if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
    const date = new Date(value + 'T00:00:00Z');
    return Number.isFinite(date.getTime()) && date.getUTCFullYear() >= 1 && date.toISOString().slice(0, 10) === value;
}

/** Accept a complete bounded report context; malformed links fall back to Statistics. */
export function categoryReportSearch(search: Record<string, unknown>): CategoryReportSearch {
    const { from, to, currency, kind, category } = search;
    if (!validDate(from) || !validDate(to) || from > to
        || Date.parse(to) - Date.parse(from) > 365 * 86_400_000
        || typeof currency !== 'string' || !/^[A-Z]{3}$/.test(currency)
        || (kind !== 'Expense' && kind !== 'Income')) return {};
    return { from, to, currency, kind,
        category: typeof category === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(category)
            ? category : undefined };
}
