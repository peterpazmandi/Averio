import { useQueryClient, useMutation } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import type { FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type { OnboardingStatusResponse } from "../../../shared/api/generated";
import { FormField } from "../../../shared/components/FormField";
import { PrimaryButton } from "../../../shared/components/PrimaryButton";
import { authQueryKeys } from "../../authentication/api/authQueryKeys";
import { completeOnboarding } from "../api/onboardingApi";
import { onboardingQueryKeys } from "../api/onboardingQueryKeys";
import { detectCountryCode, suggestCurrency } from "../detectCountry";
import type { Currency } from "./OnboardingPage";

function isCurrency(value: unknown): value is Currency {
    return value === 'HUF'
        || value === 'EUR'
        || value === 'USD';
}

/**
 * Owns the editable form state.
 *
 * State is initialized once from this user's response.
 * Background refetches do not overwrite edits.
 */
export function OnboardingForm({
    userId,
    initialData,
}: {
    userId: string;
    initialData: OnboardingStatusResponse;
}) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const queryClient = useQueryClient();

    const [displayName, setDisplayName] = useState(() => initialData.displayName ?? '',
    );

    const [householdName, setHouseholdName] = useState(
        () => initialData.household.name ?? '',
    );

    const [countryCode, setCountryCode] = useState(
        () => initialData.household.countryCode
            ?? detectCountryCode(),
    );

    const [preferredCurrency, setPreferredCurrency] =
        useState<Currency>(() => {
            const storedCurrency =
                initialData.household.preferredCurrency;

            return isCurrency(storedCurrency)
                ? storedCurrency
                : suggestCurrency(countryCode);
        });

    const [isSubmitting, setIsSubmitting] = useState(false);
    const [submitError, setSubmitError] = useState<string | null>(
        null,
    );

    const mutation = useMutation({
        mutationFn: completeOnboarding,
    });

    async function submit(event: FormEvent<HTMLFormElement>): Promise<void> {
        event.preventDefault();

        if (isSubmitting) {
            return;
        }

        setIsSubmitting(true);
        setSubmitError(null);

        try {
            await mutation.mutateAsync({
                displayName: displayName.trim(),
                householdName: householdName.trim(),
                countryCode: countryCode.trim().toUpperCase(),
                preferredCurrency,
            });

            await Promise.all([
                queryClient.invalidateQueries({
                    queryKey: onboardingQueryKeys.status(userId),
                    exact: true,
                }),

                queryClient.invalidateQueries({
                    queryKey: authQueryKeys.all,
                }),
            ]);

            await navigate({
                to: '/app',
            });
        }
        catch (error) {
            setSubmitError(
                error instanceof Error
                    ? error.message
                    : t('errors.genericDescription'),
            );
        }
        finally {
            setIsSubmitting(false);
        }
    }

    return (
        <form
            onSubmit={submit}
            className="space-y-6"
            aria-busy={isSubmitting}
        >
            <fieldset
                disabled={isSubmitting}
                className="min-w-0 space-y-6"
            >
                <FormField
                    label={t('onboarding.displayName')}
                    value={displayName}
                    minLength={2}
                    maxLength={80}
                    required
                    autoComplete="name"
                    onChange={(event) => {
                        setDisplayName(event.target.value);
                    }}
                />

                <FormField
                    label={t('onboarding.householdName')}
                    value={householdName}
                    minLength={2}
                    maxLength={120}
                    required
                    onChange={(event) => {
                        setHouseholdName(event.target.value);
                    }}
                />

                <div className="grid gap-5 sm:grid-cols-2">
                    <FormField
                        label={t('onboarding.country')}
                        value={countryCode}
                        minLength={2}
                        maxLength={2}
                        pattern="[A-Za-z]{2}"
                        required
                        layout="horizontal"
                        onChange={(event) => {
                            setCountryCode(
                                event.target.value.toUpperCase(),
                            );
                        }}
                    />

                    <label
                        className="
                            grid grid-cols-2
                            items-center gap-2
                        "
                    >
                        <span
                            className="
                                text-sm font-medium
                                text-slate-700
                                dark:text-slate-300
                            "
                        >
                            {t('onboarding.currency')}
                        </span>

                        <select
                            value={preferredCurrency}
                            onChange={(event) => {
                                const value = event.target.value;

                                if (isCurrency(value)) {
                                    setPreferredCurrency(value);
                                }
                            }}
                            className="
                                w-full
                                rounded-xl
                                border border-slate-300
                                bg-white
                                px-4 py-3
                                text-slate-950
                                outline-none
                                focus:border-primary-500
                                focus:ring-4
                                focus:ring-primary-100
                                dark:border-slate-700
                                dark:bg-slate-950
                                dark:text-white
                                dark:focus:border-primary-500
                                dark:focus:ring-primary-950
                            "
                        >
                            <option value="EUR">EUR — €</option>
                            <option value="HUF">HUF — Ft</option>
                            <option value="USD">USD — $</option>
                        </select>
                    </label>
                </div>
            </fieldset>

            {submitError && (
                <div
                    role="alert"
                    className="
                        rounded-xl
                        bg-red-50
                        p-4
                        text-sm text-red-700
                        dark:bg-red-950/40
                        dark:text-red-400
                    "
                >
                    {submitError}
                </div>
            )}

            <PrimaryButton
                type="submit"
                loading={isSubmitting}
            >
                {t('onboarding.continue')}
            </PrimaryButton>
        </form>
    );
}