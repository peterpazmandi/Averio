import { useId, useState } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { TransactionDto, TransactionFilters } from '../api/transactionApi';
import type { RecordItemRefundRequest } from '../../../shared/api/generated';
import { ApiError } from '../../../shared/api/ApiError';
import { getTransactions } from '../api/transactionApi';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { AverioLogo } from '../../../shared/components/branding/AverioLogo';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { formatDecimalAmount, normalizeBalanceInput, subtractDecimalAmounts } from '../../../shared/money/decimalAmount';
import { itemRefundKeys, itemRefundsApi } from './itemRefundQueries';

type Props = { userId: string; householdId: string; transaction: TransactionDto; onClose: () => void; onChanged?: () => Promise<void> };
type RefundSubmission = { transaction: TransactionDto; credit?: TransactionDto; request: RecordItemRefundRequest; outcomeUnknown: boolean };
const isPositive = (value: string) => subtractDecimalAmounts(value, '0') !== '0' && !value.startsWith('-');

export function RecordItemRefundDialog({ userId, householdId, transaction: savedTransaction, onClose, onChanged }: Props) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const id = useId();
    const today = new Date().toISOString().slice(0, 10);
    // Keep an uncertain write's complete intent stable across reconnect refetches.
    const [submission, setSubmission] = useState<RefundSubmission | null>(null);
    const transaction = submission?.transaction ?? savedTransaction;
    const [mode, setMode] = useState<'new' | 'existing'>('new');
    const [itemId, setItemId] = useState(transaction.items.find(i => isPositive(i.amount))?.id ?? '');
    const [amount, setAmount] = useState('');
    const [date, setDate] = useState(today);
    const [creditId, setCreditId] = useState('');
    const [received, setReceived] = useState(false);
    const [operationId, setOperationId] = useState(() => crypto.randomUUID());
    const [search, setSearch] = useState('');
    const [from, setFrom] = useState(transaction.occurredOn);
    const [to, setTo] = useState(today);
    const [filtersOpen, setFiltersOpen] = useState(false);
    const changed = () => { setOperationId(crypto.randomUUID()); setReceived(false); };
    const filtersChanged = () => { setCreditId(''); changed(); };
    const resetFilters = () => { setSearch(''); setFrom(transaction.occurredOn); setTo(today); filtersChanged(); };
    const filters = { search: search.trim(), from: from || transaction.occurredOn, to: to || today };
    const filterCount = Number(!!filters.search) + Number(filters.from !== transaction.occurredOn) + Number(filters.to !== today);
    const validRange = filters.from >= transaction.occurredOn && filters.to <= today && filters.from <= filters.to;
    const credits = useInfiniteQuery({
        queryKey: [...itemRefundKeys.credits(userId, householdId, transaction.accountId), filters],
        initialPageParam: undefined as Pick<TransactionFilters, 'cursorDate' | 'cursorCreatedAt' | 'cursorId'> | undefined,
        queryFn: ({ pageParam, signal }) => getTransactions(householdId, {
            accountId: transaction.accountId, kind: 'Income', pageSize: 25, ...filters, ...pageParam,
        }, signal),
        getNextPageParam: page => { const last = page.items.at(-1); return page.hasMore && last ? { cursorDate: last.occurredOn, cursorCreatedAt: last.createdAt, cursorId: last.id } : undefined; },
        enabled: mode === 'existing' && validRange && !submission,
    });
    const item = transaction.items.find(i => i.id === itemId);
    const options = submission?.credit ? [submission.credit] : credits.data?.pages.flatMap(p => p.items).filter(c => c.canModify && c.status === 'Posted' && !c.isArchived
        && c.visibility === transaction.visibility && c.currency === transaction.currency
        && c.occurredOn >= filters.from && c.occurredOn <= filters.to
        && item && isPositive(c.amount)
        && !subtractDecimalAmounts(item.amount, c.amount).startsWith('-')) ?? [];
    const credit = options.find(c => c.id === creditId);
    const value = normalizeBalanceInput(mode === 'existing' ? credit?.amount ?? '' : amount);
    const validAmount = !!item && !!value && value !== '0' && !value.startsWith('-') && !subtractDecimalAmounts(item.amount, value).startsWith('-');
    const validDate = !!date && date >= transaction.occurredOn && date <= today;
    const valid = validAmount && (mode === 'new' ? validDate : validRange && credits.isSuccess);
    const language = i18n.resolvedLanguage ?? i18n.language;
    const money = (v: string) => formatDecimalAmount(v, transaction.currency, language);
    const displayDate = (v: string) => new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(v + 'T00:00:00Z'));
    const mutation = useMutation({ mutationFn: (request: RecordItemRefundRequest) => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsPost({
        householdId, transactionId: transaction.id, recordItemRefundRequest: request,
    }), onSuccess: async () => {
        if (onChanged) await onChanged(); else await client.invalidateQueries();
        onClose();
    }, onError: error => {
        // A first, definite rejection permits a new review. Once a response has
        // been lost, a later rejection cannot resolve whether that write committed
        // (for example, access may have changed). Retain that original intent.
        if (!submission?.outcomeUnknown && error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408) {
            setSubmission(null); setReceived(false); setOperationId(crypto.randomUUID());
        } else setSubmission(current => current ? { ...current, outcomeUnknown: true } : null);
    } });
    const fieldsLocked = mutation.isPending || submission !== null;
    const canSubmit = !!submission || (valid && received);
    function submit(event: React.FormEvent) {
        event.preventDefault();
        if (mutation.isPending || !canSubmit) return;
        const next = submission ?? { transaction, credit, outcomeUnknown: false, request: {
            transactionItemId: itemId, amount: value!, receivedOn: mode === 'existing' ? credit!.occurredOn : date,
            operationId, expectedUpdatedAt: transaction.updatedAt, moneyReceived: received,
            creditTransactionId: mode === 'existing' ? credit!.id : null,
            expectedCreditUpdatedAt: mode === 'existing' ? credit!.updatedAt : null,
        } };
        setSubmission(next);
        mutation.mutate(next.request);
    }
    const stepHeading = (step: number, title: string) => <h3 id={`${id}-step-${step}`} className="flex items-center gap-3 text-base font-semibold">
        <span aria-hidden="true" className="flex size-8 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-sm font-bold text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">{step}</span>
        <span><span className="sr-only">{t('itemRefunds.step', { number: step })} · </span>{title}</span>
    </h3>;
    return <AverioDialog open wide title={t('itemRefunds.add')} closeLabel={t('common.actions.close')} busy={mutation.isPending} onDismiss={onClose}
        actions={<><AverioButton disabled={mutation.isPending} onClick={onClose}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="primary" type="submit" form="item-refund" disabled={mutation.isPending || !canSubmit}>{t(submission && mutation.isError ? 'itemRefunds.retryRefund' : mode === 'existing' ? 'itemRefunds.link' : 'itemRefunds.record')}</AverioButton></>}>
        <form id="item-refund" className="grid gap-5" onSubmit={submit}>
            {submission && mutation.isError && <p role="status" className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">{t('itemRefunds.uncertain')}</p>}
            <AverioCard role="group" aria-labelledby={`${id}-step-1`} className="grid gap-4">
                <div className="flex items-center justify-between gap-2">{stepHeading(1, t('itemRefunds.stepItem'))}<InfoPopover label={t('itemRefunds.item')}>{t('itemRefunds.savedHelp')}</InfoPopover></div>
                <AverioField>{t('itemRefunds.item')}<AverioSelect aria-label={t('itemRefunds.item')} value={itemId} disabled={fieldsLocked} onChange={e => { setItemId(e.target.value); setCreditId(''); changed(); }}>
                    {transaction.items.filter(i => isPositive(i.amount)).map(i => <option key={i.id} value={i.id}>{i.description} · {money(i.amount)}</option>)}
                </AverioSelect></AverioField>
                {item && <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-slate-50 p-3 text-sm dark:bg-slate-900"><span>{transaction.accountName} · {transaction.currency}</span><span>{t('itemRefunds.remaining')}: <strong>{money(item.amount)}</strong></span></div>}
            </AverioCard>
            <AverioCard role="group" aria-labelledby={`${id}-step-2`} className="grid gap-4">
                {stepHeading(2, t('itemRefunds.stepMoney'))}
                <fieldset className="grid gap-3 sm:grid-cols-2"><legend aria-label={t('itemRefunds.mode')} className="mb-2 w-full text-sm font-semibold"><span className="flex items-center justify-between gap-2"><span>{t('itemRefunds.mode')}</span><InfoPopover label={t('itemRefunds.mode')}>
                    <p><strong>{t('itemRefunds.newCredit')}</strong><br />{t('itemRefunds.newHint')}</p>
                    <p className="mt-3"><strong>{t('itemRefunds.existingCredit')}</strong><br />{t('itemRefunds.existingHint')}</p>
                    <p className="mt-3">{t('itemRefunds.existingHelp')}</p>
                </InfoPopover></span></legend>
                    {(['new', 'existing'] as const).map(v => <label key={v} className={`flex min-h-11 cursor-pointer items-start gap-3 rounded-xl border p-3 text-sm transition-colors ${mode === v ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40' : 'border-slate-200 dark:border-slate-700'}`}>
                        <input className="mt-1 size-4 shrink-0 accent-emerald-500" type="radio" name="refund-mode" value={v} checked={mode === v} aria-label={t(v === 'new' ? 'itemRefunds.newCredit' : 'itemRefunds.existingCredit')} disabled={fieldsLocked} onChange={() => { setMode(v); changed(); }} />
                        <span className="font-semibold">{t(v === 'new' ? 'itemRefunds.newCredit' : 'itemRefunds.existingCredit')}</span>
                    </label>)}
                </fieldset>
                {mode === 'new' ? <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('itemRefunds.amount')} · {transaction.currency}<AverioInput required inputMode="decimal" value={amount} disabled={fieldsLocked} aria-invalid={!!amount && !validAmount || undefined} onChange={e => { setAmount(e.target.value.replace(',', '.')); changed(); }} /></AverioField>
                    <AverioField>{t('itemRefunds.date')}<AverioInput required type="date" min={transaction.occurredOn} max={today} value={date} disabled={fieldsLocked} aria-invalid={!validDate || undefined} onChange={e => { setDate(e.target.value); changed(); }} /></AverioField>
                </div> : <div className="grid gap-4">
                    <div className="flex items-center justify-between gap-3">
                        <div className="flex min-w-0 items-center gap-1"><span className="text-sm font-semibold">{t('itemRefunds.filters')}</span><InfoPopover label={t('itemRefunds.filters')}>{t('itemRefunds.matchHelp', { amount: money(item?.amount ?? '0') })}</InfoPopover></div>
                        <AverioButton size="icon" className="relative aria-expanded:border-emerald-500 aria-expanded:bg-emerald-50 dark:aria-expanded:bg-emerald-950/40" disabled={fieldsLocked}
                            aria-label={t('transactionManagement.filters')} title={t('transactionManagement.filters')}
                            aria-expanded={filtersOpen} aria-controls={`${id}-filters`} aria-describedby={`${id}-filter-count`}
                            onClick={() => setFiltersOpen(open => !open)}>
                            <ConceptIcon name="filters" />
                            {filterCount > 0 && <span aria-hidden="true" className="absolute -right-1 -top-1 flex size-5 items-center justify-center rounded-full bg-emerald-600 text-[10px] text-white">{filterCount}</span>}
                        </AverioButton>
                        <span id={`${id}-filter-count`} className="sr-only">{t('itemRefunds.activeFilters', { count: filterCount })}</span>
                    </div>
                    <div id={`${id}-filters`} hidden={!filtersOpen}>
                        <fieldset disabled={fieldsLocked} className="grid min-w-0 gap-3 rounded-xl bg-slate-50 p-3 dark:bg-slate-900">
                            <legend className="sr-only">{t('itemRefunds.filters')}</legend>
                            <AverioField>{t('itemRefunds.search')}<AverioInput type="search" maxLength={160} value={search} onChange={e => { setSearch(e.target.value); filtersChanged(); }} onKeyDown={e => { if (e.key === 'Enter') e.preventDefault(); }} /></AverioField>
                            <div className="grid gap-3 sm:grid-cols-2">
                                <AverioField>{t('itemRefunds.from')}<AverioInput type="date" min={transaction.occurredOn} max={today} value={from} aria-invalid={!validRange || undefined} onChange={e => { setFrom(e.target.value); filtersChanged(); }} /></AverioField>
                                <AverioField>{t('itemRefunds.to')}<AverioInput type="date" min={transaction.occurredOn} max={today} value={to} aria-invalid={!validRange || undefined} onChange={e => { setTo(e.target.value); filtersChanged(); }} /></AverioField>
                            </div>
                            <div><AverioButton onClick={resetFilters}>{t('itemRefunds.resetFilters')}</AverioButton></div>
                        </fieldset>
                    </div>
                    {!validRange ? <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('itemRefunds.invalidDates')}</p> : <>
                        {credits.isPending && <p role="status">{t('common.status.loading')}</p>}
                        {credits.isError && <p role="alert">{t('itemRefunds.error')}</p>}
                        {!!options.length && <fieldset className="grid min-w-0 gap-2"><legend className="mb-2 text-sm font-semibold">{t('itemRefunds.chooseCredit')}</legend>
                            <div className="grid max-h-72 gap-2 overflow-y-auto rounded-xl p-1">
                                {options.map(c => <label key={c.id} data-credit-id={c.id} className={`flex min-w-0 cursor-pointer items-start gap-3 rounded-xl border p-3 text-sm ${creditId === c.id ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40' : 'border-slate-200 dark:border-slate-700'}`}>
                                    <input className="mt-1 size-4 shrink-0 accent-emerald-500" type="radio" name="refund-credit" value={c.id} checked={creditId === c.id} disabled={fieldsLocked || !credits.isSuccess} onChange={() => { setCreditId(c.id); changed(); }} />
                                    <span className="grid min-w-0 flex-1 gap-1"><span className="break-words font-semibold">{c.merchant || c.description}</span>{c.merchant && <span className="break-words text-xs">{c.items.map(i => i.description).join(' · ')}</span>}{c.note && <span className="line-clamp-2 break-words text-xs text-slate-600 dark:text-slate-300">{c.note}</span>}<span className="flex flex-wrap justify-between gap-2"><span className="text-slate-600 dark:text-slate-300">{displayDate(c.occurredOn)}</span><strong>{money(c.amount)}</strong></span></span>
                                </label>)}
                            </div>
                        </fieldset>}
                        {credits.isSuccess && options.length === 0 && <div role="status" className="grid justify-items-center gap-2 rounded-xl bg-slate-50 p-4 text-center text-sm dark:bg-slate-900"><div aria-hidden="true"><AverioLogo className="size-10" /></div><p className="font-semibold">{t('itemRefunds.none')}</p><p>{t(credits.hasNextPage ? 'itemRefunds.moreHelp' : 'itemRefunds.noMatchesHelp')}</p><AverioButton onClick={resetFilters}>{t('itemRefunds.resetFilters')}</AverioButton></div>}
                        {(credits.hasNextPage || credits.isError) && <AverioButton disabled={credits.isFetching || fieldsLocked} onClick={() => { void (credits.isError ? credits.refetch() : credits.fetchNextPage()); }}>{t(credits.isError ? 'common.actions.retry' : 'itemRefunds.more')}</AverioButton>}
                    </>}
                </div>}
                {item && value && !validAmount && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('itemRefunds.maximum', { amount: money(item.amount) })}</p>}
                {mode === 'new' && !validDate && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('itemRefunds.invalidDates')}</p>}
            </AverioCard>
            <AverioCard role="group" aria-labelledby={`${id}-step-3`} className="grid gap-4">
                <div className="flex items-center justify-between gap-2">{stepHeading(3, t('itemRefunds.stepReview'))}<InfoPopover label={t('itemRefunds.stepReview')}><p>{t('itemRefunds.reviewHelp')}</p><p className="mt-3">{t('itemRefunds.noIncome')}</p></InfoPopover></div>
                {valid && item ? <dl className="grid gap-3 rounded-xl border border-slate-300 bg-slate-200 p-4 text-sm sm:grid-cols-2 dark:border-slate-600 dark:bg-slate-800">
                    <div className="min-w-0"><dt className="text-slate-600 dark:text-slate-300">{t('itemRefunds.item')}</dt><dd className="break-words font-semibold">{item.description}</dd></div>
                    <div><dt className="text-slate-600 dark:text-slate-300">{t('itemRefunds.amount')}</dt><dd className="font-semibold">{money(value!)}</dd></div>
                    <div><dt className="text-slate-600 dark:text-slate-300">{t('itemRefunds.date')}</dt><dd>{displayDate(mode === 'existing' ? credit!.occurredOn : date)}</dd></div>
                    <div><dt className="text-slate-600 dark:text-slate-300">{t('itemRefunds.netAfter')}</dt><dd className="font-semibold">{money(subtractDecimalAmounts(transaction.amount, value!))}</dd></div>
                </dl> : null}
                <div className="flex items-center gap-2 text-sm"><span>{t('personalMoney.itemAllocationTitle')}</span><InfoPopover label={t('personalMoney.itemAllocationTitle')}>{t('itemRefunds.allowanceHelp')}</InfoPopover></div>
                <label className="flex min-h-11 items-start gap-3 rounded-xl border border-emerald-200 p-3 text-sm font-semibold dark:border-emerald-900"><input className="mt-1 size-4 shrink-0 accent-emerald-500" type="checkbox" checked={received} disabled={fieldsLocked || !valid} onChange={e => setReceived(e.target.checked)} />{t('itemRefunds.received')}</label>
            </AverioCard>
            {mutation.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{mutation.error.message}</p>}
        </form>
    </AverioDialog>;
}
