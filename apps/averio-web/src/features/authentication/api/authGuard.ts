import type {
    QueryClient,
} from '@tanstack/react-query';

import {
    redirect,
} from '@tanstack/react-router';

import type {
    CurrentUserResponse,
} from '../../../shared/api/generated';

import {
    ApiError,
} from '../../../shared/api/ApiError';

import {
    authQueries,
} from './authQueries';

export async function getAuthenticatedUser(
    queryClient: QueryClient,
    redirectTo?: string,
): Promise<CurrentUserResponse> {
    try {
        return await queryClient.fetchQuery({
            ...authQueries.currentUser(),
            staleTime: 0,
        });
    }
    catch (error) {
        if (
            error instanceof ApiError
            && error.status === 401
        ) {
            throw redirect({
                to: '/login',
                search: {
                    redirect:
                        redirectTo,
                },
            });
        }

        throw error;
    }
}