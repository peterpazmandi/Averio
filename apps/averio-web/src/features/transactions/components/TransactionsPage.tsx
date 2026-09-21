import { useMemo, useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { format, endOfMonth, startOfMonth, subMonths } from 'date-fns';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { getTransactions } from '../api/transactionApi';
import type { TransactionDto, TransactionFilters, TransactionInputDto, TransactionKind } from '../api/transactionApi';
import { transactionQueryKeys } from '../api/transactionQueries';
import { useTransactionMutations } from '../api/useTransactionMutations';
import { TransactionEditor } from './TransactionEditor';
import '../transactions.css';

export function TransactionsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const [search, setSearch] = useState('');
    const [accountId, setAccountId] = useState('');
    const [kind, setKind] = useState<'' | TransactionKind>('');
    const [month, setMonth] = useState(format(new Date(), 'yyyy-MM'));
    const [editor, setEditor] = useState<TransactionDto | 'new' | null>(null);
    const [notice, setNotice] = useState('');
    const accounts = useQuery(accountsQueries.list(userId, householdId, false));
    const mutations = useTransactionMutations(userId, householdId);
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.void.isPending;

    const monthOptions = useMemo(() => Array.from({ length: 12 }, (_, index) => {
        const date = subMonths(new Date(), index);
        return {
            value: format(date, 'yyyy-MM'),
            label: new Intl.DateTimeFormat(language, { month: 'long', year: 'numeric' }).format(date),
        };
    }), [language]);

    const filters = useMemo<TransactionFilters>(() => {
        const result: TransactionFilters = {
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
    }, [accountId, kind, month, search]);

    const transactions = useInfiniteQuery({
        queryKey: transactionQueryKeys.list(userId, householdId, filters),
        initialPageParam: undefined as { cursorDate: string; cursorCreatedAt: string } | undefined,
        queryFn: ({ pageParam, signal }) => getTransactions(
            householdId,
            { ...filters, ...pageParam },
            signal,
        ),
        getNextPageParam: page => {
            if (!page.hasMore || page.items.length === 0) return undefined;
            const last = page.items.at(-1)!;
            return { cursorDate: last.occurredOn, cursorCreatedAt: last.createdAt };
        },
    });

    const items = transactions.data?.pages.flatMap(page => page.items) ?? [];
    const formatter = new Intl.DateTimeFormat(language, {
        weekday: 'short', year: 'numeric', month: 'short', day: 'numeric',
        timeZone: 'UTC',
    });

    async function save(request: TransactionInputDto, expectedUpdatedAt?: string) {
        if (editor === 'new') {
            await mutations.create.mutateAsync(request);
            setNotice(t('transactionManagement.created'));
        } else if (editor && expectedUpdatedAt) {
            await mutations.update.mutateAsync({
                id: editor.id,
                request: { ...request, expectedUpdatedAt },
            });
            setNotice(t('transactionManagement.edited'));
        }
        setEditor(null);
    }

    async function voidCurrent() {
        if (!editor || editor === 'new') return;
        await mutations.void.mutateAsync({
            id: editor.id,
            expectedUpdatedAt: editor.updatedAt,
        });
        setNotice(t('transactionManagement.voidedNotice'));
        setEditor(null);
    }

    let previousDate = '';
    return (
        <section className="av-tx-page" aria-labelledby="transactions-title">
            <header className="av-page-head">
                <div>
                    <h2 id="transactions-title">{t('transactionManagement.title')}</h2>
                    <p>{t('transactionManagement.description')}</p>
                </div>
                <button type="button" className="av-btn av-primary" onClick={() => setEditor('new')}>
                    <ConceptIcon name="plus" />{t('transactionManagement.add')}
                </button>
            </header>

            <div className="av-tx-toolbar" role="search">
                <label className="av-tx-search">
                    <ConceptIcon name="search" />
                    <span className="sr-only">{t('transactionManagement.search')}</span>
                    <input type="search" value={search}
                        onChange={event => setSearch(event.target.value)}
                        placeholder={t('transactionManagement.searchPlaceholder')} />
                </label>
                <label>
                    <span className="sr-only">{t('transactionManagement.filterMonth')}</span>
                    <select className="av-select" value={month} onChange={event => setMonth(event.target.value)}>
                        <option value="">{t('transactionManagement.allDates')}</option>
                        {monthOptions.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
                    </select>
                </label>
                <label>
                    <span className="sr-only">{t('transactionManagement.account')}</span>
                    <select className="av-select" value={accountId} onChange={event => setAccountId(event.target.value)}>
                        <option value="">{t('transactionManagement.allAccounts')}</option>
                        {(accounts.data ?? []).map(account => <option key={account.id} value={account.id}>{account.name}</option>)}
                    </select>
                </label>
                <div className="av-tx-segmented" aria-label={t('transactionManagement.type')}>
                    <button type="button" className={!kind ? 'active' : ''} onClick={() => setKind('')}>
                        {t('transactionManagement.all')}
                    </button>
                    <button type="button" className={kind === 'Income' ? 'active' : ''} onClick={() => setKind('Income')}>
                        {t('transactionManagement.income')}
                    </button>
                    <button type="button" className={kind === 'Expense' ? 'active' : ''} onClick={() => setKind('Expense')}>
                        {t('transactionManagement.expense')}
                    </button>
                </div>
                {(search || accountId || kind || !month) && (
                    <button type="button" className="av-btn" onClick={() => {
                        setSearch('');
                        setAccountId('');
                        setKind('');
                        setMonth(format(new Date(), 'yyyy-MM'));
                    }}>{t('transactionManagement.reset')}</button>
                )}
            </div>

            {notice && <p role="status" className="av-tx-notice">{notice}</p>}
            {transactions.isPending && <p role="status">{t('common.status.loading')}</p>}
            {transactions.isError && (
                <div role="alert">
                    <p className="av-error">{t('errors.genericDescription')}</p>
                    <button className="av-btn" onClick={() => { void transactions.refetch(); }}>
                        {t('common.actions.retry')}
                    </button>
                </div>
            )}

            {transactions.isSuccess && items.length === 0 && (
                <div className="av-card av-tx-empty">
                    <ConceptIcon name="transactions" />
                    <p>{t(search || accountId || kind ? 'transactionManagement.noResults' : 'transactionManagement.empty')}</p>
                </div>
            )}

            {items.length > 0 && (
                <div className="av-timeline" aria-label={t('transactionManagement.title')}>
                    {items.map(transaction => {
                        const showDate = previousDate !== transaction.occurredOn;
                        previousDate = transaction.occurredOn;
                        const dateLabel = formatter.format(new Date(transaction.occurredOn + 'T00:00:00Z'));
                        const isIncome = transaction.kind === 'Income';
                        return (
                            <div key={transaction.id}>
                                {showDate && (
                                    <div className="av-timeline-date">
                                        <span>{dateLabel}</span>
                                    </div>
                                )}
                                <div className={`av-timeline-row ${isIncome ? 'income' : 'expense'}`}>
                                    <button type="button"
                                        className={`av-tx-bubble ${transaction.status === 'Voided' ? 'voided' : ''}`}
                                        disabled={!transaction.canModify}
                                        title={transaction.canModify ? t('transactionManagement.editHint') : undefined}
                                        onClick={() => setEditor(transaction)}>
                                        <span className="av-tx-bubble-top">
                                            <span className="av-tx-type-icon">
                                                {isIncome ? '↙' : '↗'}
                                            </span>
                                            <span className="av-tx-account">{transaction.accountName}</span>
                                            {transaction.visibility === 'Private' && (
                                                <span className="av-badge av-private-badge">
                                                    🔒 {t('transactionManagement.privateBadge')}
                                                </span>
                                            )}
                                        </span>
                                        <strong className="av-tx-description">{transaction.description}</strong>
                                        <strong className="av-tx-amount">
                                            {isIncome ? '+' : '−'}
                                            {formatDecimalAmount(transaction.amount, transaction.currency, language)}
                                        </strong>
                                        <span className="av-tx-bubble-foot">
                                            {transaction.note && <span>{transaction.note}</span>}
                                            <span>{t(transaction.status === 'Voided'
                                                ? 'transactionManagement.voided'
                                                : 'transactionManagement.posted')}</span>
                                        </span>
                                    </button>
                                    <span className="av-timeline-dot" aria-hidden="true" />
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {transactions.hasNextPage && (
                <div className="av-tx-load-more">
                    <button type="button" className="av-btn"
                        disabled={transactions.isFetchingNextPage}
                        onClick={() => { void transactions.fetchNextPage(); }}>
                        {t(transactions.isFetchingNextPage
                            ? 'transactionManagement.loadingMore'
                            : 'transactionManagement.loadMore')}
                    </button>
                </div>
            )}

            {editor && (
                <TransactionEditor
                    userId={userId}
                    householdId={householdId}
                    transaction={editor === 'new' ? null : editor}
                    onCancel={() => setEditor(null)}
                    onSave={save}
                    onVoid={editor === 'new' ? undefined : voidCurrent}
                    busy={busy}
                />
            )}
        </section>
    );
}
