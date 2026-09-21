import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import type { ConceptIconName } from '../../../shared/components/concept/ConceptIcon';
import { AccountOwnershipSummary } from '../../accounts/components/AccountOwnershipSummary';
import { dashboardQueries } from '../api/dashboardQueries';

interface DashboardPageProps { userId: string; householdId: string; }

export function DashboardPage({ userId, householdId }: DashboardPageProps) {
    const { t, i18n } = useTranslation();
    const dashboardQuery = useQuery(dashboardQueries.household(userId, householdId));

    if (dashboardQuery.isPending) {
        return <p role="status">{t('common.status.loading')}</p>;
    }
    if (dashboardQuery.isError) {
        return (
            <section className="av-card space-y-4">
                <p role="alert" className="av-error">{t('errors.genericDescription')}</p>
                <button type="button" className="av-btn" disabled={dashboardQuery.isFetching}
                    onClick={() => { void dashboardQuery.refetch(); }}>
                    {t('common.actions.retry')}
                </button>
            </section>
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
        name: 'category' | 'goals' | 'personal',
        icon: ConceptIconName,
    ) {
        return (
            <section className="av-card av-span-4" aria-label={t('concept.' + name + 'Title')}>
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
            </section>
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
                <section className="av-card av-balance av-span-4" aria-label={t('concept.balanceTitle')}>
                    <h3 className="av-label">{t('concept.balanceTitle')}</h3>
                    {dashboard.householdBalances.length === 0 ? (
                        <p className="av-subtle">{t('dashboard.noReportedAccounts')}</p>
                    ) : dashboard.householdBalances.map(balance => (
                        <div className="av-balance-group" key={balance.currency}>
                            <p className="av-kpi">{money(balance.amount, balance.currency)}</p>
                            <p className="av-subtle">
                                {balance.earliestBalanceDate === balance.latestBalanceDate
                                    ? t('concept.recordedOn', { date: date(balance.latestBalanceDate) })
                                    : t('concept.recordedBetween', {
                                        from: date(balance.earliestBalanceDate),
                                        to: date(balance.latestBalanceDate),
                                    })}
                            </p>
                        </div>
                    ))}
                    <p className="av-subtle">{t('concept.balanceHelp')}</p>
                    <div className="av-mini-accounts">
                        {reportedAccounts.slice(0, 3).map(account => (
                            <div key={account.id} className="av-mini-account">
                                <span className="av-account-icon"><ConceptIcon name="accounts" /></span>
                                <div>
                                    <strong>{account.name}</strong>
                                    <small>{t(account.scope === 'Household'
                                        ? 'accountOwnership.household' : 'accountOwnership.personal')} · {account.currency}</small>
                                </div>
                                <span className="av-mini-amount">{money(account.balanceAnchorAmount, account.currency)}</span>
                            </div>
                        ))}
                    </div>
                </section>

                <section className="av-card av-span-8">
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
                                            {periods.map(period => (
                                                <div className="av-dashboard-month" key={period.periodStart}>
                                                    <div className="av-dashboard-columns">
                                                        <span className="income"
                                                            title={money(period.incomeAmount, currency)}
                                                            style={{ height: `${Number(period.incomeAmount) === 0 ? 0 : Math.max(4, Number(period.incomeAmount) / max * 100)}%` }} />
                                                        <span className="expense"
                                                            title={money(period.expenseAmount, currency)}
                                                            style={{ height: `${Number(period.expenseAmount) === 0 ? 0 : Math.max(4, Number(period.expenseAmount) / max * 100)}%` }} />
                                                    </div>
                                                    <small>{monthLabel(period.periodStart)}</small>
                                                </div>
                                            ))}
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
                </section>

                <section className="av-card av-span-4" aria-label={t('transactionManagement.dashboardMonth')}>
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
                                const net = Number(period.incomeAmount) - Number(period.expenseAmount);
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
                                                <dd className="av-kpi">{money(String(net), period.currency)}</dd>
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
                </section>

                {emptyCard('category', 'categories')}
                {emptyCard('goals', 'goals')}

                <section className="av-card av-span-8">
                    <div className="av-card-head">
                        <div>
                            <h3>{t('transactionManagement.latest')}</h3>
                            <p>{t('concept.transactionsHelp')}</p>
                        </div>
                        <Link className="av-btn" to="/app/transactions">
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
                                <Link key={transaction.id} to="/app/transactions" className="av-list-row">
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
                </section>

                {emptyCard('personal', 'profile')}

                <section className="av-card av-span-12" aria-labelledby="dashboard-accounts-title">
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
                                    <h4>{account.name}</h4>
                                    <p className="av-kpi">{money(account.balanceAnchorAmount, account.currency)}</p>
                                    <p className="av-subtle">{t('concept.recordedOn', { date: date(account.balanceAnchorDate) })}</p>
                                    <AccountOwnershipSummary account={account} />
                                </article>
                            ))}
                        </div>
                    )}
                </section>
            </div>
        </div>
    );
}
