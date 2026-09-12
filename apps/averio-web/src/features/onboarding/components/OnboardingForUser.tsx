import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { AverioBrand } from "../../../shared/components/branding/AverioBrand";
import { BentoCard } from "../../../shared/components/layout/BentoCard";
import { PreferencesToolbar } from "../../../shared/components/preferences/PreferencesToolbar";
import { PrimaryButton } from "../../../shared/components/PrimaryButton";
import { onboardingQueries } from "../api/onboardingQueries";
import { OnboardingForm } from "./OnboardingForm";

/**
 * Loads onboarding data using this user's query key.
 *
 * The form is mounted only when data is available.
 */
export function OnboardingForUser({
    userId,
}: {
    userId: string;
}) {
    const { t } = useTranslation();

    const statusQuery = useQuery(onboardingQueries.status(userId));

    return (
        <main
            className="
                min-h-screen
                bg-slate-50
                px-5 py-8
                text-slate-950
                transition-colors
                dark:bg-slate-950
                dark:text-white
                sm:px-8
                lg:py-14
            "
        >
            <div className="mx-auto max-w-5xl">
                <header className="
                            flex flex-wrap
                            items-center justify-between
                            gap-4
                        "
                >
                    <AverioBrand />
                    <PreferencesToolbar />
                </header>

                <div
                    className="
                        mt-12
                        grid gap-10
                        lg:grid-cols-[0.8fr_1.2fr]
                    "
                >
                    <section>
                        <p
                            className="
                                text-sm font-semibold
                                text-primary-700
                                dark:text-primary-400
                            "
                        >
                            {t('onboarding.eyebrow')}
                        </p>

                        <h1
                            className="
                                mt-3
                                text-4xl font-semibold
                                tracking-tight
                            "
                        >
                            {t('onboarding.title')}
                        </h1>

                        <p
                            className="
                                mt-5 max-w-md
                                text-lg leading-8
                                text-slate-600
                                dark:text-slate-400
                            "
                        >
                            {t('onboarding.description')}
                        </p>

                        <div
                            className="
                                mt-8
                                rounded-2xl
                                border border-primary-200
                                bg-primary-50
                                p-5
                                text-sm leading-6
                                text-primary-900
                                dark:border-primary-900/60
                                dark:bg-primary-950/40
                                dark:text-primary-200
                            "
                        >
                            {t('onboarding.householdOfOne')}
                        </div>
                    </section>

                    <BentoCard className="p-6 sm:p-8">
                        {statusQuery.data ? (
                            <OnboardingForm
                                key={userId}
                                userId={userId}
                                initialData={statusQuery.data}
                            />
                        ) : statusQuery.isError ? (
                            <div className="space-y-4">
                                <p
                                    role="alert"
                                    className="
                                        text-sm
                                        text-red-700
                                        dark:text-red-400
                                    "
                                >
                                    {t('errors.genericDescription')}
                                </p>

                                <PrimaryButton
                                    type="button"
                                    loading={statusQuery.isFetching}
                                    onClick={() => {
                                        void statusQuery.refetch();
                                    }}
                                >
                                    {t('common.actions.retry')}
                                </PrimaryButton>
                            </div>
                        ) : (
                            <p role="status">
                                {t('common.status.loading')}
                            </p>
                        )}
                    </BentoCard>
                </div>
            </div>
        </main>
    );
}