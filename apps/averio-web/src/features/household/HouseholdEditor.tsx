import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { HouseholdResponse } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { householdApi, householdKeys } from './householdQueries';
import { onboardingQueryKeys } from '../onboarding/api/onboardingQueryKeys';
import { dashboardQueryKeys } from '../dashboard/api/dashboardQueryKeys';
import { authQueryKeys } from '../authentication/api/authQueryKeys';

// A draft retains the version it was opened with. Refetches never silently rebase it.
export function HouseholdEditor({ household, userId, onSaved, onCancel }: {
    household: HouseholdResponse; userId: string; onSaved: () => void; onCancel: () => void;
}) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const [name, setName] = useState(household.name ?? '');
    const [country, setCountry] = useState(household.countryCode ?? '');
    const [currency, setCurrency] = useState(household.preferredCurrency ?? 'EUR');
    const [version] = useState(household.version);
    const saving = useRef(false);
    const mutation = useMutation({
        mutationFn: () => householdApi.apiV1HouseholdsHouseholdIdPatch({
            householdId: household.id,
            updateHouseholdRequest: { name: name.trim(), countryCode: country.trim().toUpperCase(), preferredCurrency: currency, expectedVersion: version },
        }),
        onSuccess: async () => {
            await Promise.all([
                client.invalidateQueries({ queryKey: householdKeys.detail(userId, household.id) }),
                client.invalidateQueries({ queryKey: onboardingQueryKeys.all }),
                client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, household.id) }),
                client.invalidateQueries({ queryKey: authQueryKeys.all }),
            ]);
            onSaved();
        },
        onSettled: () => { saving.current = false; },
    });
    const error = mutation.error;
    const conflict = error instanceof ApiError && error.status === 409;
    const forbidden = error instanceof ApiError && error.status === 403;
    const fieldErrors = error instanceof ApiError ? error.validationErrors : undefined;
    const errorText = (field: string) => fieldErrors?.[field]?.join(' ');

    return <form className="space-y-5" onSubmit={event => {
        event.preventDefault();
        if (saving.current || conflict || forbidden) return;
        saving.current = true;
        mutation.mutate();
    }}>
        <div className="grid gap-4 sm:grid-cols-2">
            <AverioField className="sm:col-span-2">{t('householdSettings.name')}
                <AverioInput autoFocus required minLength={2} maxLength={120} value={name} disabled={mutation.isPending}
                    onChange={event => setName(event.target.value)} aria-invalid={Boolean(errorText('name'))}
                    aria-describedby={errorText('name') ? 'household-name-error' : undefined} />
                {errorText('name') && <span id="household-name-error" className="text-red-700 dark:text-red-300">{errorText('name')}</span>}
            </AverioField>
            <AverioField>{t('householdSettings.country')}
                <AverioInput required pattern="[A-Za-z]{2}" maxLength={2} value={country} disabled={mutation.isPending}
                    onChange={event => setCountry(event.target.value.toUpperCase())} aria-invalid={Boolean(errorText('countryCode'))}
                    aria-describedby="household-country-help household-country-error" />
                <span id="household-country-help" className="text-xs font-normal text-slate-600 dark:text-slate-300">{t('householdSettings.countryHelp')}</span>
                <span id="household-country-error" className="text-red-700 dark:text-red-300">{errorText('countryCode')}</span>
            </AverioField>
            <AverioField>{t('householdSettings.currency')}
                <AverioSelect value={currency} disabled={mutation.isPending} onChange={event => setCurrency(event.target.value)}
                    aria-describedby="household-currency-help household-currency-error" aria-invalid={Boolean(errorText('preferredCurrency'))}>
                    {['EUR', 'USD', 'HUF'].map(value => <option key={value} value={value}>{value}</option>)}
                </AverioSelect>
                <span id="household-currency-help" className="text-xs font-normal text-slate-600 dark:text-slate-300">{t('householdSettings.currencyHelp')}</span>
                <span id="household-currency-error" className="text-red-700 dark:text-red-300">{errorText('preferredCurrency')}</span>
            </AverioField>
        </div>
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">
            {conflict ? t('householdSettings.conflict') : forbidden ? t('householdSettings.forbidden') : error instanceof ApiError ? error.message : t('errors.genericDescription')}
        </p>}
        <div className="flex flex-wrap justify-end gap-2 border-t border-emerald-900/10 pt-4 dark:border-emerald-100/10">
            <AverioButton onClick={onCancel} disabled={mutation.isPending}>{t(conflict || forbidden ? 'householdSettings.returnToLatest' : 'householdSettings.cancel')}</AverioButton>
            <AverioButton type="submit" variant="primary" disabled={mutation.isPending || conflict || forbidden || name.trim().length < 2}>
                {t(mutation.isPending ? 'householdSettings.saving' : 'householdSettings.save')}
            </AverioButton>
        </div>
    </form>;
}
