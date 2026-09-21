import { useQuery } from '@tanstack/react-query';
import { endOfMonth, endOfYear, format, startOfMonth, startOfYear, subMonths } from 'date-fns';
import { useTranslation } from 'react-i18next';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { transactionQueries } from '../api/transactionQueries';
import type {
    CashFlowPeriodDto,
    CategoryCashFlowPeriodDto,
} from '../api/transactionApi';
import '../transactions.css';

export function StatisticsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const end = endOfMonth(new Date());
    const start = startOfMonth(subMonths(end, 11));
    const from = format(start, 'yyyy-MM-dd');
    const to = format(end, 'yyyy-MM-dd');
    const yearFrom = format(startOfYear(new Date()), 'yyyy-MM-dd');
    const yearTo = format(endOfYear(new Date()), 'yyyy-MM-dd');

    const cashFlow = useQuery(
        transactionQueries.cashFlow(userId, householdId, from, to, 'Month'),
    );
    const categoryFlow = useQuery(
        transactionQueries.categoryCashFlow(
            userId,
            householdId,
            yearFrom,
            yearTo,
            'Year',
        ),
    );

    const byCurrency = new Map<string, CashFlowPeriodDto[]>();
    for (const period of cashFlow.data?.periods ?? []) {
        const list = byCurrency.get(period.currency) ?? [];
        list.push(period);
        byCurrency.set(period.currency, list);
    }

    const categoriesByCurrency = new Map<string, CategoryCashFlowPeriodDto[]>();
    for (const period of categoryFlow.data?.periods ?? []) {
        const list = categoriesByCurrency.get(period.currency) ?? [];
        list.push(period);
        categoriesByCurrency.set(period.currency, list);
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
                        <div className="av-stat-bars" role="img"
                            aria-label={t('transactionManagement.dashboardCashFlow')}>
                            {periods.map(period => (
                                <div key={period.periodStart} className="av-stat-month">
                                    <div className="av-stat-columns">
                                        <span className="income"
                                            style={{ height: `${Number(period.incomeAmount) === 0 ? 0 : Math.max(3, Number(period.incomeAmount) / max * 100)}%` }}
                                            title={formatDecimalAmount(period.incomeAmount, currency, language)} />
                                        <span className="expense"
                                            style={{ height: `${Number(period.expenseAmount) === 0 ? 0 : Math.max(3, Number(period.expenseAmount) / max * 100)}%` }}
                                            title={formatDecimalAmount(period.expenseAmount, currency, language)} />
                                    </div>
                                    <span>{new Intl.DateTimeFormat(language, {
                                        month: 'short', timeZone: 'UTC',
                                    }).format(new Date(period.periodStart + 'T00:00:00Z'))}</span>
                                </div>
                            ))}
                        </div>
                        <p className="av-subtle">{t('transactionManagement.reportingNote')}</p>
                    </article>
                );
            })}

            <section className="av-card av-stat-card" aria-labelledby="category-year-title">
                <div className="av-stat-head">
                    <div>
                        <h3 id="category-year-title">
                            {t('transactionManagement.thisYearByCategory')}
                        </h3>
                        <p className="av-subtle">
                            {t('transactionManagement.categoryYearHelp')}
                        </p>
                    </div>
                    <span className="av-badge">
                        {new Intl.DateTimeFormat(language, { year: 'numeric' }).format(new Date())}
                    </span>
                </div>

                {categoryFlow.isPending && (
                    <p role="status">{t('common.status.loading')}</p>
                )}
                {categoryFlow.isError && (
                    <p role="alert" className="av-error">{t('errors.genericDescription')}</p>
                )}
                {categoryFlow.isSuccess && categoriesByCurrency.size === 0 && (
                    <div className="av-tx-empty">
                        <p>{t('transactionManagement.noCategoryCashFlow')}</p>
                    </div>
                )}

                <div className="av-category-stat-groups">
                    {[...categoriesByCurrency.entries()].map(([currency, periods]) => (
                        <div className="av-category-stat-group" key={currency}>
                            <div className="av-category-stat-title">
                                <span className="av-badge">{currency}</span>
                            </div>
                            <div className="av-category-stat-list">
                                {periods
                                    .sort((a, b) =>
                                        Number(b.expenseAmount) - Number(a.expenseAmount))
                                    .map(period => (
                                        <div className="av-category-stat-row"
                                            key={`${currency}-${period.categoryId}`}>
                                            <span className="av-category-stat-icon">
                                                {period.categoryIcon}
                                            </span>
                                            <div className="av-category-stat-copy">
                                                <strong>{period.categoryName}</strong>
                                                <span>{t('transactionManagement.itemsCount', {
                                                    count: period.itemCount,
                                                })}</span>
                                            </div>
                                            <div className="av-category-stat-values">
                                                {Number(period.incomeAmount) > 0 && (
                                                    <span className="income">
                                                        +{formatDecimalAmount(
                                                            period.incomeAmount,
                                                            currency,
                                                            language,
                                                        )}
                                                    </span>
                                                )}
                                                {Number(period.expenseAmount) > 0 && (
                                                    <span className="expense">
                                                        −{formatDecimalAmount(
                                                            period.expenseAmount,
                                                            currency,
                                                            language,
                                                        )}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    ))}
                            </div>
                        </div>
                    ))}
                </div>
            </section>
        </section>
    );
}
