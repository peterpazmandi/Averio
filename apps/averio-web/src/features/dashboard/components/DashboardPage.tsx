import { useState } from 'react';
import { PersonalMoneyCard } from './PersonalMoneyCard';
import { CategorySpendingCard } from './CategorySpendingCard';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import {
    formatDecimalAmount,
    subtractDecimalAmounts,
} from '../../../shared/money/decimalAmount';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import type { ConceptIconName } from '../../../shared/components/concept/ConceptIcon';
import { AccountOwnershipSummary } from '../../accounts/components/AccountOwnershipSummary';
import { dashboardQueries } from '../api/dashboardQueries';

interface DashboardPageProps { userId: string; householdId: string; }

interface DashboardCashFlowBarProps {
    id: string;
    tone: 'income' | 'expense';
    label: string;
    value: string;
    height: number;
    active: boolean;
    onToggle: () => void;
}

function DashboardCashFlowBar({
    id,
    tone,
    label,
    value,
    height,
    active,
    onToggle,
}: DashboardCashFlowBarProps) {
    return (
        <button
            type="button"
            className={
                `av-dashboard-bar ${tone}`
                + (active ? ' is-active' : '')
                + (height === 0 ? ' is-zero' : '')
            }
            style={{ height: `${height === 0 ? 2 : height}%` }}
            aria-label={`${label}: ${value}`}
            aria-pressed={active}
            aria-describedby={`${id}-tooltip`}
            onClick={onToggle}
        >
            <span
                id={`${id}-tooltip`}
                className="av-dashboard-bar-tooltip"
                role="tooltip"
            >
                <span>{label}</span>
                <strong>{value}</strong>
            </span>
        </button>
    );
}

export function DashboardPage({ userId, householdId }: DashboardPageProps) {
    const { t, i18n } = useTranslation();
    const [activeCashFlowBar, setActiveCashFlowBar] = useState<string | null>(null);
    const dashboardQuery = useQuery(dashboardQueries.household(userId, householdId));

    if (dashboardQuery.isPending) {
        return <p role="status">{t('common.status.loading')}</p>;
    }
    if (dashboardQuery.isError) {
        return (
            <AverioCard className="space-y-4">
                <p role="alert" className="av-error">{t('errors.genericDescription')}</p>
                <button type="button" className="av-btn" disabled={dashboardQuery.isFetching}
                    onClick={() => { void dashboardQuery.refetch(); }}>
                    {t('common.actions.retry')}
                </button>
            </AverioCard>
        );
    }

    const dashboard = dashboardQuery.data;
    const language = i18n.resolvedLanguage ?? i18n.language;
    const money = (amount: string, currency: string) =>
        formatDecimalAmount(amount, currency, language);
    const date = (value: string) => new Intl.DateTimeFormat(language, {
        dateStyle: 'medium', timeZone: 'UTC',
    }).format(new Date(value + 'T00:00:00Z'));
    const monthLabel = (value: string) => new Intl.DateTimeFormat(language, {
        month: 'short', timeZone: 'UTC',
    }).format(new Date(value + 'T00:00:00Z'));
    const reportedAccounts = dashboard.accounts.filter(account =>
        account.includeInHouseholdReports && account.visibility !== 'Private');

    const cashFlowByCurrency = new Map<string, typeof dashboard.cashFlow>();
    for (const period of dashboard.cashFlow) {
        const list = cashFlowByCurrency.get(period.currency) ?? [];
        list.push(period);
        cashFlowByCurrency.set(period.currency, list);
    }

    const now = new Date();
    const monthKey = [
        now.getUTCFullYear(),
        String(now.getUTCMonth() + 1).padStart(2, '0'),
        '01',
    ].join('-');
    const currentMonth = dashboard.cashFlow.filter(period => period.periodStart === monthKey);

    function emptyCard(
        name: 'goals',
        icon: ConceptIconName,
    ) {
        return (
            <AverioCard className="av-span-4" aria-label={t('concept.' + name + 'Title')}>
                <div className="av-card-head">
                    <div>
                        <h3>{t('concept.' + name + 'Title')}</h3>
                        <p>{t('concept.' + name + 'Help')}</p>
                    </div>
                    <span className="av-badge">{t('concept.soon')}</span>
                </div>
                <div className="av-empty">
                    <ConceptIcon name={icon} />
                    <p>{t('concept.' + name + 'Empty')}</p>
                </div>
            </AverioCard>
        );
    }

    return (
        <div>
            <header className="av-page-head">
                <div>
                    <h2>{dashboard.displayName
                        ? t('dashboard.greeting', { name: dashboard.displayName })
                        : t('dashboard.title')}</h2>
                    <p>{t('concept.intro')}</p>
                </div>
                <div className="av-page-actions">
                    <Link className="av-btn" to="/app/statistics">
                        {t('concept.viewReport')}
                    </Link>
                    <Link className="av-btn av-primary" to="/app/transactions"
                        search={{ create: true }}>
                        <ConceptIcon name="plus" />{t('concept.addTransaction')}
                    </Link>
                </div>
            </header>

            <div className="av-hero">
                <span className="av-hero-icon"><ConceptIcon name="check" /></span>
                <div>
                    <h3>{t('concept.setupTitle')}</h3>
                    <p>{t('concept.setupDescription')}</p>
                </div>
                <Link className="av-btn" to="/app/accounts">{t('dashboard.manageAccounts')}</Link>
            </div>

            <div className="av-bento">
                <AverioCard className="col-span-12 space-y-4 xl:col-span-4" aria-label={t('concept.balanceTitle')}>
                    <h3 className="text-sm font-semibold text-slate-600 dark:text-slate-300">{t('concept.balanceTitle')}</h3>
                    {dashboard.householdBalances.length === 0 ? (
                        <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">{t('dashboard.noReportedAccounts')}</p>
                    ) : dashboard.householdBalances.map(balance => (
                        <div className="space-y-1" key={balance.currency}>
                            <p className="break-words text-3xl font-bold tracking-tight">{money(balance.amount, balance.currency)}</p>
                            <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">
                                {t('accountManagement.currentBalance')}
                            </p>
                        </div>
                    ))}
                    <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">{t('concept.balanceHelp')}</p>
                    <div className="space-y-3 border-t border-slate-200 pt-4 dark:border-slate-700">
                        {reportedAccounts.slice(0, 3).map(account => (
                            <div key={account.id} className="flex flex-wrap items-center gap-2 text-sm [&_strong]:block [&_small]:text-slate-500 dark:[&_small]:text-slate-400">
                                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"><ConceptIcon name="accounts" /></span>
                                <div className="min-w-0 flex-1">
                                    <strong className="break-words">{account.name}</strong>
                                    <small>{t(account.scope === 'Household'
                                        ? 'accountOwnership.household' : 'accountOwnership.personal')} · {account.currency}</small>
                                </div>
                                <span className="ml-auto break-words font-semibold">{money(account.currentBalanceAmount, account.currency)}</span>
                            </div>
                        ))}
                    </div>
                </AverioCard>

                <AverioCard className="av-span-8">
                    <div className="av-card-head">
                        <div>
                            <h3>{t('transactionManagement.dashboardCashFlow')}</h3>
                            <p>{t('transactionManagement.dashboardCashFlowHelp')}</p>
                        </div>
                        <Link className="av-btn" to="/app/statistics">{t('concept.viewReport')}</Link>
                    </div>
                    {cashFlowByCurrency.size === 0 ? (
                        <div className="av-empty av-chart-empty">
                            <ConceptIcon name="statistics" />
                            <p>{t('transactionManagement.noCashFlow')}</p>
                        </div>
                    ) : (
                        <div className="av-dashboard-cashflows">
                            {[...cashFlowByCurrency.entries()].map(([currency, periods]) => {
                                const max = Math.max(1, ...periods.flatMap(period => [
                                    Number(period.incomeAmount),
                                    Number(period.expenseAmount),
                                ]));
                                return (
                                    <div className="av-dashboard-cashflow" key={currency}>
                                        <div className="av-dashboard-chart-title">
                                            <span className="av-badge">{currency}</span>
                                            <span>{t('transactionManagement.reportingNote')}</span>
                                        </div>
                                        <div className="av-dashboard-bars">
                                            {periods.map(period => {
                                                const month = monthLabel(
                                                    period.periodStart,
                                                );
                                                const income = Number(
                                                    period.incomeAmount,
                                                );
                                                const expense = Number(
                                                    period.expenseAmount,
                                                );
                                                const incomeId =
                                                    `dashboard-${currency}-${period.periodStart}-income`;
                                                const expenseId =
                                                    `dashboard-${currency}-${period.periodStart}-expense`;

                                                return (
                                                    <div
                                                        className="av-dashboard-month"
                                                        key={period.periodStart}
                                                    >
                                                        <div className="av-dashboard-columns">
                                                            <DashboardCashFlowBar
                                                                id={incomeId}
                                                                tone="income"
                                                                label={
                                                                    `${month} · ${
                                                                        t('statistics.income')
                                                                    }`
                                                                }
                                                                value={money(
                                                                    period.incomeAmount,
                                                                    currency,
                                                                )}
                                                                height={
                                                                    income === 0
                                                                        ? 0
                                                                        : Math.max(
                                                                            4,
                                                                            income / max * 100,
                                                                        )
                                                                }
                                                                active={
                                                                    activeCashFlowBar
                                                                    === incomeId
                                                                }
                                                                onToggle={() =>
                                                                    setActiveCashFlowBar(
                                                                        current =>
                                                                            current
                                                                            === incomeId
                                                                                ? null
                                                                                : incomeId,
                                                                    )}
                                                            />

                                                            <DashboardCashFlowBar
                                                                id={expenseId}
                                                                tone="expense"
                                                                label={
                                                                    `${month} · ${
                                                                        t('statistics.expenses')
                                                                    }`
                                                                }
                                                                value={money(
                                                                    period.expenseAmount,
                                                                    currency,
                                                                )}
                                                                height={
                                                                    expense === 0
                                                                        ? 0
                                                                        : Math.max(
                                                                            4,
                                                                            expense / max * 100,
                                                                        )
                                                                }
                                                                active={
                                                                    activeCashFlowBar
                                                                    === expenseId
                                                                }
                                                                onToggle={() =>
                                                                    setActiveCashFlowBar(
                                                                        current =>
                                                                            current
                                                                            === expenseId
                                                                                ? null
                                                                                : expenseId,
                                                                    )}
                                                            />
                                                        </div>
                                                        <small>{month}</small>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                    <div className="av-chart-legend">
                        <span><i className="income" />{t('statistics.income')}</span>
                        <span><i className="expense" />{t('statistics.expenses')}</span>
                    </div>
                </AverioCard>

                <AverioCard className="av-span-4" aria-label={t('transactionManagement.dashboardMonth')}>
                    <div className="av-card-head">
                        <div>
                            <h3>{t('transactionManagement.dashboardMonth')}</h3>
                            <p>{t('concept.monthHelp')}</p>
                        </div>
                    </div>
                    {currentMonth.length === 0 ? (
                        <p className="av-subtle">{t('transactionManagement.noCashFlow')}</p>
                    ) : (
                        <div className="av-dashboard-month-totals">
                            {currentMonth.map(period => {
                                const net = subtractDecimalAmounts(
                                    period.incomeAmount,
                                    period.expenseAmount,
                                );
                                return (
                                    <div key={period.currency} className="av-dashboard-currency-total">
                                        <span className="av-badge">{period.currency}</span>
                                        <dl className="av-month-grid">
                                            <div>
                                                <dt className="av-label">{t('statistics.income')}</dt>
                                                <dd className="av-kpi">{money(period.incomeAmount, period.currency)}</dd>
                                            </div>
                                            <div>
                                                <dt className="av-label">{t('statistics.expenses')}</dt>
                                                <dd className="av-kpi">{money(period.expenseAmount, period.currency)}</dd>
                                            </div>
                                            <div>
                                                <dt className="av-label">{t('transactionManagement.net')}</dt>
                                                <dd className="av-kpi">{money(net, period.currency)}</dd>
                                            </div>
                                            <div>
                                                <dt className="av-label">{t('navigation.transactions')}</dt>
                                                <dd className="av-kpi">{period.transactionCount}</dd>
                                            </div>
                                        </dl>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </AverioCard>

                <CategorySpendingCard key={`${userId}:${householdId}`} userId={userId} householdId={householdId} reportingCurrency={dashboard.reportingCurrency} />
                {emptyCard('goals', 'goals')}

                <AverioCard className="av-span-8">
                    <div className="av-card-head">
                        <div>
                            <h3>{t('transactionManagement.latest')}</h3>
                            <p>{t('concept.transactionsHelp')}</p>
                        </div>
                        <Link
                            className="av-btn"
                            to="/app/transactions"
                            search={{ create: undefined }}
                        >
                            {t('transactionManagement.viewAll')}
                        </Link>
                    </div>
                    {dashboard.latestTransactions.length === 0 ? (
                        <div className="av-empty">
                            <ConceptIcon name="transactions" />
                            <p>{t('transactionManagement.empty')}</p>
                        </div>
                    ) : (
                        <div className="av-list">
                            {dashboard.latestTransactions.map(transaction => (
                                <Link
                                    key={transaction.id}
                                    to="/app/transactions"
                                    search={{ transaction: transaction.id }}
                                    className="av-list-row"
                                >
                                    <span className="av-tx-icon">
                                        {transaction.kind === 'Income' ? '↙' : '↗'}
                                    </span>
                                    <span className="av-row-title">
                                        <strong>
                                            {transaction.visibility === 'Private' ? '🔒 ' : ''}
                                            {transaction.description}
                                        </strong>
                                        <span>{transaction.accountName} · {date(transaction.occurredOn)}</span>
                                    </span>
                                    <span className={transaction.kind === 'Income'
                                        ? 'av-amount-pos' : 'av-amount-neg'}>
                                        {transaction.kind === 'Income' ? '+' : '−'}
                                        {money(transaction.amount, transaction.currency)}
                                    </span>
                                </Link>
                            ))}
                        </div>
                    )}
                </AverioCard>

                <PersonalMoneyCard key={`${userId}:${householdId}`} userId={userId} householdId={householdId} />

                <AverioCard className="av-span-12" aria-labelledby="dashboard-accounts-title">
                    <div className="av-card-head">
                        <div>
                            <h3 id="dashboard-accounts-title">{t('dashboard.accountsTitle')}</h3>
                            <p>{t('dashboard.recordedBalanceHelp')}</p>
                        </div>
                        <Link to="/app/accounts" className="av-btn">{t('dashboard.manageAccounts')}</Link>
                    </div>
                    {dashboard.accounts.length === 0 ? (
                        <p className="av-subtle">{t('dashboard.noActiveAccounts')}</p>
                    ) : (
                        <div className="av-account-list">
                            {dashboard.accounts.map(account => (
                                <article key={account.id} className="av-account-row">
                                    <h4><Link to="/app/transactions" search={{ account: account.id, dates: 'all' }} className="rounded underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-primary-500">{account.name}</Link></h4>
                                    <p className="av-kpi">{money(account.currentBalanceAmount, account.currency)}</p>
                                    <p className="av-subtle">
                                        {t('accountManagement.openingBalance')}: {money(account.balanceAnchorAmount, account.currency)}
                                        {' · '}{date(account.balanceAnchorDate)}
                                    </p>
                                    <AccountOwnershipSummary account={account} />
                                </article>
                            ))}
                        </div>
                    )}
                </AverioCard>
            </div>
        </div>
    );
}
