import { queryOptions } from '@tanstack/react-query';

import { getDashboard } from './dashboardApi';
import { dashboardQueryKeys } from './dashboardQueryKeys';

export const dashboardQueries = {
    household: (
        userId: string,
        householdId: string,
    ) => queryOptions({
        queryKey: dashboardQueryKeys.household(
            userId,
            householdId,
        ),

        queryFn: ({ signal }) => getDashboard(
            householdId,
            signal,
        ),
    }),
};