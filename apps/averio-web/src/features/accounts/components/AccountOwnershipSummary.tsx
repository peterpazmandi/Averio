import { useTranslation } from 'react-i18next';
import type { AccountResponse } from '../../../shared/api/generated';

export function AccountOwnershipSummary({ account }: { account: AccountResponse }) {
    const { t } = useTranslation();
    const holders = account.owners.map((owner, index) =>
        owner.displayName || t('accountOwnership.member', { number: index + 1 }),
    ).join(', ');

    return (
        <dl className="mt-2 space-y-1 text-sm text-slate-600 dark:text-slate-400">
            <div>
                <dt className="inline font-medium">{t('accountOwnership.scope')}: </dt>
                <dd className="inline">{t(account.scope === 'Household'
                    ? 'accountOwnership.household'
                    : 'accountOwnership.personal')}</dd>
            </div>
            <div>
                <dt className="inline font-medium">{t('accountOwnership.holders')}: </dt>
                <dd className="inline">{holders || t('accountOwnership.noHolders')}</dd>
            </div>
            <div>
                <dt className="inline font-medium">{t('accountOwnership.visibility')}: </dt>
                <dd className="inline">{t(account.visibility === 'Private'
                    ? 'accountOwnership.private'
                    : 'accountOwnership.visible')}</dd>
            </div>
            <div>
                <dt className="inline font-medium">{t('accountOwnership.reports')}: </dt>
                <dd className="inline">{t(account.includeInHouseholdReports
                    ? 'accountOwnership.included'
                    : 'accountOwnership.excluded')}</dd>
            </div>
        </dl>
    );
}
