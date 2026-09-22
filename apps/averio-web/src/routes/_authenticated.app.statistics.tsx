import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { StatisticsPage } from '../features/transactions/components/StatisticsPage';

export const Route = createFileRoute('/_authenticated/app/statistics')({
    component: StatisticsRoute,
});

function StatisticsRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) {
        return null;
    }

    return (
        <StatisticsPage
            userId={auth.user.userId}
            householdId={auth.user.household.householdId}
        />
    );
}
