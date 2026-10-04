import type { ActivityDayResponse } from '../../shared/api/generated';

// Preserve decimal precision; only the final pixel coordinate becomes a Number.
function units(value: string): bigint {
    const [whole, fraction = ''] = value.split('.');
    return BigInt(whole) * 10_000n + BigInt(fraction.padEnd(4, '0'));
}

export function activityChartRows(days: ActivityDayResponse[], currency: string) {
    const rows = days.filter(day => day.currency === currency).sort((a, b) => a.date.localeCompare(b.date));
    const maximum = rows.reduce((max, day) => {
        const income = units(day.income), expenses = units(day.expenses);
        return income > max ? (income > expenses ? income : expenses) : (expenses > max ? expenses : max);
    }, 0n);
    const height = (value: string) => maximum === 0n ? 0 : Number(units(value) * 160_000n / maximum) / 1000;
    return rows.map(day => ({ ...day, incomeHeight: height(day.income), expenseHeight: height(day.expenses) }));
}
