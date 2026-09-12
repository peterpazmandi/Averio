import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { PrimaryButton } from '../../../shared/components/PrimaryButton';
import { accountsQueries } from '../api/accountsQueries';

interface AccountsPageProps {
    userId: string;
    householdId: string;
}

export function AccountsPage({
    userId,
    householdId,
}: AccountsPageProps) {
    const { t, i18n } = useTranslation();

    const accountsQuery = useQuery(
        accountsQueries.list(
            userId,
            householdId,
            false,
        ),
    );

    if (accountsQuery.isPending) {
        return (
            <p role="status">
                {t('common.status.loading')}
            </p>
        );
    }

    if (accountsQuery.isError) {
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
                    loading={accountsQuery.isFetching}
                    onClick={() => {
                        void accountsQuery.refetch();
                    }}
                >
                    {t('common.actions.retry')}
                </PrimaryButton>
            </section>
        );
    }

    const accounts = accountsQuery.data;
    const language = i18n.resolvedLanguage ?? i18n.language;

    return (
        <section
            aria-labelledby="accounts-title"
            className="space-y-6"
        >
            <header>
                <h1
                    id="accounts-title"
                    className="
                        text-3xl font-semibold
                        text-slate-950 dark:text-white
                    "
                >
                    {t('app.accounts.title')}
                </h1>

                <p className="mt-2 text-slate-600 dark:text-slate-400">
                    {t('app.accounts.description')}
                </p>
            </header>

            {accounts.length === 0 ? (
                <p className="text-slate-600 dark:text-slate-400">
                    {t('app.accounts.empty')}
                </p>
            ) : (
                <ul className="grid gap-4 sm:grid-cols-2">
                    {accounts.map((account) => (
                        <li
                            key={account.id}
                            className="
                                rounded-2xl border
                                border-slate-200 bg-white p-5
                                dark:border-slate-800
                                dark:bg-slate-900
                            "
                        >
                            <h2 className="font-semibold">
                                {account.name}
                            </h2>

                            <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
                                {t('app.accounts.recordedBalance')}
                            </p>

                            <p className="mt-1 text-2xl font-semibold">
                                {new Intl.NumberFormat(
                                    language,
                                    {
                                        style: 'currency',
                                        currency: account.currency,
                                    },
                                ).format(account.balanceAnchorAmount)}
                            </p>

                            <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                                <time dateTime={account.balanceAnchorDate}>
                                    {account.balanceAnchorDate}
                                </time>
                            </p>
                        </li>
                    ))}
                </ul>
            )}
        </section>
    );
}