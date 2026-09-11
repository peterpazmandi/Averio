import type { QueryClient } from '@tanstack/react-query';
import { onboardingQueryKeys } from '../../onboarding/api/onboardingQueryKeys';
import { authQueryKeys } from './authQueryKeys';

export async function clearCachedAuthState(queryClient: QueryClient): Promise<void> {
    await Promise.all([
        queryClient.cancelQueries({
            queryKey: authQueryKeys.all,
        }),

        queryClient.cancelQueries({
            queryKey: onboardingQueryKeys.all,
        }),
    ]);

    queryClient.removeQueries({
        queryKey: authQueryKeys.all,
    });

    queryClient.removeQueries({
        queryKey: onboardingQueryKeys.all,
    });
}