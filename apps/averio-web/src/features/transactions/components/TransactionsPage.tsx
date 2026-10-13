import { Link } from '@tanstack/react-router';
import { ActivitySelect } from '../../activities/ActivitySelect';
import { AssignActivityDialog } from '../../activities/AssignActivityDialog';
import { emptyAssignmentVersion } from '../../activities/activityQueries';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { useMemo, useRef, useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { endOfMonth, format, startOfMonth, subMonths } from 'date-fns';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { getTransactions } from '../api/transactionApi';
import type {
    TransactionDto,
    TransactionFilters,
    TransactionInputDto,
    TransactionKind,
} from '../api/transactionApi';
import { transactionQueryKeys } from '../api/transactionQueries';
import { useTransactionMutations } from '../api/useTransactionMutations';
import { TransactionEditor } from './TransactionEditor';
import '../transactions.css';

interface Props {
    userId: string;
    householdId: string;
    createRequested?: boolean;
    initialKind?: TransactionKind;
    initialActivityId?: string;
    onCreateRequestHandled?: () => void;
}

export function TransactionsPage({
    userId,
    householdId,
    createRequested = false,
    initialKind,
    initialActivityId,
    onCreateRequestHandled,
}: Props) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const defaultMonth = format(new Date(), 'yyyy-MM');

    const [search, setSearch] = useState('');
    const [activityId, setActivityId] = useState(initialActivityId ?? '');
    const [selecting, setSelecting] = useState(false);
    const [selected, setSelected] = useState<Record<string, string>>({});
    const [assigning, setAssigning] = useState(false);
    const [accountId, setAccountId] = useState('');
    const [kind, setKind] = useState<'' | TransactionKind>('');
    const [month, setMonth] = useState(initialActivityId ? '' : defaultMonth);
    const [filtersOpen, setFiltersOpen] = useState(false);
    const [editor, setEditor] = useState<TransactionDto | 'new' | null>(null);
    const [notice, setNotice] = useState('');

    const searchInput = useRef<HTMLInputElement>(null);
    const filterToggle = useRef<HTMLButtonElement>(null);

    const accounts = useQuery(
        accountsQueries.list(userId, householdId, false),
    );
    const mutations = useTransactionMutations(userId, householdId);
    const busy = mutations.create.isPending
        || mutations.update.isPending
        || mutations.void.isPending;
    const activeEditor = createRequested ? 'new' : editor;

    const monthOptions = useMemo(
        () => Array.from({ length: 12 }, (_, index) => {
            const date = subMonths(new Date(), index);
            return {
                value: format(date, 'yyyy-MM'),
                label: new Intl.DateTimeFormat(language, {
                    month: 'long',
                    year: 'numeric',
                }).format(date),
            };
        }),
        [language],
    );

    const selectedMonthLabel = month
        ? monthOptions.find(option => option.value === month)?.label ?? month
        : t('transactionManagement.allDates');

    const selectedAccountName = accountId
        ? accounts.data?.find(account => account.id === accountId)?.name
        : undefined;

    const filterCount = Number(accountId !== '')
        + Number(kind !== '')
        + Number(month !== defaultMonth) + Number(activityId !== '');

    const hasCriteria = search.trim().length > 0 || filterCount > 0;

    const filters = useMemo<TransactionFilters>(() => {
        const result: TransactionFilters = {
            activityId: activityId || undefined,
            accountId: accountId || undefined,
            kind: kind || undefined,
            search: search.trim() || undefined,
            pageSize: 40,
        };

        if (month) {
            const date = new Date(month + '-01T12:00:00');
            result.from = format(startOfMonth(date), 'yyyy-MM-dd');
            result.to = format(endOfMonth(date), 'yyyy-MM-dd');
        }

        return result;
    }, [accountId, kind, month, search, activityId]);

    const transactions = useInfiniteQuery({
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

    const items = transactions.data?.pages.flatMap(page => page.items) ?? [];

    const formatter = new Intl.DateTimeFormat(language, {
        weekday: 'short',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        timeZone: 'UTC',
    });

    function resetFilters() {
        setSearch('');
        setActivityId('');
        setSelected({});
        setAccountId('');
        setKind('');
        setMonth(defaultMonth);
        searchInput.current?.focus();
    }

    function closeEditor() {
        setEditor(null);
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

    async function voidCurrent() {
        if (!activeEditor || activeEditor === 'new') return;

        await mutations.void.mutateAsync({
            id: activeEditor.id,
            expectedUpdatedAt: activeEditor.updatedAt,
        });

        setNotice(t('transactionManagement.voidedNotice'));
        closeEditor();
    }

    return (
        <section className="av-tx-page" aria-labelledby="transactions-title">
            <header className="av-page-head">
                <div>
                    <h2 id="transactions-title">
                        {t('transactionManagement.title')}
                    </h2>
                    <p>{t('transactionManagement.description')}</p>
                </div>
                <button
                    type="button"
                    className="av-btn av-primary"
                    onClick={() => setEditor('new')}
                >
                    <ConceptIcon name="plus" />
                    {t('transactionManagement.add')}
                </button>
            </header>

            <Link to="/app/transfers" className="inline-flex min-h-11 items-center justify-center gap-2 rounded-[11px] border border-[#E4E7EC] bg-white px-3 py-2 text-xs font-semibold text-[#152033] hover:bg-[#F9FAFB] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] dark:hover:bg-[#111C2F]"><ConceptIcon name="transfers" />{t('transfers.title')}</Link>
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
                        aria-controls="transaction-filters"
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

                <div
                    id="transaction-filters"
                    className="av-search-panel"
                    hidden={!filtersOpen}
                >
                    <div className="av-tx-filter-grid">
                        <ActivitySelect userId={userId} householdId={householdId} value={activityId}
                            onChange={id => { setActivityId(id); setSelected({}); if (id) setMonth(''); }}
                            includeClosed allowCreate={false} emptyLabel={t('activities.all')} />
                        <label>
                            <span>{t('transactionManagement.filterMonth')}</span>
                            <select
                                className="av-select"
                                value={month}
                                onChange={event => { setMonth(event.target.value); setSelected({}); }}
                            >
                                <option value="">
                                    {t('transactionManagement.allDates')}
                                </option>
                                {monthOptions.map(option => (
                                    <option
                                        key={option.value}
                                        value={option.value}
                                    >
                                        {option.label}
                                    </option>
                                ))}
                            </select>
                        </label>

                        <label>
                            <span>{t('transactionManagement.account')}</span>
                            <select
                                className="av-select"
                                value={accountId}
                                onChange={event => { setAccountId(event.target.value); setSelected({}); }}
                            >
                                <option value="">
                                    {t('transactionManagement.allAccounts')}
                                </option>
                                {(accounts.data ?? []).map(account => (
                                    <option
                                        key={account.id}
                                        value={account.id}
                                    >
                                        {account.name}
                                    </option>
                                ))}
                            </select>
                        </label>

                        <fieldset className="av-tx-filter-kind">
                            <legend>{t('transactionManagement.type')}</legend>
                            <div className="av-tx-segmented">
                                <button
                                    type="button"
                                    className={!kind ? 'active' : ''}
                                    onClick={() => { setKind(''); setSelected({}); }}
                                >
                                    {t('transactionManagement.all')}
                                </button>
                                <button
                                    type="button"
                                    className={
                                        kind === 'Income'
                                            ? 'active income'
                                            : 'income'
                                    }
                                    onClick={() => { setKind('Income'); setSelected({}); }}
                                >
                                    {t('transactionManagement.income')}
                                </button>
                                <button
                                    type="button"
                                    className={
                                        kind === 'Expense'
                                            ? 'active expense'
                                            : 'expense'
                                    }
                                    onClick={() => { setKind('Expense'); setSelected({}); }}
                                >
                                    {t('transactionManagement.expense')}
                                </button>
                            </div>
                        </fieldset>
                    </div>

                    <button
                        type="button"
                        className="av-btn av-filter-done"
                        onClick={() => {
                            setFiltersOpen(false);
                            filterToggle.current?.focus();
                        }}
                    >
                        {t('transactionManagement.showResults')}
                    </button>
                </div>

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

            {notice && (
                <p role="status" className="av-tx-notice">
                    {notice}
                </p>
            )}

            {transactions.isPending && (
                <p role="status">{t('common.status.loading')}</p>
            )}

            {transactions.isError && (
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

            {transactions.isSuccess && items.length === 0 && (
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
            {items.length > 0 && (
                <div
                    className="av-timeline"
                    aria-label={t('transactionManagement.title')}
                >
                    {items.map((transaction, index) => {
                        const showDate =
                            index === 0
                            || items[index - 1]?.occurredOn
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
                                    <button
                                        type="button"
                                        className={
                                            `av-tx-bubble ${
                                                transaction.status === 'Voided'
                                                    ? 'voided'
                                                    : ''
                                            }`
                                        }
                                        disabled={!transaction.canModify}
                                        title={
                                            transaction.canModify
                                                ? t('transactionManagement.editHint')
                                                : undefined
                                        }
                                        onClick={() =>
                                            setEditor(transaction)}
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

            {transactions.hasNextPage && (
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

            {activeEditor && (
                <TransactionEditor
                    userId={userId}
                    householdId={householdId}
                    key={activeEditor === 'new' ? (initialKind ?? 'Expense') : activeEditor.id}
                    initialKind={initialKind}
                    transaction={activeEditor === 'new' ? null : activeEditor}
                    onCancel={closeEditor}
                    onSave={save}
                    onVoid={activeEditor === 'new'
                        ? undefined
                        : voidCurrent}
                    busy={busy}
                />
            )}
        </section>
    );
}
