import { createFileRoute, redirect } from '@tanstack/react-router';

import { authQueries } from '../features/authentication/api/authQueries';
import { LandingPage } from '../features/landing/LandingPage';

export const Route = createFileRoute('/')({
    beforeLoad: async ({ context }) => {
        // The marketing homepage is public even when the API is unavailable.
        // The authenticated routes still enforce their own authorization.
        let user;
        try {
            user = await context.queryClient.ensureQueryData(
                authQueries.currentUser(),
            );
        } catch {
            return;
        }

        throw redirect({
            to: user.onboardingRequired ? '/onboarding' : '/app',
            search: { redirect: undefined },
        });
    },
    component: LandingPage,
});
