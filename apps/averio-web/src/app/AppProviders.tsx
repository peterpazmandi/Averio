import type { PropsWithChildren } from 'react';

import {
    QueryClientProvider,
} from '@tanstack/react-query';

import {
    ReactQueryDevtools,
} from '@tanstack/react-query-devtools';

import {
    queryClient,
} from '../shared/query/queryClient';

export function AppProviders({
    children,
}: PropsWithChildren) {
    return (
        <QueryClientProvider client={queryClient}>
            {children}

            <ReactQueryDevtools
                initialIsOpen={false}
            />
        </QueryClientProvider>
    );
}