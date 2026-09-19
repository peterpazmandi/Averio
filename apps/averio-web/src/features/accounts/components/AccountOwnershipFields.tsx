import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { AccountOwnerOptionsResponse } from '../../../shared/api/generated';

export interface AccountOwnershipValue {
    scope: 'Household' | 'Personal';
    ownerHouseholdMemberIds: string[];
    visibility: 'Household' | 'Private';
    includeInHouseholdReports: boolean;
}

interface AccountOwnershipFieldsProps {
    options: AccountOwnerOptionsResponse;
    value: AccountOwnershipValue;
    onChange: (value: AccountOwnershipValue) => void;
    lockScope?: boolean;
}

export function AccountOwnershipFields({
    options,
    value,
    onChange,
    lockScope = false,
}: AccountOwnershipFieldsProps) {
    const { t } = useTranslation();
    const id = useId();
    const selectClass = 'w-full rounded-xl border border-slate-300 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-950';

    function changeScope(scope: AccountOwnershipValue['scope']) {
        onChange({
            ...value,
            scope,
            ownerHouseholdMemberIds: scope === 'Personal'
                ? [...new Set([...value.ownerHouseholdMemberIds, options.currentMemberId])]
                : value.ownerHouseholdMemberIds,
            visibility: scope === 'Household' ? 'Household' : value.visibility,
            includeInHouseholdReports: scope === 'Household'
                ? true
                : value.includeInHouseholdReports,
        });
    }

    return (
        <div className="space-y-5">
            <label className="block space-y-2">
                <span className="text-sm font-medium">{t('accountOwnership.scope')}</span>
                <select
                    className={selectClass}
                    value={value.scope}
                    disabled={lockScope}
                    onChange={(event) => {
                        const scope = event.target.value;
                        if (scope === 'Household' || scope === 'Personal') changeScope(scope);
                    }}
                >
                    <option value="Household">{t('accountOwnership.household')}</option>
                    <option value="Personal">{t('accountOwnership.personal')}</option>
                </select>
            </label>

            <fieldset className="space-y-2" aria-describedby={id}>
                <legend className="text-sm font-medium">{t('accountOwnership.holders')}</legend>
                <p id={id} className="text-sm text-slate-600 dark:text-slate-400">
                    {t('accountOwnership.holdersHelp')}
                </p>
                {options.members.map((member, index) => (
                    <label key={member.householdMemberId} className="flex items-center gap-3">
                        <input
                            type="checkbox"
                            checked={value.ownerHouseholdMemberIds.includes(member.householdMemberId)}
                            disabled={value.scope === 'Personal'
                                && member.householdMemberId === options.currentMemberId}
                            onChange={(event) => {
                                onChange({
                                    ...value,
                                    ownerHouseholdMemberIds: event.target.checked
                                        ? [...value.ownerHouseholdMemberIds, member.householdMemberId]
                                        : value.ownerHouseholdMemberIds.filter(
                                            (memberId) => memberId !== member.householdMemberId,
                                        ),
                                });
                            }}
                        />
                        <span>
                            {member.displayName || t('accountOwnership.member', { number: index + 1 })}
                            {member.householdMemberId === options.currentMemberId
                                ? ` (${t('accountOwnership.you')})`
                                : ''}
                        </span>
                    </label>
                ))}
                {value.ownerHouseholdMemberIds.length === 0 && (
                    <p className="text-sm">{t('accountOwnership.noHolders')}</p>
                )}
                {value.scope === 'Personal' && (
                    <p className="text-sm">{t('accountOwnership.personalHolderHelp')}</p>
                )}
            </fieldset>

            <label className="block space-y-2">
                <span className="text-sm font-medium">{t('accountOwnership.visibility')}</span>
                <select
                    className={selectClass}
                    value={value.visibility}
                    disabled={value.scope === 'Household'}
                    onChange={(event) => {
                        const visibility = event.target.value;
                        if (visibility !== 'Household' && visibility !== 'Private') return;
                        onChange({
                            ...value,
                            visibility,
                            includeInHouseholdReports: visibility === 'Private'
                                ? false
                                : value.includeInHouseholdReports,
                        });
                    }}
                >
                    <option value="Household">{t('accountOwnership.visible')}</option>
                    <option value="Private">{t('accountOwnership.private')}</option>
                </select>
            </label>
            <p className="text-sm text-slate-600 dark:text-slate-400">
                {t(value.visibility === 'Private'
                    ? 'accountOwnership.privateHelp'
                    : 'accountOwnership.visibleHelp')}
            </p>

            <label className="flex items-start gap-3">
                <input
                    type="checkbox"
                    checked={value.includeInHouseholdReports}
                    disabled={value.scope === 'Household' || value.visibility === 'Private'}
                    onChange={(event) => onChange({
                        ...value,
                        includeInHouseholdReports: event.target.checked,
                    })}
                />
                <span>{t('accountOwnership.reports')}</span>
            </label>
        </div>
    );
}
