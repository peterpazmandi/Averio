import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { transactionSearch } from '../features/transactions/transactionSearch';
import { TransactionsPage } from '../features/transactions/components/TransactionsPage';

export const Route = createFileRoute('/_authenticated/app/transactions')({
    validateSearch: transactionSearch,
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
            key={auth.user.userId + auth.user.household.householdId}
            urlState={search}
            onUrlChange={(patch, replace = false) => { void navigate({ search: previous => ({ ...previous, ...patch }), replace }); }}
            userId={auth.user.userId}
            householdId={auth.user.household.householdId}
            createRequested={search.create}
            initialKind={search.createKind}
            onCreateRequestHandled={() => {
                void navigate({
                    search: previous => ({ ...previous, create: undefined, createKind: undefined }),
                    replace: true,
                });
            }}
        />
    );
}
