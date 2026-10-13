import { useEffect, useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { transactionQueries } from '../../transactions/api/transactionQueries';
import { getCategoryDisplayName } from '../../transactions/categoryLocalization';
import { CategoryReportHelp } from './CategoryReportHelp';
import { categorySpending, utcMonthToDate } from '../categorySpending';

const tones = ['text-emerald-500', 'text-indigo-500', 'text-amber-500', 'text-sky-500', 'text-slate-400'];
const linkStyle = 'inline-flex min-h-11 items-center rounded-lg text-sm font-semibold text-emerald-700 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:text-emerald-400';

export function CategorySpendingCard({ userId, householdId, reportingCurrency }: {
    userId: string; householdId: string; reportingCurrency: string;
}) {
    const { t, i18n } = useTranslation();
    const controlId = useId();
    const [kind, setKind] = useState<'Expense' | 'Income'>('Expense');
    const copy = (key: string) => t(`categorySpending.${kind === 'Income' ? 'income' + key[0].toUpperCase() + key.slice(1) : key}`, { currency });
    const language = i18n.resolvedLanguage ?? i18n.language;
    const [today, setToday] = useState(() => utcMonthToDate(new Date()).to);
    const [selectedCurrency, setSelectedCurrency] = useState<string | null>(null);
    useEffect(() => {
        const update = () => setToday(utcMonthToDate(new Date()).to);
        const timer = window.setInterval(update, 30_000);
        window.addEventListener('focus', update);
        return () => { window.clearInterval(timer); window.removeEventListener('focus', update); };
    }, []);
    const from = today.slice(0, 8) + '01';
    const query = useQuery({
        ...transactionQueries.categoryCashFlow(userId, householdId, from, today, 'Day'),
        retry: 1,
    });
    const rows = query.data?.periods ?? [];
    const currencies = [...new Set(rows.filter(row => Number(row.expenseAmount) > 0 || Number(row.incomeAmount) > 0)
        .map(row => row.currency).concat(reportingCurrency))].sort();
    const currency = selectedCurrency && currencies.includes(selectedCurrency)
        ? selectedCurrency : currencies.includes(reportingCurrency) ? reportingCurrency : currencies[0];
    const hasActivity = rows.some(row => Number(row.expenseAmount) > 0 || Number(row.incomeAmount) > 0);
    const reportSearch = { from, to: today, currency, kind };
    const spending = categorySpending(rows, currency ?? reportingCurrency, kind);
    const money = (amount: string) => formatDecimalAmount(amount, currency ?? reportingCurrency, language);

    return (
        <AverioCard role="region" aria-labelledby="category-spending-title" className="col-span-12 space-y-4 xl:col-span-4">
            <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                    <h3 id="category-spending-title" className="text-base font-semibold">{copy('title')}</h3>
                    <p className="text-xs text-slate-600 dark:text-slate-300">{t('categorySpending.period')}</p>
                </div>
                <Link to="/app/statistics" search={reportSearch} className={linkStyle}>{t('categorySpending.report')}</Link>
            </div>
            {query.isPending ? <p role="status">{t('common.status.loading')}</p>
                : query.isError ? <div className="space-y-3">
                    <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('categorySpending.error')}</p>
                    <AverioButton disabled={query.isFetching} onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton>
                </div> : <>
                    <div className="grid grid-cols-2 gap-3">
                        <div className="space-y-1 text-xs font-medium">
                            <label htmlFor={`${controlId}-currency`}>{t('categorySpending.currency')}</label>
                            <AverioSelect id={`${controlId}-currency`} value={currency} onChange={event => setSelectedCurrency(event.target.value)}>
                                {currencies.map(value => <option key={value} value={value}>{value}</option>)}
                            </AverioSelect>
                        </div>
                        <div className="space-y-1 text-xs font-medium">
                            <label htmlFor={`${controlId}-kind`}>{t('categorySpending.type')}</label>
                            <AverioSelect id={`${controlId}-kind`} value={kind} onChange={event => setKind(event.target.value as 'Expense' | 'Income')}>
                                <option value="Expense">{t('categorySpending.expense')}</option>
                                <option value="Income">{t('categorySpending.income')}</option>
                            </AverioSelect>
                        </div>
                    </div>
                    {spending.segments.length === 0 ? <div data-testid="category-empty" className="space-y-2 py-4 text-center">
                    <svg viewBox="0 0 100 80" className="mx-auto h-20 w-24 text-emerald-500" aria-hidden="true">
                        <circle cx="50" cy="40" r="28" fill="none" stroke="currentColor" strokeWidth="12" className="opacity-20" />
                        <path d="M50 12a28 28 0 0 1 28 28" fill="none" stroke="currentColor" strokeWidth="12" />
                        <path d="M44 40h12m-6-6v12" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
                    </svg>
                    <h4 className="font-semibold">{copy(hasActivity ? 'noMatches' : 'emptyTitle')}</h4>
                    <p className="text-sm text-slate-600 dark:text-slate-300">{copy(hasActivity ? 'noMatchesHelp' : 'emptyHelp')}</p>
                    <Link to="/app/transactions" search={{ create: true, createKind: kind }} className={linkStyle}>{copy('add')}</Link>
                </div> : <>
                    <div className="flex flex-wrap items-center justify-center gap-4">
                        <svg viewBox="0 0 120 120" className="size-28 shrink-0" aria-hidden="true">
                            {spending.segments.map((segment, index) => <circle key={segment.id}
                                cx="60" cy="60" r="44" pathLength="100" fill="none" stroke="currentColor" strokeWidth="18"
                                strokeDasharray={`${segment.percentage} ${100 - segment.percentage}`}
                                strokeDashoffset={-segment.offset} transform="rotate(-90 60 60)" className={tones[index]} />)}
                        </svg>
                        <dl className="min-w-[12rem] flex-1 space-y-3">
                            {spending.segments.map((segment, index) => <div key={segment.id} data-category={segment.id} className={`grid grid-cols-[minmax(0,1fr)_auto] items-start gap-2 text-xs ${segment.id === 'other' ? 'border-t border-slate-200 pt-3 dark:border-slate-700' : 'font-semibold'}`}>
                                <dt className="flex min-w-0 items-start gap-2">
                                    <svg viewBox="0 0 8 8" className={`mt-1 size-2 shrink-0 ${tones[index]}`} aria-hidden="true"><circle cx="4" cy="4" r="4" fill="currentColor" /></svg>
                                    <span className="min-w-0 break-words">
                                        <Link to="/app/statistics" search={{ ...reportSearch, category: segment.id === 'other' ? undefined : segment.id }}
                                            className="inline-flex min-h-8 items-center rounded text-emerald-700 underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-primary-500 dark:text-emerald-400">
                                            {segment.id === 'other' ? t('categorySpending.other') : getCategoryDisplayName(segment, t)}
                                        </Link>
                                        <span className="block text-xs text-slate-600 dark:text-slate-300">{new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 1 }).format(segment.percentage / 100)}</span>
                                    </span>
                                </dt>
                                <dd className="max-w-[10rem] break-all text-right font-semibold tabular-nums">{money(segment.amount)}</dd>
                            </div>)}
                        </dl>
                    </div>
                    <div className="flex flex-wrap justify-between gap-2 border-t border-slate-200 pt-3 dark:border-slate-700">
                        <span className="text-sm">{copy('total')}</span>
                        <strong className="break-all tabular-nums" data-testid="category-spending-total">{money(spending.total)}</strong>
                    </div>
                </>}
                </>}
            <CategoryReportHelp kind={kind} />
        </AverioCard>
    );
}
