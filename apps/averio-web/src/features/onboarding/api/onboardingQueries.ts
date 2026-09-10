import { queryOptions } from '@tanstack/react-query';
import { onboardingQueryKeys } from './onboardingQueryKeys';
import { getOnboardingStatus } from './onboardingApi';

export const onboardingQueries = {
    status: (userId: string) =>
        queryOptions({
            queryKey: onboardingQueryKeys.status(userId),
            queryFn: ({ signal }) => getOnboardingStatus(signal),
        }),
};