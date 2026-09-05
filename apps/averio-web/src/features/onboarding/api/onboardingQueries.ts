import { queryOptions } from '@tanstack/react-query';
import { onboardingQueryKeys }from './onboardingQueryKeys';
import { getOnboardingStatus } from './onboardingApi';

export const onboardingQueries = {
    status: () =>
        queryOptions({
            queryKey: onboardingQueryKeys.status(),
            queryFn: getOnboardingStatus,
        }),
};