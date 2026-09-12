import { createFileRoute } from '@tanstack/react-router';

import { AccountsPage } from '../features/accounts/components/AccountsPage';

export const Route = createFileRoute(
    '/_authenticated/app/accounts',
)({
    component: AccountsRoute,
});

function AccountsRoute() {
    const { user } = Route.useRouteContext();

    return (
        <AccountsPage
            userId={user.userId}
            householdId={user.household.householdId}
        />
    );
}