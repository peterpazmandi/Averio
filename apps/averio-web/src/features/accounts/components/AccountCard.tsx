import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../../shared/api/ApiError';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import {
    ConceptIcon,
    type ConceptIconName,
} from '../../../shared/components/concept/ConceptIcon';
import type { AccountData } from '../api/accountsApi';
import { AccountOwnershipSummary } from './AccountOwnershipSummary';

interface Props {
    account: AccountData;
    currentMemberId?: string;
    busy: boolean;
    onArchive: () => Promise<unknown>;
    onRestore: () => Promise<unknown>;
}

export function AccountCard({
    account,
    currentMemberId,
    busy,
    onArchive,
    onRestore,
}: Props) {
    const { t, i18n } = useTranslation();
    const [confirm, setConfirm] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const archived = account.status === 'Archived';
    const canModify = !account.cutoverAtUtc && !!currentMemberId && (
        account.scope === 'Household'
        || account.owners.some(owner =>
            owner.householdMemberId === currentMemberId)
    );
    const language = i18n.resolvedLanguage ?? i18n.language;
    const balance = formatDecimalAmount(
        account.currentBalanceAmount,
        account.currency,
        language,
    );
    const openingBalance = formatDecimalAmount(
        account.balanceAnchorAmount,
        account.currency,
        language,
    );
    const date = new Intl.DateTimeFormat(language, {
        dateStyle: 'medium',
        timeZone: 'UTC',
    }).format(new Date(account.balanceAnchorDate + 'T00:00:00Z'));

    async function changeStatus() {
        setError(null);
        try {
            await (archived ? onRestore() : onArchive());
            setConfirm(false);
        } catch (cause) {
            setError(
                cause instanceof ApiError
                    ? cause.message
                    : t('errors.genericDescription'),
            );
        }
    }

    const types: Record<string, {
        key: string;
        icon: ConceptIconName;
        tone: string;
    }> = {
        Checking: { key: 'checking', icon: 'bank', tone: 'blue' },
        Savings: { key: 'savings', icon: 'savings', tone: 'teal' },
        Cash: { key: 'cash', icon: 'cash', tone: 'amber' },
        CreditCard: { key: 'creditCard', icon: 'accounts', tone: 'purple' },
    };
    const accountType = types[account.type];

    return (
        <article className="av-card av-account-card" aria-label={account.name}>
            <div className="flex flex-wrap items-start justify-between gap-2">
                <h3 className="break-words font-semibold">{account.name}</h3>
                <span className={
                    'av-badge av-account-status'
                    + (archived ? '' : ' av-account-status-active')
                }>
                    {t(archived
                        ? 'accountManagement.archived'
                        : 'accountManagement.active')}
                </span>
            </div>

            <div className="av-account-type-row">
                <span className="av-account-type" data-tone={accountType.tone}>
                    <ConceptIcon name={accountType.icon} />
                    {t('onboarding.account.' + accountType.key)}
                </span>
                <span className="av-account-currency">{account.currency}</span>
            </div>

            <div className="av-account-balance-block">
                <span className="av-label">
                    {t('accountManagement.currentBalance')}
                </span>
                <p className={
                    'av-kpi break-words'
                    + (balance.length > 20 ? ' av-long-amount' : '')
                }>
                    {balance}
                </p>
                <p className="av-subtle">
                    {t('accountManagement.openingBalance')}: {openingBalance}
                    {' · '}
                    <time dateTime={account.balanceAnchorDate}>{date}</time>
                </p>
            </div>

            <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">{t('accountManagement.trackedBalanceHelp')}</p>

            <AccountOwnershipSummary account={account} />

            {canModify && (
                <div className="av-account-actions">
                    {!archived && (
                        <Link
                            className="av-btn"
                            to="/app/accounts/$accountId/edit"
                            params={{ accountId: account.id }}
                            disabled={busy}
                        >
                            {t('accountManagement.edit')}
                        </Link>
                    )}
                    <button
                        type="button"
                        className="av-btn"
                        disabled={busy}
                        onClick={() => setConfirm(true)}
                    >
                        {t(archived
                            ? 'accountManagement.restore'
                            : 'accountManagement.archive')}
                    </button>
                </div>
            )}

            {confirm && (
                <div
                    className="space-y-3"
                    role="group"
                    aria-label={t('accountManagement.confirm')}
                >
                    <p>
                        {t(archived
                            ? 'accountManagement.restoreHelp'
                            : 'accountManagement.archiveHelp')}
                    </p>
                    <div className="flex flex-wrap gap-2">
                        <button
                            type="button"
                            className="av-btn av-primary"
                            disabled={busy}
                            onClick={() => {
                                void changeStatus();
                            }}
                        >
                            {t('accountManagement.confirm')}
                        </button>
                        <button
                            type="button"
                            className="av-btn"
                            disabled={busy}
                            onClick={() => setConfirm(false)}
                        >
                            {t('accountManagement.cancel')}
                        </button>
                    </div>
                </div>
            )}

            {error && <p role="alert" className="av-error">{error}</p>}
        </article>
    );
}

