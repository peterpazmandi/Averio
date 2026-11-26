import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { reimbursementKeys, reimbursementsApi } from './reimbursementQueries';
import type { ReimbursementDestinationRecoveryResponse } from '../../shared/api/generated';

export function DestinationRecoveryPanel({ userId, householdId, className = '' }: { userId: string; householdId: string; className?: string }) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const query = useQuery({
        queryKey: [...reimbursementKeys.household(userId, householdId), 'destination-recovery'],
        queryFn: ({ signal }) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsDestinationRecoveryGet({ householdId }, { signal }),
        refetchInterval: 30_000,
    });
    const accounts = useQuery(accountsQueries.list(userId, householdId));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const [selected, setSelected] = useState<ReimbursementDestinationRecoveryResponse | null>(null);
    const [destination, setDestination] = useState('');
    const [confirmed, setConfirmed] = useState(false);
    const eligible = (accounts.data ?? []).filter(a => a.status === 'Active' && !a.cutoverAtUtc && !!owners.data?.currentMemberId
        && (a.scope === 'Household' || a.owners.some(o => o.householdMemberId === owners.data.currentMemberId)));
    const mutation = useMutation({
        mutationFn: () => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsClaimIdDestinationRecoveryPut({
            householdId, claimId: selected!.claimId,
            reassignReimbursementDestinationRequest: { expectedVersion: selected!.version, destinationAccountId: destination },
        }),
        onSuccess: async () => { await client.invalidateQueries(); setSelected(null); },
    });
    const dismiss = () => {
        setSelected(null);
        if (mutation.isError) void client.invalidateQueries();
    };
    if (!selected && !query.data?.length && !query.isError) return null;
    return <AverioCard className={'grid gap-3 ' + className}>
        <h3 className="font-semibold">{t('reimbursements.destinationRecovery.title')}</h3>
        {query.isError ? <><p role="alert">{t('reimbursements.destinationRecovery.loadError')}</p><AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></>
            : <><p>{t('reimbursements.destinationRecovery.warning')}</p>
                {query.data?.map((claim, index) => <div key={claim.claimId}><AverioButton onClick={() => {
                    mutation.reset(); setDestination(''); setConfirmed(false); setSelected(claim);
                }}>{t('reimbursements.destinationRecovery.review', { number: index + 1 })}</AverioButton></div>)}</>}
        {selected && <AverioDialog open title={t('reimbursements.destinationRecovery.title')} closeLabel={t('common.actions.close')}
            busy={mutation.isPending} onDismiss={dismiss}
            actions={<><AverioButton disabled={mutation.isPending} onClick={dismiss}>{t('common.actions.close')}</AverioButton>
                <AverioButton type="submit" form="destination-recovery" variant="primary" disabled={mutation.isPending || mutation.isError || !confirmed || !eligible.some(a => a.id === destination)}>{t('common.actions.confirm')}</AverioButton></>}>
            <form id="destination-recovery" className="grid gap-4" onSubmit={event => { event.preventDefault(); if (!mutation.isPending && confirmed && eligible.some(a => a.id === destination)) mutation.mutate(); }}>
                <p>{t('reimbursements.destinationRecovery.explanation')}</p>
                {(accounts.isPending || owners.isPending) && <p role="status">{t('common.status.loading')}</p>}
                {(accounts.isError || owners.isError) && <div role="alert"><p>{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void accounts.refetch(); void owners.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
                <AverioField>{t('reimbursements.destination')}<AverioSelect required value={destination} disabled={!accounts.isSuccess || !owners.isSuccess || mutation.isPending || mutation.isError}
                    onChange={event => { setDestination(event.target.value); setConfirmed(false); }}>
                    <option value="">{t('reimbursements.chooseAccount')}</option>
                    {eligible.map(account => <option key={account.id} value={account.id}>{account.name} · {account.currency}</option>)}
                </AverioSelect></AverioField>
                <label className="flex min-h-11 items-start gap-3"><input type="checkbox" checked={confirmed} disabled={mutation.isPending || mutation.isError} onChange={event => setConfirmed(event.target.checked)} />{t('reimbursements.destinationRecovery.confirm')}</label>
                {mutation.isError && <p role="alert">{t('reimbursements.destinationRecovery.error')}</p>}
            </form>
        </AverioDialog>}
    </AverioCard>;
}
