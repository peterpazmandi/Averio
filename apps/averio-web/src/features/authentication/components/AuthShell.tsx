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
    const {
        t,
    } = useTranslation();

    return (
        <main
            className="
                min-h-screen

                bg-slate-50
                text-slate-950

                transition-colors

                dark:bg-slate-950
                dark:text-white

                lg:grid
                lg:grid-cols-[minmax(380px,0.9fr)_1.1fr]
            "
        >
            {/* ------------------------------------------------ */}
            {/* Left marketing / branding panel                  */}
            {/* ------------------------------------------------ */}

            <aside
                className="
                    relative
                    hidden
                    overflow-hidden

                    bg-slate-950
                    p-12
                    text-white

                    lg:flex
                    lg:min-h-screen
                    lg:flex-col
                    lg:justify-between
                "
            >
                {/* Decorative background glow */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        -left-28
                        top-32

                        size-80

                        rounded-full

                        bg-primary-500/10

                        blur-3xl
                    "
                />

                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        -right-24
                        bottom-20

                        size-96

                        rounded-full

                        bg-primary-500/5

                        blur-3xl
                    "
                />

                {/* Very subtle grid / bento decoration */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        inset-0

                        opacity-[0.035]

                        [background-image:linear-gradient(to_right,#ffffff_1px,transparent_1px),linear-gradient(to_bottom,#ffffff_1px,transparent_1px)]
                        [background-size:48px_48px]
                    "
                />

                <div
                    className="
                        relative
                        z-10
                    "
                >
                    {/*
                        IMPORTANT:

                        The left panel is ALWAYS dark,
                        regardless of the application's
                        light/dark theme.

                        Therefore the brand wordmark must
                        also always use its dark-surface
                        variant.
                    */}
                    <AverioBrand
                        variant="on-dark"
                    />

                    <div
                        className="
                            mt-24
                            max-w-lg
                        "
                    >
                        <div
                            className="
                                inline-flex
                                items-center

                                rounded-xl

                                border
                                border-primary-500/20

                                bg-primary-500/10

                                px-3
                                py-1.5

                                text-xs
                                font-semibold
                                uppercase
                                tracking-[0.16em]

                                text-primary-300
                            "
                        >
                            {t(
                                'authShell.eyebrow',
                            )}
                        </div>

                        <h1
                            className="
                                mt-7

                                max-w-xl

                                text-4xl
                                font-semibold
                                leading-[1.15]
                                tracking-tight

                                text-white

                                xl:text-5xl
                            "
                        >
                            {t(
                                'authShell.title',
                            )}
                        </h1>

                        <p
                            className="
                                mt-7

                                max-w-md

                                text-lg
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

                {/* Bottom footer */}
                <div
                    className="
                        relative
                        z-10

                        flex
                        items-center
                        justify-between
                    "
                >
                    <p
                        className="
                            text-sm
                            text-slate-500
                        "
                    >
                        {t(
                            'authShell.footer',
                        )}
                    </p>
                </div>
            </aside>

            {/* ------------------------------------------------ */}
            {/* Right application / authentication panel         */}
            {/* ------------------------------------------------ */}

            <section
                className="
                    relative

                    flex
                    min-h-screen
                    flex-col

                    bg-slate-50

                    px-5
                    py-5

                    transition-colors

                    dark:bg-slate-950

                    sm:px-8
                    sm:py-6

                    lg:px-12

                    xl:px-16
                "
            >
                {/* Header */}
                <header
                    className="
                        relative
                        z-20

                        flex
                        items-center
                        justify-between

                        gap-4
                    "
                >
                    {/*
                        Mobile/tablet logo.

                        Unlike the permanent dark sidebar,
                        this area follows the current theme.

                        So this uses the default brand variant:
                        - light theme -> navy wordmark
                        - dark theme -> white wordmark
                    */}
                    <div
                        className="
                            lg:hidden
                        "
                    >
                        <AverioBrand />
                    </div>

                    <div
                        className="
                            ml-auto
                        "
                    >
                        <PreferencesToolbar />
                    </div>
                </header>

                {/* Main auth area */}
                <div
                    className="
                        relative

                        flex
                        flex-1

                        items-center
                        justify-center

                        py-10

                        sm:py-12
                    "
                >
                    {/* Subtle light-mode glow */}
                    <div
                        aria-hidden="true"
                        className="
                            pointer-events-none

                            absolute
                            left-1/2
                            top-1/2

                            size-[34rem]

                            -translate-x-1/2
                            -translate-y-1/2

                            rounded-full

                            bg-primary-500/[0.035]

                            blur-3xl

                            dark:bg-primary-500/[0.02]
                        "
                    />

                    {/* Auth card */}
                    <div
                        className="
                            relative
                            z-10

                            w-full
                            max-w-md

                            rounded-3xl

                            border
                            border-slate-200/80

                            bg-white/95

                            p-6

                            shadow-[0_12px_40px_rgba(15,23,42,0.07)]

                            backdrop-blur-xl

                            transition-colors

                            dark:border-slate-800
                            dark:bg-slate-900/95
                            dark:shadow-[0_18px_60px_rgba(0,0,0,0.25)]

                            sm:p-8
                        "
                    >
                        {children}
                    </div>
                </div>
            </section>
        </main>
    );
}