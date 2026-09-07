import {
    useEffect,
    useState,
    type FormEvent,
} from 'react';

import {
    useMutation,
    useQuery,
    useQueryClient,
} from '@tanstack/react-query';

import {
    useNavigate,
} from '@tanstack/react-router';

import {
    useTranslation,
} from 'react-i18next';

import {
    authQueryKeys,
} from '../../authentication/api/authQueryKeys';

import {
    onboardingQueries,
} from '../api/onboardingQueries';

import {
    completeOnboarding,
} from '../api/onboardingApi';

import {
    onboardingQueryKeys,
} from '../api/onboardingQueryKeys';

import {
    detectCountryCode,
    suggestCurrency,
} from '../detectCountry';

import {
    FormField,
} from '../../../shared/components/FormField';

import {
    PrimaryButton,
} from '../../../shared/components/PrimaryButton';
import { AverioBrand } from '../../../shared/components/branding/AverioBrand';
import { PreferencesToolbar } from '../../../shared/components/preferences/PreferencesToolbar';
import { BentoCard } from '../../../shared/components/layout/BentoCard';

export function OnboardingPage() {
    const { t } =
        useTranslation();

    const navigate =
        useNavigate();

    const queryClient =
        useQueryClient();

    const statusQuery =
        useQuery(
            onboardingQueries.status(),
        );

    const initialCountry =
        detectCountryCode();

    const [displayName, setDisplayName] =
        useState('');

    const [householdName, setHouseholdName] =
        useState('');

    const [countryCode, setCountryCode] =
        useState(initialCountry);

    const [
        preferredCurrency,
        setPreferredCurrency,
    ] = useState<
        'HUF' | 'EUR' | 'USD'
    >(
        suggestCurrency(
            initialCountry,
        ),
    );

    useEffect(
        () => {
            const data =
                statusQuery.data;

            if (!data) {
                return;
            }

            if (data.displayName) {
                setDisplayName(
                    data.displayName,
                );
            }

            if (data.household.name) {
                setHouseholdName(
                    data.household.name,
                );
            }

            if (data.household.countryCode) {
                setCountryCode(data.household.countryCode);
            }

            if (data.household.preferredCurrency
            ) {
                setPreferredCurrency(data.household.preferredCurrency as 'HUF' | 'EUR' | 'USD');
            }
        },
        [statusQuery.data],
    );

    const mutation =
        useMutation({
            mutationFn:
                completeOnboarding,
        });

    async function submit(
        event: FormEvent,
    ) {
        event.preventDefault();

        await mutation.mutateAsync({
            displayName,
            householdName,
            countryCode:
                countryCode
                    .trim()
                    .toUpperCase(),

            preferredCurrency,
        });

        await Promise.all([
            queryClient.invalidateQueries({
                queryKey:
                    onboardingQueryKeys.all,
            }),

            queryClient.invalidateQueries({
                queryKey:
                    authQueryKeys.all,
            }),
        ]);

        await navigate({
            to: '/app',
        });
    }

    return (
        <main
            className="
                min-h-screen

                bg-slate-50
                text-slate-950

                transition-colors

                dark:bg-slate-950
                dark:text-white

                px-5 py-8
                sm:px-8
                lg:py-14
            "
        >
            <div
                className="
                    mx-auto
                    max-w-5xl
                "
            >
                <header
                    className="
                        flex
                        items-center
                        justify-between
                    "
                >
                    <AverioBrand />

                    <PreferencesToolbar />
                </header>

                <div
                    className="
                        mt-12
                        grid
                        gap-10
                        lg:grid-cols-[0.8fr_1.2fr]
                    "
                >
                    <section>
                        <p
                            className="
                                text-sm
                                font-semibold
                                text-primary-700
                            "
                        >
                            {t(
                                'onboarding.eyebrow',
                            )}
                        </p>

                        <h1
                            className="
                                mt-3
                                text-4xl
                                font-semibold
                                tracking-tight
                            "
                        >
                            {t(
                                'onboarding.title',
                            )}
                        </h1>

                        <p
                            className="
                                mt-5
                                max-w-md
                                text-lg
                                leading-8
                                text-slate-600
                            "
                        >
                            {t(
                                'onboarding.description',
                            )}
                        </p>

                        <div
                            className="
                                mt-8
                                rounded-2xl
                                border
                                border-primary-200
                                bg-primary-50
                                p-5

                                text-sm
                                leading-6
                                text-primary-900

                                dark:border-primary-900/60
                                dark:bg-primary-950/40
                                dark:text-primary-200
                            "
                        >
                            {t(
                                'onboarding.householdOfOne',
                            )}
                        </div>
                    </section>

                    <BentoCard className="p-6 sm:p-8">
                        <form
                            onSubmit={submit}
                            className="space-y-6"
                        >
                            <div className="space-y-6">
                                <FormField
                                    label={t(
                                        'onboarding.displayName',
                                    )}
                                    value={displayName}
                                    maxLength={80}
                                    required
                                    autoComplete="name"
                                    onChange={(event) =>
                                        setDisplayName(
                                            event.target.value,
                                        )
                                    }
                                />

                                <FormField
                                    label={t(
                                        'onboarding.householdName',
                                    )}
                                    value={householdName}
                                    maxLength={120}
                                    required
                                    onChange={(event) =>
                                        setHouseholdName(
                                            event.target.value,
                                        )
                                    }
                                />

                                <div
                                    className="
                                    grid
                                    gap-5
                                    sm:grid-cols-2
                                "
                                >
                                    <FormField
                                        label={t(
                                            'onboarding.country',
                                        )}
                                        value={countryCode}
                                        maxLength={2}
                                        required
                                        onChange={(event) =>
                                            setCountryCode(
                                                event.target.value
                                                    .toUpperCase(),
                                            )
                                        }
                                    />

                                    <label className="block">
                                        <span
                                            className="
                                            text-sm
                                            font-medium
                                            text-slate-700
                                        "
                                        >
                                            {t(
                                                'onboarding.currency',
                                            )}
                                        </span>

                                        <select
                                            value={
                                                preferredCurrency
                                            }
                                            onChange={(event) =>
                                                setPreferredCurrency(
                                                    event.target.value as typeof preferredCurrency,
                                                )
                                            }
                                            className="
                                            mt-2
                                            w-full
                                            rounded-xl
                                            border
                                            border-slate-300
                                            bg-white
                                            px-4
                                            py-3
                                            outline-none
                                            focus:border-primary-500
                                            focus:ring-4
                                            focus:ring-primary-100
                                        "
                                        >
                                            <option value="EUR">
                                                EUR — €
                                            </option>

                                            <option value="HUF">
                                                HUF — Ft
                                            </option>

                                            <option value="USD">
                                                USD — $
                                            </option>
                                        </select>
                                    </label>
                                </div>
                            </div>

                            {mutation.isError && (
                                <div
                                    className="
                                    mt-6
                                    rounded-xl
                                    bg-red-50
                                    p-4
                                    text-sm
                                    text-red-700
                                "
                                >
                                    {mutation.error
                                        instanceof Error
                                        ? mutation.error.message
                                        : t(
                                            'common.unexpectedError',
                                        )}
                                </div>
                            )}

                            <div className="mt-8">
                                <PrimaryButton
                                    type="submit"
                                    loading={
                                        mutation.isPending
                                    }
                                >
                                    {t(
                                        'onboarding.continue',
                                    )}
                                </PrimaryButton>
                            </div>
                        </form>
                    </BentoCard>
                </div>
            </div>
        </main>
    );
}