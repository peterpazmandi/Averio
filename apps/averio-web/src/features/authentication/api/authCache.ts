import { householdKeys } from '../../household/householdQueries';
import type { QueryClient } from '@tanstack/react-query';
import { onboardingQueryKeys } from '../../onboarding/api/onboardingQueryKeys';
import { authQueryKeys } from './authQueryKeys';
import { accountsQueryKeys } from '../../accounts/api/accountsQueryKeys';
import { dashboardQueryKeys } from '../../dashboard/api/dashboardQueryKeys';

export async function clearCachedAuthState(queryClient: QueryClient): Promise<void> {
    await Promise.all([
        queryClient.cancelQueries({ queryKey: householdKeys.all }),
        queryClient.cancelQueries({
            queryKey: authQueryKeys.all,
        }),

        queryClient.cancelQueries({
            queryKey: onboardingQueryKeys.all,
        }),

        queryClient.cancelQueries({
            queryKey: accountsQueryKeys.all,
        }),
        queryClient.cancelQueries({
            queryKey: dashboardQueryKeys.all,
        }),
    ]);

    queryClient.removeQueries({ queryKey: householdKeys.all });

    queryClient.removeQueries({
        queryKey: authQueryKeys.all,
    });

    queryClient.removeQueries({
        queryKey: onboardingQueryKeys.all,
    });

    queryClient.removeQueries({
        queryKey: accountsQueryKeys.all,
    });

    queryClient.removeQueries({
        queryKey: dashboardQueryKeys.all,
    });
}