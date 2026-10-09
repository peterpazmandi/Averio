import {
    Outlet,
    createFileRoute,
    useRouterState,
} from '@tanstack/react-router';

import {
    AuthShell,
} from '../features/authentication/components/AuthShell';

export const Route =
    createFileRoute('/_guest')({
        component:
            GuestLayout,
    });

function GuestLayout() {
    const pathname = useRouterState({ select: state => state.location.pathname });
    return (
        <AuthShell withCard={pathname !== '/invitation'}>
            <Outlet />
        </AuthShell>
    );
}