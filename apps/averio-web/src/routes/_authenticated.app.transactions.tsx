import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { TransactionsPage } from '../features/transactions/components/TransactionsPage';

export const Route = createFileRoute('/_authenticated/app/transactions')({
    validateSearch: (search: Record<string, unknown>) => ({
        create: search.create === true
            || search.create === 'true'
            || search.create === '1'
            ? true
            : undefined,
    }),
    component: TransactionsRoute,
});

function TransactionsRoute() {
    const auth = useAuth();
    const search = Route.useSearch();
    const navigate = Route.useNavigate();

    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) {
        return null;
    }

    return (
        <TransactionsPage
            userId={auth.user.userId}
            householdId={auth.user.household.householdId}
            createRequested={search.create}
            onCreateRequestHandled={() => {
                void navigate({
                    search: { create: undefined },
                    replace: true,
                });
            }}
        />
    );
}
