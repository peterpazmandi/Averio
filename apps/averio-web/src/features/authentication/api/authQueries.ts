import { queryOptions } from '@tanstack/react-query';
import { getCurrentUser } from './authApi';
import { authQueryKeys } from './authQueryKeys';

export const authQueries = {
    currentUser: () =>
        queryOptions({
            queryKey: authQueryKeys.currentUser(),
            queryFn: ({ signal }) => getCurrentUser(signal),
            retry: false,
            staleTime: 60_000,
        }),
};
