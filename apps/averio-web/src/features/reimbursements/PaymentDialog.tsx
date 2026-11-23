import { useId, useState } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { LinkReimbursementPaymentRequest, ReimbursementClaimResponse, ReimbursementPaymentResponse } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';
import { AverioLogo } from '../../shared/components/branding/AverioLogo';
import { formatDecimalAmount, normalizeBalanceInput, subtractDecimalAmounts } from '../../shared/money/decimalAmount';
import { reimbursementKeys, reimbursementsApi } from './reimbursementQueries';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';

type Props = { userId: string; householdId: string; claim: ReimbursementClaimResponse; payment?: ReimbursementPaymentResponse; onClose: () => void; onChanged?: () => Promise<void> };
export function PaymentDialog({ userId, householdId, claim, payment, onClose, onChanged }: Props) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const form = useId();
    const [search, setSearch] = useState('');
    const [creditId, setCreditId] = useState('');
    const [amount, setAmount] = useState('');
    const [confirmed, setConfirmed] = useState(false);
    const [submission, setSubmission] = useState<{ request: LinkReimbursementPaymentRequest; unknown: boolean } | null>(null);
    const money = (amount: string) => formatDecimalAmount(amount, claim.currency, i18n.resolvedLanguage ?? i18n.language);
    const candidates = useInfiniteQuery({
        queryKey: [...reimbursementKeys.household(userId, householdId), claim.id, 'candidates', claim.version, search],
        initialPageParam: 0,
        queryFn: ({ pageParam, signal }) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdPaymentCandidatesGet({ householdId, claimId: claim.id, search, offset: pageParam }, { signal }),
        getNextPageParam: (last, pages) => last.hasMore ? pages.length * 20 : undefined,
        enabled: !payment && !submission,
    });
    const rows = candidates.data?.pages.flatMap(p => p.items) ?? [];
    const credit = rows.find(c => c.id === creditId);
    const allocation = normalizeBalanceInput(amount);
    const validAmount = !!credit && !!allocation && allocation !== '0' && !allocation.startsWith('-')
        && !subtractDecimalAmounts(credit.availableAmount, allocation).startsWith('-')
        && !subtractDecimalAmounts(claim.outstandingAmount, allocation).startsWith('-');
    const refresh = async () => {
        if (onChanged) await onChanged();
        else await Promise.all([client.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }),
            client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) })]);
        onClose();
    };
    const link = useMutation({
        mutationFn: (request: LinkReimbursementPaymentRequest) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdPaymentsPost({ householdId, claimId: claim.id, linkReimbursementPaymentRequest: request }),
        onSuccess: refresh,
        onError: error => {
            if (!submission?.unknown && error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408) {
                setSubmission(null); setConfirmed(false);
            } else setSubmission(s => s ? { ...s, unknown: true } : null);
        },
    });
    // Keep the selected link and its original claim version stable on every retry.
    const unlink = useMutation({ mutationFn: () => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdPaymentsPaymentIdUnlinkPost({
        householdId, claimId: claim.id, paymentId: payment!.id, unlinkReimbursementPaymentRequest: { expectedVersion: claim.version },
    }), onSuccess: refresh });
    const dismiss = async () => {
        if (onChanged && (submission?.unknown || unlink.isError)) await onChanged();
        onClose();
    };
    const pending = link.isPending || unlink.isPending;
    const locked = pending || submission !== null;
    const error = payment ? unlink.error : link.error;
    return <AverioDialog open title={t(payment ? 'reimbursements.payments.unlink' : 'reimbursements.payments.link')} closeLabel={t('common.actions.close')}
        onDismiss={() => { void dismiss(); }} busy={pending} actions={<>
            <AverioButton disabled={pending} onClick={() => { void dismiss(); }}>{t('common.actions.close')}</AverioButton>
            <AverioButton type="submit" form={form} variant={payment ? 'danger' : 'primary'} disabled={pending || (!payment && !submission && (!confirmed || !validAmount))}>
                {t(error && (submission || payment) ? 'common.actions.retry' : payment ? 'reimbursements.payments.unlink' : 'reimbursements.payments.link')}
            </AverioButton></>}>
        <form id={form} className="grid gap-4" onSubmit={e => {
            e.preventDefault(); if (pending) return;
            if (payment) { unlink.mutate(); return; }
            if (!submission && (!credit || !confirmed || !validAmount)) return;
            const next = submission ?? { unknown: false, request: { creditTransactionId: credit!.id, expectedCreditUpdatedAt: credit!.updatedAt,
                expectedVersion: claim.version, operationId: crypto.randomUUID(), moneyReceived: true, amount: allocation! } };
            setSubmission(next); link.mutate(next.request);
        }}>
            <AverioCard className="space-y-2"><h3 className="font-semibold">{claim.provider}  {claim.description}</h3>
                <p>{t('reimbursements.outstanding')} <strong>{money(claim.outstandingAmount)}</strong></p>
                {payment && <p>{t('reimbursements.received')} <strong>{money(payment.amount)}</strong>  {payment.receivedOn}</p>}
            </AverioCard>
            {payment ? <p>{t('reimbursements.payments.unlinkHelp')}</p> : <>
                <AverioCard className="grid gap-3">
                    <div className="flex items-center gap-2"><h3 className="font-semibold">{t('reimbursements.payments.choose')}</h3><InfoPopover label={t('reimbursements.payments.choose')}>{t('reimbursements.payments.help')}</InfoPopover></div>
                    <AverioField>{t('reimbursements.payments.search')}<AverioInput value={search} maxLength={160} disabled={locked} onChange={e => { setSearch(e.target.value); setCreditId(''); setAmount(''); setConfirmed(false); }} /></AverioField>
                    {candidates.isPending && <p role="status">{t('reimbursements.loading')}</p>}
                    {candidates.isError && <p role="alert">{t('reimbursements.loadError')}</p>}
                    {candidates.isSuccess && rows.length === 0 && <div className="grid justify-items-center gap-3 py-5 text-center"><span aria-hidden="true"><AverioLogo /></span><h4 className="font-semibold">{t('reimbursements.payments.empty')}</h4><p>{t('reimbursements.payments.emptyHelp')}</p>
                        {search && <AverioButton onClick={() => { setSearch(''); setCreditId(''); setAmount(''); setConfirmed(false); }}>{t('reimbursements.payments.reset')}</AverioButton>}</div>}
                    {rows.map(row => <label key={row.id} className="flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-700">
                        <input type="radio" name={form + '-credit'} className="size-4 shrink-0 accent-emerald-500" value={row.id} checked={creditId === row.id} disabled={locked} onChange={() => { setCreditId(row.id); setAmount(subtractDecimalAmounts(row.availableAmount, claim.outstandingAmount).startsWith('-') ? row.availableAmount : claim.outstandingAmount); setConfirmed(false); }} />
                        <span className="min-w-0 break-words">{row.description}  {row.receivedOn}  <strong>{money(row.amount)}</strong></span>
                    </label>)}
                    {(candidates.hasNextPage || candidates.isError) && <AverioButton disabled={locked || candidates.isFetching} onClick={() => { void (candidates.isError ? candidates.refetch() : candidates.fetchNextPage()); }}>{t(candidates.isError ? 'common.actions.retry' : 'reimbursements.payments.more')}</AverioButton>}
                </AverioCard>
                {credit && <AverioField>{t('reimbursements.payments.allocate')}<AverioInput inputMode="decimal" value={amount} disabled={locked} onChange={e => { setAmount(e.target.value); setConfirmed(false); }} /></AverioField>}
                {credit && <p>{t('reimbursements.payments.creditAvailable')} <strong>{money(credit.availableAmount)}</strong></p>}
                {credit && !validAmount && <p role="alert" className="text-red-700 dark:text-red-300">{t('reimbursements.payments.amountInvalid')}</p>}
                {credit && validAmount && <p>{t('reimbursements.payments.remaining')} <strong>{money(subtractDecimalAmounts(claim.outstandingAmount, allocation!))}</strong>  {t('reimbursements.payments.creditAfter')}: <strong>{money(subtractDecimalAmounts(credit.availableAmount, allocation!))}</strong></p>}
                <label className="flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 shrink-0 accent-emerald-500" checked={confirmed} disabled={locked} onChange={e => setConfirmed(e.target.checked)} />{t('reimbursements.payments.confirm')}</label>
            </>}
            {submission?.unknown && <p role="status" className="text-amber-800 dark:text-amber-200">{t('reimbursements.payments.uncertain')}</p>}
            {error && <p role="alert" className="text-red-700 dark:text-red-300">{error instanceof ApiError ? error.message : t('reimbursements.payments.error')}</p>}
        </form>
    </AverioDialog>;
}

