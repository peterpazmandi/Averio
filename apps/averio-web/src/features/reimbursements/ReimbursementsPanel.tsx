import { ReimbursementRecoveryPanel } from './ReimbursementRecoveryPanel';
import { PaymentDialog } from './PaymentDialog';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';
import type { ReimbursementPaymentResponse } from '../../shared/api/generated';
import { EditClaimDialog } from './EditClaimDialog';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';
import { ManualClaimDialog } from './ManualClaimDialog';
import type { TransactionDto } from '../transactions/api/transactionApi';
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
import { moneyBackQueryKeys } from '../money-back/moneyBackQueryKeys';

function VisibleReimbursementsPanel({ userId, householdId, transactionId, sourceTransaction, onChanged, hasUnsavedChanges = false, className = '' }: { userId: string; householdId: string; transactionId?: string; sourceTransaction?: TransactionDto; onChanged?: () => Promise<void>; hasUnsavedChanges?: boolean; className?: string }) {
    const { t, i18n } = useTranslation();
    const query = useQuery(reimbursementQueries.list(userId, householdId, transactionId));
    const [selected, setSelected] = useState<{ claim: ReimbursementClaimResponse; status: string } | null>(null);
    const [editing, setEditing] = useState<ReimbursementClaimResponse | null>(null);
    const [matching, setMatching] = useState<{ claim: ReimbursementClaimResponse; payment?: ReimbursementPaymentResponse } | null>(null);
    const [creating, setCreating] = useState(false);
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? i18n.language);
    const date = (value: string) => new Intl.DateTimeFormat(i18n.resolvedLanguage, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value + 'T00:00:00Z'));
    if (query.isPending) return <p className={className} role="status">{t('reimbursements.loading')}</p>;
    if (query.isError) return <AverioCard className={className}><p role="alert">{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></AverioCard>;
    if (!query.data.length) return sourceTransaction?.canModify && sourceTransaction.kind === 'Expense' && sourceTransaction.amount !== '0' && sourceTransaction.status === 'Posted' && !sourceTransaction.isArchived
        ? <AverioCard className={'flex flex-wrap items-center justify-between gap-3 ' + className}>
            <div className="flex min-w-0 items-center gap-3"><span aria-hidden="true" className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-50 text-xl text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300">↻</span>
                <div><h3 className="text-sm font-semibold">{t('reimbursements.title')}</h3><p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{t('reimbursements.manual.shortHelp')}</p></div></div>
            <AverioButton variant="primary" onClick={() => setCreating(true)}>{t('reimbursements.manual.add')}</AverioButton>
            {creating && <ManualClaimDialog userId={userId} householdId={householdId} transaction={sourceTransaction} onClose={() => setCreating(false)} />}</AverioCard> : null;
    const currencies = [...new Set(query.data.map(c => c.currency))];
    return <section className={'grid min-w-0 gap-3 ' + className} aria-label={t('reimbursements.title')}>
        <div className="flex items-center gap-2"><h2 className="text-lg font-bold">{t('reimbursements.title')}</h2><InfoPopover label={t('reimbursements.title')}>{t('reimbursements.balanceHelp')}</InfoPopover></div>
        {!transactionId && <div className="grid gap-3 sm:grid-cols-2">{currencies.map(currency => <AverioCard key={currency} className="space-y-2">
            <p>{t('reimbursements.potential')} <strong>{money(sumDecimalAmounts(query.data.filter(c => c.currency === currency && !c.paymentUnavailable && c.status === 'NeedsSubmission').map(c => c.potentialAmount)), currency)}</strong></p>
            <p>{t('reimbursements.outstanding')} <strong>{money(sumDecimalAmounts(query.data.filter(c => c.currency === currency).map(c => c.outstandingAmount)), currency)}</strong></p>
        </AverioCard>)}</div>}
        {hasUnsavedChanges && <p role="status" className="text-sm text-amber-800 dark:text-amber-200">{t('transactionManagement.paymentProtection.saveFirst')}</p>}
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
                    {!claim.paymentUnavailable && ['Submitted', 'PartiallyReceived'].includes(claim.status) && <AverioButton variant="primary" disabled={hasUnsavedChanges} onClick={() => setMatching({ claim })}>{t('reimbursements.payments.link')}</AverioButton>}
                    {!claim.paymentUnavailable && claim.status !== 'Cancelled' && <AverioButton onClick={() => setEditing(claim)}>{t('reimbursements.edit.title')}</AverioButton>}
                    {!claim.paymentUnavailable && ['NeedsSubmission', 'Rejected'].includes(claim.status) && <AverioButton variant="primary" onClick={() => setSelected({ claim, status: 'Submitted' })}>{t('reimbursements.markSubmitted')}</AverioButton>}
                    {claim.status === 'Submitted' && claim.receivedAmount === '0' && <AverioButton onClick={() => setSelected({ claim, status: 'Rejected' })}>{t('reimbursements.reject')}</AverioButton>}
                    {claim.status !== 'Cancelled' && claim.receivedAmount === '0' && <AverioButton onClick={() => setSelected({ claim, status: 'Cancelled' })}>{t('reimbursements.cancel')}</AverioButton>}
                </>}
            </div>
            {claim.receivedAmount !== '0' && <p className="text-sm text-amber-800 dark:text-amber-200">{t('reimbursements.payments.protected')}</p>}
            {claim.payments.length > 0 && <div className="grid gap-2"><h4 className="text-sm font-semibold">{t('reimbursements.payments.history')}</h4>{claim.payments.map(payment => <div key={payment.id} data-payment-id={payment.id} className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 py-2 text-sm dark:border-slate-700">
                <span>{date(payment.receivedOn)} · {money(payment.amount, claim.currency)} · {t(payment.unlinkedAt ? 'reimbursements.payments.unlinked' : 'reimbursements.payments.linked')}</span>
                <div className="flex flex-wrap gap-2"><Link className="inline-flex min-h-11 items-center px-3 text-emerald-700 underline dark:text-emerald-300" to="/app/transactions" search={{ transaction: payment.creditTransactionId }}>{t('reimbursements.payments.view')}</Link>
                {claim.canModify && !payment.unlinkedAt && <AverioButton disabled={hasUnsavedChanges} onClick={() => setMatching({ claim, payment })}>{t('reimbursements.payments.unlink')}</AverioButton>}</div>
            </div>)}</div>}
            {transactionId && <details><summary className="min-h-11 cursor-pointer py-3 text-sm font-semibold">{t('reimbursements.history')}</summary><ul className="space-y-2 text-sm">{claim.events.map((event, index) => <li key={index}>{t(event.changeKind === 'PaymentLinked' ? 'reimbursements.payments.linked' : event.changeKind === 'PaymentUnlinked' ? 'reimbursements.payments.unlinked' : event.changeKind === 'Updated' ? 'reimbursements.edit.updated' : event.changeKind === 'BeforeUpdate' ? 'reimbursements.edit.before' : 'reimbursements.status.' + event.status)} · {date(event.recordedAt.slice(0, 10))}{event.reference ? ' · ' + event.reference : ''}
                {event.provider && event.potentialAmount && <span className="block">{event.provider} · {money(event.potentialAmount, claim.currency)}</span>}</li>)}</ul></details>}
        </AverioCard>)}
        {matching && <PaymentDialog userId={userId} householdId={householdId} claim={matching.claim} payment={matching.payment} onChanged={onChanged} onClose={() => setMatching(null)} />}
        {editing && <EditClaimDialog userId={userId} householdId={householdId} claim={editing} onClose={() => setEditing(null)} />}
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
        onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }), client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) }), client.invalidateQueries({ queryKey: moneyBackQueryKeys.household(userId, householdId) })]); onClose(); },
    });
    return <AverioDialog open title={t(submitting ? 'reimbursements.markSubmitted' : status === 'Rejected' ? 'reimbursements.reject' : 'reimbursements.cancel')}
        closeLabel={t('common.actions.close')} onDismiss={onClose} busy={mutation.isPending}
        actions={<><AverioButton disabled={mutation.isPending} onClick={onClose}>{t('common.actions.cancel')}</AverioButton><AverioButton type="submit" form="claim-status" variant="primary" disabled={mutation.isPending || (submitting && (!confirmed || !submittedOn))}>{t('common.actions.confirm')}</AverioButton></>}>
        <form id="claim-status" className="grid gap-4" onSubmit={e => { e.preventDefault(); if (!mutation.isPending) mutation.mutate(); }}>
            <AverioCard className="space-y-2"><p className="font-semibold">{claim.provider} · {claim.description}</p>
                <p>{t('reimbursements.originalPayment')} {formatDecimalAmount(claim.originalPaymentAmount, claim.currency, i18n.resolvedLanguage ?? i18n.language)}</p>
                <p>{t('reimbursements.estimate')} {formatDecimalAmount(claim.potentialAmount, claim.currency, i18n.resolvedLanguage ?? i18n.language)}</p></AverioCard>
            <div className="flex items-center gap-2 text-sm"><span>{t('reimbursements.title')}</span><InfoPopover label={t('reimbursements.title')}>{t('reimbursements.balanceHelp')}</InfoPopover></div>
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

export function ReimbursementsPanel(props: Parameters<typeof VisibleReimbursementsPanel>[0]) {
    return <>{!props.transactionId && <ReimbursementRecoveryPanel userId={props.userId} householdId={props.householdId} className={props.className} />}<VisibleReimbursementsPanel {...props} /></>;
}
