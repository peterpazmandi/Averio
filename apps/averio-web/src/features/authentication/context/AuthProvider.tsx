import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useState,
    type PropsWithChildren,
} from 'react';

import {
    useQuery,
    useQueryClient,
} from '@tanstack/react-query';

import type {
    CurrentUserResponse,
} from '../../../shared/api/generated';

import {
    ApiError,
} from '../../../shared/api/ApiError';

import {
    authQueries,
} from '../api/authQueries';

import {
    clearCachedAuthState,
} from '../api/authCache';

type AuthStatus =
    | 'loading'
    | 'authenticated'
    | 'anonymous'
    | 'error';

interface AuthContextValue {
    status: AuthStatus;
    user?: CurrentUserResponse;
    error?: Error;
    refresh: () => Promise<CurrentUserResponse>;
    clear: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: PropsWithChildren) {
    const queryClient = useQueryClient();

    const [
        sessionRejected,
        setSessionRejected,
    ] = useState(false);

    const query = useQuery({
        ...authQueries.currentUser(),

        enabled:
            !sessionRejected,
    });

    const queryReturnedUnauthorized =
        query.error instanceof ApiError
        && query.error.status === 401;

    const isUnauthorized =
        sessionRejected
        || queryReturnedUnauthorized;

    const clear = useCallback(
        async (identityChanged = true): Promise<void> => {
            setSessionRejected(true);

            await clearCachedAuthState(
                queryClient,
                { identityChanged },
            );
        },
        [
            queryClient,
        ],
    );

    useEffect(
        () => {
            if (!queryReturnedUnauthorized) {
                return;
            }

            const timeout = window.setTimeout(
                () => {
                    // An initial anonymous check is not a change of identity.
                    // Preserve pending public actions such as email verification.
                    void clear(query.data !== undefined);
                },
                0,
            );

            return () => window.clearTimeout(timeout);
        },
        [
            clear,
            query.data,
            queryReturnedUnauthorized,
        ],
    );

    const user =
        !isUnauthorized
            ? query.data
            : undefined;

    const status: AuthStatus =
        isUnauthorized
            ? 'anonymous'
            : query.isPending
                ? 'loading'
                : user
                    ? 'authenticated'
                    : query.error
                        ? 'error'
                        : 'anonymous';

    const refresh = useCallback(
        async (): Promise<CurrentUserResponse> => {
            setSessionRejected(false);

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
                    setSessionRejected(true);

                    await clearCachedAuthState(
                        queryClient,
                        { identityChanged: queryClient.getQueryData(authQueries.currentUser().queryKey) !== undefined },
                    );
                }

                throw error;
            }
        },
        [
            queryClient,
        ],
    );

    return (
        <AuthContext.Provider
            value={{
                status,
                user,
                error:
                    !isUnauthorized
                    && query.error instanceof Error
                        ? query.error
                        : undefined,
                refresh,
                clear,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth(): AuthContextValue {
    const context =
        useContext(AuthContext);

    if (!context) {
        throw new Error(
            'useAuth must be used inside AuthProvider.',
        );
    }

    return context;
}
