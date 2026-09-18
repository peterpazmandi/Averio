import { createFileRoute } from '@tanstack/react-router';

import { useAuth } from '../features/authentication/context/AuthProvider';
import { DashboardPage } from '../features/dashboard/components/DashboardPage';

export const Route = createFileRoute('/_authenticated/app/')({
    component: DashboardRoute,
});

function DashboardRoute() {
    const auth = useAuth();

    if (
        auth.status !== 'authenticated'
        || !auth.user
        || auth.user.onboardingRequired
    ) {
        return null;
    }

    const user = auth.user;

    return (
        <DashboardPage
            userId={user.userId}
            householdId={user.household.householdId}
        />
    );
}