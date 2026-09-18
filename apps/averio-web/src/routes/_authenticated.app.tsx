import { useMutation } from '@tanstack/react-query';
import {
    createFileRoute,
    Navigate,
    Outlet,
    redirect,
} from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { getAuthenticatedUser } from '../features/authentication/api/authGuard';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { AppShell } from '../shared/components/AppShell';
import { PrimaryButton } from '../shared/components/PrimaryButton';

export const Route = createFileRoute('/_authenticated/app')({
    beforeLoad: async ({ context, location }) => {
        const user = await getAuthenticatedUser(
            context.queryClient,
            location.href,
        );

        if (user.onboardingRequired) {
            throw redirect({
                to: '/onboarding',
            });
        }

        return {
            user,
        };
    },

    component: AppLayout,
});

function AppLayout() {
    const auth = useAuth();
    const { t } = useTranslation();

    const refreshMutation = useMutation({
        mutationFn: auth.refresh,
    });

    if (auth.status === 'loading') {
        return (
            <main className="min-h-screen bg-slate-50 p-8 text-slate-950 dark:bg-slate-950 dark:text-white">
                <p role="status">
                    {t('common.status.loading')}
                </p>
            </main>
        );
    }

    if (auth.status === 'anonymous') {
        return (
            <Navigate
                to="/login"
                search={{ redirect: undefined }}
                replace
            />
        );
    }

    if (auth.status !== 'authenticated' || !auth.user) {
        return (
            <main className="min-h-screen bg-slate-50 p-8 text-slate-950 dark:bg-slate-950 dark:text-white">
                <div className="mx-auto max-w-md space-y-4">
                    <p role="alert">
                        {t('errors.genericDescription')}
                    </p>

                    <PrimaryButton
                        type="button"
                        loading={refreshMutation.isPending}
                        onClick={() => refreshMutation.mutate()}
                    >
                        {t('common.actions.retry')}
                    </PrimaryButton>
                </div>
            </main>
        );
    }

    const user = auth.user;

    if (user.onboardingRequired) {
        return <Navigate to="/onboarding" replace />;
    }

    return (
        <AppShell key={`${user.userId}:${user.household.householdId}`}>
            <Outlet />
        </AppShell>
    );
}