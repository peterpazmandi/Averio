import { RecordItemRefundDialog } from './RecordItemRefundDialog';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ItemRefundResponse } from '../../../shared/api/generated';
import type { TransactionDto } from '../api/transactionApi';
import { transactionQueries } from '../api/transactionQueries';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioDisclosure } from '../../../shared/components/ui/AverioDisclosure';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { formatDecimalAmount, sumDecimalAmounts } from '../../../shared/money/decimalAmount';
import { itemRefundKeys, itemRefundQueries, itemRefundsApi } from './itemRefundQueries';

type Props = { userId: string; householdId: string; transaction: TransactionDto; onChanged?: () => Promise<void>; hasUnsavedChanges?: boolean; disabled?: boolean };
const linkClass = 'inline-flex min-h-11 items-center rounded-lg text-sm font-semibold text-emerald-700 underline underline-offset-4 focus-visible:outline-2 dark:text-emerald-400';

export function ItemRefundsPanel(props: Props) {
    // Observe the parent's live saved query without replacing its editing draft.
    const saved = useQuery({ ...transactionQueries.detail(props.userId, props.householdId, props.transaction.id), enabled: false, initialData: props.transaction });
    return <SavedItemRefundsPanel {...props} transaction={saved.data?.id === props.transaction.id ? saved.data : props.transaction} />;
}

function SavedItemRefundsPanel(props: Props) {
    const { transaction, userId, householdId } = props;
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const records = useQuery({ ...itemRefundQueries.receipt(userId, householdId, transaction.id), enabled: transaction.kind === 'Expense' });
    const [adding, setAdding] = useState(false);
    const [reversing, setReversing] = useState<ItemRefundResponse | null>(null);
    const money = (v: string) => formatDecimalAmount(v, transaction.currency, i18n.resolvedLanguage ?? i18n.language);
    const returned = sumDecimalAmounts(transaction.items.map(i => i.refundedAmount ?? '0'));
    const history = transaction.items.some(i => i.hasRefundHistory);
    const canModify = transaction.canModify && transaction.status === 'Posted' && !transaction.isArchived;
    const hasAvailable = transaction.items.some(i => i.amount !== '0');
    const reverse = useMutation({ mutationFn: () => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsRefundIdCancellationPost({
        householdId, transactionId: transaction.id, refundId: reversing!.id,
        cancelItemRefundRequest: { expectedVersion: reversing!.version, expectedUpdatedAt: transaction.updatedAt },
    }), onSuccess: async () => {
        if (props.onChanged) await props.onChanged(); else await client.invalidateQueries();
        setReversing(null);
    } });
    if (transaction.kind === 'Refund') return <RefundOrigin {...props} />;
    if (transaction.kind !== 'Expense') return null;
    return <AverioCard className="mb-4 grid gap-3" role="region" aria-label={t('itemRefunds.title')}>
        <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2"><h3 className="font-semibold">{t('itemRefunds.title')}</h3><InfoPopover label={t('itemRefunds.title')}><p>{t('itemRefunds.expenseHelp')}</p>{history && <p className="mt-3">{t('itemRefunds.protected')}</p>}</InfoPopover></div>
            {canModify && <AverioButton disabled={props.disabled || !hasAvailable || !records.isSuccess || props.hasUnsavedChanges} onClick={() => setAdding(true)}>{t('itemRefunds.add')}</AverioButton>}
        </div>
        {props.hasUnsavedChanges && <p className="text-sm text-amber-800 dark:text-amber-200">{t('itemRefunds.savedHelp')}</p>}
        {history && <>
            <dl className="grid gap-3 text-sm sm:grid-cols-3">
                <div><dt>{t('itemRefunds.original')}</dt><dd className="font-semibold">{money(sumDecimalAmounts([transaction.amount, returned]))}</dd></div>
                <div><dt>{t('itemRefunds.returned')}</dt><dd className="font-semibold">{money(returned)}</dd></div>
                <div><dt>{t('itemRefunds.net')}</dt><dd data-testid="refund-net-expense" className="font-semibold">{money(transaction.amount)}</dd></div>
            </dl>
        </>}
        {records.isPending && <p role="status">{t('common.status.loading')}</p>}
        {records.isError && <div role="alert"><p>{t('itemRefunds.error')}</p><AverioButton onClick={() => { void records.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
        {history && records.isSuccess && <AverioDisclosure title={t('itemRefunds.details')} defaultOpen>
            <div className="grid gap-3">
                {transaction.items.map(item => <div key={item.id} className="min-w-0 border-b border-slate-200 pb-3 text-sm dark:border-slate-700">
                    <p className="break-words font-semibold">{item.description}</p><p className="break-words text-xs">{t('systemCategories.' + item.categoryName, { defaultValue: item.categoryName })}</p>
                    <p>{t('itemRefunds.original')}: {money(sumDecimalAmounts([item.amount, item.refundedAmount ?? '0']))} · {t('itemRefunds.returned')}: {money(item.refundedAmount ?? '0')} · {t('itemRefunds.net')}: {money(item.amount)}</p>
                    {records.data.filter(r => r.transactionItemId === item.id).map(r => <div key={r.id} data-refund-id={r.id} className="mt-2 flex flex-wrap items-center justify-between gap-2 rounded-xl bg-slate-50 p-3 dark:bg-slate-900">
                        <span>{new Intl.DateTimeFormat(i18n.resolvedLanguage ?? i18n.language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(r.receivedOn + 'T00:00:00Z'))} · {money(r.amount)} {r.cancelledAt && <span className="ml-2 rounded-full bg-slate-200 px-2 py-1 text-xs dark:bg-slate-700">{t('itemRefunds.reversed')}</span>}</span>
                        <div className="flex flex-wrap gap-2"><Link to="/app/transactions" search={{ transaction: r.creditTransactionId }} className={linkClass}>{t('itemRefunds.viewCredit')}</Link>
                            {canModify && !r.cancelledAt && <AverioButton disabled={props.disabled || props.hasUnsavedChanges} onClick={() => { reverse.reset(); setReversing(r); }}>{t('itemRefunds.reverse')}</AverioButton>}</div>
                    </div>)}
                </div>)}
            </div>
        </AverioDisclosure>}
        {adding && <RecordItemRefundDialog {...props} onClose={() => setAdding(false)} />}
        {reversing && <AverioDialog open title={t('itemRefunds.reverseTitle')} closeLabel={t('common.actions.close')} busy={reverse.isPending} onDismiss={() => setReversing(null)}
            actions={<><AverioButton disabled={reverse.isPending} onClick={() => setReversing(null)}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="danger" disabled={reverse.isPending} onClick={() => reverse.mutate()}>{t('itemRefunds.reverse')}</AverioButton></>}>
            <p>{t('itemRefunds.reverseHelp')}</p>{reverse.isError && <p role="alert">{reverse.error.message}</p>}
        </AverioDialog>}
    </AverioCard>;
}

function RefundOrigin({ userId, householdId, transaction }: Props) {
    const { t } = useTranslation();
    const origin = useQuery({ queryKey: [...itemRefundKeys.receipt(userId, householdId, transaction.id), 'origin'],
        queryFn: ({ signal }) => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsOriginGet({ householdId, transactionId: transaction.id }, { signal }) });
    return <AverioCard className="grid gap-2"><div className="flex items-center gap-2"><h3 className="font-semibold">{t('itemRefunds.refund')}</h3><InfoPopover label={t('itemRefunds.refund')}>{t('itemRefunds.creditHelp')}</InfoPopover></div>
        {origin.isPending && <p role="status">{t('common.status.loading')}</p>}{origin.isError && <p role="alert">{t('itemRefunds.error')}</p>}
        {origin.data && <Link to="/app/transactions" search={{ transaction: origin.data.transactionId }} className={linkClass}>{t('itemRefunds.viewOriginal')}</Link>}
    </AverioCard>;
}
