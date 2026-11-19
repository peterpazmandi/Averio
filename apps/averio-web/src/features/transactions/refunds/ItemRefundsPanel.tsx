import { useState } from 'react';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ItemRefundResponse } from '../../../shared/api/generated';
import type { TransactionDto, TransactionFilters } from '../api/transactionApi';
import { getTransactions } from '../api/transactionApi';
import { transactionQueries } from '../api/transactionQueries';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioDisclosure } from '../../../shared/components/ui/AverioDisclosure';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { formatDecimalAmount, normalizeBalanceInput, sumDecimalAmounts, subtractDecimalAmounts } from '../../../shared/money/decimalAmount';
import { itemRefundKeys, itemRefundQueries, itemRefundsApi } from './itemRefundQueries';

type Props = { userId: string; householdId: string; transaction: TransactionDto; onChanged?: () => void; hasUnsavedChanges?: boolean };
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
    }), onSuccess: async () => { await client.invalidateQueries(); props.onChanged?.(); setReversing(null); } });
    if (transaction.kind === 'Refund') return <RefundOrigin {...props} />;
    if (transaction.kind !== 'Expense') return null;
    return <AverioCard className="mb-4 grid gap-3" role="region" aria-label={t('itemRefunds.title')}>
        <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-semibold">{t('itemRefunds.title')}</h3>
            {canModify && <AverioButton disabled={!hasAvailable || !records.isSuccess || props.hasUnsavedChanges} onClick={() => setAdding(true)}>{t('itemRefunds.add')}</AverioButton>}
        </div>
        <p className="text-sm text-slate-600 dark:text-slate-300">{t('itemRefunds.expenseHelp')}</p>
        {props.hasUnsavedChanges && <p className="text-sm text-amber-800 dark:text-amber-200">{t('itemRefunds.savedHelp')}</p>}
        {history && <>
            <dl className="grid gap-3 text-sm sm:grid-cols-3">
                <div><dt>{t('itemRefunds.original')}</dt><dd className="font-semibold">{money(sumDecimalAmounts([transaction.amount, returned]))}</dd></div>
                <div><dt>{t('itemRefunds.returned')}</dt><dd className="font-semibold">{money(returned)}</dd></div>
                <div><dt>{t('itemRefunds.net')}</dt><dd data-testid="refund-net-expense" className="font-semibold">{money(transaction.amount)}</dd></div>
            </dl>
            <p className="text-xs text-slate-600 dark:text-slate-300">{t('itemRefunds.protected')}</p>
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
                            {canModify && !r.cancelledAt && <AverioButton disabled={props.hasUnsavedChanges} onClick={() => { reverse.reset(); setReversing(r); }}>{t('itemRefunds.reverse')}</AverioButton>}</div>
                    </div>)}
                </div>)}
            </div>
        </AverioDisclosure>}
        {adding && <RecordRefundDialog {...props} onClose={() => setAdding(false)} />}
        {reversing && <AverioDialog open title={t('itemRefunds.reverseTitle')} closeLabel={t('common.actions.close')} busy={reverse.isPending} onDismiss={() => setReversing(null)}
            actions={<><AverioButton disabled={reverse.isPending} onClick={() => setReversing(null)}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="danger" disabled={reverse.isPending} onClick={() => reverse.mutate()}>{t('itemRefunds.reverse')}</AverioButton></>}>
            <p>{t('itemRefunds.reverseHelp')}</p>{reverse.isError && <p role="alert">{reverse.error.message}</p>}
        </AverioDialog>}
    </AverioCard>;
}

function RecordRefundDialog({ userId, householdId, transaction, onClose, onChanged }: Props & { onClose: () => void }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const [mode, setMode] = useState<'new' | 'existing'>('new');
    const [itemId, setItemId] = useState(transaction.items.find(i => i.amount !== '0')?.id ?? '');
    const [amount, setAmount] = useState('');
    const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
    const [creditId, setCreditId] = useState('');
    const [received, setReceived] = useState(false);
    const [operationId, setOperationId] = useState(() => crypto.randomUUID());
    const changed = () => setOperationId(crypto.randomUUID());
    const credits = useInfiniteQuery({ queryKey: itemRefundKeys.credits(userId, householdId, transaction.accountId),
        initialPageParam: undefined as Pick<TransactionFilters, 'cursorDate' | 'cursorCreatedAt' | 'cursorId'> | undefined,
        queryFn: ({ pageParam, signal }) => getTransactions(householdId, { accountId: transaction.accountId, kind: 'Income', pageSize: 100, ...pageParam }, signal),
        getNextPageParam: page => { const last = page.items.at(-1); return page.hasMore && last ? { cursorDate: last.occurredOn, cursorCreatedAt: last.createdAt, cursorId: last.id } : undefined; },
        enabled: mode === 'existing',
    });
    const item = transaction.items.find(i => i.id === itemId);
    const options = credits.data?.pages.flatMap(p => p.items).filter(c => c.visibility === transaction.visibility && c.occurredOn >= transaction.occurredOn && c.occurredOn <= new Date().toISOString().slice(0, 10)) ?? [];
    const credit = options.find(c => c.id === creditId);
    const value = normalizeBalanceInput(mode === 'existing' ? credit?.amount ?? '' : amount);
    const valid = !!item && !!value && value !== '0' && !value.startsWith('-') && !subtractDecimalAmounts(item.amount, value).startsWith('-');
    const money = (v: string) => formatDecimalAmount(v, transaction.currency, i18n.resolvedLanguage ?? i18n.language);
    const mutation = useMutation({ mutationFn: () => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsPost({
        householdId, transactionId: transaction.id, recordItemRefundRequest: { transactionItemId: itemId, amount: value!, receivedOn: mode === 'existing' ? credit!.occurredOn : date,
            operationId, expectedUpdatedAt: transaction.updatedAt, moneyReceived: received,
            creditTransactionId: mode === 'existing' ? credit!.id : null, expectedCreditUpdatedAt: mode === 'existing' ? credit!.updatedAt : null },
    }), onSuccess: async () => { await client.invalidateQueries(); onChanged?.(); onClose(); } });
    return <AverioDialog open wide title={t('itemRefunds.add')} closeLabel={t('common.actions.close')} busy={mutation.isPending} onDismiss={onClose}
        actions={<><AverioButton disabled={mutation.isPending} onClick={onClose}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="primary" type="submit" form="item-refund" disabled={mutation.isPending || !valid || !received}>{t(mode === 'existing' ? 'itemRefunds.link' : 'itemRefunds.record')}</AverioButton></>}>
        <form id="item-refund" className="grid gap-4" onSubmit={e => { e.preventDefault(); if (valid && received && !mutation.isPending) mutation.mutate(); }}>
            <p className="text-sm">{t('itemRefunds.savedHelp')}</p>
            <AverioCard className="grid gap-4">
                <AverioField>{t('itemRefunds.item')}<AverioSelect aria-label={t('itemRefunds.item')} value={itemId} disabled={mutation.isPending} onChange={e => { setItemId(e.target.value); changed(); }}>
                    {transaction.items.filter(i => i.amount !== '0').map(i => <option key={i.id} value={i.id}>{i.description} · {money(i.amount)}</option>)}
                </AverioSelect></AverioField>
                {item && <p className="text-sm">{t('itemRefunds.remaining')}: <strong>{money(item.amount)}</strong> · {transaction.accountName}</p>}
                <fieldset className="grid gap-2"><legend className="mb-2 text-sm font-semibold">{t('itemRefunds.mode')}</legend>
                    {(['new', 'existing'] as const).map(v => <label key={v} className="flex min-h-11 items-center gap-3 rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-700"><input type="radio" name="refund-mode" value={v} checked={mode === v} disabled={mutation.isPending} onChange={() => { setMode(v); changed(); }} />{t(v === 'new' ? 'itemRefunds.newCredit' : 'itemRefunds.existingCredit')}</label>)}
                </fieldset>
                {mode === 'new' ? <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('itemRefunds.amount')} · {transaction.currency}<AverioInput required inputMode="decimal" value={amount} disabled={mutation.isPending} aria-invalid={!!amount && !valid || undefined} onChange={e => { setAmount(e.target.value.replace(',', '.')); changed(); }} /></AverioField>
                    <AverioField>{t('itemRefunds.date')}<AverioInput required type="date" min={transaction.occurredOn} max={new Date().toISOString().slice(0, 10)} value={date} disabled={mutation.isPending} onChange={e => { setDate(e.target.value); changed(); }} /></AverioField>
                </div> : <div className="grid gap-3">
                    <p className="text-sm">{t('itemRefunds.existingHelp')}</p>
                    <AverioField>{t('itemRefunds.chooseCredit')}<AverioSelect aria-label={t('itemRefunds.chooseCredit')} required value={creditId} disabled={mutation.isPending} onChange={e => { setCreditId(e.target.value); changed(); }}>
                        <option value="">{t('itemRefunds.chooseCredit')}</option>{options.map(c => <option key={c.id} value={c.id}>{c.occurredOn} · {c.description} · {money(c.amount)}</option>)}
                    </AverioSelect></AverioField>
                    {credits.isPending && <p role="status">{t('common.status.loading')}</p>}
                    {credits.isError && <p role="alert">{t('itemRefunds.error')}</p>}
                    {credits.isSuccess && options.length === 0 && !credits.hasNextPage && <p>{t('itemRefunds.none')}</p>}
                    {(credits.hasNextPage || credits.isError) && <AverioButton disabled={credits.isFetching} onClick={() => { void (credits.isError ? credits.refetch() : credits.fetchNextPage()); }}>{t(credits.isError ? 'common.actions.retry' : 'itemRefunds.more')}</AverioButton>}
                </div>}
                {item && value && !valid && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('itemRefunds.maximum', { amount: money(item.amount) })}</p>}
                <p className="text-sm">{t('itemRefunds.expenseHelp')}</p>
                <div className="flex items-center gap-2 text-sm"><span>{t('personalMoney.itemAllocationTitle')}</span><InfoPopover label={t('itemRefunds.title')}>{t('itemRefunds.allowanceHelp')}</InfoPopover></div>
                <label className="flex min-h-11 items-start gap-3 text-sm font-semibold"><input className="mt-1 size-4 shrink-0 accent-emerald-500" type="checkbox" checked={received} disabled={mutation.isPending} onChange={e => setReceived(e.target.checked)} />{t('itemRefunds.received')}</label>
            </AverioCard>
            {mutation.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{mutation.error.message}</p>}
        </form>
    </AverioDialog>;
}

function RefundOrigin({ userId, householdId, transaction }: Props) {
    const { t } = useTranslation();
    const origin = useQuery({ queryKey: [...itemRefundKeys.receipt(userId, householdId, transaction.id), 'origin'],
        queryFn: ({ signal }) => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsOriginGet({ householdId, transactionId: transaction.id }, { signal }) });
    return <AverioCard className="grid gap-2"><h3 className="font-semibold">{t('itemRefunds.refund')}</h3><p className="text-sm">{t('itemRefunds.creditHelp')}</p>
        {origin.isPending && <p role="status">{t('common.status.loading')}</p>}{origin.isError && <p role="alert">{t('itemRefunds.error')}</p>}
        {origin.data && <Link to="/app/transactions" search={{ transaction: origin.data.transactionId }} className={linkClass}>{t('itemRefunds.viewOriginal')}</Link>}
    </AverioCard>;
}
