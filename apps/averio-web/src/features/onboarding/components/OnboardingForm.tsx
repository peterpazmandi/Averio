import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import type {
    CreateAccountRequest,
    OnboardingStatusResponse,
} from '../../../shared/api/generated';

import { FormField } from '../../../shared/components/FormField';
import { PrimaryButton } from '../../../shared/components/PrimaryButton';

import { authQueryKeys } from '../../authentication/api/authQueryKeys';

import {
    completeFirstAccount,
    saveOnboardingHousehold,
    saveOnboardingProfile,
} from '../api/onboardingApi';

import { onboardingQueryKeys } from '../api/onboardingQueryKeys';
import { detectCountryCode, suggestCurrency } from '../detectCountry';
import type { Currency } from './OnboardingPage';

type AccountType =
    | 'Checking'
    | 'Savings'
    | 'Cash'
    | 'CreditCard';

type AccountSharing =
    | 'shared'
    | 'personalVisible'
    | 'private';

type CreditCardBalanceKind =
    | 'owed'
    | 'credit';

function isCurrency(value: unknown): value is Currency {
    return value === 'HUF'
        || value === 'EUR'
        || value === 'USD';
}

function isAccountType(value: string): value is AccountType {
    return value === 'Checking'
        || value === 'Savings'
        || value === 'Cash'
        || value === 'CreditCard';
}

function isAccountSharing(value: string): value is AccountSharing {
    return value === 'shared'
        || value === 'personalVisible'
        || value === 'private';
}

function previousLocalDate(): string {
    const date = new Date();

    date.setDate(date.getDate() - 1);

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    return `${year}-${month}-${day}`;
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

    const currentStep = initialData.currentStep;
    const householdId = initialData.household.householdId;

    const isProfileStep = currentStep === 'profile';
    const isHouseholdStep = currentStep === 'household';
    const isFirstAccountStep = currentStep === 'firstAccount';

    const [displayName, setDisplayName] = useState(
        () => initialData.displayName ?? '',
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

    const [accountName, setAccountName] = useState('');

    const [accountType, setAccountType] =
        useState<AccountType>('Checking');

    const [accountSharing, setAccountSharing] =
        useState<AccountSharing>('shared');

    const [
        includeInHouseholdReports,
        setIncludeInHouseholdReports,
    ] = useState(true);

    const [accountCurrencyOverride, setAccountCurrencyOverride] =
        useState<Currency | null>(null);

    const accountCurrency =
        accountCurrencyOverride ?? preferredCurrency;

    const [balanceAmount, setBalanceAmount] = useState('');

    const [balanceAnchorDate, setBalanceAnchorDate] = useState(
        previousLocalDate,
    );

    const [creditCardBalanceKind, setCreditCardBalanceKind] =
        useState<CreditCardBalanceKind>('owed');

    const [isSubmitting, setIsSubmitting] = useState(false);

    const [submitError, setSubmitError] = useState<string | null>(
        null,
    );

    const submittingRef = useRef(false);

    const profileMutation = useMutation({
        mutationFn: saveOnboardingProfile,
        retry: false,
    });

    const householdMutation = useMutation({
        mutationFn: saveOnboardingHousehold,
        retry: false,
    });

    const firstAccountMutation = useMutation({
        mutationFn: (request: CreateAccountRequest) =>
            completeFirstAccount(
                householdId,
                request,
            ),
        retry: false,
    });

    function buildAccountRequest(): CreateAccountRequest {
        const enteredAmount = Number(balanceAmount);

        if (
            balanceAmount.trim() === ''
            || !Number.isFinite(enteredAmount)
        ) {
            throw new Error(
                t('onboarding.account.invalidBalance'),
            );
        }

        if (
            accountType === 'CreditCard'
            && enteredAmount < 0
        ) {
            throw new Error(
                t('onboarding.account.enterPositiveAmount'),
            );
        }

        const balanceAnchorAmount =
            accountType === 'CreditCard'
                && creditCardBalanceKind === 'owed'
                ? -enteredAmount
                : enteredAmount;

        const ownership =
            accountSharing === 'shared'
                ? 'Household'
                : 'Personal';

        const visibility =
            accountSharing === 'private'
                ? 'Private'
                : 'Household';

        const includeInReports =
            accountSharing === 'shared'
                ? true
                : accountSharing === 'private'
                    ? false
                    : includeInHouseholdReports;

        return {
            name: accountName.trim(),
            type: accountType,
            ownership,
            visibility,
            includeInHouseholdReports: includeInReports,
            currency: accountCurrency,
            balanceAnchorAmount,
            balanceAnchorDate,
        };
    }

    async function submit(
        event: FormEvent<HTMLFormElement>,
    ): Promise<void> {
        event.preventDefault();

        if (submittingRef.current) {
            return;
        }

        if (
            !isProfileStep
            && !isHouseholdStep
            && !isFirstAccountStep
        ) {
            return;
        }

        submittingRef.current = true;
        setIsSubmitting(true);
        setSubmitError(null);

        try {
            if (isProfileStep) {
                await profileMutation.mutateAsync({
                    displayName: displayName.trim(),
                });
            }
            else if (isHouseholdStep) {
                await householdMutation.mutateAsync({
                    householdName: householdName.trim(),
                    countryCode: countryCode
                        .trim()
                        .toUpperCase(),
                    preferredCurrency,
                });
            }
            else {
                await firstAccountMutation.mutateAsync(
                    buildAccountRequest(),
                );
            }

            await Promise.all([
                queryClient.invalidateQueries({
                    queryKey: onboardingQueryKeys.status(userId),
                    exact: true,
                }),

                queryClient.invalidateQueries({
                    queryKey: authQueryKeys.all,
                }),
            ]);

            if (isFirstAccountStep) {
                await navigate({
                    to: '/app',
                    replace: true,
                });
            }
        }
        catch (error) {
            setSubmitError(
                error instanceof Error
                    ? error.message
                    : t('errors.genericDescription'),
            );
        }
        finally {
            submittingRef.current = false;
            setIsSubmitting(false);
        }
    }

    const stepTitle =
        isProfileStep
            ? t('onboarding.steps.profile')
            : isHouseholdStep
                ? t('onboarding.steps.household')
                : t('onboarding.steps.firstAccount');

    const stepNumber =
        isProfileStep
            ? 1
            : isHouseholdStep
                ? 2
                : 3;

    if (
        !isProfileStep
        && !isHouseholdStep
        && !isFirstAccountStep
    ) {
        return (
            <p role="status">
                {currentStep === 'completed'
                    ? t('onboarding.setupCompleted')
                    : t('errors.genericDescription')}
            </p>
        );
    }

    return (
        <form
            onSubmit={submit}
            className="space-y-6"
            aria-busy={isSubmitting}
        >
            <header aria-live="polite">
                <p className="text-sm text-slate-500 dark:text-slate-400">
                    {t('onboarding.stepProgress', {
                        current: stepNumber,
                        total: 3,
                    })}
                </p>

                <h2 className="mt-2 text-2xl font-semibold">
                    {stepTitle}
                </h2>
            </header>

            <fieldset
                disabled={isSubmitting}
                className="min-w-0 space-y-6"
            >
                {isProfileStep && (
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
                )}

                {isHouseholdStep && (
                    <>
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

                        <FormField
                            label={t('onboarding.country')}
                            value={countryCode}
                            minLength={2}
                            maxLength={2}
                            pattern="[A-Za-z]{2}"
                            required
                            onChange={(event) => {
                                setCountryCode(
                                    event.target.value.toUpperCase(),
                                );
                            }}
                        />

                        <label className="block space-y-2">
                            <span className="text-sm font-medium">
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
                                    w-full rounded-xl border
                                    border-slate-300 bg-white px-4 py-3
                                    dark:border-slate-700 dark:bg-slate-950
                                "
                            >
                                <option value="EUR">EUR — €</option>
                                <option value="HUF">HUF — Ft</option>
                                <option value="USD">USD — $</option>
                            </select>
                        </label>
                    </>
                )}

                {isFirstAccountStep && (
                    <>
                        <FormField
                            label={t('onboarding.account.name')}
                            value={accountName}
                            maxLength={100}
                            required
                            onChange={(event) => {
                                setAccountName(event.target.value);
                            }}
                        />

                        <label className="block space-y-2">
                            <span className="text-sm font-medium">
                                {t('onboarding.account.type')}
                            </span>

                            <select
                                value={accountType}
                                onChange={(event) => {
                                    const value = event.target.value;

                                    if (isAccountType(value)) {
                                        setAccountType(value);
                                    }
                                }}
                                className="
                                    w-full rounded-xl border
                                    border-slate-300 bg-white px-4 py-3
                                    dark:border-slate-700 dark:bg-slate-950
                                "
                            >
                                <option value="Checking">
                                    {t('onboarding.account.checking')}
                                </option>
                                <option value="Savings">
                                    {t('onboarding.account.savings')}
                                </option>
                                <option value="Cash">
                                    {t('onboarding.account.cash')}
                                </option>
                                <option value="CreditCard">
                                    {t('onboarding.account.creditCard')}
                                </option>
                            </select>
                        </label>

                        <label className="block space-y-2">
                            <span className="text-sm font-medium">
                                {t('onboarding.account.sharing')}
                            </span>

                            <select
                                value={accountSharing}
                                onChange={(event) => {
                                    const value = event.target.value;

                                    if (isAccountSharing(value)) {
                                        setAccountSharing(value);
                                    }
                                }}
                                className="
                                    w-full rounded-xl border
                                    border-slate-300 bg-white px-4 py-3
                                    dark:border-slate-700 dark:bg-slate-950
                                "
                            >
                                <option value="shared">
                                    {t('onboarding.account.shared')}
                                </option>
                                <option value="personalVisible">
                                    {t('onboarding.account.personalVisible')}
                                </option>
                                <option value="private">
                                    {t('onboarding.account.private')}
                                </option>
                            </select>
                        </label>

                        <p className="text-sm text-slate-600 dark:text-slate-400">
                            {accountSharing === 'private'
                                ? t('onboarding.account.privateHelp')
                                : t('onboarding.account.visibleHelp')}
                        </p>

                        {accountSharing === 'personalVisible' && (
                            <label className="flex items-start gap-3">
                                <input
                                    type="checkbox"
                                    checked={includeInHouseholdReports}
                                    onChange={(event) => {
                                        setIncludeInHouseholdReports(
                                            event.target.checked,
                                        );
                                    }}
                                    className="mt-1"
                                />

                                <span>
                                    {t('onboarding.account.includeInReports')}
                                </span>
                            </label>
                        )}

                        <label className="block space-y-2">
                            <span className="text-sm font-medium">
                                {t('onboarding.account.currency')}
                            </span>

                            <select
                                value={accountCurrency}
                                onChange={(event) => {
                                    const value = event.target.value;

                                    if (isCurrency(value)) {
                                        setAccountCurrencyOverride(value);
                                    }
                                }}
                                className="
                                    w-full rounded-xl border
                                    border-slate-300 bg-white px-4 py-3
                                    dark:border-slate-700 dark:bg-slate-950
                                "
                            >
                                <option value="EUR">EUR — €</option>
                                <option value="HUF">HUF — Ft</option>
                                <option value="USD">USD — $</option>
                            </select>
                        </label>

                        <p className="text-sm text-slate-600 dark:text-slate-400">
                            {t('onboarding.account.currencyHelp')}
                        </p>

                        {accountType === 'CreditCard' && (
                            <label className="block space-y-2">
                                <span className="text-sm font-medium">
                                    {t('onboarding.account.balanceKind')}
                                </span>

                                <select
                                    value={creditCardBalanceKind}
                                    onChange={(event) => {
                                        const value = event.target.value;

                                        if (
                                            value === 'owed'
                                            || value === 'credit'
                                        ) {
                                            setCreditCardBalanceKind(value);
                                        }
                                    }}
                                    className="
                                        w-full rounded-xl border
                                        border-slate-300 bg-white px-4 py-3
                                        dark:border-slate-700 dark:bg-slate-950
                                    "
                                >
                                    <option value="owed">
                                        {t('onboarding.account.owed')}
                                    </option>
                                    <option value="credit">
                                        {t('onboarding.account.credit')}
                                    </option>
                                </select>
                            </label>
                        )}

                        <FormField
                            label={
                                accountType === 'CreditCard'
                                    ? creditCardBalanceKind === 'owed'
                                        ? t('onboarding.account.owed')
                                        : t('onboarding.account.credit')
                                    : t('onboarding.account.balance')
                            }
                            type="number"
                            inputMode="decimal"
                            step="0.0001"
                            min={
                                accountType === 'CreditCard'
                                    ? 0
                                    : undefined
                            }
                            value={balanceAmount}
                            required
                            onChange={(event) => {
                                setBalanceAmount(event.target.value);
                            }}
                        />

                        <FormField
                            label={t('onboarding.account.balanceDate')}
                            type="date"
                            value={balanceAnchorDate}
                            required
                            onChange={(event) => {
                                setBalanceAnchorDate(event.target.value);
                            }}
                        />

                        <p className="text-sm text-slate-600 dark:text-slate-400">
                            {t('onboarding.account.balanceHelp')}
                        </p>
                    </>
                )}

            </fieldset>

            {submitError && (
                <div
                    role="alert"
                    className="
                        rounded-xl bg-red-50 p-4
                        text-sm text-red-700
                        dark:bg-red-950/40 dark:text-red-400
                    "
                >
                    {submitError}
                </div>
            )}

            <PrimaryButton
                type="submit"
                loading={isSubmitting}
                disabled={isSubmitting}
            >
                {isFirstAccountStep
                    ? t('onboarding.finishSetup')
                    : t('onboarding.continue')}
            </PrimaryButton>
        </form>
    );
}