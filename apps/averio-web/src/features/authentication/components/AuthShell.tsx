import type {
    PropsWithChildren,
} from 'react';

import {
    useTranslation,
} from 'react-i18next';

import {
    AverioBrand,
} from '../../../shared/components/branding/AverioBrand';

import { ConceptPreferences } from '../../../shared/components/concept/ConceptPreferences';

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

                transition-colors
                duration-200

                dark:bg-[#07101f]
                dark:text-white

                lg:grid-cols-[minmax(380px,0.9fr)_minmax(0,1.1fr)]
            "
        >
            {/* ================================================== */}
            {/* LEFT: Averio branding / marketing area            */}
            {/* ================================================== */}

            <aside
                className="
                    relative
                    hidden
                    min-w-0
                    overflow-hidden

                    bg-[#020817]

                    p-12
                    text-white

                    lg:flex
                    lg:flex-col
                    lg:justify-between
                "
            >
                {/* ------------------------------------------------ */}
                {/* Background effects                               */}
                {/* ------------------------------------------------ */}

                {/*
                    Main subtle Averio-green glow.

                    Kept deliberately weak because this area
                    should feel premium rather than neon.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        -left-40
                        top-24

                        size-[32rem]

                        rounded-full

                        bg-primary-500/[0.07]

                        blur-[110px]
                    "
                />

                {/*
                    Secondary glow toward the bottom/right.

                    This makes the branding side slightly richer
                    without visually competing with the form.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        -bottom-48
                        right-[-8rem]

                        size-[34rem]

                        rounded-full

                        bg-primary-500/[0.035]

                        blur-[120px]
                    "
                />

                {/*
                    Very subtle navy/blue glow.

                    The official Averio logo already contains navy,
                    so this connects the background with the brand
                    without changing the logo itself.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        right-[-10rem]
                        top-[30%]

                        size-[28rem]

                        rounded-full

                        bg-[#1E2E60]/20

                        blur-[120px]
                    "
                />

                {/*
                    Soft grid.

                    Extremely low opacity:
                    it gives the marketing surface some structure,
                    but should not be consciously noticeable.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        inset-0

                        opacity-[0.025]

                        [background-image:linear-gradient(to_right,#ffffff_1px,transparent_1px),linear-gradient(to_bottom,#ffffff_1px,transparent_1px)]
                        [background-size:48px_48px]
                    "
                />

                {/*
                    Slight dark gradient at the outer edge.

                    This helps visually separate the branding panel
                    from the application side on wide screens.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        inset-y-0
                        right-0

                        w-40

                        bg-gradient-to-r
                        from-transparent
                        to-black/10
                    "
                />

                {/* ------------------------------------------------ */}
                {/* Branding / marketing copy                        */}
                {/* ------------------------------------------------ */}

                <div
                    className="
                        relative
                        z-10
                    "
                >
                    {/*
                        The left panel is ALWAYS dark.

                        Do not tie this wordmark color to the
                        application's theme.
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
                                max-w-full
                                items-center

                                rounded-xl

                                border
                                border-primary-500/20

                                bg-primary-500/[0.08]

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

                {/* ------------------------------------------------ */}
                {/* Branding footer                                  */}
                {/* ------------------------------------------------ */}

                <div
                    className="
                        relative
                        z-10
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

            {/* ================================================== */}
            {/* RIGHT: Authentication / application surface       */}
            {/* ================================================== */}

            <section
                className="
                    relative

                    flex
                    min-h-screen
                    min-w-0
                    flex-col

                    overflow-hidden

                    bg-slate-50

                    px-4
                    py-4

                    transition-colors
                    duration-200

                    dark:bg-[#07101f]

                    sm:px-6
                    sm:py-6

                    lg:px-12

                    xl:px-16
                "
            >
                {/* ------------------------------------------------ */}
                {/* Very subtle right-side background decoration     */}
                {/* ------------------------------------------------ */}

                {/*
                    Light theme glow.

                    Barely visible — just enough to stop the large
                    light area from feeling completely flat.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        left-1/2
                        top-1/2

                        size-[42rem]

                        -translate-x-1/2
                        -translate-y-1/2

                        rounded-full

                        bg-primary-500/[0.025]

                        blur-[120px]

                        dark:bg-primary-500/[0.018]
                    "
                />

                {/*
                    Dark mode gets a tiny navy influence as well.

                    This helps visually connect both halves without
                    making their backgrounds identical.
                */}
                <div
                    aria-hidden="true"
                    className="
                        pointer-events-none

                        absolute
                        -right-44
                        top-[18%]

                        hidden
                        size-[30rem]

                        rounded-full

                        bg-[#1E2E60]/10

                        blur-[120px]

                        dark:block
                    "
                />

                {/* ================================================== */}
                {/* Header                                             */}
                {/* ================================================== */}

                <header className="relative z-20 flex w-full min-w-0 flex-wrap items-center justify-between gap-3 lg:justify-end">
                    <div className="min-w-0 lg:hidden">
                        <AverioBrand />
                    </div>
                    <ConceptPreferences />
                </header>

                {/* ================================================== */}
                {/* Authentication content                            */}
                {/* ================================================== */}

                <div
                    className="
                        relative
                        z-10

                        flex
                        min-w-0
                        flex-1

                        items-center
                        justify-center

                        py-8

                        sm:py-12
                    "
                >
                    {/* ------------------------------------------------ */}
                    {/* Main auth card                                   */}
                    {/* ------------------------------------------------ */}

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

                            transition-all
                            duration-200

                            dark:border-slate-700/60

                            dark:bg-slate-900/90

                            dark:shadow-[0_20px_70px_rgba(0,0,0,0.28)]

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
