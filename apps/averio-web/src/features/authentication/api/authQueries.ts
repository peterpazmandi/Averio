import { queryOptions } from '@tanstack/react-query';
import { getCurrentUser } from './authApi';
import { authQueryKeys } from './authQueryKeys';

export const authQueries = {
    currentUser: () =>
        queryOptions({
            queryKey: authQueryKeys.currentUser(),
            // Shared by AuthProvider and imperative route guards. Do not consume the
            // observer AbortSignal: StrictMode's temporary unmount must not cancel
            // the request a guard is awaiting. Explicit logout/cache cancellation
            // still rejects query waiters and prevents late results being cached.
            queryFn: () => getCurrentUser(),
            retry: false,
            staleTime: 60_000,
        }),
};
