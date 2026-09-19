import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AccountResponse } from '../../../shared/api/generated';
import { ApiError } from '../../../shared/api/ApiError';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { AccountOwnershipSummary } from './AccountOwnershipSummary';

interface Props {
    account: AccountResponse;
    currentMemberId?: string;
    busy: boolean;
    onEdit: () => void;
    onArchive: () => Promise<unknown>;
    onRestore: () => Promise<unknown>;
}

export function AccountCard({ account, currentMemberId, busy, onEdit, onArchive, onRestore }: Props) {
    const { t, i18n } = useTranslation();
    const [confirm, setConfirm] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const archived = account.status === 'Archived';
    const canModify = !!currentMemberId && (account.scope === 'Household'
        || account.owners.some(owner => owner.householdMemberId === currentMemberId));
    const language = i18n.resolvedLanguage ?? i18n.language;
    const balance = formatDecimalAmount(account.balanceAnchorAmount, account.currency, language);
    const date = new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeZone: 'UTC' })
        .format(new Date(account.balanceAnchorDate + 'T00:00:00Z'));
    async function changeStatus() {
        setError(null);
        try { await (archived ? onRestore() : onArchive()); setConfirm(false); }
        catch (cause) { setError(cause instanceof ApiError ? cause.message : t('errors.genericDescription')); }
    }
    const typeKeys: Record<string, string> = { Checking: 'checking', Savings: 'savings', Cash: 'cash', CreditCard: 'creditCard' };
    return (
        <article className="av-card min-w-0 space-y-3" aria-label={account.name}>
            <div className="flex flex-wrap items-start justify-between gap-2">
                <h3 className="break-words font-semibold">{account.name}</h3>
                <span className="av-badge">{t(archived ? 'accountManagement.archived' : 'accountManagement.active')}</span>
            </div>
            <p className="av-subtle">{t('onboarding.account.' + typeKeys[account.type])} · {account.currency}</p>
            <p className={'av-kpi break-words' + (balance.length > 20 ? ' av-long-amount' : '')}>{balance}</p>
            <p className="av-subtle">{t('app.accounts.recordedBalance')} · <time dateTime={account.balanceAnchorDate}>{date}</time></p>
            <AccountOwnershipSummary account={account} />
            {canModify && <div className="flex flex-wrap gap-2">
                {!archived && <button type="button" className="av-btn" disabled={busy} onClick={onEdit}>{t('accountManagement.edit')}</button>}
                <button type="button" className="av-btn" disabled={busy} onClick={() => setConfirm(true)}>
                    {t(archived ? 'accountManagement.restore' : 'accountManagement.archive')}
                </button>
            </div>}
            {confirm && <div className="space-y-3" role="group" aria-label={t('accountManagement.confirm')}>
                <p>{t(archived ? 'accountManagement.restoreHelp' : 'accountManagement.archiveHelp')}</p>
                <div className="flex flex-wrap gap-2">
                    <button type="button" className="av-btn av-primary" disabled={busy} onClick={() => { void changeStatus(); }}>{t('accountManagement.confirm')}</button>
                    <button type="button" className="av-btn" disabled={busy} onClick={() => setConfirm(false)}>{t('accountManagement.cancel')}</button>
                </div>
            </div>}
            {error && <p role="alert" className="av-error">{error}</p>}
        </article>
    );
}
