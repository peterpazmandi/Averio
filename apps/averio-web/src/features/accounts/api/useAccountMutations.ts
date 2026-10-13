import { transferKeys } from '../../transfers/transferQueries';
import { activityKeys } from '../../activities/activityQueries';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { CreateAccountRequest, UpdateAccountRequest } from '../../../shared/api/generated';
import { ApiError } from '../../../shared/api/ApiError';
import { accountsQueryKeys } from './accountsQueryKeys';
import { dashboardQueryKeys } from '../../dashboard/api/dashboardQueryKeys';
import { archiveAccount, createAccount, restoreAccount, updateAccount } from './accountsApi';

export function useAccountMutations(userId: string, householdId: string) {
    const client = useQueryClient();
    async function refresh() {
        await Promise.all([
            client.invalidateQueries({ queryKey: ['statistics', userId, householdId] }),
            client.invalidateQueries({ queryKey: activityKeys.all(userId, householdId) }),
            client.invalidateQueries({ queryKey: transferKeys.all(userId, householdId) }),
            client.invalidateQueries({ queryKey: accountsQueryKeys.household(userId, householdId) }),
            client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, householdId) }),
        ]);
    }
    const create = useMutation({
        mutationFn: (request: CreateAccountRequest) => createAccount(householdId, request),
        onSuccess: refresh, retry: false,
    });
    const update = useMutation({
        mutationFn: ({ id, request }: { id: string; request: UpdateAccountRequest }) => updateAccount(householdId, id, request),
        onSuccess: refresh,
        onError: async (error) => {
            if (error instanceof ApiError && error.status === 409) await refresh();
        },
        retry: false,
    });
    const archive = useMutation({ mutationFn: (id: string) => archiveAccount(householdId, id), onSuccess: refresh, retry: false });
    const restore = useMutation({ mutationFn: (id: string) => restoreAccount(householdId, id), onSuccess: refresh, retry: false });
    return { create, update, archive, restore };
}
