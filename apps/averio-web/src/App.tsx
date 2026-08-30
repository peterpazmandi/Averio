import {
    useQuery,
} from '@tanstack/react-query';

import {
    useTranslation,
} from 'react-i18next';

import {
    systemQueries,
} from './features/system/api/systemQueries';

import {
    LanguageSwitcher,
} from './features/system/components/LanguageSwitcher';

import {
    formatAppDateTime,
} from './shared/date/dateFormatting';

function App() {
    const {
        t,
    } = useTranslation();

    const healthQuery =
        useQuery(
            systemQueries.health(),
        );

    return (
        <main
            className="
                min-h-screen
                bg-slate-50
                px-6
                py-10
                text-slate-900
            "
        >
            <div
                className="
                    mx-auto
                    max-w-4xl
                    space-y-8
                "
            >
                <header
                    className="
                        flex
                        flex-col
                        gap-6
                        sm:flex-row
                        sm:items-center
                        sm:justify-between
                    "
                >
                    <div>
                        <h1
                            className="
                                text-3xl
                                font-semibold
                                tracking-tight
                            "
                        >
                            {t('app.name')}
                        </h1>

                        <p
                            className="
                                mt-2
                                text-slate-600
                            "
                        >
                            {t('app.description')}
                        </p>
                    </div>

                    <LanguageSwitcher />
                </header>

                <section
                    className="
                        rounded-2xl
                        border
                        border-slate-200
                        bg-white
                        p-6
                        shadow-sm
                    "
                >
                    <h2
                        className="
                            text-lg
                            font-semibold
                        "
                    >
                        {t('system.title')}
                    </h2>

                    {healthQuery.isPending && (
                        <p
                            className="
                                mt-4
                                text-slate-500
                            "
                        >
                            {t('system.loading')}
                        </p>
                    )}

                    {healthQuery.isError && (
                        <div
                            className="
                                mt-4
                                rounded-xl
                                bg-red-50
                                p-4
                                text-red-700
                            "
                        >
                            {t('system.error')}
                        </div>
                    )}

                    {healthQuery.data && (
                        <div
                            className="
                                mt-6
                                grid
                                gap-4
                                sm:grid-cols-2
                            "
                        >
                            <StatusCard
                                title={t('system.api')}
                                available
                                connectedText={
                                    t('system.connected')
                                }
                                unavailableText={
                                    t('system.unavailable')
                                }
                            />

                            <StatusCard
                                title={
                                    t('system.database')
                                }
                                available={
                                    healthQuery.data
                                        .databaseConnected
                                }
                                connectedText={
                                    t('system.connected')
                                }
                                unavailableText={
                                    t('system.unavailable')
                                }
                            />

                            <p
                                className="
                                    text-sm
                                    text-slate-500
                                    sm:col-span-2
                                "
                            >
                                {t(
                                    'system.checkedAt',
                                    {
                                        date:
                                            formatAppDateTime(
                                                healthQuery.data
                                                    .checkedAtUtc,
                                            ),
                                    },
                                )}
                            </p>
                        </div>
                    )}
                </section>
            </div>
        </main>
    );
}

interface StatusCardProps {
    title: string;
    available: boolean;
    connectedText: string;
    unavailableText: string;
}

function StatusCard({
    title,
    available,
    connectedText,
    unavailableText,
}: StatusCardProps) {
    return (
        <div
            className="
                rounded-xl
                border
                border-slate-200
                p-4
            "
        >
            <div
                className="
                    text-sm
                    text-slate-500
                "
            >
                {title}
            </div>

            <div
                className="
                    mt-2
                    flex
                    items-center
                    gap-2
                    font-medium
                "
            >
                <span
                    className={`
                        size-2.5
                        rounded-full
                        ${
                            available
                                ? 'bg-emerald-500'
                                : 'bg-red-500'
                        }
                    `}
                />

                {available
                    ? connectedText
                    : unavailableText}
            </div>
        </div>
    );
}

export default App;