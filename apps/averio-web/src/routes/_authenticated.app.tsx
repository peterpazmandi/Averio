import {
    createFileRoute,
    Outlet,
    redirect,
} from '@tanstack/react-router';

import { getAuthenticatedUser } from '../features/authentication/api/authGuard';
import { AppShell } from '../shared/components/AppShell';

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
    return (
        <AppShell>
            <Outlet />
        </AppShell>
    );
}