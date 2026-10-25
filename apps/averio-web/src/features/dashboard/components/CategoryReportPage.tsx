import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount, sumDecimalAmounts } from '../../../shared/money/decimalAmount';
import { transactionQueries } from '../../transactions/api/transactionQueries';
import { getCategoryDisplayName } from '../../transactions/categoryLocalization';
import { categorySpending } from '../categorySpending';
import { categoryReportSearch, type CategoryReportSearch } from '../categoryReportSearch';
import { CategoryReportHelp } from './CategoryReportHelp';

type Report = Required<Omit<CategoryReportSearch, 'category'>> & Pick<CategoryReportSearch, 'category'>;
const linkStyle = 'inline-flex min-h-11 items-center rounded text-sm font-semibold text-emerald-700 underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-primary-500 dark:text-emerald-400';

export function CategoryReportPage({ userId, householdId, report }: {
    userId: string; householdId: string; report: Report;
}) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const navigate = useNavigate({ from: '/app/statistics' });
    const [draft, setDraft] = useState(report);
    const [invalid, setInvalid] = useState(false);
    const query = useQuery({ ...transactionQueries.categoryCashFlow(userId, householdId, report.from, report.to, 'Day'), retry: 1 });
    const rows = query.data?.periods ?? [];
    const selectedRows = report.category ? rows.filter(row => row.categoryId === report.category) : rows;
    const totals = categorySpending(selectedRows, report.currency, report.kind, Infinity);
    const currencies = [...new Set([report.currency, ...rows.map(row => row.currency)])].sort();
    const amountKey = report.kind === 'Income' ? 'incomeAmount' : 'expenseAmount';
    const daily = new Map<string, string>();
    for (const row of selectedRows) {
        if (row.currency === report.currency && Number(row[amountKey]) > 0) {
            daily.set(row.periodStart, sumDecimalAmounts([daily.get(row.periodStart) ?? '0', row[amountKey]]));
        }
    }
    const money = (amount: string) => formatDecimalAmount(amount, report.currency, language);
    const date = (value: string) => new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'));
    const title = t(report.kind === 'Income' ? 'categorySpending.incomeTitle' : 'categorySpending.title');
    const selectedName = totals.segments[0] && getCategoryDisplayName(totals.segments[0], t);

    return <section aria-labelledby="category-report-title" className="space-y-4">
        <header className="flex flex-wrap items-center justify-between gap-3">
            <div>
                <h2 id="category-report-title" className="text-xl font-semibold">{title} <CategoryReportHelp kind={report.kind} /></h2>
                <p className="text-sm text-slate-600 dark:text-slate-300">{date(report.from)} – {date(report.to)} · {report.currency}</p>
            </div>
            <Link to="/app/statistics" search={{}} className={linkStyle}>{t('categorySpending.allStatistics')}</Link>
        </header>
        <AverioCard>
            <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5" onSubmit={event => {
                event.preventDefault();
                const next = categoryReportSearch(draft);
                if (!next.from) { setInvalid(true); return; }
                setInvalid(false);
                void navigate({ search: next });
            }}>
                <AverioField>{t('categorySpending.from')}<AverioInput type="date" required value={draft.from}
                    onChange={event => setDraft({ ...draft, from: event.target.value })} /></AverioField>
                <AverioField>{t('categorySpending.to')}<AverioInput type="date" required value={draft.to}
                    onChange={event => setDraft({ ...draft, to: event.target.value })} /></AverioField>
                <AverioField>{t('categorySpending.currency')}<AverioSelect value={draft.currency}
                    onChange={event => setDraft({ ...draft, currency: event.target.value })}>
                    {currencies.map(currency => <option key={currency}>{currency}</option>)}
                </AverioSelect></AverioField>
                <AverioField>{t('categorySpending.type')}<AverioSelect value={draft.kind}
                    onChange={event => setDraft({ ...draft, kind: event.target.value as Report['kind'], category: undefined })}>
                    <option value="Expense">{t('categorySpending.expense')}</option>
                    <option value="Income">{t('categorySpending.income')}</option>
                </AverioSelect></AverioField>
                <AverioButton type="submit" variant="primary" className="self-end">{t('categorySpending.apply')}</AverioButton>
                {invalid && <p role="alert" className="text-sm text-red-700 dark:text-red-300 sm:col-span-2 lg:col-span-5">{t('categorySpending.invalidRange')}</p>}
            </form>
        </AverioCard>
        <AverioCard className="space-y-4">
            {report.category && <Link to="/app/statistics" search={{ ...report, category: undefined }} className={linkStyle}>{t('categorySpending.allCategories')}</Link>}
            {query.isPending ? <p role="status">{t('common.status.loading')}</p> : query.isError ? <div className="space-y-3">
                <p role="alert">{t('categorySpending.error')}</p>
                <AverioButton disabled={query.isFetching} onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton>
            </div> : totals.segments.length === 0 ? <div className="space-y-2 py-4 text-center" data-testid="category-report-empty">
                <svg aria-hidden="true" viewBox="0 0 80 80" className="mx-auto size-20 text-emerald-500"><circle cx="40" cy="40" r="26" fill="none" stroke="currentColor" strokeWidth="10" opacity="0.25" /><path d="M32 40h16" stroke="currentColor" strokeWidth="3" /></svg>
                <h3 className="font-semibold">{t('categorySpending.reportEmpty')}</h3>
                <p className="text-sm text-slate-600 dark:text-slate-300">{t('categorySpending.reportEmptyHelp')}</p>
                <Link to="/app/transactions" search={{ create: true, createKind: report.kind }} className={linkStyle}>{t(report.kind === 'Income' ? 'categorySpending.incomeAdd' : 'categorySpending.add')}</Link>
            </div> : <>
                {report.category ? <>
                    <h3 className="font-semibold">{selectedName}</h3>
                    <p className="text-sm text-slate-600 dark:text-slate-300">{t('categorySpending.daily')}</p>
                    <dl className="divide-y divide-slate-200 dark:divide-slate-700" data-testid="category-daily">
                        {[...daily].sort(([a], [b]) => b.localeCompare(a)).map(([day, amount]) => <div key={day} className="flex flex-wrap justify-between gap-2 py-3">
                            <dt>{date(day)}</dt><dd className="break-all font-semibold tabular-nums">{money(amount)}</dd>
                        </div>)}
                    </dl>
                </> : <dl className="divide-y divide-slate-200 dark:divide-slate-700" data-testid="category-report-list">
                    {totals.segments.map(segment => <div key={segment.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                        <dt className="min-w-0 break-words"><Link to="/app/statistics" search={{ ...report, category: segment.id }} className={linkStyle}>{getCategoryDisplayName(segment, t)}</Link></dt>
                        <dd className="break-all font-semibold tabular-nums">{money(segment.amount)}</dd>
                    </div>)}
                </dl>}
                <div className="flex flex-wrap justify-between gap-2 border-t border-slate-200 pt-3 dark:border-slate-700">
                    <span>{t(report.kind === 'Income' ? 'categorySpending.incomeTotal' : 'categorySpending.total')}</span>
                    <strong className="break-all tabular-nums" data-testid="category-report-total">{money(totals.total)}</strong>
                </div>
            </>}
        </AverioCard>
    </section>;
}
