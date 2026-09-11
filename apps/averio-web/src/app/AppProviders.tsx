import type { PropsWithChildren } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { AuthProvider } from '../features/authentication/context/AuthProvider';
import { queryClient } from '../shared/query/queryClient';
import { ThemeProvider } from '../shared/theme/ThemeProvider';

export function AppProviders({ children }: PropsWithChildren) {
    return (
        <QueryClientProvider client={queryClient}>
            <ThemeProvider>
                <AuthProvider>
                    {children}
                </AuthProvider>
            </ThemeProvider>

            <ReactQueryDevtools
                initialIsOpen={false}
            />
        </QueryClientProvider>
    );
}