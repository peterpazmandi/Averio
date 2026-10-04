import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ActivityAssignmentChangeRequest } from '../../shared/api/generated';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { ActivitySelect } from './ActivitySelect';
import { activityApi, activityKeys } from './activityQueries';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';
import { dashboardQueryKeys } from '../dashboard/api/dashboardQueryKeys';

export function AssignActivityDialog({ userId, householdId, transactions, onClose, onSaved }: {
    userId: string; householdId: string; transactions: ActivityAssignmentChangeRequest[];
    onClose: () => void; onSaved: () => void;
}) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const [activityId, setActivityId] = useState('');
    const mutation = useMutation({
        mutationFn: () => activityApi.apiV1HouseholdsHouseholdIdActivitiesAssignmentsPut({ householdId,
            assignActivitiesRequest: { activityId: activityId || null, transactions } }),
        onSuccess: async () => {
            await Promise.all([
                client.invalidateQueries({ queryKey: activityKeys.all(userId, householdId) }),
                client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) }),
                client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, householdId) }),
            ]);
            onSaved();
        },
    });
    return <AverioDialog open title={t('activities.assign')} closeLabel={t('common.actions.close')} busy={mutation.isPending}
        onDismiss={onClose} actions={<><AverioButton onClick={onClose} disabled={mutation.isPending}>{t('common.actions.cancel')}</AverioButton>
            <AverioButton variant="primary" disabled={mutation.isPending || transactions.length === 0} onClick={() => mutation.mutate()}>
                {t(activityId ? 'activities.assign' : 'activities.removeAssignment')}</AverioButton></>}>
        <div className="grid gap-4">
            <p>{t('activities.selected', { count: transactions.length })}</p>
            <ActivitySelect userId={userId} householdId={householdId} value={activityId} onChange={setActivityId} disabled={mutation.isPending} />
            <p className="text-xs">{t('activities.assignmentHelp')}</p>
            {mutation.isError && <p role="alert" className="text-red-700 dark:text-red-300">{mutation.error.message}</p>}
        </div>
    </AverioDialog>;
}
