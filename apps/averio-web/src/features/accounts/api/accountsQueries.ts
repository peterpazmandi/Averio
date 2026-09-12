import { queryOptions } from '@tanstack/react-query';
import { getAccounts } from './accountsApi';
import { accountsQueryKeys } from './accountsQueryKeys';

export const accountsQueries = {
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