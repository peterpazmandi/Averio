import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { RecurringPage } from '../features/recurring/RecurringPage';
export const Route = createFileRoute('/_authenticated/app/recurring')({ component: RecurringRoute });
function RecurringRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <RecurringPage key={auth.user.userId + auth.user.household.householdId} userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
