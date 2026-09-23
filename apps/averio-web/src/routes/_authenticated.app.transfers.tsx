import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { TransfersPage } from '../features/transfers/TransfersPage';

export const Route = createFileRoute('/_authenticated/app/transfers')({ component: TransfersRoute });

function TransfersRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <TransfersPage userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
