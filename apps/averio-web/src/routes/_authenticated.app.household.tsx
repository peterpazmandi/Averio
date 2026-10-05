import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { HouseholdPage } from '../features/household/HouseholdPage';

export const Route = createFileRoute('/_authenticated/app/household')({ component: HouseholdRoute });

function HouseholdRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <HouseholdPage key={auth.user.userId + auth.user.household.householdId}
        userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
