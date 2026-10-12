import type { CategoryCashFlowPeriodDto } from '../transactions/api/transactionApi';
import { subtractDecimalAmounts, sumDecimalAmounts } from '../../shared/money/decimalAmount';

export function utcMonthToDate(now: Date) {
    const to = now.toISOString().slice(0, 10);
    return { from: to.slice(0, 8) + '01', to };
}

/** Direct category amounts: a parent's own items never include its children's items. */
export function categorySpending(rows: CategoryCashFlowPeriodDto[], currency: string) {
    const groups = new Map<string, CategoryCashFlowPeriodDto>();
    for (const row of rows) {
        if (row.currency !== currency) continue;
        const previous = groups.get(row.categoryId);
        groups.set(row.categoryId, {
            ...row,
            expenseAmount: sumDecimalAmounts([previous?.expenseAmount ?? '0', row.expenseAmount]),
        });
    }
    const categories = [...groups.values()]
        .filter(row => Number(row.expenseAmount) > 0)
        .sort((a, b) => {
            const difference = subtractDecimalAmounts(b.expenseAmount, a.expenseAmount);
            return difference === '0' ? a.categoryId.localeCompare(b.categoryId)
                : difference.startsWith('-') ? -1 : 1;
        });
    const total = sumDecimalAmounts(categories.map(row => row.expenseAmount));
    const segments = categories.slice(0, 4).map(row => ({
        id: row.categoryId,
        systemKey: row.categorySystemKey,
        name: row.categoryName,
        amount: row.expenseAmount,
    }));
    if (categories.length > 4) segments.push({
        id: 'other', systemKey: null, name: null,
        amount: sumDecimalAmounts(categories.slice(4).map(row => row.expenseAmount)),
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
