import type { CategoryCashFlowPeriodDto } from '../transactions/api/transactionApi';
import { subtractDecimalAmounts, sumDecimalAmounts } from '../../shared/money/decimalAmount';

export function utcMonthToDate(now: Date) {
    const to = now.toISOString().slice(0, 10);
    return { from: to.slice(0, 8) + '01', to };
}

/** Direct category amounts: a parent's own items never include its children's items. */
export function categorySpending(rows: CategoryCashFlowPeriodDto[], currency: string, kind: 'Expense' | 'Income' = 'Expense') {
    const amountKey = kind === 'Income' ? 'incomeAmount' : 'expenseAmount';
    const groups = new Map<string, CategoryCashFlowPeriodDto>();
    for (const row of rows) {
        if (row.currency !== currency) continue;
        const previous = groups.get(row.categoryId);
        groups.set(row.categoryId, {
            ...row,
            [amountKey]: sumDecimalAmounts([previous?.[amountKey] ?? '0', row[amountKey]]),
        });
    }
    const categories = [...groups.values()]
        .filter(row => Number(row[amountKey]) > 0)
        .sort((a, b) => {
            const difference = subtractDecimalAmounts(b[amountKey], a[amountKey]);
            return difference === '0' ? a.categoryId.localeCompare(b.categoryId)
                : difference.startsWith('-') ? -1 : 1;
        });
    const total = sumDecimalAmounts(categories.map(row => row[amountKey]));
    const segments = categories.slice(0, 4).map(row => ({
        id: row.categoryId,
        systemKey: row.categorySystemKey,
        name: row.categoryName,
        amount: row[amountKey],
    }));
    if (categories.length > 4) segments.push({
        id: 'other', systemKey: null, name: null,
        amount: sumDecimalAmounts(categories.slice(4).map(row => row[amountKey])),
    });
    // Floating point is only used for chart geometry, never money totals or labels.
    let offset = 0;
    return {
        total,
        segments: segments.map(segment => {
            const percentage = Number(segment.amount) / Number(total) * 100;
            const result = { ...segment, percentage, offset };
            offset += percentage;
            return result;
        }),
    };
}
