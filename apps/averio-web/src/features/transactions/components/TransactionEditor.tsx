import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import type {
    TransactionDto,
    TransactionInputDto,
    TransactionKind,
    TransactionLabelInputDto,
    TransactionLabelScope,
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

interface EditorItem {
    key: string;
    description: string;
    categoryName: string;
    amount: string;
}

let nextItemKey = 1;

function itemKey() {
    return `transaction-item-${nextItemKey++}`;
}

function todayIso() {
    const now = new Date();
    const offset = now.getTimezoneOffset();
    return new Date(now.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

function normalizeAmount(value: string) {
    return value.trim().replace(',', '.');
}

function amountToScaled(value: string): bigint | null {
    const normalized = normalizeAmount(value);
    if (!/^\d+(?:\.\d{0,4})?$/.test(normalized)) return null;

    const [whole, fraction = ''] = normalized.split('.');
    return BigInt(whole) * 10_000n
        + BigInt((fraction + '0000').slice(0, 4));
}

function scaledToAmount(value: bigint) {
    const whole = value / 10_000n;
    const fraction = (value % 10_000n).toString().padStart(4, '0').replace(/0+$/, '');
    return fraction ? `${whole}.${fraction}` : whole.toString();
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
        && (!transaction || account.currency === transaction.currency)
        && (
            account.scope === 'Household'
            || account.owners.some(owner =>
                owner.householdMemberId === owners.data?.currentMemberId)
        )), [accounts.data, owners.data?.currentMemberId, transaction]);

    const firstAccountId = transaction?.accountId ?? writableAccounts[0]?.id ?? '';
    const [accountId, setAccountId] = useState(firstAccountId);
    const [kind, setKind] = useState<TransactionKind>(transaction?.kind ?? 'Expense');
    const [visibility, setVisibility] = useState<TransactionVisibility>(
        transaction?.visibility ?? 'Household',
    );
    const [merchant, setMerchant] = useState(transaction?.merchant ?? '');
    const [note, setNote] = useState(transaction?.note ?? '');
    const [occurredOn, setOccurredOn] = useState(transaction?.occurredOn ?? todayIso());
    const [items, setItems] = useState<EditorItem[]>(() =>
        transaction?.items.length
            ? transaction.items
                .sort((a, b) => a.position - b.position)
                .map(item => ({
                    key: item.id || itemKey(),
                    description: item.description,
                    categoryName: item.categoryName ?? '',
                    amount: item.amount,
                }))
            : [{
                key: itemKey(),
                description: '',
                categoryName: '',
                amount: '',
            }],
    );
    const [labels, setLabels] = useState<TransactionLabelInputDto[]>(
        () => transaction?.labels.map(label => ({
            name: label.name,
            scope: label.scope,
        })) ?? [],
    );
    const [labelName, setLabelName] = useState('');
    const [labelScope, setLabelScope] = useState<TransactionLabelScope>('Household');
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
    const language = i18n.resolvedLanguage ?? i18n.language;

    const total = useMemo(() => {
        let sum = 0n;
        for (const item of items) {
            const value = amountToScaled(item.amount);
            if (value !== null) sum += value;
        }
        return scaledToAmount(sum);
    }, [items]);

    function updateItem(key: string, patch: Partial<EditorItem>) {
        setItems(current => current.map(item =>
            item.key === key ? { ...item, ...patch } : item));
    }

    function addItem() {
        setItems(current => [...current, {
            key: itemKey(),
            description: '',
            categoryName: '',
            amount: '',
        }]);
    }

    function removeItem(key: string) {
        setItems(current => current.length === 1
            ? current
            : current.filter(item => item.key !== key));
    }

    function addLabel() {
        const name = labelName.trim();
        if (!name) return;

        const duplicate = labels.some(label =>
            label.scope === labelScope
            && label.name.toLocaleLowerCase() === name.toLocaleLowerCase());

        if (duplicate) {
            setError(t('transactionManagement.labelDuplicate'));
            return;
        }

        if (labels.length >= 20) {
            setError(t('transactionManagement.labelLimit'));
            return;
        }

        setLabels(current => [...current, { name, scope: labelScope }]);
        setLabelName('');
        setError('');
    }

    async function submit(event: React.FormEvent) {
        event.preventDefault();
        setError('');

        if (!accountId || !occurredOn || items.length === 0) {
            setError(t('transactionManagement.formInvalid'));
            return;
        }

        const mappedItems = items.map(item => ({
            description: item.description.trim(),
            categoryName: item.categoryName.trim() || null,
            amount: normalizeAmount(item.amount),
        }));

        const invalidItem = mappedItems.some(item =>
            !item.description
            || !/^\d+(?:\.\d{1,4})?$/.test(item.amount)
            || Number(item.amount) <= 0);

        if (invalidItem || amountToScaled(total) === 0n) {
            setError(t('transactionManagement.itemsInvalid'));
            return;
        }

        try {
            await onSave({
                accountId,
                kind,
                visibility: effectiveVisibility,
                merchant: merchant.trim() || null,
                note: note.trim() || null,
                occurredOn,
                items: mappedItems,
                labels,
            }, transaction?.updatedAt);
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : t('errors.genericDescription'));
        }
    }

    return (
        <div className="av-tx-modal-backdrop" role="presentation" onMouseDown={(event) => {
            if (event.target === event.currentTarget && !busy) onCancel();
        }}>
            <section className="av-tx-modal av-tx-modal-wide" role="dialog" aria-modal="true"
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
                            <label key={value}
                                className={`${value.toLowerCase()} ${kind === value ? 'active' : ''}`}>
                                <input type="radio" name="kind" value={value}
                                    checked={kind === value} onChange={() => setKind(value)} />
                                <span>{t(value === 'Income'
                                    ? 'transactionManagement.income'
                                    : 'transactionManagement.expense')}</span>
                            </label>
                        ))}
                    </fieldset>

                    <div className="av-tx-form-grid">
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
                        <label>
                            <span>{t('transactionManagement.date')}</span>
                            <input type="date" value={occurredOn} disabled={busy}
                                onChange={event => setOccurredOn(event.target.value)} required />
                        </label>
                    </div>

                    <label>
                        <span>{t('transactionManagement.merchant')}</span>
                        <input maxLength={160} value={merchant} disabled={busy}
                            onChange={event => setMerchant(event.target.value)}
                            placeholder={t('transactionManagement.merchantPlaceholder')} />
                    </label>

                    <section className="av-tx-items" aria-labelledby="transaction-items-title">
                        <div className="av-tx-section-head">
                            <div>
                                <h3 id="transaction-items-title">{t('transactionManagement.items')}</h3>
                                <p>{t('transactionManagement.itemsHelp')}</p>
                            </div>
                            <button type="button" className="av-btn av-compact"
                                onClick={addItem} disabled={busy || items.length >= 100}>
                                + {t('transactionManagement.addItem')}
                            </button>
                        </div>

                        <div className="av-tx-item-list">
                            {items.map((item, index) => (
                                <div className="av-tx-item-row" key={item.key}>
                                    <span className="av-tx-item-number">{index + 1}</span>
                                    <label className="av-tx-item-description">
                                        <span>{t('transactionManagement.itemDescription')}</span>
                                        <input value={item.description} maxLength={160} disabled={busy}
                                            onChange={event => updateItem(item.key, { description: event.target.value })}
                                            placeholder={t('transactionManagement.itemDescriptionPlaceholder')}
                                            required />
                                    </label>
                                    <label>
                                        <span>{t('transactionManagement.category')}</span>
                                        <input value={item.categoryName} maxLength={80} disabled={busy}
                                            onChange={event => updateItem(item.key, { categoryName: event.target.value })}
                                            placeholder={t('transactionManagement.categoryPlaceholder')} />
                                    </label>
                                    <label>
                                        <span>{t('transactionManagement.amount')}</span>
                                        <div className="av-tx-amount-input">
                                            <input inputMode="decimal" value={item.amount} disabled={busy}
                                                onChange={event => updateItem(item.key, { amount: event.target.value })}
                                                placeholder="0.00" required />
                                            <strong>{currency}</strong>
                                        </div>
                                    </label>
                                    <button type="button" className="av-icon-btn av-tx-remove-item"
                                        disabled={busy || items.length === 1}
                                        onClick={() => removeItem(item.key)}
                                        aria-label={t('transactionManagement.removeItem')}>×</button>
                                </div>
                            ))}
                        </div>

                        <p className="av-subtle av-tx-category-help">
                            {t('transactionManagement.categorySnapshotHelp')}
                        </p>

                        <div className={`av-tx-total ${kind.toLowerCase()}`}>
                            <span>{t('transactionManagement.total')}</span>
                            <strong>
                                {kind === 'Income' ? '+' : '−'}
                                {currency
                                    ? formatDecimalAmount(total, currency, language)
                                    : total}
                            </strong>
                        </div>
                    </section>

                    <section className="av-tx-labels" aria-labelledby="transaction-labels-title">
                        <div className="av-tx-section-head">
                            <div>
                                <h3 id="transaction-labels-title">{t('transactionManagement.labels')}</h3>
                                <p>{t('transactionManagement.labelsHelp')}</p>
                            </div>
                        </div>
                        <div className="av-tx-label-entry">
                            <input value={labelName} maxLength={40} disabled={busy}
                                onChange={event => setLabelName(event.target.value)}
                                onKeyDown={event => {
                                    if (event.key === 'Enter') {
                                        event.preventDefault();
                                        addLabel();
                                    }
                                }}
                                placeholder={t('transactionManagement.labelPlaceholder')} />
                            <select className="av-select" value={labelScope} disabled={busy}
                                onChange={event => setLabelScope(event.target.value as TransactionLabelScope)}>
                                <option value="Household">{t('transactionManagement.householdLabel')}</option>
                                <option value="Account">{t('transactionManagement.accountLabel')}</option>
                            </select>
                            <button type="button" className="av-btn av-compact" onClick={addLabel}
                                disabled={busy || !labelName.trim()}>
                                {t('transactionManagement.addLabel')}
                            </button>
                        </div>
                        {labels.length > 0 && (
                            <div className="av-tx-label-list">
                                {labels.map((label, index) => (
                                    <span key={`${label.scope}-${label.name}-${index}`}
                                        className={`av-tx-label-chip ${label.scope.toLowerCase()}`}>
                                        <span aria-hidden="true">{label.scope === 'Household' ? '⌂' : '▣'}</span>
                                        <span>{label.name}</span>
                                        <button type="button" disabled={busy}
                                            onClick={() => setLabels(current =>
                                                current.filter((_, labelIndex) => labelIndex !== index))}
                                            aria-label={t('transactionManagement.removeLabel', { name: label.name })}>
                                            ×
                                        </button>
                                    </span>
                                ))}
                            </div>
                        )}
                    </section>

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
                        <p className="av-subtle">{t('transactionManagement.currencyLocked')}</p>
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
                        <button type="submit" className="av-btn av-primary"
                            disabled={busy || writableAccounts.length === 0}>
                            {t(busy ? 'transactionManagement.saving' : 'transactionManagement.save')}
                        </button>
                    </footer>
                </form>
            </section>
        </div>
    );
}
