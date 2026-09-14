import { queryOptions } from '@tanstack/react-query';
import { getAccounts, getAccountOwnerOptions } from './accountsApi';
import { accountsQueryKeys } from './accountsQueryKeys';

export const accountsQueries = {
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
