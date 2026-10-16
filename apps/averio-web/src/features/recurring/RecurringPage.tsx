import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { RecurringRuleResponse, SaveRecurringRuleRequest } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { AverioLogo } from '../../shared/components/branding/AverioLogo';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { recurringApi, recurringQueries } from './recurringQueries';
import { RecurringEditor } from './RecurringEditor';

export function RecurringPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const list = useQuery(recurringQueries.list(userId, householdId));
    const accounts = useQuery(accountsQueries.list(userId, householdId, true));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const [editor, setEditor] = useState<RecurringRuleResponse | 'new' | null>(null);
    const [review, setReview] = useState<{ rule: RecurringRuleResponse; skip: boolean } | null>(null);
    const [history, setHistory] = useState<RecurringRuleResponse | null>(null);
    const [status, setStatus] = useState('all');
    const [notice, setNotice] = useState('');
    const writable = (accounts.data ?? []).filter(a => !a.cutoverAtUtc && (a.scope === 'Household' || a.owners.some(o => o.householdMemberId === owners.data?.currentMemberId)));
    const canCreate = accounts.isSuccess && owners.isSuccess && writable.some(a => a.status === 'Active');
    const refresh = () => client.invalidateQueries({ predicate: q => q.queryKey.includes(userId) && q.queryKey.includes(householdId) });
    const mutation = useMutation({ mutationFn: async ({ rule, request }: { rule: RecurringRuleResponse | null; request: SaveRecurringRuleRequest }) => {
        if (rule) await recurringApi.apiV1HouseholdsHouseholdIdRecurringRuleIdPut({ householdId, ruleId: rule.id, saveRecurringRuleRequest: request });
        else await recurringApi.apiV1HouseholdsHouseholdIdRecurringPost({ householdId, saveRecurringRuleRequest: request });
    }, onSuccess: refresh });
    const today = new Date().toISOString().slice(0, 10);
    const rows = (list.data ?? []).filter(r => status === 'all' || (status === 'active' ? !r.isPaused && !r.isStopped && !!r.nextDueOn : r.isPaused));
    const date = (value: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'));
    return <section className="grid min-w-0 gap-5">
        <div className="flex flex-wrap items-start justify-between gap-4"><div><h2 className="text-2xl font-bold">{t('recurring.title')}</h2><p className="mt-2 max-w-2xl text-sm text-slate-500 dark:text-slate-400">{t('recurring.intro')}</p></div>
            <AverioButton variant="primary" disabled={!canCreate} onClick={() => setEditor('new')}>{t('recurring.add')}</AverioButton></div>
        {notice && <p role="status">{notice}</p>}
        <div className="flex flex-wrap items-end gap-3"><AverioField>{t('recurring.status')}<AverioSelect value={status} onChange={e => setStatus(e.target.value)}>
            <option value="all">{t('recurring.all')}</option><option value="active">{t('recurring.active')}</option><option value="paused">{t('recurring.paused')}</option></AverioSelect></AverioField>
            <AverioButton disabled={list.isFetching} onClick={() => { void refresh(); }}>{t('recurring.refresh')}</AverioButton></div>
        {(list.isPending || accounts.isPending || owners.isPending) && <p role="status">{t('recurring.loading')}</p>}
        {(list.isError || accounts.isError || owners.isError) && <div role="alert">{t('recurring.error')} <AverioButton onClick={() => { void refresh(); }}>{t('recurring.retry')}</AverioButton></div>}
        {list.isSuccess && accounts.isSuccess && owners.isSuccess && rows.length === 0 && <AverioCard className="py-10 text-center">
            <div aria-hidden="true" className="mx-auto mb-6 flex h-28 w-40 items-center justify-center gap-3 rounded-2xl border-2 border-emerald-500/40 bg-emerald-500/10"><span className="text-4xl text-emerald-500">↻</span><AverioLogo className="h-16 w-12" /></div>
            <h3 className="text-xl font-bold">{t(status !== 'all' ? 'recurring.noMatches' : 'recurring.emptyTitle')}</h3>
            <p className="mx-auto my-4 max-w-md">{t(canCreate ? 'recurring.emptyHelp' : 'recurring.noAccounts')}</p>
            {status !== 'all' ? <AverioButton onClick={() => setStatus('all')}>{t('recurring.reset')}</AverioButton> : canCreate ? <AverioButton variant="primary" onClick={() => setEditor('new')}>{t('recurring.add')}</AverioButton> :
                <Link to="/app/accounts/new" className="font-semibold text-emerald-700 underline dark:text-emerald-300">{t('recurring.createAccount')}</Link>}
        </AverioCard>}
        {rows.map(rule => { const due = !!rule.nextDueOn && rule.nextDueOn <= today && !rule.isPaused && !rule.isStopped;
            return <AverioCard key={rule.id}><div className="flex flex-wrap items-start justify-between gap-4">
                <div className="min-w-0"><h3 className="break-words text-lg font-bold">{rule.name}</h3><p className="mt-1 text-sm">{accounts.data?.find(a => a.id === rule.accountId)?.name} · {t('recurring.' + rule.frequency)} · {t('recurring.' + rule.visibility)}</p>
                    <span className="mt-2 inline-block rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-semibold">{t('recurring.' + (rule.isStopped ? 'stopped' : rule.isPaused ? 'paused' : rule.isBlocked ? 'blocked' : !rule.nextDueOn ? 'completed' : rule.automatic ? 'auto' : 'manual'))}</span></div>
                <div className="min-w-0 text-right"><p className={rule.kind === 'Income' ? 'break-words text-xl font-bold text-emerald-700 dark:text-emerald-400' : 'break-words text-xl font-bold text-red-700 dark:text-red-400'}>{rule.kind === 'Income' ? '+' : '−'}{formatDecimalAmount(rule.amount, rule.currency, i18n.language)}</p>
                    {rule.nextDueOn && <p className="mt-1 text-sm">{t(due ? 'recurring.due' : 'recurring.next')} · {date(rule.nextDueOn)}</p>}</div>
            </div>{rule.isBlocked && <p role="status" className="mt-3 text-sm">{t('recurring.blockedHelp')}</p>}{!rule.canRecord && <p className="mt-3 text-sm">{t('recurring.unavailable')}</p>}
                <div className="mt-4 flex flex-wrap gap-2 border-t border-slate-200 pt-4 dark:border-slate-700">
                    {due && <><AverioButton variant="primary" disabled={!rule.canRecord} onClick={() => setReview({ rule, skip: false })}>{t('recurring.review')}</AverioButton>
                        <AverioButton disabled={!rule.canRecord} onClick={() => setReview({ rule, skip: true })}>{t('recurring.skip')}</AverioButton></>}
                    {!rule.isStopped && <AverioButton onClick={() => setEditor(rule)}>{t('recurring.edit')}</AverioButton>}
                    <AverioButton onClick={() => setHistory(rule)}>{t('recurring.history')}</AverioButton>
                </div></AverioCard>; })}
        {editor && <RecurringEditor userId={userId} householdId={householdId} rule={editor === 'new' ? null : editor}
            onClose={() => setEditor(null)}
            onSave={async request => { await mutation.mutateAsync({ rule: editor === 'new' ? null : editor, request }); setEditor(null); setNotice(t('recurring.saved')); }} />}
        {review && <OccurrenceDialog householdId={householdId} rule={review.rule} skip={review.skip} onClose={() => setReview(null)}
            onDone={async () => { await refresh(); setNotice(t(review.skip ? 'recurring.skipped' : 'recurring.recorded')); setReview(null); }} />}
        {history && <HistoryDialog userId={userId} householdId={householdId} rule={history} onClose={() => setHistory(null)} />}
    </section>;
}

function OccurrenceDialog({ householdId, rule, skip, onClose, onDone }: { householdId: string; rule: RecurringRuleResponse; skip: boolean; onClose: () => void; onDone: () => Promise<void> }) {
    const { t } = useTranslation();
    const [amounts, setAmounts] = useState(rule.items.map(i => i.amount));
    const mutation = useMutation({ mutationFn: () => recurringApi.apiV1HouseholdsHouseholdIdRecurringRuleIdOccurrencesPost({ householdId, ruleId: rule.id,
        recurringOccurrenceRequest: { expectedVersion: rule.version, dueOn: rule.nextDueOn!, skip, itemAmounts: skip ? null : amounts } }), onSuccess: onDone });
    return <AverioDialog open title={t(skip ? 'recurring.skip' : 'recurring.confirmTitle')} closeLabel={t('recurring.cancel')} onDismiss={onClose} busy={mutation.isPending}
        actions={<><AverioButton disabled={mutation.isPending} onClick={onClose}>{t('recurring.cancel')}</AverioButton><AverioButton type="submit" form="recurring-occurrence" variant={skip ? 'danger' : 'primary'} disabled={mutation.isPending}>{t(skip ? 'recurring.skip' : 'recurring.record')}</AverioButton></>}>
        <form id="recurring-occurrence" onSubmit={e => { e.preventDefault(); mutation.mutate(); }} className="grid gap-4">
            <AverioCard><h3 className="font-bold">{rule.name} · {rule.nextDueOn}</h3><p className="mt-2">{t(skip ? 'recurring.skipHelp' : 'recurring.confirmHelp')}</p>
                {!skip && <div className="mt-4 grid gap-3"><p>{t('recurring.itemAmounts')}</p>{rule.items.map((item, index) => <AverioField key={index}>{t('recurring.amount')} · {item.description} · {rule.currency}
                    <AverioInput required inputMode="decimal" pattern="[0-9]+([.,][0-9]{1,4})?" value={amounts[index]} disabled={mutation.isPending}
                        onChange={e => setAmounts(current => current.map((amount, i) => i === index ? e.target.value.replace(',', '.') : amount))} />
                </AverioField>)}</div>}</AverioCard>
            {mutation.isError && <p role="alert">{mutation.error instanceof ApiError ? mutation.error.message : t('errors.genericDescription')} {t('recurring.updatedElsewhere')}</p>}
        </form>
    </AverioDialog>;
}
function HistoryDialog({ userId, householdId, rule, onClose }: { userId: string; householdId: string; rule: RecurringRuleResponse; onClose: () => void }) {
    const { t } = useTranslation();
    const query = useQuery(recurringQueries.history(userId, householdId, rule.id));
    return <AverioDialog open title={t('recurring.history')} closeLabel={t('recurring.cancel')} onDismiss={onClose} actions={<AverioButton onClick={onClose}>{t('recurring.cancel')}</AverioButton>}>
        {query.isPending && <p role="status">{t('recurring.loading')}</p>}
        {query.isError && <p role="alert">{t('recurring.error')} <AverioButton onClick={() => { void query.refetch(); }}>{t('recurring.retry')}</AverioButton></p>}
        {query.isSuccess && !query.data.length && <p>{t('recurring.historyEmpty')}</p>}
        <ul className="grid gap-3">{query.data?.map(x => <li key={x.dueOn} className="flex flex-wrap justify-between gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-700"><span>{x.dueOn}</span>
            {x.transactionId ? <Link className="font-semibold text-emerald-700 underline dark:text-emerald-300" to="/app/transactions" search={{ transaction: x.transactionId }}>{t('recurring.viewTransaction')}</Link> : <span>{t('recurring.historySkipped')}</span>}</li>)}</ul>
    </AverioDialog>;
}
