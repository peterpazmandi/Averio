import { createFileRoute } from '@tanstack/react-router';

import { useAuth } from '../features/authentication/context/AuthProvider';
import { AccountsPage } from '../features/accounts/components/AccountsPage';

export const Route = createFileRoute(
    '/_authenticated/app/accounts',
)({
    component: AccountsRoute,
});

function AccountsRoute() {
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
        <AccountsPage
            key={`${user.userId}:${user.household.householdId}`}
            userId={user.userId}
            householdId={user.household.householdId}
        />
    );
}
