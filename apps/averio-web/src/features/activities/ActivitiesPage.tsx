import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ActivityResponse } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { activityQueries } from './activityQueries';
import { ActivityEditor } from './ActivityEditor';
import { ActivityIllustration } from './ActivityIllustration';

export function ActivitiesPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const query = useQuery(activityQueries.list(userId, householdId));
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('all');
    const [editor, setEditor] = useState<ActivityResponse | 'new' | null>(null);
    const filtered = search.trim() !== '' || status !== 'all';
    const rows = (query.data ?? []).filter(x => (status === 'all' || x.isClosed === (status === 'closed'))
        && x.name.toLocaleLowerCase(i18n.language).includes(search.trim().toLocaleLowerCase(i18n.language)))
        .sort((a, b) => Number(a.isClosed) - Number(b.isClosed) || a.name.localeCompare(b.name, i18n.language));
    const date = (value: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'));
    return <section className="grid min-w-0 gap-5 text-[#152033] dark:text-[#F3F6FA]" aria-labelledby="activities-title">
        <header className="flex flex-wrap items-start justify-between gap-4">
            <div><h2 id="activities-title" className="text-2xl font-bold">{t('activities.title')}</h2>
                <p className="mt-1 text-sm text-[#667085] dark:text-[#AAB4C4]">{t('activities.description')}</p></div>
            <AverioButton variant="primary" onClick={() => setEditor('new')}>{t('activities.create')}</AverioButton>
        </header>
        <div className="grid gap-3 sm:grid-cols-[1fr_180px]">
            <AverioField>{t('activities.search')}<AverioInput type="search" value={search} onChange={e => setSearch(e.target.value)} /></AverioField>
            <AverioField>{t('activities.status')}<AverioSelect value={status} onChange={e => setStatus(e.target.value)}>
                <option value="all">{t('activities.all')}</option><option value="open">{t('activities.open')}</option><option value="closed">{t('activities.closed')}</option>
            </AverioSelect></AverioField>
        </div>
        <p className="text-xs text-[#667085] dark:text-[#AAB4C4]">{t('activities.totalsHelp')}</p>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <div role="alert"><p>{query.error.message}</p><AverioButton onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
        {query.isSuccess && rows.length === 0 && <AverioCard className="p-8 text-center">
            <ActivityIllustration /><h3 className="text-lg font-bold">{t(filtered ? 'activities.noMatches' : 'activities.emptyTitle')}</h3>
            <p className="mx-auto my-3 max-w-md text-sm">{t(filtered ? 'activities.noMatchesHelp' : 'activities.emptyHelp')}</p>
            <AverioButton variant="primary" onClick={() => { if (filtered) { setSearch(''); setStatus('all'); } else setEditor('new'); }}>
                {t(filtered ? 'transactionManagement.reset' : 'activities.create')}</AverioButton>
        </AverioCard>}
        <div className="grid gap-4 lg:grid-cols-2">
            {query.isSuccess && rows.map(activity => <AverioCard key={activity.id} className="min-w-0 p-5">
                <div className="flex items-start justify-between gap-3"><h3 className="text-lg font-bold wrap-anywhere">{activity.name}</h3>
                    <span className="shrink-0 rounded-full border border-emerald-300 px-2 py-1 text-xs dark:border-emerald-800">{t(activity.isClosed ? 'activities.closed' : 'activities.open')}</span></div>
                {(activity.startsOn || activity.endsOn) && <p className="mt-2 text-xs">{[activity.startsOn, activity.endsOn].filter((x): x is string => Boolean(x)).map(date).join(' – ')}</p>}
                {activity.totals.length === 0 && <p className="my-5 text-sm">{t('activities.noTotals')}</p>}
                {activity.totals.map(total => <div key={total.currency} className="my-4 grid gap-3 border-t border-emerald-200 pt-4 dark:border-emerald-900">
                    <div className="flex flex-wrap items-baseline justify-between gap-2"><span className="text-sm font-semibold">{t('activities.net')} · {total.currency}</span>
                        <strong className={`text-xl tabular-nums ${total.net.startsWith('-') ? 'text-red-700 dark:text-red-300' : 'text-emerald-700 dark:text-emerald-300'}`}>
                            {formatDecimalAmount(total.net, total.currency, i18n.language)}</strong></div>
                    <dl className="grid grid-cols-2 gap-3 text-sm">
                        <div><dt>{t('activities.income')}</dt><dd className="mt-1 font-semibold tabular-nums">{formatDecimalAmount(total.income, total.currency, i18n.language)}</dd></div>
                        <div><dt>{t('activities.expenses')}</dt><dd className="mt-1 font-semibold tabular-nums">{formatDecimalAmount(total.expenses, total.currency, i18n.language)}</dd></div>
                    </dl>
                    <p className="text-xs">{t('activities.transactionCount', { count: total.transactionCount })}</p>
                </div>)}
                <footer className="mt-4 flex flex-wrap gap-3 border-t border-emerald-200 pt-4 dark:border-emerald-900">
                    <Link to="/app/transactions" search={{ create: undefined, activity: activity.id }}
                        className="inline-flex min-h-11 items-center rounded-xl bg-emerald-600 px-3 text-sm font-semibold text-white hover:bg-emerald-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500">
                        {t('activities.viewTransactions')}</Link>
                    <AverioButton onClick={() => setEditor(activity)}>{t('common.actions.edit')}</AverioButton>
                </footer>
            </AverioCard>)}
        </div>
        {editor && <ActivityEditor userId={userId} householdId={householdId} activity={editor === 'new' ? undefined : editor} onClose={() => setEditor(null)} />}
    </section>;
}
