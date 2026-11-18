import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ReimbursementClaimResponse, RecurringReimbursementConfiguration } from '../../shared/api/generated';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { ReimbursementFields } from '../recurring/ReimbursementFields';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';
import { reimbursementKeys, reimbursementsApi } from './reimbursementQueries';

export function EditClaimDialog({ userId, householdId, claim, onClose }: { userId: string; householdId: string; claim: ReimbursementClaimResponse; onClose: () => void }) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const accounts = useQuery(accountsQueries.list(userId, householdId));
    const [configuration, setConfiguration] = useState<RecurringReimbursementConfiguration>({ provider: claim.provider, calculation: 'Fixed', value: claim.potentialAmount, maximumAmount: null,
        destinationAccountId: claim.destinationAccountId, paymentDelayDays: claim.paymentDelayDays, submissionReminderDays: claim.submissionReminderDays });
    const mutation = useMutation({ mutationFn: () => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdPut({ householdId, claimId: claim.id,
        updateReimbursementClaimRequest: { expectedVersion: claim.version, _configuration: configuration } }),
        onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }), client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) })]); onClose(); } });
    return <AverioDialog open wide title={t('reimbursements.edit.title')} closeLabel={t('common.actions.close')} onDismiss={onClose} busy={mutation.isPending}
        actions={<><AverioButton onClick={onClose} disabled={mutation.isPending}>{t('common.actions.cancel')}</AverioButton><AverioButton type="submit" form="edit-claim" variant="primary" disabled={mutation.isPending || !accounts.isSuccess}>{t('common.actions.save')}</AverioButton></>}>
        <form id="edit-claim" className="grid gap-4" onSubmit={e => { e.preventDefault(); if (!mutation.isPending && accounts.isSuccess) mutation.mutate(); }}>
            <p className="font-semibold">{claim.description}</p><p className="text-sm">{t('reimbursements.edit.help')}</p>
            {accounts.isPending && <p role="status">{t('common.status.loading')}</p>}
            {accounts.isError && <div role="alert"><p>{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void accounts.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
            {accounts.isSuccess && <ReimbursementFields allowDisable={false} value={configuration} onChange={value => { if (value) setConfiguration(value); }} accounts={accounts.data} currency={claim.currency} paymentAmount={claim.originalPaymentAmount} sourceAccountId={claim.destinationAccountId} busy={mutation.isPending} />}
            {mutation.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{mutation.error.message}</p>}
        </form>
    </AverioDialog>;
}
