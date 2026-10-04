import { useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Link } from '@tanstack/react-router';
import type { ActivityResponse } from '../../shared/api/generated';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { activityQueries } from './activityQueries';
import { activityChartRows } from './activityChart';
import { ActivityIllustration } from './ActivityIllustration';

export function ActivityTimeline({ userId, householdId, activity, onClose }: {
    userId: string; householdId: string; activity: ActivityResponse; onClose: () => void;
}) {
    const { t, i18n } = useTranslation();
    const titleId = useId();
    const query = useQuery(activityQueries.timeline(userId, householdId, activity.id));
    const [selectedCurrency, setSelectedCurrency] = useState('');
    const currencies = [...new Set((query.data ?? []).map(day => day.currency))].sort();
    const currency = currencies.includes(selectedCurrency) ? selectedCurrency : currencies[0] ?? '';
    const rows = activityChartRows(query.data ?? [], currency);
    const date = (value: string) => new Intl.DateTimeFormat(i18n.language, {
        year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC',
    }).format(new Date(value + 'T00:00:00Z'));
    const amount = (value: string) => formatDecimalAmount(value, currency, i18n.language);
    const width = Math.max(420, rows.length * 86 + 24);
    const slot = (width - 24) / Math.max(1, rows.length);
    return <AverioDialog open title={t('activities.chartTitle', { name: activity.name })}
        closeLabel={t('common.actions.close')} onDismiss={onClose}
        actions={<AverioButton onClick={onClose}>{t('common.actions.close')}</AverioButton>}>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <div role="alert"><p>{query.error.message}</p>
            <AverioButton onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
        {query.isSuccess && rows.length === 0 && <AverioCard className="p-5 text-center">
            <ActivityIllustration /><h3 className="font-bold">{t('activities.chartEmpty')}</h3>
            <p className="my-3">{t('activities.noTotals')}</p>
            <Link to="/app/transactions" search={{ activity: activity.id }} onClick={onClose}
                className="inline-flex min-h-11 items-center rounded-xl bg-emerald-600 px-3 font-semibold text-white focus-visible:outline-2 focus-visible:outline-offset-2">
                {t('activities.viewTransactions')}</Link>
        </AverioCard>}
        {query.isSuccess && rows.length > 0 && <div className="grid min-w-0 gap-4">
            <AverioField>{t('activities.chartCurrency')}<AverioSelect value={currency} onChange={e => setSelectedCurrency(e.target.value)}>
                {currencies.map(code => <option key={code} value={code}>{code}</option>)}
            </AverioSelect></AverioField>
            <p className="text-xs">{t('activities.chartHelp')}</p>
            <AverioCard className="min-w-0 p-3">
                <div className="mb-3 flex flex-wrap gap-4 text-xs font-semibold">
                    <span className="flex items-center gap-2"><span aria-hidden="true" className="h-3 w-3 bg-emerald-600 dark:bg-emerald-400" />{t('activities.income')}</span>
                    <span className="flex items-center gap-2"><span aria-hidden="true" className="h-3 w-3 bg-orange-700 dark:bg-orange-400" />{t('activities.expenses')}</span>
                </div>
                <div tabIndex={0} role="region" aria-label={t('activities.chartScroll')}
                    className="overflow-x-auto rounded-md focus-visible:outline-2 focus-visible:outline-primary-500">
                    <svg width={width} height={270} viewBox={`0 0 ${width} 270`} role="img" aria-labelledby={titleId} className="max-w-none! min-w-full">
                        <title id={titleId}>{t('activities.chartDescription', { currency })}</title>
                        <text x={12} y={14} className="fill-current text-[10px]">{amount(rows.find(day => day.incomeHeight === 160)?.income
                            ?? rows.find(day => day.expenseHeight === 160)?.expenses ?? '0')}</text>
                        <line x1={12} x2={width - 12} y1={24} y2={24} strokeDasharray="4 4" className="stroke-slate-300 dark:stroke-slate-600" />
                        <line x1={12} x2={width - 12} y1={184} y2={184} className="stroke-slate-400 dark:stroke-slate-500" />
                        <text x={12} y={200} className="fill-current text-[10px]">0</text>
                        {rows.map((day, index) => {
                            const center = 12 + slot * (index + 0.5);
                            return <g key={day.date}>
                                <rect x={center - 23} y={184 - day.incomeHeight} width={21} height={day.incomeHeight}
                                    className="fill-emerald-600 dark:fill-emerald-400"><title>{date(day.date)} · {t('activities.income')}: {amount(day.income)}</title></rect>
                                <rect x={center + 2} y={184 - day.expenseHeight} width={21} height={day.expenseHeight}
                                    className="fill-orange-700 dark:fill-orange-400"><title>{date(day.date)} · {t('activities.expenses')}: {amount(day.expenses)}</title></rect>
                                <text x={center} y={205} textAnchor="end" transform={`rotate(-40 ${center} 205)`}
                                    className="fill-current text-[10px]">{date(day.date)}</text>
                            </g>;
                        })}
                    </svg>
                </div>
            </AverioCard>
            <details>
                <summary className="cursor-pointer py-2 font-semibold focus-visible:outline-2">{t('activities.chartValues')}</summary>
                <div className="overflow-x-auto">
                    <table className="w-full text-left text-xs tabular-nums">
                        <caption className="sr-only">{t('activities.chartDescription', { currency })}</caption>
                        <thead><tr><th scope="col" className="p-2">{t('activities.chartDate')}</th>
                            <th scope="col" className="p-2">{t('activities.income')}</th><th scope="col" className="p-2">{t('activities.expenses')}</th></tr></thead>
                        <tbody>{rows.map(day => <tr key={day.date} className="border-t border-emerald-200 dark:border-emerald-900">
                            <th scope="row" className="p-2 font-normal">{date(day.date)}</th><td className="p-2">{amount(day.income)}</td><td className="p-2">{amount(day.expenses)}</td>
                        </tr>)}</tbody>
                    </table>
                </div>
            </details>
        </div>}
    </AverioDialog>;
}
