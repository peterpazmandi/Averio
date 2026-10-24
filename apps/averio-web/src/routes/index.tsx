import { createFileRoute, Navigate } from '@tanstack/react-router';

import { useAuth } from '../features/authentication/context/AuthProvider';
import { LandingPage } from '../features/landing/LandingPage';

export const Route = createFileRoute('/')({
    component: HomePage,
});

function HomePage() {
    const { status, user } = useAuth();

    // AuthProvider checks the session in the background. Public content must
    // render immediately even if that request is slow or never settles.
    if (status === 'authenticated' && user) {
        return (
            <Navigate
                to={user.onboardingRequired ? '/onboarding' : '/app'}
                search={{ redirect: undefined }}
                replace
            />
        );
    }

    return <LandingPage />;
}

