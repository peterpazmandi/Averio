import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import type {
    TransactionDto,
    TransactionInputDto,
    TransactionKind,
    TransactionVisibility,
} from '../api/transactionApi';

interface Props {
    userId: string;
    householdId: string;
    transaction: TransactionDto | null;
    onCancel: () => void;
    onSave: (request: TransactionInputDto, expectedUpdatedAt?: string) => Promise<void>;
    onVoid?: () => Promise<void>;
    busy: boolean;
}

function todayIso() {
    const now = new Date();
    const offset = now.getTimezoneOffset();
    return new Date(now.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

export function TransactionEditor({
    userId,
    householdId,
    transaction,
    onCancel,
    onSave,
    onVoid,
    busy,
}: Props) {
    const { t, i18n } = useTranslation();
    const accounts = useQuery(accountsQueries.list(userId, householdId, false));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const writableAccounts = useMemo(() => (accounts.data ?? []).filter(account =>
        account.status === 'Active'
        && (
            account.scope === 'Household'
            || account.owners.some(owner =>
                owner.householdMemberId === owners.data?.currentMemberId)
        )), [accounts.data, owners.data?.currentMemberId]);

    const firstAccountId = transaction?.accountId ?? writableAccounts[0]?.id ?? '';
    const [accountId, setAccountId] = useState(firstAccountId);
    const [kind, setKind] = useState<TransactionKind>(transaction?.kind ?? 'Expense');
    const [visibility, setVisibility] = useState<TransactionVisibility>(
        transaction?.visibility ?? 'Household',
    );
    const [amount, setAmount] = useState(transaction?.amount ?? '');
    const [description, setDescription] = useState(transaction?.description ?? '');
    const [note, setNote] = useState(transaction?.note ?? '');
    const [occurredOn, setOccurredOn] = useState(transaction?.occurredOn ?? todayIso());
    const [error, setError] = useState('');

    useEffect(() => {
        if (!accountId && writableAccounts[0]) setAccountId(writableAccounts[0].id);
    }, [accountId, writableAccounts]);

    const account = writableAccounts.find(item => item.id === accountId);
    const forcedVisibility: TransactionVisibility | undefined =
        account?.visibility === 'Private'
            ? 'Private'
            : account?.scope === 'Household'
                ? 'Household'
                : undefined;
    const effectiveVisibility = forcedVisibility ?? visibility;
    const currency = account?.currency ?? transaction?.currency ?? '';

    async function submit(event: React.FormEvent) {
        event.preventDefault();
        setError('');
        const normalized = amount.trim().replace(',', '.');
        if (!accountId || !/^\d+(?:\.\d{1,4})?$/.test(normalized)
            || Number(normalized) <= 0 || !description.trim() || !occurredOn) {
            setError(t('validation.invalidAmount'));
            return;
        }

        try {
            await onSave({
                accountId,
                kind,
                visibility: effectiveVisibility,
                amount: normalized,
                description: description.trim(),
                note: note.trim() || null,
                occurredOn,
            }, transaction?.updatedAt);
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : t('errors.genericDescription'));
        }
    }

    return (
        <div className="av-tx-modal-backdrop" role="presentation" onMouseDown={(event) => {
            if (event.target === event.currentTarget && !busy) onCancel();
        }}>
            <section className="av-tx-modal" role="dialog" aria-modal="true"
                aria-labelledby="transaction-editor-title">
                <header className="av-tx-modal-head">
                    <div>
                        <span className="av-eyebrow">{t('transactionManagement.type')}</span>
                        <h2 id="transaction-editor-title">
                            {t(transaction ? 'transactionManagement.edit' : 'transactionManagement.add')}
                        </h2>
                    </div>
                    <button type="button" className="av-icon-btn" onClick={onCancel}
                        disabled={busy} aria-label={t('common.actions.close')}>×</button>
                </header>

                <form className="av-tx-form" onSubmit={submit}>
                    <fieldset className="av-tx-kind" disabled={busy}>
                        {(['Income', 'Expense'] as const).map(value => (
                            <label key={value} className={kind === value ? 'active' : ''}>
                                <input type="radio" name="kind" value={value}
                                    checked={kind === value} onChange={() => setKind(value)} />
                                <span>{t(value === 'Income'
                                    ? 'transactionManagement.income'
                                    : 'transactionManagement.expense')}</span>
                            </label>
                        ))}
                    </fieldset>

                    <label>
                        <span>{t('transactionManagement.account')}</span>
                        <select className="av-select" value={accountId} disabled={busy}
                            onChange={event => setAccountId(event.target.value)} required>
                            <option value="" disabled>{t('transactionManagement.account')}</option>
                            {writableAccounts.map(item => (
                                <option key={item.id} value={item.id}>
                                    {item.name} · {item.currency}
                                </option>
                            ))}
                        </select>
                    </label>

                    <div className="av-tx-form-grid">
                        <label>
                            <span>{t('transactionManagement.amount')}</span>
                            <div className="av-tx-amount-input">
                                <input inputMode="decimal" value={amount} disabled={busy}
                                    onChange={event => setAmount(event.target.value)}
                                    placeholder="0.00" required />
                                <strong>{currency}</strong>
                            </div>
                        </label>
                        <label>
                            <span>{t('transactionManagement.date')}</span>
                            <input type="date" value={occurredOn} disabled={busy}
                                onChange={event => setOccurredOn(event.target.value)} required />
                        </label>
                    </div>

                    <label>
                        <span>{t('transactionManagement.descriptionLabel')}</span>
                        <input maxLength={160} value={description} disabled={busy}
                            onChange={event => setDescription(event.target.value)}
                            placeholder={t('transactionManagement.descriptionPlaceholder')} required />
                    </label>

                    <label>
                        <span>{t('transactionManagement.note')}</span>
                        <textarea maxLength={500} value={note} disabled={busy}
                            onChange={event => setNote(event.target.value)}
                            placeholder={t('transactionManagement.notePlaceholder')} />
                    </label>

                    <label>
                        <span>{t('transactionManagement.visibility')}</span>
                        <select className="av-select" value={effectiveVisibility}
                            disabled={busy || forcedVisibility !== undefined}
                            onChange={event => setVisibility(event.target.value as TransactionVisibility)}>
                            <option value="Household">{t('transactionManagement.household')}</option>
                            <option value="Private">{t('transactionManagement.private')}</option>
                        </select>
                    </label>

                    {effectiveVisibility === 'Private' && (
                        <p className="av-tx-private-help">{t('transactionManagement.privateHelp')}</p>
                    )}
                    {currency && (
                        <p className="av-subtle">
                            {t('transactionManagement.currencyLocked')}
                            {/^\\d+(?:[.,]\\d{0,4})?$/.test(amount) && amount
                                ? ` · ${formatDecimalAmount(
                                    amount.replace(',', '.').replace(/\\.$/, ''),
                                    currency,
                                    i18n.resolvedLanguage ?? i18n.language,
                                )}`
                                : ''}
                        </p>
                    )}
                    {error && <p className="av-error" role="alert">{error}</p>}

                    <footer className="av-tx-modal-actions">
                        {transaction && onVoid && transaction.status === 'Posted' && (
                            <button type="button" className="av-btn av-danger" disabled={busy}
                                onClick={() => {
                                    if (window.confirm(t('transactionManagement.voidConfirm'))) {
                                        void onVoid().catch(reason => setError(
                                            reason instanceof Error ? reason.message : t('errors.genericDescription'),
                                        ));
                                    }
                                }}>
                                {t('transactionManagement.void')}
                            </button>
                        )}
                        <span className="av-tx-action-spacer" />
                        <button type="button" className="av-btn" onClick={onCancel} disabled={busy}>
                            {t('common.actions.cancel')}
                        </button>
                        <button type="submit" className="av-btn av-primary" disabled={busy || writableAccounts.length === 0}>
                            {t(busy ? 'transactionManagement.saving' : 'transactionManagement.save')}
                        </button>
                    </footer>
                </form>
            </section>
        </div>
    );
}
