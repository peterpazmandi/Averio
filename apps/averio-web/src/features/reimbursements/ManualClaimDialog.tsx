import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { RecurringReimbursementConfiguration } from '../../shared/api/generated';
import type { TransactionDto } from '../transactions/api/transactionApi';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { ReimbursementFields } from '../recurring/ReimbursementFields';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';
import { reimbursementKeys, reimbursementsApi } from './reimbursementQueries';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';

export function ManualClaimDialog({ userId, householdId, transaction, onClose }: { userId: string; householdId: string; transaction: TransactionDto; onClose: () => void }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const accounts = useQuery(accountsQueries.list(userId, householdId));
    const [configuration, setConfiguration] = useState<RecurringReimbursementConfiguration | null>({ provider: '', calculation: 'Fixed', value: '', maximumAmount: null,
        destinationAccountId: transaction.accountId, paymentDelayDays: 7, submissionReminderDays: 2 });
    const mutation = useMutation({ mutationFn: () => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsPost({ householdId,
        createReimbursementClaimRequest: { transactionId: transaction.id, expectedUpdatedAt: transaction.updatedAt, _configuration: configuration! } }),
        onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: reimbursementKeys.household(userId, householdId) }), client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) })]); onClose(); } });
    return <AverioDialog open wide title={t('reimbursements.manual.add')} closeLabel={t('common.actions.close')} onDismiss={onClose} busy={mutation.isPending}
        actions={<><AverioButton onClick={onClose} disabled={mutation.isPending}>{t('common.actions.cancel')}</AverioButton><AverioButton type="submit" form="manual-claim" variant="primary" disabled={!configuration || mutation.isPending || !accounts.isSuccess}>{t('reimbursements.manual.create')}</AverioButton></>}>
        <form id="manual-claim" className="grid gap-4" onSubmit={e => { e.preventDefault(); if (configuration && !mutation.isPending && accounts.isSuccess) mutation.mutate(); }}>
            <div className="space-y-2"><p className="font-semibold">{transaction.description} · {formatDecimalAmount(transaction.amount, transaction.currency, i18n.resolvedLanguage ?? i18n.language)}</p>
                <p className="text-sm">{t('reimbursements.manual.savedExpense')}</p></div>
            {accounts.isPending && <p role="status">{t('common.status.loading')}</p>}
            {accounts.isError && <div role="alert"><p>{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void accounts.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
            {accounts.isSuccess && <ReimbursementFields userId={userId} householdId={householdId} value={configuration} onChange={setConfiguration} accounts={accounts.data} currency={transaction.currency} paymentAmount={transaction.amount} sourceAccountId={transaction.accountId} busy={mutation.isPending} />}
            {mutation.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{mutation.error.message}</p>}
        </form>
    </AverioDialog>;
}
