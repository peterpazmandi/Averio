import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { format } from 'date-fns';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { PersonalMoneySnapshotResponse } from '../../shared/api/generated';
import { StatisticsApi, TransactionsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { personalMoneyApi, personalMoneyKeys } from './personalMoneyQueries';
import { nextRefundPage, refundEligibility, validRefundAmount } from './refundEligibility';
import type { RefundCursor } from './refundEligibility';

const transactionsApi = new TransactionsApi(generatedApiConfiguration);
const statisticsApi = new StatisticsApi(generatedApiConfiguration);
const decimalPattern = /^[0-9]+([.,][0-9]{1,4})?$/;
const normalize = (value: string) => value.trim().replace(',', '.');

export function PersonalMoneyReconciliationPanel({ userId, householdId, wallet }: {
    userId: string; householdId: string; wallet: PersonalMoneySnapshotResponse;
}) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const [purchaseId, setPurchaseId] = useState('');
    const [refundId, setRefundId] = useState('');
    const [refundAmount, setRefundAmount] = useState('');
    const [refundOperation, setRefundOperation] = useState(() => crypto.randomUUID());
    const [surrenderAmount, setSurrenderAmount] = useState('');
    const [surrenderDate, setSurrenderDate] = useState(format(new Date(), 'yyyy-MM-dd'));
    const [surrenderOperation, setSurrenderOperation] = useState(() => crypto.randomUUID());
    const [feedback, setFeedback] = useState('');
    const locale = i18n.resolvedLanguage ?? i18n.language;
    const formatMoney = (amount: string, currency = wallet.currency) =>
        new Intl.NumberFormat(locale, { style: 'currency', currency }).format(Number(amount));

    const purchases = useInfiniteQuery({
        queryKey: ['personal-money', userId, householdId, 'refundable-purchases'],
        initialPageParam: undefined as RefundCursor | undefined,
        queryFn: ({ pageParam, signal }) => transactionsApi.apiV1HouseholdsHouseholdIdTransactionsGet(
            { householdId, kind: 'Expense', includeArchived: true, pageSize: 100, ...pageParam }, { signal }),
        getNextPageParam: nextRefundPage,
    });
    const incomes = useInfiniteQuery({
        queryKey: ['personal-money', userId, householdId, 'refund-incomes'],
        initialPageParam: undefined as RefundCursor | undefined,
        queryFn: ({ pageParam, signal }) => transactionsApi.apiV1HouseholdsHouseholdIdTransactionsGet(
            { householdId, kind: 'Income', includeArchived: true, pageSize: 100, ...pageParam }, { signal }),
        getNextPageParam: nextRefundPage,
    });
    const eligibility = refundEligibility(wallet);
    const refundable = (purchases.data?.pages.flatMap(page => page.items) ?? []).filter(x =>
        eligibility.remaining.has(x.id) && x.status === 'Posted' && x.currency === wallet.currency);
    const selectedPurchase = refundable.find(x => x.id === purchaseId);
    const refundOptions = (incomes.data?.pages.flatMap(page => page.items) ?? []).filter(x =>
        eligibility.canUseIncome(x, selectedPurchase));
    const selectedRefund = refundOptions.find(x => x.id === refundId);
    const validRefund = Boolean(selectedPurchase && selectedRefund
        && validRefundAmount(refundAmount, selectedRefund.amount, eligibility.remaining.get(purchaseId) ?? '0'));
    const validSurrender = decimalPattern.test(surrenderAmount) && Number(normalize(surrenderAmount)) > 0
        && Number(normalize(surrenderAmount)) <= Number(wallet.available);

    const refresh = async () => {
        await client.invalidateQueries({ queryKey: personalMoneyKeys.mine(userId, householdId) });
    };
    const refund = useMutation({
        mutationFn: () => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeRefundsPost({
            householdId, refundPersonalMoneyRequest: {
                refundTransactionId: refundId, purchaseTransactionId: purchaseId,
                amount: normalize(refundAmount), operationId: refundOperation,
            },
        }),
        onSuccess: async () => {
            await refresh(); setRefundId(''); setPurchaseId(''); setRefundAmount('');
            setRefundOperation(crypto.randomUUID()); setFeedback(t('personalMoney.refundSuccess'));
        },
        onError: (error: Error) => setFeedback(error.message),
    });
    const surrender = useMutation({
        mutationFn: () => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeSurrendersPost({
            householdId, surrenderPersonalMoneyRequest: {
                amount: normalize(surrenderAmount), occurredOn: surrenderDate,
                operationId: surrenderOperation,
            },
        }),
        onSuccess: async () => {
            await refresh(); setSurrenderAmount(''); setSurrenderOperation(crypto.randomUUID());
            setFeedback(t('personalMoney.surrenderSuccess'));
        },
        onError: (error: Error) => setFeedback(error.message),
    });

    return <section className="space-y-4" aria-label={t('personalMoney.reconcileTitle')}>
        <header className="space-y-1">
            <h4 className="text-base font-semibold">{t('personalMoney.reconcileTitle')}</h4>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.reconcileHelp')}</p>
        </header>
        {feedback && <p role="status" className="text-sm">{feedback}</p>}
        <div className="grid gap-4 lg:grid-cols-2">
            <form className="space-y-4 rounded-xl border border-slate-200 p-4 dark:border-slate-700"
                onSubmit={e => { e.preventDefault(); if (validRefund && !refund.isPending) refund.mutate(); }}>
                <h5 className="font-semibold">{t('personalMoney.refundTitle')}</h5>
                <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.refundHelp')}</p>
                <AverioField>{t('personalMoney.refundOriginal')}
                    <AverioSelect aria-label={t('personalMoney.refundOriginal')} value={purchaseId} disabled={refund.isPending} onChange={e => {
                        setPurchaseId(e.target.value); setRefundId(''); setRefundAmount('');
                        setRefundOperation(crypto.randomUUID());
                    }}>
                        <option value="">{t('personalMoney.selectPurchase')}</option>
                        {refundable.map(x => <option key={x.id} value={x.id}>
                            {x.occurredOn} · {x.merchant || x.description} · {formatMoney(eligibility.remaining.get(x.id)!)}
                        </option>)}
                    </AverioSelect>
                </AverioField>
                {purchases.isPending && <p role="status">{t('common.status.loading')}</p>}
                {purchases.isError && <p role="alert">{t('personalMoney.refundLoadError')}</p>}
                {(purchases.hasNextPage || purchases.isError) && <AverioButton disabled={purchases.isFetching || refund.isPending}
                    onClick={() => { void (purchases.isError && !purchases.isFetchNextPageError ? purchases.refetch() : purchases.fetchNextPage()); }}>
                    {t(purchases.isError ? 'common.actions.retry' : 'transactionManagement.loadMore')}
                </AverioButton>}
                {selectedPurchase && incomes.isSuccess && !incomes.hasNextPage && refundOptions.length === 0 && <p className="text-xs text-slate-600 dark:text-slate-300">
                    {t('personalMoney.noRecordedRefund')}{' '}
                    <Link to="/app/transactions" className="font-semibold text-emerald-700 underline dark:text-emerald-300">
                        {t('personalMoney.goToTransactions')}
                    </Link>
                </p>}
                <AverioField>{t('personalMoney.refundIncome')}
                    <AverioSelect aria-label={t('personalMoney.refundIncome')} value={refundId} disabled={!selectedPurchase || refund.isPending} onChange={e => {
                        setRefundId(e.target.value); setRefundAmount(refundOptions.find(x => x.id === e.target.value)?.amount ?? '');
                        setRefundOperation(crypto.randomUUID());
                    }}>
                        <option value="">{t('personalMoney.selectRefund')}</option>
                        {refundOptions.map(x => <option key={x.id} value={x.id}>
                            {x.occurredOn} · {x.merchant || x.description} · {formatMoney(x.amount)}
                        </option>)}
                    </AverioSelect>
                </AverioField>
                {incomes.isPending && <p role="status">{t('common.status.loading')}</p>}
                {incomes.isError && <p role="alert">{t('personalMoney.refundLoadError')}</p>}
                {(incomes.hasNextPage || incomes.isError) && <AverioButton disabled={incomes.isFetching || refund.isPending}
                    onClick={() => { void (incomes.isError && !incomes.isFetchNextPageError ? incomes.refetch() : incomes.fetchNextPage()); }}>
                    {t(incomes.isError ? 'common.actions.retry' : 'transactionManagement.loadMore')}
                </AverioButton>}
                <AverioField>{t('personalMoney.amount')}
                    <AverioInput inputMode="decimal" value={refundAmount} disabled={!selectedRefund || refund.isPending}
                        onChange={e => { setRefundAmount(e.target.value); setRefundOperation(crypto.randomUUID()); }} />
                </AverioField>
                <AverioButton type="submit" variant="primary" disabled={!validRefund || refund.isPending}>
                    {t('personalMoney.applyRefund')}</AverioButton>
            </form>
            <form className="space-y-4 rounded-xl border border-slate-200 p-4 dark:border-slate-700"
                onSubmit={e => { e.preventDefault(); if (validSurrender && !surrender.isPending) surrender.mutate(); }}>
                <h5 className="font-semibold">{t('personalMoney.surrenderTitle')}</h5>
                <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.surrenderHelp')}</p>
                <AverioField>{t('personalMoney.amount')}
                    <AverioInput value={surrenderAmount} inputMode="decimal" disabled={surrender.isPending}
                        onChange={e => { setSurrenderAmount(e.target.value); setSurrenderOperation(crypto.randomUUID()); }} />
                </AverioField>
                <AverioField>{t('personalMoney.surrenderDate')}
                    <AverioInput type="date" value={surrenderDate} disabled={surrender.isPending}
                        onChange={e => { setSurrenderDate(e.target.value); setSurrenderOperation(crypto.randomUUID()); }} />
                </AverioField>
                <AverioButton type="submit" disabled={!validSurrender || surrender.isPending}>
                    {t('personalMoney.surrender')}</AverioButton>
            </form>
        </div>
    </section>;
}

export function HouseholdBoundaryFlowPanel({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const now = new Date();
    const from = format(new Date(now.getFullYear(), now.getMonth() - 5, 1), 'yyyy-MM-dd');
    const to = format(now, 'yyyy-MM-dd');
    const report = useQuery({
        queryKey: [...personalMoneyKeys.boundary(userId, householdId), from, to],
        queryFn: ({ signal }) => statisticsApi.apiV1HouseholdsHouseholdIdStatisticsBoundaryFlowGet(
            { householdId, from, to }, { signal }),
    });
    const locale = i18n.resolvedLanguage ?? i18n.language;
    const money = (amount: string, currency: string) =>
        new Intl.NumberFormat(locale, { style: 'currency', currency }).format(Number(amount));
    return <AverioCard className="space-y-3 p-5 sm:p-6">
        <header className="space-y-1">
            <h4 className="text-lg font-semibold">{t('personalMoney.boundaryTitle')}</h4>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.boundaryHelp')}</p>
        </header>
        {report.isPending && <p role="status">{t('common.status.loading')}</p>}
        {report.isError && <p role="alert">{t('personalMoney.boundaryError')}</p>}
        {report.data && (report.data.months.length === 0
            ? <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.boundaryEmpty')}</p>
            : <div className="overflow-x-auto"><table className="w-full min-w-[420px] text-left text-sm">
                <thead><tr className="border-b border-slate-200 dark:border-slate-700">
                    <th scope="col" className="py-2">{t('personalMoney.boundaryMonth')}</th>
                    <th scope="col" className="py-2">{t('personalMoney.boundaryOut')}</th>
                    <th scope="col" className="py-2">{t('personalMoney.boundaryIn')}</th>
                </tr></thead>
                <tbody>{report.data.months.map(x => <tr key={x.month + x.currency}
                    className="border-b border-slate-100 dark:border-slate-800">
                    <td className="py-2">{x.month} · {x.currency}</td>
                    <td className="py-2">{money(x.payoutsToExcludedAccounts, x.currency)}</td>
                    <td className="py-2">{money(x.returnsToHousehold, x.currency)}</td>
                </tr>)}</tbody>
            </table></div>)}
    </AverioCard>;
}
