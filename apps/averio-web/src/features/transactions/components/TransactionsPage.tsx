import { ImportedBadge } from '../../imports/ImportedBadge';
import { recurringApi, recurringQueries } from '../../recurring/recurringQueries';
import { Link, useRouter } from '@tanstack/react-router';
import { ActivitySelect } from '../../activities/ActivitySelect';
import { AssignActivityDialog } from '../../activities/AssignActivityDialog';
import { activityQueries, emptyAssignmentVersion } from '../../activities/activityQueries';
import { datePresets, type TransactionSearch, type DatePreset } from '../transactionSearch';
import { effectivePeriod, resolveTransactionDates } from '../transactionDates';
import { useTransactionToday } from '../useTransactionToday';
import { SavedViews } from './SavedViews';
import { LabelFilters } from './LabelFilters';
import { ReimbursementBadge } from '../../reimbursements/ReimbursementBadge';
import { TransactionLinkDetail } from './TransactionLinkDetail';
import { itemRefundKeys } from '../refunds/itemRefundQueries';
import { reimbursementKeys } from '../../reimbursements/reimbursementQueries';
import { moneyBackQueryKeys } from '../../money-back/moneyBackQueryKeys';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioLogo } from '../../../shared/components/branding/AverioLogo';
import { useMemo, useRef, useState } from 'react';
import { useInfiniteQuery, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { getTransactions, archiveTransaction } from '../api/transactionApi';
import type {
    TransactionDto,
    TransactionFilters,
    TransactionInputDto,
    TransactionKind,
} from '../api/transactionApi';
import { transactionQueries, transactionQueryKeys } from '../api/transactionQueries';
import { savedViewQueries } from '../api/savedViewQueries';
import { useTransactionMutations } from '../api/useTransactionMutations';
import { personalMoneyQueries } from '../../personal-money/personalMoneyQueries';
import { personalMoneyRelations } from '../../personal-money/personalMoneyRelations';
import { PersonalMoneyRelationBadge } from '../../personal-money/PersonalMoneyRelationBadge';
import { TransactionEditor } from './TransactionEditor';
import '../transactions.css';

interface Props {
    userId: string;
    householdId: string;
    createRequested?: boolean;
    initialKind?: TransactionKind;
    urlState: TransactionSearch;
    onUrlChange: (patch: Partial<TransactionSearch>, replace?: boolean) => Promise<void>;
    onCreateRequestHandled?: () => void;
}

export function TransactionsPage({
    userId,
    householdId,
    createRequested = false,
    initialKind,
    urlState,
    onUrlChange,
    onCreateRequestHandled,
}: Props) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const today = useTransactionToday();

    const [search, setSearch] = useState('');
    const [personalMoneyOnly, setPersonalMoneyOnly] = useState(false);
    const [importedOnly, setImportedOnly] = useState(false);
    const [showArchived, setShowArchived] = useState(false);
    const router = useRouter();
    const openedFromList = useRef<string | null>(null);
    const activityId = urlState.activity ?? '';
    const accountId = urlState.account ?? '';
    const kind = urlState.kind ?? '';
    const period = effectivePeriod(urlState);
    const { from, to } = resolveTransactionDates(urlState, today);
    const invalidRange = Boolean(from && to && from > to);
    const labelIds = useMemo(() => urlState.labels ?? [], [urlState.labels]);
    const setActivityId = (value: string) => onUrlChange({ activity: value || undefined, ...(value ? { period: 'all', dates: undefined, from: undefined, to: undefined } : {}) });
    const setAccountId = (value: string) => onUrlChange({ account: value || undefined });
    const setKind = (value: '' | TransactionKind) => onUrlChange({ kind: value || undefined });
    const setPeriod = (value: DatePreset) => onUrlChange({ period: value, dates: undefined,
        from: value === 'custom' ? from : undefined, to: value === 'custom' ? to : undefined });
    const [selecting, setSelecting] = useState(false);
    const selectionScope = JSON.stringify([accountId, kind, from, to, activityId, labelIds, search, personalMoneyOnly, importedOnly, showArchived]);
    const [selection, setSelection] = useState<{ scope: string; values: Record<string, string> }>({ scope: '', values: {} });
    const selected = selection.scope === selectionScope ? selection.values : {};
    const setSelected = (value: Record<string, string> | ((current: Record<string, string>) => Record<string, string>)) =>
        setSelection(current => ({ scope: selectionScope, values: typeof value === 'function' ? value(current.scope === selectionScope ? current.values : {}) : value }));
    const [assigning, setAssigning] = useState(false);
    const [filtersOpen, setFiltersOpen] = useState(false);
    const [editor, setEditor] = useState<'new' | null>(null);
    const [notice, setNotice] = useState('');

    const searchInput = useRef<HTMLInputElement>(null);
    const filterToggle = useRef<HTMLButtonElement>(null);
    const accounts = useQuery(accountsQueries.list(userId, householdId, true));
    const activities = useQuery({ ...activityQueries.list(userId, householdId), enabled: !!activityId });
    const filterLabels = useQuery({ ...savedViewQueries.labels(userId, householdId), enabled: labelIds.length > 0 });
    const personalMoney = useQuery(personalMoneyQueries.mine(userId, householdId));
    const relations = useMemo(() => personalMoneyRelations(personalMoney.data), [personalMoney.data]);
    const queryClient = useQueryClient();
    const mutations = useTransactionMutations(userId, householdId);
    const busy = mutations.create.isPending
        || mutations.update.isPending
        || mutations.void.isPending;
    const detail = useQuery({ ...transactionQueries.detail(userId, householdId, urlState.transaction ?? ''), enabled: Boolean(urlState.transaction), retry: false });
    const activeEditor = urlState.transaction ? (detail.isSuccess && detail.data?.canModify && detail.data.status !== 'Voided' && detail.data.kind !== 'Refund' ? detail.data : null) : createRequested ? 'new' : editor;
    const openTransaction = (transaction: TransactionDto) => {
        openedFromList.current = transaction.id;
        onUrlChange({ transaction: transaction.id, create: undefined, createKind: undefined });
    };

    const filterCount = Number(importedOnly) + Number(showArchived) + Number(personalMoneyOnly)
        + Number(accountId !== '')
        + Number(kind !== '')
        + Number(period !== 'all') + Number(activityId !== '') + labelIds.length + Number(!!search.trim());

    const hasOtherCriteria = !!(search.trim() || importedOnly || showArchived || personalMoneyOnly
        || accountId || kind || activityId || labelIds.length);

    const filters = useMemo<TransactionFilters>(() => {
        const result: TransactionFilters = {
            activityId: activityId || undefined,
            accountId: accountId || undefined,
            kind: kind || undefined,
            search: search.trim() || undefined,
            pageSize: 40,
            includeArchived: showArchived, importedOnly,
            from, to, labelIds,
        };

        return result;
    }, [accountId, kind, from, to, search, activityId, labelIds, showArchived, importedOnly]);

    const transactions = useInfiniteQuery({
        enabled: !invalidRange,
        queryKey: transactionQueryKeys.list(
            userId,
            householdId,
            filters,
        ),
        initialPageParam: undefined as
            | {
                cursorDate: string;
                cursorCreatedAt: string;
                cursorId: string;
            }
            | undefined,
        queryFn: ({ pageParam, signal }) => getTransactions(
            householdId,
            { ...filters, ...pageParam },
            signal,
        ),
        getNextPageParam: page => {
            if (!page.hasMore || page.items.length === 0) {
                return undefined;
            }

            const last = page.items.at(-1)!;
            return {
                cursorDate: last.occurredOn,
                cursorCreatedAt: last.createdAt,
                cursorId: last.id,
            };
        },
    });

    const items = invalidRange ? [] : transactions.data?.pages.flatMap(page => page.items) ?? [];
    const visibleItems = personalMoneyOnly ? items.filter(item => relations.transactions.has(item.id)) : items;
    const eligibleItems = visibleItems.filter(item => item.status === 'Posted' && item.kind !== 'Refund');
    const needsHistoryCheck = !invalidRange && transactions.isSuccess && items.length === 0
        && !hasOtherCriteria && (period === 'this-month' || period === 'all');
    const history = useQuery({
        ...transactionQueries.list(userId, householdId, { pageSize: 1, includeArchived: true }),
        enabled: needsHistoryCheck,
    });
    const firstUse = needsHistoryCheck && history.isSuccess && history.data.items.length === 0;
    const selectedCount = Object.keys(selected).length;
    const remainingEligible = eligibleItems.some(item => !(item.id in selected));

    const formatter = new Intl.DateTimeFormat(language, {
        weekday: 'short',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        timeZone: 'UTC',
    });

    function resetFilters(allDates = false) {
        setImportedOnly(false);
        setSearch('');
        setPersonalMoneyOnly(false);
        setShowArchived(false);
        setSelected({});
        void onUrlChange({ account: undefined, kind: undefined, activity: undefined, dates: undefined, from: undefined, to: undefined, period: allDates ? 'all' : undefined, labels: undefined });
        (filtersOpen ? searchInput.current : filterToggle.current)?.focus();
    }

    const activeFilters: { key: string; label: string; remove: () => void }[] = [];
    if (period !== 'all') activeFilters.push({
        key: 'period',
        label: t('savedViews.' + period) + (period === 'custom' && (from || to) ? `: ${from ?? '…'} – ${to ?? '…'}` : ''),
        remove: () => { void setPeriod('all'); },
    });
    if (search.trim()) activeFilters.push({ key: 'search', label: t('transactionManagement.searchFilter', { value: search.trim() }), remove: () => setSearch('') });
    if (accountId) activeFilters.push({ key: 'account', label: t('transactionManagement.filterValue', { label: t('transactionManagement.account'), value: accounts.data?.find(account => account.id === accountId)?.name ?? t(accounts.isPending ? 'common.status.loading' : 'transactionLinks.unavailableAccount') }), remove: () => { void setAccountId(''); } });
    if (activityId) activeFilters.push({ key: 'activity', label: t('transactionManagement.filterValue', { label: t('activities.activity'), value: activities.data?.find(activity => activity.id === activityId)?.name ?? t(activities.isPending ? 'common.status.loading' : 'activities.unavailable') }), remove: () => { void setActivityId(''); } });
    if (kind) activeFilters.push({ key: 'kind', label: t('transactionManagement.filterValue', { label: t('transactionManagement.type'), value: t(kind === 'Refund' ? 'itemRefunds.refund' : `transactionManagement.${kind.toLowerCase()}`) }), remove: () => { void setKind(''); } });
    for (const id of labelIds) activeFilters.push({ key: id, label: t('transactionManagement.filterValue', { label: t('savedViews.labels'), value: filterLabels.data?.find(label => label.id === id)?.name ?? t(filterLabels.isPending ? 'common.status.loading' : 'savedViews.unavailableLabel') }), remove: () => { const labels = labelIds.filter(labelId => labelId !== id); void onUrlChange({ labels: labels.length ? labels : undefined }); } });
    if (showArchived) activeFilters.push({ key: 'archived', label: t('transactionManagement.includeArchived'), remove: () => setShowArchived(false) });
    if (importedOnly) activeFilters.push({ key: 'imported', label: t('importHistory.importedOnly'), remove: () => setImportedOnly(false) });
    if (personalMoneyOnly) activeFilters.push({ key: 'personal', label: t('personalMoney.filterOnly'), remove: () => setPersonalMoneyOnly(false) });

    function closeEditor() {
        setEditor(null);
        if (urlState.transaction) {
            if (openedFromList.current === urlState.transaction) { openedFromList.current = null; router.history.back(); }
            else onUrlChange({ transaction: undefined }, true);
        }
        if (createRequested) {
            onCreateRequestHandled?.();
        }
    }

    async function save(
        request: TransactionInputDto,
        expectedUpdatedAt?: string,
    ) {
        if (activeEditor === 'new') {
            await mutations.create.mutateAsync(request);
            setNotice(t('transactionManagement.created'));
        } else if (activeEditor && expectedUpdatedAt) {
            await mutations.update.mutateAsync({
                id: activeEditor.id,
                request: { ...request, expectedUpdatedAt },
            });
            setNotice(t('transactionManagement.edited'));
        }

        closeEditor();
    }

    async function archiveCurrent(archived: boolean, expectedUpdatedAt: string) {
        if (!activeEditor || activeEditor === 'new') return;
        await archiveTransaction(householdId, activeEditor.id, archived, expectedUpdatedAt);
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) }),
            queryClient.invalidateQueries({ queryKey: itemRefundKeys.all(userId, householdId) }),
            queryClient.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }),
            queryClient.invalidateQueries({ queryKey: moneyBackQueryKeys.household(userId, householdId) }),
        ]);
        setNotice(t(archived ? 'transactionManagement.archivedNotice' : 'transactionManagement.restoredNotice'));
        closeEditor();
    }

    async function voidCurrent(expectedUpdatedAt: string) {
        if (!activeEditor || activeEditor === 'new') return;

        await mutations.void.mutateAsync({
            id: activeEditor.id,
            expectedUpdatedAt,
        });

        setNotice(t('transactionManagement.voidedNotice'));
        closeEditor();
    }

    return (
        <section className="av-tx-page" aria-labelledby="transactions-title">
            <h2 id="transactions-title" className="sr-only">{t('transactionManagement.title')}</h2>
            <header className="mb-4 flex flex-wrap items-center justify-end gap-2">
                <div className="flex flex-wrap items-center justify-end gap-2">
                    <AverioButton ref={filterToggle} size="icon" className="relative sm:w-auto sm:px-3" aria-label={t('transactionManagement.filterCount', { count: filterCount })}
                        aria-expanded={filtersOpen} aria-controls="transaction-filters" onClick={() => setFiltersOpen(open => !open)}>
                        <ConceptIcon name="filters" /><span className="hidden sm:inline">{t('transactionManagement.filters')}</span>
                        {filterCount > 0 && <span aria-hidden="true" className="absolute -top-1 -right-1 grid min-w-5 place-items-center rounded-full bg-primary-600 px-1 text-[10px] leading-5 text-white">{filterCount}</span>}
                    </AverioButton>
                    <details className="relative" onKeyDown={event => {
                        if (event.key === 'Escape') {
                            event.preventDefault();
                            event.currentTarget.open = false;
                            event.currentTarget.querySelector('summary')?.focus();
                        }
                    }} onBlur={event => {
                        if (!event.currentTarget.contains(event.relatedTarget)) event.currentTarget.open = false;
                    }}>
                        <summary aria-label={t('transactionManagement.moreActions')} className="flex size-11 cursor-pointer list-none items-center justify-center gap-2 rounded-[11px] border border-[#E4E7EC] bg-white text-xs font-semibold text-[#152033] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 sm:w-auto sm:px-3 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] [&::-webkit-details-marker]:hidden"><ConceptIcon name="more" /><span className="hidden sm:inline">{t('transactionManagement.moreActions')}</span></summary>
                        <div className="absolute -right-20 z-20 mt-2 grid w-52 max-w-[calc(100vw-2rem)] rounded-xl border border-slate-200 bg-white p-2 text-sm shadow-lg sm:right-0 dark:border-slate-700 dark:bg-[#0D1726]">
                            {[{ to: '/app/imports', label: t('imports.button') }, { to: '/app/receipts', label: t('receipts.capture') }, { to: '/app/transfers', label: t('transfers.title') }].map(item =>
                                <Link key={item.to} to={item.to} className="flex min-h-11 items-center rounded-lg px-3 font-semibold hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-primary-500 dark:hover:bg-slate-800">{item.label}</Link>)}
                        </div>
                    </details>
                    <AverioButton size="icon" className="sm:w-auto sm:px-3" aria-label={t('activities.selectTransactions')}
                        aria-pressed={selecting} disabled={!selecting && (!transactions.isSuccess || eligibleItems.length === 0)}
                        onClick={() => { setSelecting(!selecting); setSelected({}); }}>
                        <ConceptIcon name="check" /><span className="hidden sm:inline">{t('activities.selectTransactions')}</span>
                    </AverioButton>
                    <AverioButton variant="primary" aria-label={t('transactionManagement.add')} onClick={() => setEditor('new')}>
                        <ConceptIcon name="plus" />{t('transactionManagement.addShort')}
                    </AverioButton>
                </div>
            </header>

            {activeFilters.length > 0 && <div role="group" className="mb-4 flex min-w-0 flex-wrap gap-2" aria-label={t('transactionManagement.filterSummary')}>
                {activeFilters.map(filter => <button key={filter.key} type="button" aria-label={t('transactionManagement.removeFilter', { value: filter.label })}
                    className="inline-flex min-h-11 max-w-full items-center gap-2 rounded-full bg-emerald-100 px-3 text-left text-xs font-semibold text-emerald-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:bg-emerald-950 dark:text-emerald-200"
                    onClick={() => { filterToggle.current?.focus(); filter.remove(); setSelected({}); }}>
                    <span className="min-w-0 break-all">{filter.label}</span><ConceptIcon name="close" />
                </button>)}
            </div>}

            <div id="transaction-filters" className={filtersOpen ? 'mb-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-700 dark:bg-[#0D1726]' : ''}>
                <SavedViews userId={userId} householdId={householdId} state={urlState} invalid={invalidRange} controlsVisible={filtersOpen}
                    unsavedFiltersActive={!!search.trim() || importedOnly || showArchived || personalMoneyOnly}
                    onApply={filters => { setSearch(''); setSelected({}); return onUrlChange(filters); }} />
                <div hidden={!filtersOpen}>
                    <div role="search" aria-label={t('transactionManagement.search')} className="mb-4 flex gap-2">
                        <AverioInput ref={searchInput} id="transaction-search" type="search" autoComplete="off" aria-label={t('transactionManagement.search')}
                            value={search} placeholder={t('transactionManagement.searchPlaceholder')}
                            onChange={event => { setSearch(event.target.value); setSelected({}); }} />
                        {search && <AverioButton size="icon" aria-label={t('transactionManagement.clearSearch')}
                            onClick={() => { setSearch(''); setSelected({}); searchInput.current?.focus(); }}><ConceptIcon name="close" /></AverioButton>}
                    </div>
                    <div className="grid gap-4 sm:grid-cols-2">
                        <AverioField>{t('savedViews.period')}
                            <AverioSelect value={period} onChange={event => { setPeriod(event.target.value as DatePreset); setSelected({}); }}>
                                {datePresets.map(value => <option key={value} value={value}>{t('savedViews.' + value)}</option>)}
                            </AverioSelect>
                        </AverioField>
                        <AverioField>{t('transactionManagement.account')}
                            <AverioSelect value={accountId} onChange={event => { setAccountId(event.target.value); setSelected({}); }}>
                                <option value="">{t('transactionManagement.allAccounts')}</option>
                                {accountId && !accounts.data?.some(account => account.id === accountId) && <option value={accountId}>{t('transactionLinks.unavailableAccount')}</option>}
                                {(accounts.data ?? []).map(account => <option key={account.id} value={account.id}>{account.name}</option>)}
                            </AverioSelect>
                        </AverioField>
                        {period === 'custom' && <>
                            <AverioField>{t('transactionLinks.from')}<AverioInput type="date" value={from ?? ''} aria-invalid={invalidRange}
                                onChange={event => { onUrlChange({ from: event.target.value || undefined, to, dates: undefined, period: 'custom' }); setSelected({}); }} /></AverioField>
                            <AverioField>{t('transactionLinks.to')}<AverioInput type="date" value={to ?? ''} aria-invalid={invalidRange}
                                onChange={event => { onUrlChange({ to: event.target.value || undefined, from, dates: undefined, period: 'custom' }); setSelected({}); }} /></AverioField>
                        </>}
                    </div>
                    <details className="mt-4 border-t border-slate-200 pt-2 dark:border-slate-700">
                        <summary className="min-h-11 cursor-pointer py-3 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-primary-500">{t('transactionManagement.moreFilters')}</summary>
                        <div className="grid items-start gap-4 sm:grid-cols-2">
                            <ActivitySelect userId={userId} householdId={householdId} value={activityId}
                                onChange={id => { setActivityId(id); setSelected({}); }} includeClosed allowCreate={false} emptyLabel={t('activities.all')} />
                            <fieldset className="min-w-0">
                                <legend className="mb-[7px] text-[13px] font-semibold">{t('transactionManagement.type')}</legend>
                                <div className="grid grid-cols-2 gap-1 rounded-xl border border-slate-200 p-0.5 dark:border-slate-700 sm:grid-cols-4">
                                    {(['', 'Income', 'Expense', 'Refund'] as const).map(value => <button key={value} type="button" aria-pressed={kind === value}
                                        className="min-h-11 min-w-0 rounded-lg px-2 text-xs font-semibold text-slate-600 focus-visible:outline-2 focus-visible:outline-emerald-500 aria-pressed:bg-emerald-50 aria-pressed:text-emerald-800 dark:text-slate-300 dark:aria-pressed:bg-emerald-950 dark:aria-pressed:text-emerald-200"
                                        onClick={() => { setKind(value); setSelected({}); }}>
                                        {t(value === 'Refund' ? 'itemRefunds.refund' : 'transactionManagement.' + (value ? value.toLowerCase() : 'all'))}
                                    </button>)}
                                </div>
                            </fieldset>
                            <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={showArchived} className="size-4 accent-emerald-500"
                                onChange={event => { setShowArchived(event.target.checked); setSelected({}); }} />{t('transactionManagement.includeArchived')}</label>
                            <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={importedOnly} className="size-4 accent-emerald-500"
                                onChange={event => { setImportedOnly(event.target.checked); setSelected({}); }} />{t('importHistory.importedOnly')}</label>
                            <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={personalMoneyOnly} className="size-4 accent-emerald-500"
                                disabled={personalMoney.isPending || personalMoney.isError}
                                onChange={event => { setPersonalMoneyOnly(event.target.checked); setSelected({}); }} />{t('personalMoney.filterOnly')}</label>
                            <div className="sm:col-span-2"><LabelFilters userId={userId} householdId={householdId} value={labelIds}
                                onChange={labels => { setSelected({}); void onUrlChange({ labels: labels.length ? labels : undefined }); }} /></div>
                        </div>
                    </details>
                    <div className="mt-4"><AverioButton onClick={() => resetFilters()}>{t('transactionManagement.reset')}</AverioButton></div>
                </div>
            </div>
            <span className="sr-only" role="status" aria-live="polite">{t('transactionManagement.filterCount', { count: filterCount })}</span>
            {accounts.isError && <div role="alert">{t('errors.genericDescription')} <AverioButton onClick={() => void accounts.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
            {activityId && activities.isError && <div role="alert">{t('errors.genericDescription')} <AverioButton onClick={() => void activities.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
            {labelIds.length > 0 && filterLabels.isError && <div role="alert">{t('savedViews.labelError')} <AverioButton onClick={() => void filterLabels.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
            {filtersOpen && !personalMoneyOnly && personalMoney.isError && <div role="alert">{t('personalMoney.loadError')} <AverioButton onClick={() => void personalMoney.refetch()}>{t('common.actions.retry')}</AverioButton></div>}

            {notice && (
                <p role="status" className="av-tx-notice">
                    {notice}
                </p>
            )}

            {!invalidRange && transactions.isPending && (
                <p role="status">{t('common.status.loading')}</p>
            )}

            {!invalidRange && transactions.isError && (
                <div role="alert">
                    <p className="av-error">
                        {t('errors.genericDescription')}
                    </p>
                    <button
                        className="av-btn"
                        onClick={() => {
                            void transactions.refetch();
                        }}
                    >
                        {t('common.actions.retry')}
                    </button>
                </div>
            )}

            {needsHistoryCheck && history.isPending && <p role="status">{t('common.status.loading')}</p>}
            {needsHistoryCheck && history.isError && <div role="alert">{t('errors.genericDescription')} <AverioButton onClick={() => void history.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
            {personalMoneyOnly && personalMoney.isPending && <p role="status">{t('common.status.loading')}</p>}
            {!invalidRange && transactions.isSuccess && items.length === 0 && (!personalMoneyOnly || personalMoney.isSuccess) && (!needsHistoryCheck || history.isSuccess) && (
                <div className="grid min-w-0 justify-items-center gap-4 rounded-[18px] border border-[#E4E7EC] bg-white px-5 py-10 text-center dark:border-[#233044] dark:bg-[#0D1726] sm:py-14">
                    <div aria-hidden="true" className="relative grid h-32 w-48 place-items-center rounded-full bg-primary-500/5 dark:bg-primary-500/10">
                        <div className="grid size-20 place-items-center rounded-2xl border border-emerald-200 bg-white text-primary-600 shadow-sm dark:border-emerald-800 dark:bg-[#142234]"><ConceptIcon name="transactions" /></div>
                        <span className="absolute right-8 bottom-2 grid size-9 place-items-center rounded-xl bg-[#142234]"><AverioLogo className="h-7 w-auto" /></span>
                    </div>
                    <div className="max-w-sm">
                        <h3 className="m-0 text-xl font-bold tracking-tight">{t(firstUse ? 'transactionManagement.emptyTitle' : 'transactionManagement.noResultsTitle')}</h3>
                        <p className="mt-2 text-sm leading-relaxed text-[#667085] dark:text-[#AAB4C4]">{t(firstUse ? 'transactionManagement.empty' : 'transactionManagement.noResults')}</p>
                    </div>
                    {firstUse
                        ? <AverioButton variant="primary" onClick={() => setEditor('new')}><ConceptIcon name="plus" />{t('transactionManagement.add')}</AverioButton>
                        : period === 'all' && !hasOtherCriteria
                            ? <AverioButton onClick={() => setShowArchived(true)}>{t('transactionManagement.includeArchived')}</AverioButton>
                            : <AverioButton onClick={() => resetFilters(true)}>{t('transactionManagement.clearAllFilters')}</AverioButton>}
                </div>
            )}

            {selecting && <div className="mb-3 flex flex-wrap items-center gap-3">
                <span role="status" aria-live="polite" className="text-sm">{t('activities.selected', { count: Object.keys(selected).length })}</span>
                <AverioButton className="max-w-full" disabled={!remainingEligible || selectedCount >= 100} onClick={() => setSelected(current => {
                    const next = { ...current };
                    for (const item of eligibleItems) {
                        if (Object.keys(next).length >= 100) break;
                        if (!(item.id in next)) next[item.id] = item.activityAssignmentVersion ?? emptyAssignmentVersion;
                    }
                    return next;
                })}>{t('activities.selectLoaded')}</AverioButton>
                <AverioButton disabled={Object.keys(selected).length === 0} onClick={() => setAssigning(true)}>{t('activities.assign')}</AverioButton>
                <AverioButton onClick={() => { setSelecting(false); setSelected({}); }}>{t('common.actions.cancel')}</AverioButton>
                {selectedCount >= 100 && remainingEligible && <span role="status" className="text-sm text-amber-700 dark:text-amber-300">{t('activities.selectionLimit')}</span>}
            </div>}
            {assigning && <AssignActivityDialog userId={userId} householdId={householdId}
                transactions={Object.entries(selected).map(([transactionId, expectedVersion]) => ({ transactionId, expectedVersion }))}
                onClose={() => setAssigning(false)} onSaved={() => { setAssigning(false); setSelected({}); setSelecting(false); setNotice(t('activities.assigned')); }} />}
            {personalMoneyOnly && personalMoney.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('personalMoney.loadError')}</p>}
            {personalMoneyOnly && personalMoney.isSuccess && items.length > 0 && visibleItems.length === 0 && (
                <div role="status" className="flex flex-wrap items-center gap-3 rounded-xl border border-dashed border-slate-300 p-4 text-sm dark:border-slate-700">
                    <span>{t('personalMoney.noLoadedRelations')}</span>
                    <AverioButton onClick={() => { setPersonalMoneyOnly(false); setSelected({}); filterToggle.current?.focus(); }}>{t('transactionManagement.removeFilter', { value: t('personalMoney.filterOnly') })}</AverioButton>
                </div>
            )}
            {personalMoneyOnly && <p className="text-xs text-slate-600 dark:text-slate-300">
                {t('personalMoney.loadedFilterHelp')}
            </p>}
            {visibleItems.length > 0 && (
                <div
                    className="av-timeline"
                    aria-label={t('transactionManagement.title')}
                >
                    {visibleItems.map((transaction, index) => {
                        const showDate =
                            index === 0
                            || visibleItems[index - 1]?.occurredOn
                                !== transaction.occurredOn;

                        const dateLabel = formatter.format(
                            new Date(
                                transaction.occurredOn + 'T00:00:00Z',
                            ),
                        );

                        const isIncome =
                            transaction.kind !== 'Expense';

                        const title =
                            transaction.merchant
                            ?? transaction.description;
                        const selectable = selecting && transaction.status === 'Posted' && transaction.kind !== 'Refund';
                        const isSelected = transaction.id in selected;

                        return (
                            <div key={transaction.id}>
                                {showDate && (
                                    <div className="av-timeline-date">
                                        <span>{dateLabel}</span>
                                    </div>
                                )}

                                <div className={
                                    `av-timeline-row ${
                                        isIncome
                                            ? 'income'
                                            : 'expense'
                                    }`
                                }>
                                    <div data-transaction-id={transaction.id} className={`av-tx-bubble relative min-h-[98px] min-w-0 w-[min(100%,430px)] rounded-[17px] border border-[var(--border)] bg-[var(--panel)] px-[15px] py-[14px] text-left text-[var(--text)] shadow-[var(--shadow)] transition-[transform,border-color,box-shadow] duration-150 hover:-translate-y-0.5 hover:shadow-[0_15px_35px_rgba(16,24,40,0.1)] ${
                                        isIncome
                                            ? 'hover:border-[color-mix(in_srgb,var(--brand)_40%,var(--border))]'
                                            : 'hover:border-[color-mix(in_srgb,#D92D20_40%,var(--border))]'
                                    } ${transaction.status === 'Voided' ? 'opacity-[.58]' : ''} ${
                                        isSelected ? 'ring-2 ring-primary-500 ring-offset-2 ring-offset-[color:var(--bg)]' : ''
                                    }`}>
                                    {selectable && <label className="absolute top-1 right-1 z-10 flex size-11 cursor-pointer items-center justify-center rounded-lg has-[:disabled]:cursor-not-allowed">
                                        <input type="checkbox" className="size-5 cursor-pointer accent-primary-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-50"
                                            aria-label={t('activities.selectTransaction', { name: title })} checked={isSelected}
                                            disabled={!isSelected && Object.keys(selected).length >= 100}
                                            onChange={event => setSelected(current => {
                                                const next = { ...current };
                                                if (event.target.checked) next[transaction.id] = transaction.activityAssignmentVersion ?? emptyAssignmentVersion;
                                                else delete next[transaction.id];
                                                return next;
                                            })} />
                                    </label>}
                                    <button type="button" className="block w-full cursor-pointer text-left text-inherit focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary-500" title={
                                            transaction.canModify && transaction.status !== 'Voided'
                                                ? t('transactionManagement.editHint')
                                                : t('transactionLinks.details')
                                        }
                                        onClick={() =>
                                            openTransaction(transaction)}
                                    >
                                        <span className={`av-tx-bubble-top ${selectable ? 'pr-11' : ''}`}>
                                            <span className="av-tx-type-icon">
                                                {isIncome ? '↙' : '↗'}
                                            </span>
                                            <span className="av-tx-account">
                                                {transaction.accountName}
                                            </span>

                                            {transaction.visibility === 'Private' && (
                                                <span className="av-badge av-private-badge">
                                                    🔒{' '}
                                                    {t('transactionManagement.privateBadge')}
                                                </span>
                                            )}
                                        </span>

                                        <strong className="av-tx-description">
                                            {title}
                                        </strong>

                                        {relations.transactions.get(transaction.id) && <PersonalMoneyRelationBadge
                                            relation={relations.transactions.get(transaction.id)!} detail />}
                                        {transaction.kind === 'Refund' && <span className="inline-flex rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{t('itemRefunds.refund')}</span>}
                                        {transaction.items.some(i => i.hasRefundHistory) && <span className="block text-xs">{t('itemRefunds.net')}</span>}
                                        <ReimbursementBadge status={transaction.reimbursementStatus} />
                                        {transaction.activityName && <span className="inline-flex rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{transaction.activityName}</span>}
                                        {transaction.items.length > 1 && (
                                            <span className="av-tx-item-count">
                                                {t('transactionManagement.itemCount', {
                                                    count: transaction.items.length,
                                                })}
                                            </span>
                                        )}

                                        <strong className="av-tx-amount">
                                            {isIncome ? '+' : '−'}
                                            {formatDecimalAmount(
                                                transaction.amount,
                                                transaction.currency,
                                                language,
                                            )}
                                        </strong>

                                        {transaction.labels.length > 0 && (
                                            <span className="av-tx-bubble-labels">
                                                {transaction.labels
                                                    .slice(0, 4)
                                                    .map(label => (
                                                        <span
                                                            key={label.id}
                                                            className={
                                                                `av-tx-label-chip ${
                                                                    label.scope.toLowerCase()
                                                                }`
                                                            }
                                                        >
                                                            <span aria-hidden="true">
                                                                {label.scope === 'Household'
                                                                    ? '⌂'
                                                                    : '▣'}
                                                            </span>
                                                            {label.name}
                                                        </span>
                                                    ))}
                                            </span>
                                        )}

                                        <span className="av-tx-bubble-foot">
                                            {transaction.note && (
                                                <span>{transaction.note}</span>
                                            )}
                                            <span>
                                                {t(transaction.status === 'Voided'
                                                    ? 'transactionManagement.voided'
                                                    : 'transactionManagement.posted')}
                                            </span>
                                        </span>
                                    </button>
                                    {transaction.isImported && <ImportedBadge batchId={transaction.importBatchId} />}
                                    </div>

                                    <span
                                        className="av-timeline-dot"
                                        aria-hidden="true"
                                    />
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {invalidRange && <p role="alert">{t('transactionLinks.invalidRange')}</p>}
            {!invalidRange && transactions.hasNextPage && (
                <div className="av-tx-load-more">
                    <button
                        type="button"
                        className="av-btn"
                        disabled={transactions.isFetchingNextPage}
                        onClick={() => {
                            void transactions.fetchNextPage();
                        }}
                    >
                        {t(transactions.isFetchingNextPage
                            ? 'transactionManagement.loadingMore'
                            : 'transactionManagement.loadMore')}
                    </button>
                </div>
            )}

            {urlState.transaction && !activeEditor && <TransactionLinkDetail
                query={detail} onClose={closeEditor} userId={userId} householdId={householdId}
                personalRelation={relations.transactions.get(urlState.transaction)} />}
            {activeEditor && (
                <TransactionEditor
                    userId={userId}
                    householdId={householdId}
                    key={activeEditor === 'new' ? (initialKind ?? 'Expense') : activeEditor.id}
                    initialKind={initialKind}
                    transaction={activeEditor === 'new' ? null : activeEditor}
                    personalRelation={activeEditor === 'new' ? undefined : relations.transactions.get(activeEditor.id)}
                    onCancel={closeEditor}
                    onSave={save}
                    onSaveRecurring={async request => {
                        await recurringApi.apiV1HouseholdsHouseholdIdRecurringPost({ householdId, saveRecurringRuleRequest: request });
                        await queryClient.invalidateQueries(recurringQueries.list(userId, householdId));
                        closeEditor();
                        setNotice(t('recurring.saved'));
                    }}
                    onArchive={activeEditor === 'new' ? undefined : archiveCurrent}
                    onVoid={activeEditor === 'new'
                        ? undefined
                        : voidCurrent}
                    busy={busy}
                />
            )}
        </section>
    );
}

