import {
    createFileRoute,
    redirect,
} from '@tanstack/react-router';

import {
    ApiError,
} from '../shared/api/ApiError';

import {
    authQueries,
} from '../features/authentication/api/authQueries';

export const Route =
    createFileRoute('/')({
        beforeLoad:
            async ({
                context,
            }) => {
                try {
                    const user =
                        await context
                            .queryClient
                            .ensureQueryData(
                                authQueries
                                    .currentUser(),
                            );

                    throw redirect({
                        to:
                            user.onboardingRequired
                                ? '/onboarding'
                                : '/app',
                        search: {
                            redirect: undefined,
                        },
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
                                redirect: undefined,
                            },
                        });
                    }

                    throw error;
                }
            },
    });