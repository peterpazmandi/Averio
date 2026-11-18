import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ReimbursementClaimResponse } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount, sumDecimalAmounts } from '../../shared/money/decimalAmount';
import { reimbursementKeys, reimbursementQueries, reimbursementsApi } from './reimbursementQueries';

export function ReimbursementsPanel({ userId, householdId, transactionId }: { userId: string; householdId: string; transactionId?: string }) {
    const { t, i18n } = useTranslation();
    const query = useQuery(reimbursementQueries.list(userId, householdId, transactionId));
    const [selected, setSelected] = useState<{ claim: ReimbursementClaimResponse; status: string } | null>(null);
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? i18n.language);
    const date = (value: string) => new Intl.DateTimeFormat(i18n.resolvedLanguage, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'));
    if (query.isPending) return <p role="status">{t('reimbursements.loading')}</p>;
    if (query.isError) return <AverioCard><p role="alert">{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></AverioCard>;
    if (!query.data.length) return null;
    const currencies = [...new Set(query.data.map(c => c.currency))];
    return <section className="grid min-w-0 gap-3" aria-label={t('reimbursements.title')}>
        <h2 className="text-lg font-bold">{t('reimbursements.title')}</h2>
        <p className="text-sm">{t('reimbursements.balanceHelp')}</p>
        {!transactionId && <div className="grid gap-3 sm:grid-cols-2">{currencies.map(currency => <AverioCard key={currency} className="space-y-2">
            <p>{t('reimbursements.potential')} <strong>{money(sumDecimalAmounts(query.data.filter(c => c.currency === currency && !c.paymentUnavailable && c.status === 'NeedsSubmission').map(c => c.potentialAmount)), currency)}</strong></p>
            <p>{t('reimbursements.outstanding')} <strong>{money(sumDecimalAmounts(query.data.filter(c => c.currency === currency).map(c => c.outstandingAmount)), currency)}</strong></p>
        </AverioCard>)}</div>}
        {query.data.map(claim => <AverioCard key={claim.id} className="grid gap-3">
            <div className="flex flex-wrap items-start justify-between gap-2"><div><h3 className="break-words font-semibold">{claim.description}</h3><p className="break-words text-sm">{claim.provider} · {date(claim.paymentOn)}</p></div>
                <span className="rounded-full bg-emerald-50 px-3 py-1 text-sm text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{t('reimbursements.status.' + claim.status)}</span></div>
            <dl className="grid gap-2 text-sm sm:grid-cols-2">
                <div><dt>{t(claim.status === 'NeedsSubmission' ? 'reimbursements.potential' : 'reimbursements.estimate')}</dt><dd className="font-semibold">{money(claim.potentialAmount, claim.currency)}</dd></div>
                <div><dt>{t('reimbursements.outstanding')}</dt><dd className="font-semibold">{money(claim.outstandingAmount, claim.currency)}</dd></div>
                <div><dt>{t('reimbursements.received')}</dt><dd>{money(claim.receivedAmount, claim.currency)}</dd></div>
                {claim.submittedOn && <div><dt>{t('reimbursements.submittedOn')}</dt><dd>{date(claim.submittedOn)}</dd></div>}
                {claim.expectedOn && <div><dt>{t('reimbursements.expectedOn')}</dt><dd>{date(claim.expectedOn)}</dd></div>}
                {claim.reference && <div><dt>{t('reimbursements.reference')}</dt><dd className="break-words">{claim.reference}</dd></div>}
            </dl>
            {claim.paymentUnavailable && <p role="status" className="text-sm text-amber-800 dark:text-amber-200">{t('reimbursements.voided')}</p>}
            {claim.paymentChanged && <p role="status" className="text-sm text-amber-800 dark:text-amber-200">{t('reimbursements.paymentChanged')}</p>}
            {claim.submissionReminderDue && <p className="text-sm font-semibold text-amber-800 dark:text-amber-200">{t('reimbursements.submitReminder')}</p>}
            {claim.paymentFollowUpDue && <p className="text-sm font-semibold text-amber-800 dark:text-amber-200">{t('reimbursements.followUp')}</p>}
            {claim.status === 'NeedsSubmission' && <p className="text-sm">{t('reimbursements.externalHelp')}</p>}
            <div className="flex flex-wrap gap-2 border-t border-slate-200 pt-3 dark:border-slate-700">
                {!transactionId && <Link className="inline-flex min-h-11 items-center rounded-xl px-3 text-sm font-semibold text-emerald-700 underline focus-visible:outline-2 dark:text-emerald-300" to="/app/transactions" search={{ transaction: claim.transactionId }}>{t('reimbursements.viewPayment')}</Link>}
                {claim.canModify && <>
                    {!claim.paymentUnavailable && ['NeedsSubmission', 'Rejected'].includes(claim.status) && <AverioButton variant="primary" onClick={() => setSelected({ claim, status: 'Submitted' })}>{t('reimbursements.markSubmitted')}</AverioButton>}
                    {claim.status === 'Submitted' && <AverioButton onClick={() => setSelected({ claim, status: 'Rejected' })}>{t('reimbursements.reject')}</AverioButton>}
                    {claim.status !== 'Cancelled' && <AverioButton onClick={() => setSelected({ claim, status: 'Cancelled' })}>{t('reimbursements.cancel')}</AverioButton>}
                </>}
            </div>
            {transactionId && <details><summary className="min-h-11 cursor-pointer py-3 text-sm font-semibold">{t('reimbursements.history')}</summary><ul className="space-y-2 text-sm">{claim.events.map((event, index) => <li key={index}>{t('reimbursements.status.' + event.status)} · {date(event.recordedAt.slice(0, 10))}{event.reference ? ' · ' + event.reference : ''}</li>)}</ul></details>}
        </AverioCard>)}
        {selected && <ClaimStatusDialog key={selected.claim.id + selected.status} userId={userId} householdId={householdId} claim={selected.claim} status={selected.status} onClose={() => setSelected(null)} />}
    </section>;
}
function ClaimStatusDialog({ userId, householdId, claim, status, onClose }: { userId: string; householdId: string; claim: ReimbursementClaimResponse; status: string; onClose: () => void }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const today = new Date().toISOString().slice(0, 10);
    const [submittedOn, setSubmittedOn] = useState(today);
    const [reference, setReference] = useState('');
    const [confirmed, setConfirmed] = useState(false);
    const submitting = status === 'Submitted';
    const mutation = useMutation({ mutationFn: () => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdStatusPut({ householdId, claimId: claim.id,
        changeReimbursementRequest: { expectedVersion: claim.version, status, receiptSubmitted: confirmed, submittedOn: submitting ? submittedOn : null, reference: submitting ? reference.trim() || null : null } }),
        onSuccess: async () => { await client.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }); onClose(); },
    });
    return <AverioDialog open title={t(submitting ? 'reimbursements.markSubmitted' : status === 'Rejected' ? 'reimbursements.reject' : 'reimbursements.cancel')}
        closeLabel={t('common.actions.close')} onDismiss={onClose} busy={mutation.isPending}
        actions={<><AverioButton disabled={mutation.isPending} onClick={onClose}>{t('common.actions.cancel')}</AverioButton><AverioButton type="submit" form="claim-status" variant="primary" disabled={mutation.isPending || (submitting && (!confirmed || !submittedOn))}>{t('common.actions.confirm')}</AverioButton></>}>
        <form id="claim-status" className="grid gap-4" onSubmit={e => { e.preventDefault(); if (!mutation.isPending) mutation.mutate(); }}>
            <AverioCard className="space-y-2"><p className="font-semibold">{claim.provider} · {claim.description}</p>
                <p>{t('reimbursements.originalPayment')} {formatDecimalAmount(claim.originalPaymentAmount, claim.currency, i18n.resolvedLanguage ?? i18n.language)}</p>
                <p>{t('reimbursements.estimate')} {formatDecimalAmount(claim.potentialAmount, claim.currency, i18n.resolvedLanguage ?? i18n.language)}</p></AverioCard>
            <p className="text-sm">{t('reimbursements.balanceHelp')}</p>
            {submitting ? <>
                <p className="text-sm">{t('reimbursements.externalHelp')}</p>
                <AverioField>{t('reimbursements.submittedOn')}<AverioInput type="date" required min={claim.paymentOn} max={today} value={submittedOn} disabled={mutation.isPending} onChange={e => setSubmittedOn(e.target.value)} /></AverioField>
                <AverioField>{t('reimbursements.reference')}<AverioInput maxLength={160} value={reference} disabled={mutation.isPending} onChange={e => setReference(e.target.value)} /></AverioField>
                <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" required className="size-4 shrink-0 accent-emerald-500" checked={confirmed} disabled={mutation.isPending} onChange={e => setConfirmed(e.target.checked)} />{t('reimbursements.confirmExternal')}</label>
            </> : <p>{t('reimbursements.closeHelp')}</p>}
            {mutation.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{t('reimbursements.saveError')}</p>}
        </form>
    </AverioDialog>;
}
