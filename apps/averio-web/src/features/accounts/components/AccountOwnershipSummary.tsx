import { useTranslation } from 'react-i18next';
import type { AccountResponse } from '../../../shared/api/generated';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import type { ConceptIconName } from '../../../shared/components/concept/ConceptIcon';
import '../accounts.css';

export function AccountOwnershipSummary({ account }: { account: AccountResponse }) {
    const { t } = useTranslation();
    const holders = account.owners.map((owner, index) =>
        (owner.displayName || t('accountOwnership.member', { number: index + 1 })) + (owner.isFormerMember ? ' (' + t('invitations.history.formerMember') + ')' : ''),
    ).join(', ');
    const rows: { icon: ConceptIconName; tone: string; label: string; value: string }[] = [
        { icon: account.scope === 'Household' ? 'household' : 'profile', tone: account.scope === 'Household' ? 'blue' : 'purple',
            label: t('accountManagement.moneyFor'), value: t(account.scope === 'Household' ? 'accountOwnership.household' : 'accountOwnership.personal') },
        { icon: 'group', tone: 'neutral', label: t('accountManagement.heldBy'), value: holders || t('accountOwnership.noHolders') },
        { icon: account.visibility === 'Private' ? 'lock' : 'group', tone: account.visibility === 'Private' ? 'amber' : 'teal', label: t('accountManagement.visibleTo'),
            value: t(account.visibility === 'Private' ? 'accountOwnership.private' : 'accountOwnership.visible') },
        { icon: account.includeInHouseholdReports ? 'reportIncluded' : 'reportExcluded', tone: account.includeInHouseholdReports ? 'teal' : 'neutral', label: t('accountManagement.reporting'),
            value: t(account.includeInHouseholdReports ? 'accountOwnership.included' : 'accountOwnership.excluded') },
    ];
    return (
        <dl className="av-account-details">
            {rows.map(row => <div key={row.label} className="av-account-detail" data-tone={row.tone}>
                <span className="av-detail-icon"><ConceptIcon name={row.icon} /></span>
                <div><dt>{row.label}</dt><dd>{row.value}</dd></div>
            </div>)}
        </dl>
    );
}

