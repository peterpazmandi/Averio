import type { PropsWithChildren } from 'react';

import { useMutation } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';

import { logoutUser } from '../../features/authentication/api/authApi';
import { useAuth } from '../../features/authentication/context/AuthProvider';
import { AverioBrand } from './branding/AverioBrand';

export function AppShell({
    children,
}: PropsWithChildren) {
    const auth = useAuth();
    const navigate = useNavigate();

    const logoutMutation = useMutation({
        mutationFn: logoutUser,

        onSuccess: async () => {
            auth.clear();

            await navigate({
                to: '/login',
                search: {
                    redirect: undefined,
                }
            });
        },
    });

    const displayName =
        auth.user?.displayName
        ?? auth.user?.email
        ?? 'Averio user';

    return (
        <div className="min-h-screen bg-slate-50">
            <header
                className="
                    border-b
                    border-slate-200
                    bg-white
                "
            >
                <div
                    className="
                        mx-auto
                        flex
                        h-16
                        max-w-7xl
                        items-center
                        justify-between
                        px-4
                        sm:px-6
                        lg:px-8
                    "
                >
                    <div className="flex items-center gap-3">
                        <AverioBrand variant="on-dark" />

                        <span
                            className="
                                text-lg
                                font-semibold
                                tracking-tight
                                text-slate-950
                            "
                        >
                            Averio
                        </span>
                    </div>

                    <div className="flex items-center gap-4">
                        <div
                            className="
                                hidden
                                text-right
                                sm:block
                            "
                        >
                            <p
                                className="
                                    text-sm
                                    font-medium
                                    text-slate-900
                                "
                            >
                                {displayName}
                            </p>

                            {auth.user?.displayName && (
                                <p
                                    className="
                                        text-xs
                                        text-slate-500
                                    "
                                >
                                    {auth.user.email}
                                </p>
                            )}
                        </div>

                        <button
                            type="button"
                            disabled={logoutMutation.isPending}
                            onClick={() => {
                                logoutMutation.mutate();
                            }}
                            className="
                                rounded-lg
                                border
                                border-slate-300
                                bg-white
                                px-3
                                py-2
                                text-sm
                                font-medium
                                text-slate-700
                                transition
                                hover:bg-slate-50
                                disabled:cursor-not-allowed
                                disabled:opacity-50
                            "
                        >
                            {logoutMutation.isPending
                                ? 'Signing out…'
                                : 'Sign out'}
                        </button>
                    </div>
                </div>
            </header>

            <main
                className="
                    mx-auto
                    max-w-7xl
                    px-4
                    py-8
                    sm:px-6
                    lg:px-8
                "
            >
                {children}
            </main>
        </div>
    );
}