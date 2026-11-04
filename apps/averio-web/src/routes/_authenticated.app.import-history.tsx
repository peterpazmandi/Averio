import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { ImportHistoryPage } from '../features/imports/ImportHistoryPage';
export const Route = createFileRoute('/_authenticated/app/import-history')({
    validateSearch: (search: Record<string, unknown>): { batch?: string } => ({
        batch: typeof search.batch === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(search.batch) ? search.batch : undefined,
    }),
    component: ImportHistoryRoute,
});
function ImportHistoryRoute() {
    const auth = useAuth();
    const { batch } = Route.useSearch();
    const navigate = Route.useNavigate();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <ImportHistoryPage key={auth.user.userId + auth.user.household.householdId + (batch ?? '')}
        userId={auth.user.userId} householdId={auth.user.household.householdId} batchId={batch}
        onSelect={id => { void navigate({ search: { batch: id } }); }} />;
}
