import { useId, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ActivityResponse } from '../../shared/api/generated';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { activityApi, activityKeys } from './activityQueries';
import { transactionQueryKeys } from '../transactions/api/transactionQueries';
import { dashboardQueryKeys } from '../dashboard/api/dashboardQueryKeys';

export function ActivityEditor({ userId, householdId, activity, onClose, onCreated }: {
    userId: string; householdId: string; activity?: ActivityResponse;
    onClose: () => void; onCreated?: (id: string) => void;
}) {
    const { t } = useTranslation();
    const formId = useId();
    const client = useQueryClient();
    const [name, setName] = useState(activity?.name ?? '');
    const [startsOn, setStartsOn] = useState(activity?.startsOn ?? '');
    const [endsOn, setEndsOn] = useState(activity?.endsOn ?? '');
    const [isClosed, setIsClosed] = useState(activity?.isClosed ?? false);
    const save = useMutation({
        mutationFn: async () => {
            const details = { name: name.trim(), startsOn: startsOn || null, endsOn: endsOn || null };
            if (activity) {
                await activityApi.apiV1HouseholdsHouseholdIdActivitiesActivityIdPut({ householdId,
                    activityId: activity.id, updateActivityRequest: { ...details, isClosed, expectedUpdatedAt: activity.updatedAt } });
                return activity.id;
            }
            return (await activityApi.apiV1HouseholdsHouseholdIdActivitiesPost({ householdId, createActivityRequest: details })).id;
        },
        onSuccess: async id => {
            await Promise.all([
                client.invalidateQueries({ queryKey: activityKeys.all(userId, householdId) }),
                client.invalidateQueries({ queryKey: transactionQueryKeys.all(userId, householdId) }),
                client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, householdId) }),
            ]);
            onCreated?.(id); onClose();
        },
    });
    return <AverioDialog open title={t(activity ? 'activities.edit' : 'activities.create')}
        closeLabel={t('common.actions.close')} onDismiss={onClose} busy={save.isPending}
        actions={<><AverioButton onClick={onClose} disabled={save.isPending}>{t('common.actions.cancel')}</AverioButton>
            <AverioButton variant="primary" type="submit" form={formId} disabled={save.isPending}>{t('common.actions.save')}</AverioButton></>}>
        <form id={formId} onSubmit={e => { e.preventDefault(); if (!save.isPending) save.mutate(); }} className="grid gap-4">
            <AverioCard className="grid gap-4 p-4">
                <p className="text-sm">{t('activities.privateHelp')}</p>
                <AverioField>{t('activities.name')}<AverioInput required maxLength={100} value={name}
                    disabled={save.isPending} onChange={e => setName(e.target.value)} /></AverioField>
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('activities.startsOn')}<AverioInput type="date" value={startsOn}
                        disabled={save.isPending} onChange={e => setStartsOn(e.target.value)} /></AverioField>
                    <AverioField>{t('activities.endsOn')}<AverioInput type="date" min={startsOn || undefined} value={endsOn}
                        disabled={save.isPending} onChange={e => setEndsOn(e.target.value)} /></AverioField>
                </div>
                <p className="text-xs">{t('activities.dateHelp')}</p>
                {activity && <label className="flex min-h-11 items-center gap-3"><input type="checkbox" checked={isClosed}
                    disabled={save.isPending} onChange={e => setIsClosed(e.target.checked)} />{t('activities.closed')}</label>}
                {activity && <p className="text-xs">{t('activities.closedHelp')}</p>}
            </AverioCard>
            {save.isError && <p role="alert" className="text-red-700 dark:text-red-300">{save.error.message}</p>}
        </form>
    </AverioDialog>;
}
