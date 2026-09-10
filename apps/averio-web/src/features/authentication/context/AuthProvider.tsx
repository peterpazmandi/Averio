import { createContext, useContext, type PropsWithChildren, } from 'react';
import { useQuery, useQueryClient, } from '@tanstack/react-query';
import type { CurrentUserResponse, } from '../../../shared/api/generated';
import { ApiError, } from '../../../shared/api/ApiError';
import { authQueries, } from '../api/authQueries';
import { authQueryKeys, } from '../api/authQueryKeys';
import { onboardingQueryKeys } from '../../onboarding/api/onboardingQueryKeys';

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

const AuthContext = createContext<AuthContextValue | undefined>(undefined,);

export function AuthProvider({ children }: PropsWithChildren) {
    const queryClient = useQueryClient();

    const query = useQuery(authQueries.currentUser());

    const isUnauthorized =
        query.error instanceof ApiError
        && query.error.status === 401;

    const status: AuthStatus =
        query.isPending
            ? 'loading'
            : query.data
                ? 'authenticated'
                : isUnauthorized
                    ? 'anonymous'
                    : 'error';

    const refresh = async (): Promise<CurrentUserResponse> => {
        return await queryClient.fetchQuery({
            ...authQueries.currentUser(),
            staleTime: 0,
        });
    };

    const clear = async (): Promise<void> => {
        await Promise.all([
            queryClient.cancelQueries({ queryKey: authQueryKeys.all }),
            queryClient.cancelQueries({ queryKey: onboardingQueryKeys.all }),
        ]);

        queryClient.removeQueries({ queryKey: authQueryKeys.all });
        queryClient.removeQueries({ queryKey: onboardingQueryKeys.all });
    };

    return (
        <AuthContext.Provider
            value={{
                status,
                user: query.data,
                error: query.error instanceof Error ? query.error : undefined,
                refresh,
                clear,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth(): AuthContextValue {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used inside AuthProvider.');
    }

    return context;
}