import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';

import type {
    CreateAccountRequest,
    OnboardingStatusResponse,
} from '../../../shared/api/generated';

import {
    normalizeBalanceInput,
    negateDecimalAmount,
} from '../../../shared/money/decimalAmount';

import { FormField } from '../../../shared/components/FormField';
import { PrimaryButton } from '../../../shared/components/PrimaryButton';

import { accountsQueries } from '../../accounts/api/accountsQueries';
import { accountsQueryKeys } from '../../accounts/api/accountsQueryKeys';
import { dashboardQueryKeys } from '../../dashboard/api/dashboardQueryKeys';
import { AccountOwnershipFields } from '../../accounts/components/AccountOwnershipFields';
import type { AccountOwnershipValue } from '../../accounts/components/AccountOwnershipFields';

import { authQueryKeys } from '../../authentication/api/authQueryKeys';

import {
    completeFirstAccount,
    saveOnboardingHousehold,
    saveOnboardingProfile,
} from '../api/onboardingApi';

import { onboardingQueryKeys } from '../api/onboardingQueryKeys';
import { detectCountryCode, suggestCurrency } from '../detectCountry';
import type { Currency } from './OnboardingPage';

export type OnboardingStep = 'profile' | 'household' | 'firstAccount';

type AccountType =
    | 'Checking'
    | 'Savings'
    | 'Cash'
    | 'CreditCard';


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
    currentStep,
    onStepChange,
}: {
    userId: string;
    initialData: OnboardingStatusResponse;
    currentStep: string;
    onStepChange: (step: OnboardingStep) => void;
}) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const queryClient = useQueryClient();

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

    const ownerOptionsQuery = useQuery({
        ...accountsQueries.ownerOptions(userId, householdId),
        enabled: isFirstAccountStep,
    });

    // Null means the default has not been edited. Refetches never overwrite edits.
    const [ownershipOverride, setOwnershipOverride] =
        useState<AccountOwnershipValue | null>(null);

    const ownershipValue: AccountOwnershipValue = ownershipOverride ?? {
        scope: 'Household',
        ownerHouseholdMemberIds: ownerOptionsQuery.data
            ? [ownerOptionsQuery.data.currentMemberId]
            : [],
        visibility: 'Household',
        includeInHouseholdReports: true,
    };

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
        const enteredAmount = normalizeBalanceInput(balanceAmount);

        if (
            enteredAmount === null
        ) {
            throw new Error(
                t('onboarding.account.invalidBalance'),
            );
        }

        if (
            accountType === 'CreditCard'
            && enteredAmount.startsWith('-')
        ) {
            throw new Error(
                t('onboarding.account.enterPositiveAmount'),
            );
        }

        const balanceAnchorAmount =
            accountType === 'CreditCard'
                && creditCardBalanceKind === 'owed'
                ? negateDecimalAmount(enteredAmount)
                : enteredAmount;

        if (!ownerOptionsQuery.isSuccess) {
            throw new Error(t('accountOwnership.loadRequired'));
        }

        return {
            name: accountName.trim(),
            type: accountType,
            ...ownershipValue,
            currency: accountCurrency,
            balanceAnchorAmount,
            balanceAnchorDate,
        };
    }

    function goBack(): void {
        if (submittingRef.current || isProfileStep) {
            return;
        }

        setSubmitError(null);
        onStepChange(isFirstAccountStep ? 'household' : 'profile');
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
                await Promise.all([
                    queryClient.invalidateQueries({
                        queryKey: accountsQueryKeys.household(userId, householdId),
                    }),
                    queryClient.invalidateQueries({
                        queryKey: dashboardQueryKeys.household(userId, householdId),
                    }),
                ]);
                await navigate({
                    to: '/app',
                    replace: true,
                });
            }
            else {
                onStepChange(isProfileStep ? 'household' : 'firstAccount');
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

                        {ownerOptionsQuery.isPending && (
                            <p role="status">{t('common.status.loading')}</p>
                        )}
                        {ownerOptionsQuery.isError && (
                            <div role="alert" className="space-y-2">
                                <p>{t('accountOwnership.loadRequired')}</p>
                                <PrimaryButton
                                    type="button"
                                    loading={ownerOptionsQuery.isFetching}
                                    onClick={() => { void ownerOptionsQuery.refetch(); }}
                                >
                                    {t('common.actions.retry')}
                                </PrimaryButton>
                            </div>
                        )}
                        {ownerOptionsQuery.isSuccess && (
                            <AccountOwnershipFields
                                options={ownerOptionsQuery.data}
                                value={ownershipValue}
                                onChange={setOwnershipOverride}
                            />
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
                            type="text"
                            inputMode="decimal"
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

            <div className="flex flex-wrap items-center justify-between gap-3">
                {!isProfileStep && (
                    <button
                        type="button"
                        className="av-btn"
                        disabled={isSubmitting}
                        onClick={goBack}
                    >
                        {t('common.actions.back')}
                    </button>
                )}

                <div className="ml-auto">
                    <PrimaryButton
                        type="submit"
                        loading={isSubmitting}
                        disabled={isSubmitting || (isFirstAccountStep && !ownerOptionsQuery.isSuccess)}
                    >
                        {isFirstAccountStep
                            ? t('onboarding.finishSetup')
                            : t('onboarding.continue')}
                    </PrimaryButton>
                </div>
            </div>
        </form>
    );
}
