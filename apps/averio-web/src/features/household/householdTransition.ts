import type { QueryClient } from '@tanstack/react-query';
import { authQueryKeys } from '../authentication/api/authQueryKeys';

/** Refresh the live auth observer while discarding the departed household's data. */
export async function refreshHouseholdContext<T>(
    client: QueryClient,
    refreshAuth: () => Promise<T>,
): Promise<T> {
    await client.cancelQueries();
    // Removing this query would leave AuthProvider observing a detached query
    // whose data still points at the previous household.
    client.removeQueries({
        predicate: query => query.queryKey[0] !== authQueryKeys.all[0],
    });
    client.getMutationCache().clear();
    return await refreshAuth();
}
