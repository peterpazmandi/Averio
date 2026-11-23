import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ReimbursementRecoveryLinkResponse } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { reimbursementKeys, reimbursementsApi } from './reimbursementQueries';

export function ReimbursementRecoveryPanel({ userId, householdId, className = '' }: { userId: string; householdId: string; className?: string }) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const query = useQuery({
        queryKey: [...reimbursementKeys.household(userId, householdId), 'payment-recovery'],
        queryFn: ({ signal }) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsPaymentRecoveryGet({ householdId }, { signal }),
        refetchInterval: 30_000,
    });
    // Freeze the reviewed IDs across refetches and lost responses. Never expand a retry to new links.
    const [reviewed, setReviewed] = useState<ReimbursementRecoveryLinkResponse[] | null>(null);
    const [confirmed, setConfirmed] = useState(false);
    const mutation = useMutation({
        mutationFn: (links: ReimbursementRecoveryLinkResponse[]) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsPaymentRecoveryPost({ householdId, recoverReimbursementPaymentsRequest: { links } }),
        onSuccess: async () => { await client.invalidateQueries(); setReviewed(null); setConfirmed(false); },
    });
    if (!reviewed && !query.data?.length && !query.isError) return null;
    return <AverioCard className={'grid gap-3 ' + className} >
        <h3 className="font-semibold">{t('reimbursements.recovery.title')}</h3>
        {query.isError ? <><p role="alert">{t('reimbursements.recovery.loadError')}</p><AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></>
            : <><p className="text-sm">{t('reimbursements.recovery.warning')}</p><AverioButton onClick={() => { mutation.reset(); setConfirmed(false); setReviewed(query.data ?? []); }}>{t('reimbursements.recovery.review')}</AverioButton></>}
        {reviewed && <AverioDialog open busy={mutation.isPending} actions={null} title={t('reimbursements.recovery.title')} onDismiss={() => { if (!mutation.isPending) setReviewed(null); }} closeLabel={t('common.actions.close')}>
            <div className="grid gap-4">
                <p>{t('reimbursements.recovery.confirm', { count: reviewed.length })}</p>
                <label className="flex min-h-11 items-start gap-3"><input type="checkbox" checked={confirmed} disabled={mutation.isPending || mutation.isError} onChange={e => setConfirmed(e.target.checked)} />{t('reimbursements.recovery.acknowledge')}</label>
                {mutation.isError && <p role="alert">{t('reimbursements.recovery.retryHelp')}</p>}
                <div className="flex flex-wrap justify-end gap-2"><AverioButton disabled={mutation.isPending} onClick={() => setReviewed(null)}>{t('common.actions.close')}</AverioButton>
                    <AverioButton variant="primary" disabled={!confirmed || mutation.isPending} onClick={() => mutation.mutate(reviewed)}>{t(mutation.isError ? 'common.actions.retry' : 'reimbursements.recovery.remove')}</AverioButton></div>
            </div>
        </AverioDialog>}
    </AverioCard>;
}
