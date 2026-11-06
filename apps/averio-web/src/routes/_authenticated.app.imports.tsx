import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { ImportPage } from '../features/imports/ImportPage';
export const Route = createFileRoute('/_authenticated/app/imports')({ component: ImportRoute });
function ImportRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <ImportPage key={auth.user.userId + auth.user.household.householdId}
        userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
