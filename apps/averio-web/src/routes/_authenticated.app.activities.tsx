import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { ActivitiesPage } from '../features/activities/ActivitiesPage';
export const Route = createFileRoute('/_authenticated/app/activities')({ component: ActivitiesRoute });
function ActivitiesRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <ActivitiesPage key={auth.user.userId + auth.user.household.householdId}
        userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
