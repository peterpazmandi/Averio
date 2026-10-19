import { useMutation, useQueryClient } from '@tanstack/react-query';
import { activityKeys } from '../../activities/activityQueries';
import { createTransaction, updateTransaction, voidTransaction } from './transactionApi';
import type { TransactionInputDto, UpdateTransactionInputDto } from './transactionApi';
import { transactionQueryKeys } from './transactionQueries';
import { dashboardQueryKeys } from '../../dashboard/api/dashboardQueryKeys';
import { accountsQueryKeys } from '../../accounts/api/accountsQueryKeys';

export function useTransactionMutations(userId: string, householdId: string) {
    const queryClient = useQueryClient();

    async function invalidate() {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: ['personal-money', userId, householdId] }),
            queryClient.invalidateQueries({ queryKey: activityKeys.all(userId, householdId) }),
            queryClient.invalidateQueries({
                queryKey: transactionQueryKeys.all(userId, householdId),
            }),
            queryClient.invalidateQueries({
                queryKey: ['statistics', userId, householdId],
            }),
            queryClient.invalidateQueries({
                queryKey: dashboardQueryKeys.household(userId, householdId),
            }),
            queryClient.invalidateQueries({
                queryKey: accountsQueryKeys.household(userId, householdId),
            }),
        ]);
    }

    return {
        create: useMutation({
            mutationFn: (request: TransactionInputDto) =>
                createTransaction(householdId, request),
            onSuccess: invalidate,
        }),
        update: useMutation({
            mutationFn: ({ id, request }: { id: string; request: UpdateTransactionInputDto }) =>
                updateTransaction(householdId, id, request),
            onSuccess: invalidate,
        }),
        void: useMutation({
            mutationFn: ({ id, expectedUpdatedAt }: { id: string; expectedUpdatedAt: string }) =>
                voidTransaction(householdId, id, expectedUpdatedAt),
            onSuccess: invalidate,
        }),
    };
}
