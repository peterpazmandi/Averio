import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { CategoryReportPage } from '../features/dashboard/components/CategoryReportPage';
import { categoryReportSearch } from '../features/dashboard/categoryReportSearch';
import { StatisticsPage } from '../features/transactions/components/StatisticsPage';

export const Route = createFileRoute('/_authenticated/app/statistics')({
    validateSearch: categoryReportSearch,
    component: StatisticsRoute,
});

function StatisticsRoute() {
    const auth = useAuth();
    const search = Route.useSearch();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) {
        return null;
    }

    const { from, to, currency, kind, category } = search;
    if (from && to && currency && kind) {
        return <CategoryReportPage key={`${auth.user.userId}:${auth.user.household.householdId}:${JSON.stringify(search)}`}
            userId={auth.user.userId} householdId={auth.user.household.householdId}
            report={{ from, to, currency, kind, category }} />;
    }

    return (
        <StatisticsPage
            userId={auth.user.userId}
            householdId={auth.user.household.householdId}
        />
    );
}
