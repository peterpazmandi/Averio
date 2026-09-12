import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { PrimaryButton } from '../../../shared/components/PrimaryButton';
import { dashboardQueries } from '../api/dashboardQueries';

interface DashboardPageProps {
    userId: string;
    householdId: string;
}

export function DashboardPage({
    userId,
    householdId,
}: DashboardPageProps) {
    const { t, i18n } = useTranslation();

    const dashboardQuery = useQuery(
        dashboardQueries.household(
            userId,
            householdId,
        ),
    );

    if (dashboardQuery.isPending) {
        return (
            <p role="status">
                {t('common.status.loading')}
            </p>
        );
    }

    if (dashboardQuery.isError) {
        return (
            <section className="space-y-4">
                <p
                    role="alert"
                    className="text-red-700 dark:text-red-400"
                >
                    {t('errors.genericDescription')}
                </p>

                <PrimaryButton
                    type="button"
                    loading={dashboardQuery.isFetching}
                    onClick={() => {
                        void dashboardQuery.refetch();
                    }}
                >
                    {t('common.actions.retry')}
                </PrimaryButton>
            </section>
        );
    }

    const dashboard = dashboardQuery.data;

    const language =
        i18n.resolvedLanguage
        ?? i18n.language;

    function formatMoney(
        amount: number,
        currency: string,
    ): string {
        return new Intl.NumberFormat(language, {
            style: 'currency',
            currency,
            minimumFractionDigits: 2,
            maximumFractionDigits: 4,
        }).format(amount);
    }

    function formatDate(value: string): string {
        return new Intl.DateTimeFormat(language, {
            dateStyle: 'medium',
            timeZone: 'UTC',
        }).format(
            new Date(`${value}T00:00:00Z`),
        );
    }

    return (
        <div className="space-y-8">
            <header>
                <h1 className="text-3xl font-semibold">
                    {dashboard.displayName
                        ? t('app.dashboard.greeting', {
                            name: dashboard.displayName,
                        })
                        : t('app.dashboard.title')}
                </h1>

                <p className="mt-2 text-slate-600 dark:text-slate-400">
                    {t('app.dashboard.recordedBalanceHelp')}
                </p>
            </header>

            <section
                aria-labelledby="household-balances-title"
                className="space-y-4"
            >
                <div>
                    <h2
                        id="household-balances-title"
                        className="text-xl font-semibold"
                    >
                        {t('app.dashboard.householdBalances')}
                    </h2>

                    <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                        {t('app.dashboard.reportingCurrency', {
                            currency: dashboard.reportingCurrency,
                        })}
                    </p>

                    <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
                        {t('app.dashboard.noConversion')}
                    </p>
                </div>

                {dashboard.householdBalances.length === 0 ? (
                    <p className="text-slate-600 dark:text-slate-400">
                        {t('app.dashboard.noReportedAccounts')}
                    </p>
                ) : (
                    <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                        {dashboard.householdBalances.map((balance) => (
                            <li
                                key={balance.currency}
                                className="
                                    rounded-2xl border
                                    border-slate-200 bg-white p-5
                                    dark:border-slate-800
                                    dark:bg-slate-900
                                "
                            >
                                <p className="text-sm font-medium text-slate-600 dark:text-slate-400">
                                    {balance.currency}
                                </p>

                                <p className="mt-2 text-3xl font-semibold">
                                    {formatMoney(
                                        balance.amount,
                                        balance.currency,
                                    )}
                                </p>

                                <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
                                    {balance.earliestBalanceDate
                                        === balance.latestBalanceDate
                                        ? t('app.dashboard.recordedOn', {
                                            date: formatDate(
                                                balance.latestBalanceDate,
                                            ),
                                        })
                                        : t('app.dashboard.recordedBetween', {
                                            from: formatDate(
                                                balance.earliestBalanceDate,
                                            ),
                                            to: formatDate(
                                                balance.latestBalanceDate,
                                            ),
                                        })}
                                </p>
                            </li>
                        ))}
                    </ul>
                )}
            </section>

            <section
                aria-labelledby="dashboard-accounts-title"
                className="space-y-4"
            >
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <h2
                        id="dashboard-accounts-title"
                        className="text-xl font-semibold"
                    >
                        {t('app.dashboard.accountsTitle')}
                    </h2>

                    <Link
                        to="/app/accounts"
                        className="
                            rounded
                            font-medium text-primary-700
                            hover:underline
                            dark:text-primary-300
                        "
                    >
                        {t('app.dashboard.manageAccounts')}
                    </Link>
                </div>

                {dashboard.accounts.length === 0 ? (
                    <p className="text-slate-600 dark:text-slate-400">
                        {t('app.dashboard.noActiveAccounts')}
                    </p>
                ) : (
                    <ul className="grid gap-4 sm:grid-cols-2">
                        {dashboard.accounts.map((account) => (
                            <li
                                key={account.id}
                                className="
                                    rounded-2xl border
                                    border-slate-200 bg-white p-5
                                    dark:border-slate-800
                                    dark:bg-slate-900
                                "
                            >
                                <h3 className="font-semibold">
                                    {account.name}
                                </h3>

                                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                                    {account.visibility === 'Private'
                                        ? t('app.dashboard.privateAccount')
                                        : account.includeInHouseholdReports
                                            ? t('app.dashboard.includedAccount')
                                            : t('app.dashboard.excludedAccount')}
                                </p>

                                <p className="mt-4 text-2xl font-semibold">
                                    {formatMoney(
                                        account.balanceAnchorAmount,
                                        account.currency,
                                    )}
                                </p>

                                <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                                    {t('app.dashboard.recordedOn', {
                                        date: formatDate(
                                            account.balanceAnchorDate,
                                        ),
                                    })}
                                </p>
                            </li>
                        ))}
                    </ul>
                )}
            </section>
        </div>
    );
}