import { ImportedBadge } from '../../imports/ImportedBadge';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { recurringApi, recurringQueries } from '../../recurring/recurringQueries';
import { Link, useRouter } from '@tanstack/react-router';
import { ActivitySelect } from '../../activities/ActivitySelect';
import { AssignActivityDialog } from '../../activities/AssignActivityDialog';
import { emptyAssignmentVersion } from '../../activities/activityQueries';
import { datePresets, type TransactionSearch, type DatePreset } from '../transactionSearch';
import { effectivePeriod, resolveTransactionDates } from '../transactionDates';
import { useTransactionToday } from '../useTransactionToday';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { SavedViews } from './SavedViews';
import { LabelFilters } from './LabelFilters';
import { TransactionLinkDetail } from './TransactionLinkDetail';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
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
    const selectionScope = JSON.stringify([accountId, kind, from, to, activityId, labelIds, search, personalMoneyOnly, importedOnly]);
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
    const personalMoney = useQuery(personalMoneyQueries.mine(userId, householdId));
    const relations = useMemo(() => personalMoneyRelations(personalMoney.data), [personalMoney.data]);
    const queryClient = useQueryClient();
    const mutations = useTransactionMutations(userId, householdId);
    const busy = mutations.create.isPending
        || mutations.update.isPending
        || mutations.void.isPending;
    const detail = useQuery({ ...transactionQueries.detail(userId, householdId, urlState.transaction ?? ''), enabled: Boolean(urlState.transaction), retry: false });
    const activeEditor = urlState.transaction ? (detail.isSuccess && detail.data?.canModify && detail.data.status !== 'Voided' ? detail.data : null) : createRequested ? 'new' : editor;
    const openTransaction = (transaction: TransactionDto) => {
        openedFromList.current = transaction.id;
        onUrlChange({ transaction: transaction.id, create: undefined, createKind: undefined });
    };

    const selectedMonthLabel = t('savedViews.' + period);

    const selectedAccountName = accountId
        ? accounts.data?.find(account => account.id === accountId)?.name
        : undefined;

    const filterCount = Number(importedOnly) + Number(showArchived) + Number(personalMoneyOnly)
        + Number(accountId !== '')
        + Number(kind !== '')
        + Number(period !== 'this-month') + Number(activityId !== '') + labelIds.length;

    const hasCriteria = search.trim().length > 0 || filterCount > 0;

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

    const formatter = new Intl.DateTimeFormat(language, {
        weekday: 'short',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        timeZone: 'UTC',
    });

    function resetFilters() {
        setImportedOnly(false);
        setSearch('');
        setPersonalMoneyOnly(false);
        setShowArchived(false);
        setSelected({});
        onUrlChange({ account: undefined, kind: undefined, activity: undefined, dates: undefined, from: undefined, to: undefined, period: undefined, labels: undefined });
        searchInput.current?.focus();
    }

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
        await queryClient.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) });
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
            <header className="av-page-head">
                <div>
                    <h2 id="transactions-title" aria-label={t('transactionManagement.title')}>{t('transactionManagement.title')} <InfoPopover label={t('transactionManagement.title')}>{t('transactionManagement.description')}</InfoPopover></h2>
                </div>
                <div className="flex flex-wrap items-center gap-2">
            <Link to="/app/imports" className="inline-flex min-h-11 items-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold focus-visible:outline-2 dark:border-slate-700 dark:bg-slate-900">{t('imports.button')}</Link>
            <Link to="/app/receipts" className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs font-semibold focus-visible:outline-2 dark:border-slate-700 dark:bg-slate-900"><ConceptIcon name="receipt" />{t('receipts.capture')}</Link>
            <Link to="/app/transfers" className="inline-flex min-h-11 items-center justify-center gap-2 rounded-[11px] border border-[#E4E7EC] bg-white px-3 py-2 text-xs font-semibold text-[#152033] hover:bg-[#F9FAFB] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] dark:hover:bg-[#111C2F]"><ConceptIcon name="transfers" />{t('transfers.title')}</Link>
                <button
                    type="button"
                    className="av-btn av-primary"
                    onClick={() => setEditor('new')}
                >
                    <ConceptIcon name="plus" />
                    {t('transactionManagement.add')}
                </button>
                </div>
            </header>

            <label className="mb-3 flex min-h-11 w-fit items-center gap-3 rounded-xl border border-slate-200 bg-white px-3 text-sm font-medium dark:border-slate-700 dark:bg-[#0D1726]">
                <input type="checkbox" className="size-4 accent-emerald-500" checked={showArchived}
                    onChange={event => { setShowArchived(event.target.checked); setSelected({}); }} />
                {t('transactionManagement.includeArchived')}
            </label>
            <div
                className="av-tx-search-shell"
                role="search"
                aria-label={t('transactionManagement.search')}
            >
                <div className="av-search-toolbar">
                    <div className="av-search-input">
                        <ConceptIcon name="search" />
                        <label
                            className="sr-only"
                            htmlFor="transaction-search"
                        >
                            {t('transactionManagement.search')}
                        </label>
                        <input
                            ref={searchInput}
                            id="transaction-search"
                            type="search"
                            autoComplete="off"
                            value={search}
                            placeholder={t('transactionManagement.searchPlaceholder')}
                            onChange={event => { setSearch(event.target.value); setSelected({}); }}
                        />
                        {search && (
                            <button
                                type="button"
                                className="av-search-clear"
                                aria-label={t('transactionManagement.clearSearch')}
                                onClick={() => {
                                    setSearch('');
                                    setSelected({});
                                    searchInput.current?.focus();
                                }}
                            >
                                <ConceptIcon name="close" />
                            </button>
                        )}
                    </div>

                    <button
                        ref={filterToggle}
                        type="button"
                        className="av-btn av-filter-toggle"
                        aria-expanded={filtersOpen}
                        aria-haspopup="dialog"
                        onClick={() => setFiltersOpen(open => !open)}
                    >
                        <ConceptIcon name="filters" />
                        <span>{t('transactionManagement.filters')}</span>
                        {filterCount > 0 && (
                            <span className="av-filter-count">
                                {filterCount}
                            </span>
                        )}
                    </button>
                </div>

                {filtersOpen && <AverioDialog open title={t('transactionManagement.filters')} closeLabel={t('common.actions.close')}
                    onDismiss={() => setFiltersOpen(false)} actions={<>
                        <AverioButton onClick={resetFilters}>{t('transactionManagement.reset')}</AverioButton>
                        <AverioButton variant="primary" onClick={() => setFiltersOpen(false)}>{t('transactionManagement.showResults')}</AverioButton>
                    </>}>
                    <div className="grid items-start gap-4 sm:grid-cols-2">
                        <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={importedOnly} onChange={event => setImportedOnly(event.target.checked)} className="size-4 accent-emerald-500" />{t('importHistory.importedOnly')}</label>

                        <AverioField className="sm:col-span-2">{t('savedViews.period')}
                            <AverioSelect aria-label={t('savedViews.period')} value={period} onChange={event => setPeriod(event.target.value as DatePreset)}>
                                {datePresets.map(value => <option key={value} value={value}>{t('savedViews.' + value)}</option>)}
                            </AverioSelect>
                        </AverioField>
                        <AverioField>{t('transactionLinks.from')}<AverioInput type="date" value={from ?? ''}
                            onChange={event => { onUrlChange({ from: event.target.value || undefined, to, dates: undefined, period: 'custom' }); setSelected({}); }} /></AverioField>
                        <AverioField>{t('transactionLinks.to')}<AverioInput type="date" value={to ?? ''}
                            onChange={event => { onUrlChange({ to: event.target.value || undefined, from, dates: undefined, period: 'custom' }); setSelected({}); }} /></AverioField>
                        <AverioField>{t('transactionManagement.account')}
                            <AverioSelect value={accountId} onChange={event => { setAccountId(event.target.value); setSelected({}); }}>
                                <option value="">{t('transactionManagement.allAccounts')}</option>
                                {accountId && !accounts.data?.some(account => account.id === accountId) && <option value={accountId}>{t('transactionLinks.unavailableAccount')}</option>}
                                {(accounts.data ?? []).map(account => <option key={account.id} value={account.id}>{account.name}</option>)}
                            </AverioSelect>
                        </AverioField>
                        <ActivitySelect userId={userId} householdId={householdId} value={activityId}
                            onChange={id => { setActivityId(id); setSelected({}); }}
                            includeClosed allowCreate={false} emptyLabel={t('activities.all')} />
                        <fieldset className="min-w-0">
                            <legend className="mb-[7px] text-[13px] font-semibold">{t('transactionManagement.type')}</legend>
                            <div className="grid h-11 grid-cols-3 rounded-[11px] border border-slate-200 p-0.5 dark:border-slate-700">
                                {(['', 'Income', 'Expense'] as const).map(value => <button key={value} type="button"
                                    aria-pressed={kind === value}
                                    className="min-w-0 rounded-lg px-2 text-xs font-semibold text-slate-600 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-emerald-500 aria-pressed:bg-emerald-50 aria-pressed:text-emerald-800 dark:text-slate-300 dark:hover:bg-slate-800 dark:aria-pressed:bg-emerald-950 dark:aria-pressed:text-emerald-200"
                                    onClick={() => { setKind(value); setSelected({}); }}>
                                    {t('transactionManagement.' + (value ? value.toLowerCase() : 'all'))}
                                </button>)}
                            </div>
                        </fieldset>
                    </div>
                    <label className="mt-4 flex min-h-11 items-center gap-3 rounded-xl border border-emerald-300/40 bg-emerald-50 p-3 text-sm font-semibold dark:bg-emerald-950/30">
                        <input type="checkbox" className="size-4 accent-emerald-500" checked={personalMoneyOnly}
                            disabled={personalMoney.isPending || personalMoney.isError}
                            onChange={event => { setPersonalMoneyOnly(event.target.checked); setSelected({}); }} />
                        {t('personalMoney.filterOnly')}
                    </label>
                    <div className="mt-5 border-t border-slate-200 pt-4 dark:border-slate-700">
                        <LabelFilters userId={userId} householdId={householdId} value={labelIds}
                            onChange={labels => onUrlChange({ labels: labels.length ? labels : undefined })} />
                    </div>
                    {invalidRange && <p role="alert" className="mt-3 text-red-600 dark:text-red-300">{t('transactionLinks.invalidRange')}</p>}
                </AverioDialog>}

                <div
                    className="av-search-summary"
                    aria-label={t('transactionManagement.filterSummary')}
                >
                    <div className="av-search-selection">
                        <span className="av-badge">{selectedMonthLabel}</span>
                        {selectedAccountName && (
                            <span className="av-badge">
                                {selectedAccountName}
                            </span>
                        )}
                        {personalMoneyOnly && <span className="av-badge">{t('personalMoney.filterOnly')}</span>}
                        {kind && (
                            <span
                                className={
                                    'av-badge av-tx-filter-kind-badge '
                                    + kind.toLowerCase()
                                }
                            >
                                {t(kind === 'Income'
                                    ? 'transactionManagement.income'
                                    : 'transactionManagement.expense')}
                            </span>
                        )}
                    </div>

                    {hasCriteria && (
                        <button
                            type="button"
                            className="av-search-reset"
                            onClick={resetFilters}
                        >
                            {t('transactionManagement.reset')}
                        </button>
                    )}
                </div>
            </div>

            <SavedViews userId={userId} householdId={householdId} state={urlState} invalid={invalidRange}
                onApply={filters => { setSearch(''); setSelected({}); return onUrlChange(filters); }} />

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

            {!invalidRange && transactions.isSuccess && items.length === 0 && (
                <div className="av-card av-tx-empty">
                    <ConceptIcon name="transactions" />
                    <p>
                        {t(hasCriteria
                            ? 'transactionManagement.noResults'
                            : 'transactionManagement.empty')}
                    </p>
                </div>
            )}

            {transactions.isSuccess && items.length > 0 && <div className="flex flex-wrap items-center gap-3">
                <AverioButton onClick={() => { setSelecting(!selecting); setSelected({}); }}>
                    {t(selecting ? 'common.actions.cancel' : 'activities.selectTransactions')}</AverioButton>
                {selecting && <><span className="text-sm">{t('activities.selected', { count: Object.keys(selected).length })}</span>
                    <AverioButton disabled={Object.keys(selected).length === 0} onClick={() => setAssigning(true)}>{t('activities.assign')}</AverioButton></>}
            </div>}
            {assigning && <AssignActivityDialog userId={userId} householdId={householdId}
                transactions={Object.entries(selected).map(([transactionId, expectedVersion]) => ({ transactionId, expectedVersion }))}
                onClose={() => setAssigning(false)} onSaved={() => { setAssigning(false); setSelected({}); setSelecting(false); setNotice(t('activities.assigned')); }} />}
            {personalMoneyOnly && personalMoney.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('personalMoney.loadError')}</p>}
            {personalMoneyOnly && personalMoney.isSuccess && items.length > 0 && visibleItems.length === 0 && (
                <p role="status" className="rounded-xl border border-dashed border-slate-300 p-4 text-sm dark:border-slate-700">
                    {t('personalMoney.noLoadedRelations')}
                </p>
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
                            transaction.kind === 'Income';

                        const title =
                            transaction.merchant
                            ?? transaction.description;

                        return (
                            <div key={transaction.id}>
                                {selecting && transaction.status === 'Posted' && <label className="flex min-h-11 items-center gap-2 text-sm">
                                    <input type="checkbox" checked={transaction.id in selected}
                                        disabled={!(transaction.id in selected) && Object.keys(selected).length >= 100}
                                        onChange={event => setSelected(current => {
                                            const next = { ...current };
                                            if (event.target.checked) next[transaction.id] = transaction.activityAssignmentVersion ?? emptyAssignmentVersion;
                                            else delete next[transaction.id];
                                            return next;
                                        })} />{t('activities.selectTransaction', { name: title })}
                                </label>}
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
                                    <div data-transaction-id={transaction.id} className={
                                            `av-tx-bubble ${
                                                transaction.status === 'Voided'
                                                    ? 'voided'
                                                    : ''
                                            }`
                                        } >
                                    <button type="button" className="block w-full cursor-pointer text-left text-inherit focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary-500" title={
                                            transaction.canModify && transaction.status !== 'Voided'
                                                ? t('transactionManagement.editHint')
                                                : t('transactionLinks.details')
                                        }
                                        onClick={() =>
                                            openTransaction(transaction)}
                                    >
                                        <span className="av-tx-bubble-top">
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

            {invalidRange && !filtersOpen && <p role="alert">{t('transactionLinks.invalidRange')}</p>}
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
                query={detail} onClose={closeEditor}
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

