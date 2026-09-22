import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
    endOfMonth,
    endOfYear,
    format,
    startOfMonth,
    startOfYear,
    subMonths,
} from 'date-fns';
import { useTranslation } from 'react-i18next';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { getCategoryDisplayName } from '../categoryLocalization';
import { transactionQueries } from '../api/transactionQueries';
import type {
    CashFlowPeriodDto,
    CategoryCashFlowPeriodDto,
} from '../api/transactionApi';
import '../transactions.css';

interface InteractiveBarProps {
    id: string;
    tone: 'income' | 'expense';
    label: string;
    amount: string;
    currency: string;
    language: string;
    max: number;
    active: boolean;
    onToggle: () => void;
}

function InteractiveBar({
    id,
    tone,
    label,
    amount,
    currency,
    language,
    max,
    active,
    onToggle,
}: InteractiveBarProps) {
    const numericAmount = Number(amount);
    const formatted = formatDecimalAmount(
        amount,
        currency,
        language,
    );
    const height = numericAmount === 0
        ? 2
        : Math.max(4, numericAmount / max * 100);

    return (
        <button
            type="button"
            className={
                `av-stat-bar ${tone}`
                + (active ? ' is-active' : '')
                + (numericAmount === 0 ? ' is-zero' : '')
            }
            style={{ height: `${height}%` }}
            aria-label={`${label}: ${formatted}`}
            aria-pressed={active}
            aria-describedby={`${id}-tooltip`}
            onClick={onToggle}
        >
            <span
                id={`${id}-tooltip`}
                className="av-stat-tooltip"
                role="tooltip"
            >
                <span>{label}</span>
                <strong>{formatted}</strong>
            </span>
        </button>
    );
}

export function StatisticsPage({
    userId,
    householdId,
}: {
    userId: string;
    householdId: string;
}) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const [activeBar, setActiveBar] = useState<string | null>(null);

    const end = endOfMonth(new Date());
    const start = startOfMonth(subMonths(end, 11));
    const from = format(start, 'yyyy-MM-dd');
    const to = format(end, 'yyyy-MM-dd');
    const yearFrom = format(startOfYear(new Date()), 'yyyy-MM-dd');
    const yearTo = format(endOfYear(new Date()), 'yyyy-MM-dd');

    const cashFlow = useQuery(
        transactionQueries.cashFlow(
            userId,
            householdId,
            from,
            to,
            'Month',
        ),
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

    const categoriesByCurrency =
        new Map<string, CategoryCashFlowPeriodDto[]>();

    for (const period of categoryFlow.data?.periods ?? []) {
        const list =
            categoriesByCurrency.get(period.currency) ?? [];
        list.push(period);
        categoriesByCurrency.set(period.currency, list);
    }

    return (
        <section
            className="space-y-6"
            aria-labelledby="statistics-title"
        >
            <header className="av-page-head">
                <div>
                    <h2 id="statistics-title">
                        {t('transactionManagement.statistics')}
                    </h2>
                    <p>
                        {t('transactionManagement.statisticsDescription')}
                    </p>
                </div>
                <span className="av-badge">
                    {t('transactionManagement.last12Months')}
                </span>
            </header>

            {cashFlow.isPending && (
                <p role="status">{t('common.status.loading')}</p>
            )}

            {cashFlow.isError && (
                <p role="alert" className="av-error">
                    {t('errors.genericDescription')}
                </p>
            )}

            {cashFlow.isSuccess && byCurrency.size === 0 && (
                <div className="av-card av-tx-empty">
                    <p>{t('transactionManagement.noCashFlow')}</p>
                </div>
            )}

            {[...byCurrency.entries()].map(([currency, periods]) => {
                const max = Math.max(
                    1,
                    ...periods.flatMap(period => [
                        Number(period.incomeAmount),
                        Number(period.expenseAmount),
                    ]),
                );

                const income = periods.reduce(
                    (sum, period) =>
                        sum + Number(period.incomeAmount),
                    0,
                );

                const expenses = periods.reduce(
                    (sum, period) =>
                        sum + Number(period.expenseAmount),
                    0,
                );

                return (
                    <article
                        key={currency}
                        className="av-card av-stat-card"
                    >
                        <div className="av-stat-head">
                            <div>
                                <span className="av-badge">
                                    {currency}
                                </span>
                                <h3>
                                    {t('transactionManagement.dashboardCashFlow')}
                                </h3>
                            </div>

                            <dl className="av-stat-totals">
                                <div>
                                    <dt>{t('statistics.income')}</dt>
                                    <dd>
                                        {formatDecimalAmount(
                                            String(income),
                                            currency,
                                            language,
                                        )}
                                    </dd>
                                </div>
                                <div>
                                    <dt>{t('statistics.expenses')}</dt>
                                    <dd>
                                        {formatDecimalAmount(
                                            String(expenses),
                                            currency,
                                            language,
                                        )}
                                    </dd>
                                </div>
                                <div>
                                    <dt>{t('transactionManagement.net')}</dt>
                                    <dd>
                                        {formatDecimalAmount(
                                            String(income - expenses),
                                            currency,
                                            language,
                                        )}
                                    </dd>
                                </div>
                            </dl>
                        </div>

                        <div
                            className="av-stat-bars"
                            role="group"
                            aria-label={t(
                                'transactionManagement.dashboardCashFlow',
                            )}
                        >
                            {periods.map(period => {
                                const monthLabel =
                                    new Intl.DateTimeFormat(
                                        language,
                                        {
                                            month: 'short',
                                            timeZone: 'UTC',
                                        },
                                    ).format(
                                        new Date(
                                            period.periodStart
                                            + 'T00:00:00Z',
                                        ),
                                    );

                                const incomeId =
                                    `${currency}-${period.periodStart}-income`;
                                const expenseId =
                                    `${currency}-${period.periodStart}-expense`;

                                return (
                                    <div
                                        key={period.periodStart}
                                        className="av-stat-month"
                                    >
                                        <div className="av-stat-columns">
                                            <InteractiveBar
                                                id={incomeId}
                                                tone="income"
                                                label={
                                                    `${monthLabel} · ${
                                                        t('statistics.income')
                                                    }`
                                                }
                                                amount={period.incomeAmount}
                                                currency={currency}
                                                language={language}
                                                max={max}
                                                active={
                                                    activeBar === incomeId
                                                }
                                                onToggle={() =>
                                                    setActiveBar(current =>
                                                        current === incomeId
                                                            ? null
                                                            : incomeId)}
                                            />

                                            <InteractiveBar
                                                id={expenseId}
                                                tone="expense"
                                                label={
                                                    `${monthLabel} · ${
                                                        t('statistics.expenses')
                                                    }`
                                                }
                                                amount={period.expenseAmount}
                                                currency={currency}
                                                language={language}
                                                max={max}
                                                active={
                                                    activeBar === expenseId
                                                }
                                                onToggle={() =>
                                                    setActiveBar(current =>
                                                        current === expenseId
                                                            ? null
                                                            : expenseId)}
                                            />
                                        </div>
                                        <span>{monthLabel}</span>
                                    </div>
                                );
                            })}
                        </div>

                        <p className="av-subtle">
                            {t('transactionManagement.reportingNote')}
                        </p>
                    </article>
                );
            })}

            <section
                className="av-card av-stat-card"
                aria-labelledby="category-year-title"
            >
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
                        {new Intl.DateTimeFormat(
                            language,
                            { year: 'numeric' },
                        ).format(new Date())}
                    </span>
                </div>

                {categoryFlow.isPending && (
                    <p role="status">
                        {t('common.status.loading')}
                    </p>
                )}

                {categoryFlow.isError && (
                    <p role="alert" className="av-error">
                        {t('errors.genericDescription')}
                    </p>
                )}

                {categoryFlow.isSuccess
                    && categoriesByCurrency.size === 0 && (
                    <div className="av-tx-empty">
                        <p>
                            {t('transactionManagement.noCategoryCashFlow')}
                        </p>
                    </div>
                )}

                <div className="av-category-stat-groups">
                    {[...categoriesByCurrency.entries()]
                        .map(([currency, periods]) => (
                            <div
                                className="av-category-stat-group"
                                key={currency}
                            >
                                <div className="av-category-stat-title">
                                    <span className="av-badge">
                                        {currency}
                                    </span>
                                </div>

                                <div className="av-category-stat-list">
                                    {periods
                                        .sort((a, b) =>
                                            Number(b.expenseAmount)
                                            - Number(a.expenseAmount))
                                        .map(period => (
                                            <div
                                                className="av-category-stat-row"
                                                key={
                                                    `${currency}-${period.categoryId}`
                                                }
                                            >
                                                <span className="av-category-stat-icon">
                                                    {period.categoryIcon}
                                                </span>

                                                <div className="av-category-stat-copy">
                                                    <strong>
                                                        {getCategoryDisplayName(
                                                            {
                                                                systemKey:
                                                                    period.categorySystemKey,
                                                                name:
                                                                    period.categoryName,
                                                            },
                                                            t,
                                                        )}
                                                    </strong>
                                                    <span>
                                                        {t(
                                                            'transactionManagement.itemsCount',
                                                            {
                                                                count:
                                                                    period.itemCount,
                                                            },
                                                        )}
                                                    </span>
                                                </div>

                                                <div className="av-category-stat-values">
                                                    {Number(period.incomeAmount) > 0 && (
                                                        <span className="income">
                                                            +
                                                            {formatDecimalAmount(
                                                                period.incomeAmount,
                                                                currency,
                                                                language,
                                                            )}
                                                        </span>
                                                    )}

                                                    {Number(period.expenseAmount) > 0 && (
                                                        <span className="expense">
                                                            −
                                                            {formatDecimalAmount(
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
