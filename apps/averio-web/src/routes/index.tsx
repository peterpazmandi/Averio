import { createFileRoute, Navigate, useLocation } from '@tanstack/react-router';

import { useAuth } from '../features/authentication/context/AuthProvider';
import { LandingPage } from '../features/landing/LandingPage';

export const Route = createFileRoute('/')({
    component: HomePage,
});

function HomePage() {
    const { status, user } = useAuth();
    const hash = useLocation({ select: location => location.hash });
    // Explicit public section links remain available even with an active session.
    const publicSection = ['main-content', 'top', 'why-averio', 'features', 'how-it-works', 'faq'].includes(hash);

    // AuthProvider checks the session in the background. Public content must
    // render immediately even if that request is slow or never settles.
    if (status === 'authenticated' && user && !publicSection) {
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

