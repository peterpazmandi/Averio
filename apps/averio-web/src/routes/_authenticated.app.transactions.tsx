import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { TransactionsPage } from '../features/transactions/components/TransactionsPage';

export const Route = createFileRoute('/_authenticated/app/transactions')({
    component: TransactionsRoute,
});

function TransactionsRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) {
        return null;
    }

    return (
        <TransactionsPage
            userId={auth.user.userId}
            householdId={auth.user.household.householdId}
        />
    );
}
