import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { ReceiptsPage } from '../features/receipts/ReceiptsPage';

export const Route = createFileRoute('/_authenticated/app/receipts')({ component: ReceiptsRoute });
function ReceiptsRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <ReceiptsPage key={auth.user.userId + auth.user.household.householdId}
        userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
