import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { HouseholdResponse, RecurringAllowanceResponse } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { personalMoneyApi, personalMoneyKeys, recurringAllowanceKeys } from './personalMoneyQueries';

const amountPattern = /^[0-9]+([.,][0-9]{1,4})?$/;
const normalized = (v: string) => v.trim().replace(',', '.');

// Preserve a machine-readable API reason without leaking server details to users.
// Only a dedicated migration error is shown as schema-related; all other failures
// retain a generic actionable message, rather than pretending we know their cause.
async function recurringErrorCode(failure: unknown): Promise<string | null> {
    // apiFetchResponse converts RFC 7807 payloads into the Averio-specific
    // ApiError (including its code), before the generated client sees them.
    if (failure instanceof ApiError) return failure.code ?? null;
    const response = (failure as { response?: Response } | null)?.response;
    if (!response) return null;
    try {
        const problem = await response.clone().json() as { code?: unknown };
        return typeof problem.code === 'string' ? problem.code : null;
    } catch { return null; }
}
const migrationNotReady = 'recurring_allowance_database_update_required';
// The API posts due occurrences on UTC calendar dates, not the browser's time zone.
const utcToday = () => new Date().toISOString().slice(0, 10);
const nextMonth = () => {
    const d = new Date();
    return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1))
        .toISOString().slice(0, 10);
};

function useSchedules(userId: string, householdId: string) {
    return useQuery({
        queryKey: recurringAllowanceKeys.list(userId, householdId),
        retry: false,
        queryFn: async ({ signal }) => {
            try {
                return await personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneySchedulesGet(
                    { householdId }, { signal });
            } catch (failure) {
                throw new Error(await recurringErrorCode(failure) ?? 'recurring_allowance_load_failed', { cause: failure });
            }
        },
    });
}

function ScheduleRow({ schedule, userId, householdId, onChanged }: {
    schedule: RecurringAllowanceResponse; userId: string; householdId: string;
    onChanged: () => Promise<void>;
}) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const [amount, setAmount] = useState(schedule.amount);
    const [endsOn, setEndsOn] = useState(schedule.endsOn ?? '');
    const [confirmStop, setConfirmStop] = useState(false);
    const [error, setError] = useState('');
    const [historyExpanded, setHistoryExpanded] = useState(false);
    const [manageOpen, setManageOpen] = useState(false);
    const history = useQuery({
        queryKey: recurringAllowanceKeys.history(userId, householdId, schedule.id),
        enabled: historyExpanded,
        queryFn: ({ signal }) => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneySchedulesScheduleIdHistoryGet(
            { householdId, scheduleId: schedule.id }, { signal }),
    });
    const mutate = useMutation({
        mutationFn: async (action: 'save' | 'toggle' | 'stop') =>
            personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneySchedulesScheduleIdPut({
                householdId, scheduleId: schedule.id,
                updateRecurringAllowanceRequest: {
                    amount: action === 'save' ? normalized(amount) : schedule.amount,
                    endsOn: action === 'save' ? endsOn || null : schedule.endsOn,
                    isPaused: action === 'toggle' ? !schedule.isPaused : schedule.isPaused,
                    isStopped: action === 'stop',
                    expectedVersion: schedule.version,
                },
            }),
        onSuccess: async () => {
            setError(''); setConfirmStop(false); setManageOpen(false);
            await client.invalidateQueries({ queryKey: recurringAllowanceKeys.history(userId, householdId, schedule.id) });
            await onChanged();
        },
        onError: async (failure: Error) => {
            setError(failure.message);
            await onChanged(); // Refresh stale concurrency tokens after a conflict.
        },
    });
    const money = new Intl.NumberFormat(i18n.resolvedLanguage ?? i18n.language,
        { style: 'currency', currency: schedule.currency }).format(Number(schedule.amount));
    const valid = amountPattern.test(amount) && Number(normalized(amount)) > 0
        && (!endsOn || endsOn >= schedule.startsOn)
        && (!endsOn || !schedule.nextDueOn || endsOn >= schedule.nextDueOn);
    const status = schedule.isStopped ? 'stopped' : schedule.isBlocked ? 'blocked'
        : schedule.isPaused ? 'paused' : schedule.nextDueOn ? 'active' : 'completed';
    const manageable = !schedule.isStopped && schedule.nextDueOn !== null;
    return <div className="min-w-0 space-y-2 py-3 first:pt-0 last:pb-0">
        <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="min-w-0 flex-1">
                <p className="break-words text-sm font-semibold">{money} <span className="font-normal text-slate-600 dark:text-slate-300">· {t('personalMoney.recurring.monthly')}</span></p>
                <p className="text-xs text-slate-600 dark:text-slate-300">
                    {t('personalMoney.recurring.nextDue')}: {schedule.nextDueOn ?? '—'}
                </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
                <span className="rounded-full bg-emerald-50 px-2 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
                    {t('personalMoney.recurring.status.' + status)}
                </span>
                {manageable && <AverioButton type="button" aria-expanded={manageOpen}
                    aria-controls={`recurring-edit-${schedule.id}`} onClick={() => setManageOpen(!manageOpen)}>
                    {t('personalMoney.recurring.' + (manageOpen ? 'hide' : 'manage'))}
                </AverioButton>}
            </div>
        </div>
        {manageable && manageOpen && <div id={`recurring-edit-${schedule.id}`} className="space-y-3 rounded-xl bg-slate-50 p-3 dark:bg-slate-900/60">
            <div className="grid gap-3 sm:grid-cols-2">
                <AverioField>{t('personalMoney.amount')}
                    <AverioInput inputMode="decimal" disabled={mutate.isPending} value={amount}
                        onChange={e => setAmount(e.target.value)} />
                </AverioField>
                <AverioField>{t('personalMoney.recurring.end')}
                    <AverioInput type="date" disabled={mutate.isPending} value={endsOn}
                        onChange={e => setEndsOn(e.target.value)} />
                </AverioField>
            </div>
            <div className="flex flex-wrap gap-2">
                <AverioButton type="button" disabled={!valid || mutate.isPending ||
                    (normalized(amount) === schedule.amount && endsOn === (schedule.endsOn ?? ''))}
                    onClick={() => mutate.mutate('save')}>{t('personalMoney.recurring.save')}</AverioButton>
                <AverioButton type="button" disabled={mutate.isPending}
                    onClick={() => mutate.mutate('toggle')}>
                    {t('personalMoney.recurring.' + (schedule.isPaused ? 'resume' : 'pause'))}
                </AverioButton>
                {!confirmStop ? <AverioButton type="button" disabled={mutate.isPending}
                    onClick={() => setConfirmStop(true)}>{t('personalMoney.recurring.stop')}</AverioButton>
                    : <AverioButton type="button" disabled={mutate.isPending} variant="danger"
                        onClick={() => mutate.mutate('stop')}>{t('personalMoney.recurring.confirmStop')}</AverioButton>}
                {confirmStop && <AverioButton type="button" onClick={() => setConfirmStop(false)}>
                    {t('common.actions.cancel')}</AverioButton>}
            </div>
        </div>}
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error}</p>}
        <details className="text-sm" onToggle={event => setHistoryExpanded(event.currentTarget.open)}>
            <summary className="w-fit cursor-pointer py-1 text-xs font-semibold text-slate-600 dark:text-slate-300">
                {t('personalMoney.recurring.history')}
            </summary>
            {history.isLoading && <p role="status">{t('common.status.loading')}</p>}
            {history.isError && <p role="alert">{t('personalMoney.recurring.historyError')}</p>}
            {history.data && (history.data.length === 0 ? <p className="mt-2 text-slate-600 dark:text-slate-300">
                {t('personalMoney.recurring.noHistory')}</p> : <ul className="mt-2 space-y-1">
                {history.data.map(e => <li key={e.dueOn} className="flex justify-between gap-2">
                    <span>{e.dueOn}</span>
                    <strong>{new Intl.NumberFormat(i18n.resolvedLanguage ?? i18n.language,
                        { style: 'currency', currency: schedule.currency }).format(Number(e.amount))}</strong>
                </li>)}
            </ul>)}
        </details>
    </div>;
}

export function RecurringAllowanceManager({ userId, householdId, household }: {
    userId: string; householdId: string; household: HouseholdResponse;
}) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const schedules = useSchedules(userId, householdId);
    const [recipient, setRecipient] = useState(household.members.find(x => x.isCurrentUser)?.id ?? '');
    const [amount, setAmount] = useState('');
    const [currency, setCurrency] = useState(household.preferredCurrency || 'EUR');
    const [startsOn, setStartsOn] = useState(nextMonth);
    const [endsOn, setEndsOn] = useState('');
    const [operationId, setOperationId] = useState(() => crypto.randomUUID());
    const [feedback, setFeedback] = useState('');
    const [error, setError] = useState('');
    const [showCreate, setShowCreate] = useState(false);
    const refresh = async () => {
        await client.invalidateQueries({ queryKey: recurringAllowanceKeys.list(userId, householdId) });
        await client.invalidateQueries({ queryKey: personalMoneyKeys.mine(userId, householdId) });
    };
    const create = useMutation({
        mutationFn: () => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneySchedulesPost({
            householdId, createRecurringAllowanceRequest: {
                recipientMemberId: recipient, currency, amount: normalized(amount),
                startsOn, endsOn: endsOn || null, operationId,
            },
        }),
        onSuccess: async () => {
            await refresh(); setAmount(''); setOperationId(crypto.randomUUID());
            setError(''); setFeedback(t('personalMoney.recurring.created')); setShowCreate(false);
        },
        onError: async (failure: Error) => {
            const code = await recurringErrorCode(failure);
            setError(t('personalMoney.recurring.' + (code === migrationNotReady ? 'schemaUnavailable' : 'createError')));
            setFeedback('');
        },
    });
    const valid = !!recipient && amountPattern.test(amount) && Number(normalized(amount)) > 0
        && startsOn >= utcToday() && (!endsOn || endsOn >= startsOn);
    const names = new Map(household.members.map(x => [x.id, x.displayName || t('householdSettings.unnamedMember')]));
    if (household.role !== 'owner') return null;
    return <AverioCard className="space-y-3 !p-4 sm:!p-5">
        <header className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0 flex-1 space-y-1">
                <h4 className="text-base font-semibold">{t('personalMoney.recurring.title')}</h4>
                <p className="max-w-2xl text-xs leading-relaxed text-slate-600 dark:text-slate-300">
                    {t('personalMoney.recurring.description')}
                </p>
            </div>
            <AverioButton type="button" variant={showCreate ? 'secondary' : 'primary'}
                disabled={schedules.isError || schedules.isLoading} aria-expanded={showCreate}
                aria-controls="recurring-allowance-form" onClick={() => setShowCreate(!showCreate)}>
                {t('personalMoney.recurring.' + (showCreate ? 'cancelCreate' : 'create'))}
            </AverioButton>
        </header>
        {feedback && <p role="status" className="text-sm text-emerald-700 dark:text-emerald-300">{feedback}</p>}
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error}</p>}
        {schedules.isError && <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-800 dark:bg-red-950/30 dark:text-red-200">
            <p className="min-w-0 flex-1">{t('personalMoney.recurring.' +
                (schedules.error?.message === migrationNotReady ? 'schemaUnavailable' : 'loadError'))}</p>
            <AverioButton type="button" disabled={schedules.isFetching}
                onClick={() => { void schedules.refetch(); }}>
                {t('personalMoney.recurring.retry')}
            </AverioButton>
        </div>}
        {showCreate && !schedules.isError && <form id="recurring-allowance-form" className="grid gap-3 border-t border-slate-200 pt-3 dark:border-slate-700"
            aria-label={t('personalMoney.recurring.create')}
            onSubmit={e => { e.preventDefault(); if (valid && !create.isPending) create.mutate(); }}>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                <AverioField>{t('personalMoney.recipient')}
                    <AverioSelect value={recipient} required onChange={e => { setRecipient(e.target.value); setOperationId(crypto.randomUUID()); }}>
                        {household.members.map(m => <option key={m.id} value={m.id}>{names.get(m.id)}</option>)}
                    </AverioSelect>
                </AverioField>
                <AverioField>{t('personalMoney.amount')}
                    <AverioInput inputMode="decimal" value={amount} required
                        onChange={e => { setAmount(e.target.value); setOperationId(crypto.randomUUID()); }} />
                </AverioField>
                <AverioField>{t('personalMoney.currency')}
                    <AverioSelect value={currency} onChange={e => { setCurrency(e.target.value); setOperationId(crypto.randomUUID()); }}>
                        {['EUR','HUF','USD'].map(code => <option key={code} value={code}>{code}</option>)}
                    </AverioSelect>
                </AverioField>
                <AverioField>{t('personalMoney.recurring.start')}
                    <AverioInput type="date" value={startsOn} required min={utcToday()}
                        onChange={e => { setStartsOn(e.target.value); setOperationId(crypto.randomUUID()); }} />
                </AverioField>
                <AverioField>{t('personalMoney.recurring.end')}
                    <AverioInput type="date" value={endsOn} min={startsOn}
                        onChange={e => { setEndsOn(e.target.value); setOperationId(crypto.randomUUID()); }} />
                </AverioField>
            </div>
            <div className="flex justify-end">
                <AverioButton type="submit" variant="primary" disabled={create.isPending || !valid}>
                    {t('personalMoney.recurring.create')}
                </AverioButton>
            </div>
        </form>}
        <div className="space-y-2 border-t border-slate-200 pt-3 dark:border-slate-700">
            <h5 className="text-sm font-semibold">{t('personalMoney.recurring.existing')}
                {schedules.data && <span className="ml-2 text-xs font-normal text-slate-500 dark:text-slate-400">
                    ({schedules.data.length})
                </span>}
            </h5>
            {schedules.isLoading && <p role="status">{t('common.status.loading')}</p>}
            {schedules.data && (schedules.data.length === 0
                ? <p className="py-2 text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.recurring.none')}</p>
                : <div className="divide-y divide-slate-200 dark:divide-slate-700">
                    {schedules.data.map(item => <div key={item.id} className="min-w-0">
                        <p className="pt-3 text-xs font-semibold text-slate-600 dark:text-slate-300">
                            {names.get(item.recipientMemberId) ?? t('householdSettings.unnamedMember')}
                        </p>
                        <ScheduleRow key={item.version} schedule={item} householdId={householdId}
                            userId={userId} onChanged={refresh} />
                    </div>)}
                </div>)}
        </div>
    </AverioCard>;
}

export function MyRecurringAllowances({ userId, householdId, memberId }: {
    userId: string; householdId: string; memberId: string;
}) {
    const { t, i18n } = useTranslation();
    const schedules = useSchedules(userId, householdId);
    const mine = (schedules.data ?? []).filter(x => x.recipientMemberId === memberId && !x.isStopped);
    const upcoming = mine.filter(x => x.nextDueOn !== null && !x.isPaused && !x.isBlocked);
    const inactive = mine.filter(x => x.nextDueOn === null || x.isPaused || x.isBlocked);
    if (schedules.isLoading || schedules.isError || mine.length === 0) return null;
    const money = (item: RecurringAllowanceResponse) =>
        new Intl.NumberFormat(i18n.resolvedLanguage ?? i18n.language,
            { style: 'currency', currency: item.currency }).format(Number(item.amount));
    return <section className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
        <h4 className="font-semibold">{t('personalMoney.recurring.myUpcoming')}</h4>
        {upcoming.length === 0 && <p className="text-sm text-slate-600 dark:text-slate-300">
            {t('personalMoney.recurring.noUpcoming')}</p>}
        {upcoming.length > 0 && <ul className="space-y-2">
            {upcoming.map(x => <li key={x.id} className="flex flex-wrap justify-between gap-2 text-sm">
                <span>{x.nextDueOn} · {t('personalMoney.recurring.monthly')}</span>
                <strong>{money(x)}</strong>
            </li>)}
        </ul>}
        {inactive.length > 0 && <div className="space-y-2 border-t border-slate-200 pt-3 dark:border-slate-700">
            <h5 className="text-sm font-semibold">{t('personalMoney.recurring.notUpcoming')}</h5>
            <ul className="space-y-2">
                {inactive.map(x => <li key={x.id} className="flex flex-wrap justify-between gap-2 text-sm">
                    <span>{money(x)} · {t('personalMoney.recurring.status.' +
                        (x.isBlocked ? 'blocked' : x.isPaused ? 'paused' : 'completed'))}</span>
                </li>)}
            </ul>
        </div>}
    </section>;
}
