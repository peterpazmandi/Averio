import { createFileRoute, redirect } from '@tanstack/react-router';
import { getAuthenticatedUser } from '../features/authentication/api/authGuard';
import { OnboardingPage } from '../features/onboarding/components/OnboardingPage';

export const Route = createFileRoute(
    '/_authenticated/onboarding',
)({
    beforeLoad: async ({ context }) => {
        const user = await getAuthenticatedUser(
            context.queryClient,
        );

        if (!user.onboardingRequired) {
            throw redirect({
                to: '/app',
            });
        }
    },

    component: OnboardingPage,
});