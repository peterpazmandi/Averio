import type { QueryClient } from '@tanstack/react-query';
import { authQueryKeys } from './authQueryKeys';
import { clearCsrfToken } from '../../../shared/api/csrf';

export async function clearCachedAuthState(client: QueryClient): Promise<void> {
    clearCsrfToken();
    await client.cancelQueries();
    // Reset in place so AuthProvider stays attached, without a logout refetch.
    const authQuery = client.getQueryCache().find({ queryKey: authQueryKeys.currentUser(), exact: true });
    authQuery?.reset();
    client.removeQueries({ predicate: query => query !== authQuery });
    client.getMutationCache().clear();
}
