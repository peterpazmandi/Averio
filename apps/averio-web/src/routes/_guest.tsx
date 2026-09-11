import {
    Outlet,
    createFileRoute,
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
    return (
        <AuthShell>
            <Outlet />
        </AuthShell>
    );
}