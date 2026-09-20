import { queryOptions } from '@tanstack/react-query';
import { getAccount, getAccounts, getAccountOwnerOptions } from './accountsApi';
import { accountsQueryKeys } from './accountsQueryKeys';

export const accountsQueries = {
    detail: (userId: string, householdId: string, accountId: string) => queryOptions({
        queryKey: accountsQueryKeys.detail(userId, householdId, accountId),
        queryFn: ({ signal }) => getAccount(householdId, accountId, signal),
    }),
    ownerOptions: (userId: string, householdId: string) => queryOptions({
        queryKey: accountsQueryKeys.ownerOptions(userId, householdId),
        queryFn: ({ signal }) => getAccountOwnerOptions(householdId, signal),
    }),

    list: (
        userId: string,
        householdId: string,
        includeArchived = false,
    ) => queryOptions({
        queryKey: accountsQueryKeys.list(
            userId,
            householdId,
            includeArchived,
        ),

        queryFn: ({ signal }) => getAccounts(
            householdId,
            includeArchived,
            signal,
        ),
    }),
};
