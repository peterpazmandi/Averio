import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { NotificationsPage } from '../features/notifications/NotificationsPage';

export const Route = createFileRoute('/_authenticated/app/notifications')({ component: NotificationsRoute });

function NotificationsRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <NotificationsPage key={auth.user.userId + auth.user.household.householdId} userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}
