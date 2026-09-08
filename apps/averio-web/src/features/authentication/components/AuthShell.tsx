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
    LanguageSwitcher,
} from '../../system/components/LanguageSwitcher';

import {
    ThemeToggle,
} from '../../../shared/components/preferences/ThemeToggle';

export function AuthShell({
    children,
}: PropsWithChildren) {
    const {
        t,
    } = useTranslation();

    return (
        <main
            className="
                grid
                min-h-screen
                w-full
                min-w-0

                bg-slate-50
                text-slate-950

                dark:bg-slate-950
                dark:text-white

                lg:grid-cols-[minmax(380px,0.9fr)_minmax(0,1.1fr)]
            "
        >
            {/* Desktop marketing side */}
            <aside
                className="
                    relative
                    hidden
                    min-w-0
                    overflow-hidden

                    bg-slate-950
                    p-12
                    text-white

                    lg:flex
                    lg:flex-col
                    lg:justify-between
                "
            >
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

                <div
                    className="
                        relative
                        z-10
                    "
                >
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

                <p
                    className="
                        relative
                        z-10
                        text-sm
                        text-slate-500
                    "
                >
                    {t(
                        'authShell.footer',
                    )}
                </p>
            </aside>

            {/* Application/authentication side */}
            <section
                className="
                    flex
                    min-h-screen
                    min-w-0
                    flex-col

                    bg-slate-50

                    px-4
                    py-4

                    dark:bg-slate-950

                    sm:px-6
                    sm:py-6

                    lg:px-12
                    xl:px-16
                "
            >
                {/* Mobile header */}
                <header
                    className="
                        w-full
                        min-w-0

                        lg:flex
                        lg:justify-end
                    "
                >
                    <div
                        className="
                            flex
                            min-w-0
                            items-center
                            justify-between
                            gap-3

                            lg:hidden
                        "
                    >
                        <AverioBrand />

                        <div className="shrink-0">
                            <ThemeToggle />
                        </div>
                    </div>

                    <div
                        className="
                            mt-3
                            min-w-0

                            lg:hidden
                        "
                    >
                        <LanguageSwitcher
                            fullWidth
                        />
                    </div>

                    {/* Desktop preferences */}
                    <div
                        className="
                            hidden
                            items-center
                            gap-2

                            lg:flex
                        "
                    >
                        <LanguageSwitcher />
                        <ThemeToggle />
                    </div>
                </header>

                {/* Form area */}
                <div
                    className="
                        flex
                        min-w-0
                        flex-1

                        items-center
                        justify-center

                        py-8

                        sm:py-12
                    "
                >
                    <div
                        className="
                            w-full
                            min-w-0
                            max-w-md

                            rounded-3xl

                            border
                            border-slate-200/80

                            bg-white/95

                            p-5

                            shadow-[0_12px_40px_rgba(15,23,42,0.07)]

                            backdrop-blur-xl

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