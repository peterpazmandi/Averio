import type {
    PropsWithChildren,
} from 'react';

import {
    useTranslation,
} from 'react-i18next';

import {
    AverioBrand,
} from '../../../shared/components/branding/AverioBrand';

import {
    PreferencesToolbar,
} from '../../../shared/components/preferences/PreferencesToolbar';

export function AuthShell({
    children,
}: PropsWithChildren) {
    const { t } =
        useTranslation();

    return (
        <main
            className="
                min-h-screen
                bg-slate-50
                transition-colors
                dark:bg-slate-950
                lg:grid
                lg:grid-cols-[minmax(380px,0.9fr)_1.1fr]
            "
        >
            <aside
                className="
                    relative hidden overflow-hidden
                    bg-slate-950 p-12
                    text-white
                    lg:flex lg:flex-col
                    lg:justify-between
                "
            >
                <div
                    className="
                        absolute -left-24 top-32
                        size-72 rounded-full
                        bg-primary-500/10 blur-3xl
                    "
                />

                <div
                    className="
                        absolute -right-20 bottom-12
                        size-80 rounded-full
                        bg-primary-500/5 blur-3xl
                    "
                />

                <div className="relative">
                    <AverioBrand />

                    <div className="mt-24 max-w-md">
                        <div
                            className="
                                inline-flex
                                rounded-xl
                                border border-primary-500/20
                                bg-primary-500/10
                                px-3 py-1.5
                                text-xs font-semibold
                                uppercase tracking-[0.16em]
                                text-primary-300
                            "
                        >
                            {t(
                                'authShell.eyebrow',
                            )}
                        </div>

                        <h1
                            className="
                                mt-6 text-4xl
                                font-semibold
                                leading-tight
                                tracking-tight
                            "
                        >
                            {t(
                                'authShell.title',
                            )}
                        </h1>

                        <p
                            className="
                                mt-6 text-lg
                                leading-8
                                text-slate-400
                            "
                        >
                            {t(
                                'authShell.description',
                            )}
                        </p>
                    </div>
                </div>

                <p
                    className="
                        relative text-sm
                        text-slate-500
                    "
                >
                    {t('authShell.footer')}
                </p>
            </aside>

            <section
                className="
                    flex min-h-screen
                    flex-col
                    px-5 py-5
                    sm:px-10
                    lg:px-16
                "
            >
                <header
                    className="
                        flex items-center
                        justify-between
                    "
                >
                    <div className="lg:hidden">
                        <AverioBrand />
                    </div>

                    <div className="ml-auto">
                        <PreferencesToolbar />
                    </div>
                </header>

                <div
                    className="
                        flex flex-1
                        items-center
                        justify-center
                        py-12
                    "
                >
                    <div
                        className="
                            w-full max-w-md
                            rounded-3xl
                            border border-slate-200
                            bg-white p-6
                            shadow-sm
                            sm:p-8

                            dark:border-slate-800
                            dark:bg-slate-900
                        "
                    >
                        {children}
                    </div>
                </div>
            </section>
        </main>
    );
}