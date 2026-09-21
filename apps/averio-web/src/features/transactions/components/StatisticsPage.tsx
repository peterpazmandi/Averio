import { useQuery } from '@tanstack/react-query';
import { endOfMonth, format, startOfMonth, subMonths } from 'date-fns';
import { useTranslation } from 'react-i18next';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { transactionQueries } from '../api/transactionQueries';
import type { CashFlowPeriodDto } from '../api/transactionApi';
import '../transactions.css';

export function StatisticsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const end = endOfMonth(new Date());
    const start = startOfMonth(subMonths(end, 11));
    const from = format(start, 'yyyy-MM-dd');
    const to = format(end, 'yyyy-MM-dd');
    const cashFlow = useQuery(transactionQueries.cashFlow(userId, householdId, from, to, 'Month'));

    const byCurrency = new Map<string, CashFlowPeriodDto[]>();
    for (const period of cashFlow.data?.periods ?? []) {
        const list = byCurrency.get(period.currency) ?? [];
        list.push(period);
        byCurrency.set(period.currency, list);
    }

    return (
        <section className="space-y-6" aria-labelledby="statistics-title">
            <header className="av-page-head">
                <div>
                    <h2 id="statistics-title">{t('transactionManagement.statistics')}</h2>
                    <p>{t('transactionManagement.statisticsDescription')}</p>
                </div>
                <span className="av-badge">{t('transactionManagement.last12Months')}</span>
            </header>

            {cashFlow.isPending && <p role="status">{t('common.status.loading')}</p>}
            {cashFlow.isError && <p role="alert" className="av-error">{t('errors.genericDescription')}</p>}
            {cashFlow.isSuccess && byCurrency.size === 0 && (
                <div className="av-card av-tx-empty"><p>{t('transactionManagement.noCashFlow')}</p></div>
            )}

            {[...byCurrency.entries()].map(([currency, periods]) => {
                const max = Math.max(1, ...periods.flatMap(period => [
                    Number(period.incomeAmount), Number(period.expenseAmount),
                ]));
                const income = periods.reduce((sum, period) => sum + Number(period.incomeAmount), 0);
                const expenses = periods.reduce((sum, period) => sum + Number(period.expenseAmount), 0);
                return (
                    <article key={currency} className="av-card av-stat-card">
                        <div className="av-stat-head">
                            <div>
                                <span className="av-badge">{currency}</span>
                                <h3>{t('transactionManagement.dashboardCashFlow')}</h3>
                            </div>
                            <dl className="av-stat-totals">
                                <div><dt>{t('statistics.income')}</dt><dd>{formatDecimalAmount(String(income), currency, language)}</dd></div>
                                <div><dt>{t('statistics.expenses')}</dt><dd>{formatDecimalAmount(String(expenses), currency, language)}</dd></div>
                                <div><dt>{t('transactionManagement.net')}</dt><dd>{formatDecimalAmount(String(income - expenses), currency, language)}</dd></div>
                            </dl>
                        </div>
                        <div className="av-stat-bars" role="img" aria-label={t('transactionManagement.dashboardCashFlow')}>
                            {periods.map(period => (
                                <div key={period.periodStart} className="av-stat-month">
                                    <div className="av-stat-columns">
                                        <span className="income" style={{ height: `${Math.max(3, Number(period.incomeAmount) / max * 100)}%` }}
                                            title={formatDecimalAmount(period.incomeAmount, currency, language)} />
                                        <span className="expense" style={{ height: `${Math.max(3, Number(period.expenseAmount) / max * 100)}%` }}
                                            title={formatDecimalAmount(period.expenseAmount, currency, language)} />
                                    </div>
                                    <span>{new Intl.DateTimeFormat(language, { month: 'short', timeZone: 'UTC' })
                                        .format(new Date(period.periodStart + 'T00:00:00Z'))}</span>
                                </div>
                            ))}
                        </div>
                        <p className="av-subtle">{t('transactionManagement.reportingNote')}</p>
                    </article>
                );
            })}
        </section>
    );
}
