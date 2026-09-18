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
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, language);
    const date = (value: string) => new Intl.DateTimeFormat(language, {
        dateStyle: 'medium', timeZone: 'UTC',
    }).format(new Date(value + 'T00:00:00Z'));
    const reportedAccounts = dashboard.accounts.filter((account) =>
        account.includeInHouseholdReports && account.visibility !== 'Private');

    function emptyCard(name: 'cashFlow' | 'category' | 'goals' | 'transactions' | 'personal',
        icon: ConceptIconName, wide = false) {
        return (
            <section className={'av-card ' + (wide ? 'av-span-8' : 'av-span-4')}
                aria-label={t('concept.' + name + 'Title')}>
                <div className="av-card-head">
                    <div>
                        <h3>{t('concept.' + name + 'Title')}</h3>
                        <p>{t('concept.' + name + 'Help')}</p>
                    </div>
                    <span className="av-badge">{t('concept.soon')}</span>
                </div>
                <div className={'av-empty' + (name === 'cashFlow' ? ' av-chart-empty' : '')}>
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
                    <button type="button" className="av-btn" disabled title={t('concept.soon')}>
                        {t('concept.viewReport')}
                    </button>
                    <button type="button" className="av-btn av-primary" disabled
                        title={t('concept.transactionsEmpty')}>
                        <ConceptIcon name="plus" />{t('concept.addTransaction')}
                    </button>
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
                    ) : dashboard.householdBalances.map((balance) => (
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
                        {reportedAccounts.slice(0, 3).map((account) => (
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

                {emptyCard('cashFlow', 'statistics', true)}

                <section className="av-card av-span-4" aria-label={t('concept.monthTitle')}>
                    <div className="av-card-head">
                        <div><h3>{t('concept.monthTitle')}</h3><p>{t('concept.monthHelp')}</p></div>
                        <span className="av-badge">{t('concept.soon')}</span>
                    </div>
                    <dl className="av-month-grid">
                        {(['income', 'expenses', 'netCashFlow', 'upcoming'] as const).map((key) => (
                            <div key={key}>
                                <dt className="av-label">{t('concept.' + key)}</dt>
                                <dd className="av-kpi" aria-label={t('concept.notAvailable')}>—</dd>
                            </div>
                        ))}
                    </dl>
                    <p className="av-subtle">{t('concept.cashFlowEmpty')}</p>
                </section>

                {emptyCard('category', 'categories')}
                {emptyCard('goals', 'goals')}
                {emptyCard('transactions', 'transactions', true)}
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
                            {dashboard.accounts.map((account) => (
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
