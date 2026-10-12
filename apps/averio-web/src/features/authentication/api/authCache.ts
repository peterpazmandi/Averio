import type { QueryClient } from '@tanstack/react-query';
import { authQueryKeys } from './authQueryKeys';
import { clearCsrfToken } from '../../../shared/api/csrf';
import type { ApiError } from '../../../shared/api/ApiError';

export async function clearCachedAuthState(
    client: QueryClient,
    { identityChanged = true, unauthorizedError }: {
        identityChanged?: boolean;
        unauthorizedError?: ApiError;
    } = {},
): Promise<void> {
    if (identityChanged) clearCsrfToken();
    await client.cancelQueries();
    // Reset in place so AuthProvider stays attached, without a logout refetch.
    const authQuery = client.getQueryCache().find({ queryKey: authQueryKeys.currentUser(), exact: true });
    authQuery?.reset();
    // Route guards cannot set AuthProvider's local sessionRejected flag.
    // Keep their 401 observable after removing stale identity data so the
    // mounted provider transitions to anonymous instead of idle/loading.
    if (unauthorizedError) {
        authQuery?.setState({
            status: 'error',
            error: unauthorizedError,
            errorUpdatedAt: Date.now(),
        });
    }
    client.removeQueries({ predicate: query => query !== authQuery });
    client.getMutationCache().clear();
}
