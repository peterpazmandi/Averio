import { PersonalMoneyHistory } from '../../personal-money/PersonalMoneyHistory';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { personalMoneyQueries } from '../../personal-money/personalMoneyQueries';

export function PersonalMoneyCard({ userId, householdId }: {
    userId: string;
    householdId: string;
}) {
    const { t, i18n } = useTranslation();
    const query = useQuery(personalMoneyQueries.mine(userId, householdId));
    const wallet = query.data;
    const language = i18n.resolvedLanguage ?? i18n.language;

    return (
        <AverioCard className="col-span-12 flex flex-col gap-4 xl:col-span-4"
            aria-labelledby="dashboard-personal-money-title">
            <div className="flex items-start gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200" aria-hidden="true">
                    <ConceptIcon name="savings" />
                </span>
                <div className="min-w-0 space-y-1">
                    <h3 id="dashboard-personal-money-title" className="font-semibold">{t('personalMoney.myWallet')}</h3>
                    <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">{t('personalMoney.description')}</p>
                </div>
            </div>
            {query.isPending ? (
                <p role="status" className="text-sm">{t('common.status.loading')}</p>
            ) : query.isError ? (
                <div className="space-y-3">
                    <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('personalMoney.loadError')}</p>
                    <AverioButton disabled={query.isFetching} onClick={() => { void query.refetch(); }}>
                        {t('common.actions.retry')}
                    </AverioButton>
                </div>
            ) : wallet ? (
                <>
                    <dl className="rounded-xl border border-emerald-500/20 bg-emerald-50 p-4 dark:bg-emerald-950/20">
                        <dt className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.available')}</dt>
                        <dd className="mt-2 break-words text-2xl font-bold">
                            {formatDecimalAmount(wallet.available, wallet.currency, language)}
                        </dd>
                    </dl>
                    <PersonalMoneyHistory entries={wallet.entries.slice(0, 5)} currency={wallet.currency} />
                </>
            ) : (
                <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">{t('personalMoney.empty')}</p>
            )}
            <Link to="/app/household" hash="household-personal-money" hashScrollIntoView={false}
                className="mt-auto inline-flex min-h-11 items-center justify-center rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2 text-center text-sm font-semibold text-emerald-800 hover:bg-emerald-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200 dark:hover:bg-emerald-900">
                {t('personalMoney.dashboardManage')}
            </Link>
        </AverioCard>
    );
}
