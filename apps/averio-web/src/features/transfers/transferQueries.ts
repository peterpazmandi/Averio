import { ApiError } from '../../shared/api/ApiError';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { TransfersApi } from '../../shared/api/generated';
import type { CreateTransferRequest, UpdateTransferRequest } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { accountsQueryKeys } from '../accounts/api/accountsQueryKeys';
import { dashboardQueryKeys } from '../dashboard/api/dashboardQueryKeys';

export const transferApi = new TransfersApi(generatedApiConfiguration);
export const transferKeys = {
    all: (userId: string, householdId: string) => ['transfers', userId, householdId] as const,
    list: (userId: string, householdId: string, accountId: string, from: string, to: string) =>
        [...transferKeys.all(userId, householdId), { accountId, from, to }] as const,
};

export function useTransferMutations(userId: string, householdId: string) {
    const client = useQueryClient();
    const invalidate = () => Promise.all([
        client.invalidateQueries({ queryKey: transferKeys.all(userId, householdId) }),
        client.invalidateQueries({ queryKey: accountsQueryKeys.household(userId, householdId) }),
        client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, householdId) }),
    ]);
    const refreshOnAccountOrTransferChange = async (error: Error) => {
        if (error instanceof ApiError && [403, 404, 409].includes(error.status)) await invalidate();
    };
    return {
        create: useMutation({
            mutationFn: (request: CreateTransferRequest) => transferApi.apiV1HouseholdsHouseholdIdTransfersPost({
                householdId, createTransferRequest: request,
            }), onSuccess: invalidate, onError: refreshOnAccountOrTransferChange,
        }),
        update: useMutation({
            mutationFn: ({ id, request }: { id: string; request: UpdateTransferRequest }) =>
                transferApi.apiV1HouseholdsHouseholdIdTransfersTransferIdPut({ householdId, transferId: id, updateTransferRequest: request }),
            onSuccess: invalidate, onError: refreshOnAccountOrTransferChange,
        }),
        void: useMutation({
            mutationFn: ({ id, expectedUpdatedAt }: { id: string; expectedUpdatedAt: string }) =>
                transferApi.apiV1HouseholdsHouseholdIdTransfersTransferIdDelete({ householdId, transferId: id, expectedUpdatedAt }),
            onSuccess: invalidate, onError: refreshOnAccountOrTransferChange,
        }),
    };
}
