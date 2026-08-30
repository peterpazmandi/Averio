import {
    queryOptions,
} from '@tanstack/react-query';

import {
    getSystemHealth,
} from './systemApi';

import {
    systemQueryKeys,
} from './systemQueryKeys';

export const systemQueries = {
    health: () =>
        queryOptions({
            queryKey:
                systemQueryKeys.health(),

            queryFn:
                getSystemHealth,

            staleTime:
                30_000,
        }),
};