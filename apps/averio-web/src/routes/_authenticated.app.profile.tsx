import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { ProfilePage } from '../features/profile/ProfilePage';

export const Route = createFileRoute('/_authenticated/app/profile')({ component: ProfileRoute });

function ProfileRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <ProfilePage key={auth.user.userId} userId={auth.user.userId} />;
}
