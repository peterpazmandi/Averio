import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ImportHistoryItem } from '../../shared/api/generated';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioLogo } from '../../shared/components/branding/AverioLogo';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { ImportUndo, ImportReconciliation } from './ImportTools';
import { importQueries } from './importQueries';

const linkStyle = 'font-semibold text-primary-700 underline-offset-4 hover:underline dark:text-primary-300';
export function ImportHistoryPage({ userId, householdId, batchId, onSelect }: {
    userId: string; householdId: string; batchId?: string; onSelect: (id?: string) => void;
}) {
    const { t, i18n } = useTranslation();
    const [page, setPage] = useState(0);
    const [review, setReview] = useState(false);
    const history = useQuery({ ...importQueries.history(userId, householdId, page), enabled: !batchId });
    const detail = useQuery(importQueries.records(userId, householdId, batchId ?? '', page, review));
    const query = batchId ? detail : history;
    const locale = i18n.resolvedLanguage ?? 'en';
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, locale);
    const date = (value: string | Date) => new Date(value).toLocaleString(locale);
    const summary = (batch: ImportHistoryItem) => <div className="grid min-w-0 gap-3">
        <h3 className="break-words text-lg font-bold">{batch.sourceName || t('importHistory.unnamed')}</h3>
        <p className="text-sm text-slate-600 dark:text-slate-300">{t('importHistory.completed', { date: date(batch.completedAt) })}</p>
        <p>{t('importHistory.count', { count: batch.summary.count })} · {t('importHistory.period', { from: batch.summary.from, to: batch.summary.to })}</p>
        <dl className="grid gap-3 sm:grid-cols-3">{batch.summary.totals.map(total => <div key={total.kind + total.currency}>
            <dt className="text-sm text-slate-600 dark:text-slate-300">{t('imports.' + total.kind)} · {total.count}</dt>
            <dd className="break-words text-lg font-semibold tabular-nums">{money(total.amount, total.currency)}</dd>
        </div>)}</dl>
        {!batch.trackingAvailable && <p className="text-sm text-amber-800 dark:text-amber-200">{t('importHistory.legacy')}</p>}
    </div>;
    return <section className="grid min-w-0 gap-5 text-[#152033] dark:text-[#F3F6FA]" aria-labelledby="history-title">
        <header className="flex flex-wrap items-center justify-between gap-3">
            <div><h2 id="history-title" className="text-2xl font-bold">{t('importHistory.title')}</h2>
                <p className="mt-2 max-w-3xl text-sm text-slate-600 dark:text-slate-300">{t('importHistory.intro')}</p></div>
            <Link to="/app/imports" className={linkStyle}>{t('importHistory.newImport')}</Link>
        </header>
        {batchId && <div><AverioButton onClick={() => onSelect()}>{t('importHistory.back')}</AverioButton></div>}
        {query.isPending ? <p role="status">{t('importHistory.loading')}</p>
            : query.isError ? <AverioCard><p role="alert">{t('importHistory.error')}</p>
                <AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></AverioCard>
            : batchId && detail.data ? <>
                <AverioCard>{summary(detail.data.batch)}
                    <footer className="mt-4 flex flex-wrap gap-3 border-t border-slate-200 pt-4 dark:border-slate-700">
                        <AverioButton onClick={() => { setReview(!review); setPage(0); }}>
                            {t(review ? 'importHistory.records' : 'importHistory.preview')}</AverioButton>
                        <AverioButton disabled={detail.isFetching} onClick={() => { void detail.refetch(); }}>{t('importHistory.refresh')}</AverioButton>
                    </footer>
                </AverioCard>
                {review && <ImportUndo userId={userId} householdId={householdId} batchId={batchId} />}
                {!review && <ImportReconciliation userId={userId} householdId={householdId} batchId={batchId} />}
                {review && <AverioCard><h3 className="text-xl font-bold">{t('importHistory.undo')}</h3>
                    <p className="my-3 text-sm">{t('importTools.recordStates')}</p>
                    <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                        {(['unchanged', 'edited', 'voided', 'unavailable', 'untracked', 'readOnly'] as const).map(state =>
                            <div key={state}><dt className="text-sm">{t('importHistory.' + (state === 'readOnly' ? state : state[0].toUpperCase() + state.slice(1)))}</dt>
                                <dd className="text-2xl font-bold">{detail.data.review[state]}</dd></div>)}
                    </dl>
                    <p className="mt-4 text-xs text-slate-600 dark:text-slate-300">{t('importHistory.checked', { date: date(detail.data.checkedAt) })}</p>
                </AverioCard>}
                <h3 className="text-lg font-bold">{t('importHistory.original')}</h3>
                <p className="text-sm text-slate-600 dark:text-slate-300">{t('importHistory.privateHelp')}</p>
                <ol className="grid min-w-0 gap-3">{detail.data.records.map(record => {
                    const r = record.source;
                    const category = record.importedCategory
                        ? record.categoryIsBase ? t('systemCategories.' + record.importedCategory, { defaultValue: record.importedCategory }) : record.importedCategory
                        : r.category;
                    return <li key={r.sheet + ':' + r.rowNumber}><AverioCard className="min-w-0 break-words">
                        <div className="flex flex-wrap justify-between gap-3"><div>
                            <p className="font-semibold">{r.date} · {t('imports.' + r.kind)}</p>
                            <p>{r.account}{r.destination ? ' → ' + r.destination : ''}</p></div>
                            <div className="font-semibold tabular-nums">{money(r.amount, r.currency)}
                                {r.receivedAmount && <p className="text-sm">{t('importHistory.received', { amount: money(r.receivedAmount, r.receivedCurrency ?? r.currency) })}</p>}</div>
                        </div>
                        <p className="mt-2 text-xs text-slate-600 dark:text-slate-300">{t('importHistory.source', { sheet: r.sheet, row: r.rowNumber })}</p>
                        {record.importedAccount && <p className="mt-2 text-sm">{t('importHistory.destination', { account: record.importedAccount + (record.importedDestination ? ' → ' + record.importedDestination : '') })}</p>}
                        {category && <p className="text-sm">{t('importHistory.category', { name: category })}</p>}
                        {r.tags && <p className="text-sm">{t('importHistory.tags', { tags: r.tags })}</p>}
                        {r.note && <p className="whitespace-pre-wrap text-sm">{t('importHistory.note', { note: r.note })}</p>}
                        <footer className="mt-3 flex flex-wrap items-center gap-3 border-t border-slate-200 pt-3 text-sm dark:border-slate-700">
                            <span className={record.state === 'Edited' ? 'font-bold text-amber-800 dark:text-amber-200' : 'font-semibold'}>{t(record.state === 'Skipped' ? 'importTools.Skipped' : 'importHistory.' + record.state)}</span>
                            {record.recordId && !record.canModify && <span>{t('importHistory.readOnly')}</span>}
                            {record.recordId && (r.kind === 'Transfer'
                                ? <Link to="/app/transfers" className={linkStyle}>{t('importHistory.transfers')}</Link>
                                : <Link to="/app/transactions" search={{ transaction: record.recordId }} className={linkStyle}>{t('importHistory.open')}</Link>)}
                        </footer>
                    </AverioCard></li>;
                })}</ol>
            </> : history.data?.items.length ? <div className="grid gap-4">{history.data.items.map(batch => <AverioCard key={batch.id}>
                {summary(batch)}<footer className="mt-4 border-t border-slate-200 pt-4 dark:border-slate-700">
                    <AverioButton onClick={() => onSelect(batch.id)}>{t('importHistory.records')}</AverioButton>
                </footer></AverioCard>)}</div> : <AverioCard className="grid justify-items-center gap-4 py-10 text-center">
                <div aria-hidden="true" className="rounded-full bg-primary-500/10 p-6"><AverioLogo className="h-20 w-auto" /></div>
                <h3 className="text-xl font-bold">{t('importHistory.empty')}</h3><p>{t('importHistory.emptyHelp')}</p>
                <Link to="/app/imports" className={linkStyle}>{t('importHistory.newImport')}</Link>
            </AverioCard>}
        {query.isSuccess && (page > 0 || query.data?.hasMore) && <footer className="flex flex-wrap items-center justify-between gap-3">
            <AverioButton disabled={page === 0 || query.isFetching} onClick={() => setPage(p => p - 1)}>{t('importHistory.previous')}</AverioButton>
            <span>{t('importHistory.page', { page: page + 1 })}</span>
            <AverioButton disabled={!query.data?.hasMore || query.isFetching} onClick={() => setPage(p => p + 1)}>{t('importHistory.next')}</AverioButton>
        </footer>}
    </section>;
}
