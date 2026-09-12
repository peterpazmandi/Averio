import { createFileRoute } from '@tanstack/react-router';

import { DashboardPage } from '../features/dashboard/components/DashboardPage';

export const Route = createFileRoute('/_authenticated/app/')({
    component: DashboardRoute,
});

function DashboardRoute() {
    const { user } = Route.useRouteContext();

    return (
        <DashboardPage
            userId={user.userId}
            householdId={user.household.householdId}
        />
    );
}