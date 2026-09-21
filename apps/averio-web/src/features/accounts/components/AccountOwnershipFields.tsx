import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { AccountOwnerOptionsResponse } from '../../../shared/api/generated';
import { AccountFieldHelp } from './AccountFieldHelp';
import '../accounts.css';

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

export function AccountOwnershipFields({ options, value, onChange, lockScope = false }: AccountOwnershipFieldsProps) {
    const { t } = useTranslation();
    const id = useId();
    const household = value.scope === 'Household';
    const privateAccount = value.visibility === 'Private';
    const reportsLocked = household || privateAccount;
    function changeScope(scope: AccountOwnershipValue['scope']) {
        onChange({ ...value, scope,
            ownerHouseholdMemberIds: scope === 'Personal'
                ? [...new Set([...value.ownerHouseholdMemberIds, options.currentMemberId])] : value.ownerHouseholdMemberIds,
            visibility: scope === 'Household' ? 'Household' : value.visibility,
            includeInHouseholdReports: scope === 'Household' ? true : value.includeInHouseholdReports,
        });
    }
    return (
        <div className="av-ownership-fields">
            <section className="av-form-section" aria-labelledby={id + '-ownership'}>
                <header><h3 id={id + '-ownership'}>{t('accountManagement.ownershipSection')}</h3>
                    <p>{t('accountOwnership.holdersHelp')}</p></header>
                <div className="av-field" data-locked={lockScope}>
                    <label htmlFor={id + '-scope'}>{t('accountOwnership.scope')}{lockScope && <span className="av-fixed-label">{t('accountManagement.fixed')}</span>}</label>
                    <select id={id + '-scope'} className="av-control" value={value.scope} disabled={lockScope}
                        aria-describedby={lockScope ? id + '-scope-help' : undefined}
                        onChange={event => { const scope = event.target.value; if (scope === 'Household' || scope === 'Personal') changeScope(scope); }}>
                        <option value="Household">{t('accountOwnership.household')}</option>
                        <option value="Personal">{t('accountOwnership.personal')}</option>
                    </select>
                    {lockScope && <AccountFieldHelp id={id + '-scope-help'} field={t('accountOwnership.scope')}>{t('accountManagement.scopeFixed')}</AccountFieldHelp>}
                </div>
                <fieldset className="av-holders">
                    <legend>{t('accountOwnership.holders')}</legend>
                    {options.members.map((member, index) => {
                        const locked = !household && member.householdMemberId === options.currentMemberId;
                        const name = member.displayName || t('accountOwnership.member', { number: index + 1 });
                        return <div className="av-holder-choice" key={member.householdMemberId} data-locked={locked}>
                            <label className="av-check-row">
                                <input type="checkbox" checked={value.ownerHouseholdMemberIds.includes(member.householdMemberId)} disabled={locked}
                                    aria-describedby={locked ? id + '-holder-help' : undefined}
                                    onChange={event => onChange({ ...value, ownerHouseholdMemberIds: event.target.checked
                                        ? [...value.ownerHouseholdMemberIds, member.householdMemberId]
                                        : value.ownerHouseholdMemberIds.filter(memberId => memberId !== member.householdMemberId) })} />
                                <span>{name}{member.householdMemberId === options.currentMemberId ? ' (' + t('accountOwnership.you') + ')' : ''}</span>
                                {locked && <span className="av-fixed-label">{t('accountManagement.fixed')}</span>}
                            </label>
                            {locked && <AccountFieldHelp id={id + '-holder-help'} field={name}>{t('accountOwnership.personalHolderHelp')}</AccountFieldHelp>}
                        </div>;
                    })}
                    {value.ownerHouseholdMemberIds.length === 0 && <p className="av-field-description">{t('accountOwnership.noHolders')}</p>}
                </fieldset>
            </section>
            <section className="av-form-section" aria-labelledby={id + '-sharing'}>
                <header><h3 id={id + '-sharing'}>{t('accountManagement.sharingSection')}</h3></header>
                <div className="av-field" data-locked={household}>
                    <label htmlFor={id + '-visibility'}>{t('accountOwnership.visibility')}{household && <span className="av-fixed-label">{t('accountManagement.fixed')}</span>}</label>
                    <select id={id + '-visibility'} className="av-control" value={value.visibility} disabled={household}
                        aria-describedby={household ? id + '-visibility-help' : id + '-visibility-description'}
                        onChange={event => { const visibility = event.target.value; if (visibility !== 'Household' && visibility !== 'Private') return;
                            onChange({ ...value, visibility, includeInHouseholdReports: visibility === 'Private' ? false : value.includeInHouseholdReports }); }}>
                        <option value="Household">{t('accountOwnership.visible')}</option>
                        <option value="Private">{t('accountOwnership.private')}</option>
                    </select>
                    {household ? <AccountFieldHelp id={id + '-visibility-help'} field={t('accountOwnership.visibility')}>{t('accountManagement.visibilityFixed')}</AccountFieldHelp>
                        : <p id={id + '-visibility-description'} className="av-field-description">{t(privateAccount ? 'accountOwnership.privateHelp' : 'accountOwnership.visibleHelp')}</p>}
                </div>
                <div className="av-field" data-locked={reportsLocked}>
                    <label className="av-check-row">
                        <input type="checkbox" checked={value.includeInHouseholdReports} disabled={reportsLocked}
                            aria-describedby={reportsLocked ? id + '-reports-help' : undefined}
                            onChange={event => onChange({ ...value, includeInHouseholdReports: event.target.checked })} />
                        <span>{t('accountOwnership.reports')}</span>
                        {reportsLocked && <span className="av-fixed-label">{t('accountManagement.fixed')}</span>}
                    </label>
                    {reportsLocked && <AccountFieldHelp id={id + '-reports-help'} field={t('accountOwnership.reports')}>
                        {t(household ? 'accountManagement.householdReportingFixed' : 'accountManagement.privateReportingFixed')}
                    </AccountFieldHelp>}
                </div>
            </section>
        </div>
    );
}
