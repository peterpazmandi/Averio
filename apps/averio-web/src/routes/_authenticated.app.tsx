import { createFileRoute, redirect } from '@tanstack/react-router';
import { AppShell } from '../shared/components/AppShell';
import { getAuthenticatedUser } from '../features/authentication/api/authGuard';

export const Route =
    createFileRoute(
        '/_authenticated/app',
    )({
        beforeLoad:
            async ({
                context,
            }) => {
                const user =
                    await getAuthenticatedUser(
                        context.queryClient,
                    );

                if (user.onboardingRequired) {
                    throw redirect({
                        to: '/onboarding',
                    });
                }
            },

        component:
            AppPage,
    });

function AppPage() {
    return (
        <AppShell>
            <div
                className="
                    rounded-2xl
                    border
                    border-slate-200
                    bg-white
                    p-8
                "
            >
                <p
                    className="
                        text-sm
                        font-semibold
                        text-primary-700
                    "
                >
                    Averio foundation
                </p>

                <h1
                    className="
                        mt-2
                        text-3xl
                        font-semibold
                    "
                >
                    Dashboard
                </h1>

                <p
                    className="
                        mt-4
                        text-slate-600
                    "
                >
                    Identity, household and onboarding
                    are ready. Financial data comes next.
                </p>
            </div>
        </AppShell>
    );
}