import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { createLabel } from '../api/transactionApi';
import { transactionQueries, transactionQueryKeys } from '../api/transactionQueries';
import { CategoryPicker } from './CategoryPicker';
import type {
    LabelScope,
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

interface EditorItem {
    key: string;
    description: string;
    categoryId: string;
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
    const queryClient = useQueryClient();
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
            ? [...transaction.items]
                .sort((a, b) => a.position - b.position)
                .map(item => ({
                    key: item.id || itemKey(),
                    description: item.description,
                    categoryId: item.categoryId,
                    amount: item.amount,
                }))
            : [{
                key: itemKey(),
                description: '',
                categoryId: '',
                amount: '',
            }],
    );
    const [selectedLabelIds, setSelectedLabelIds] = useState<string[]>(
        () => transaction?.labels.map(label => label.id) ?? [],
    );
    const [newLabelName, setNewLabelName] = useState('');
    const [newLabelScope, setNewLabelScope] = useState<LabelScope>('Household');
    const [newLabelOpen, setNewLabelOpen] = useState(false);
    const [error, setError] = useState('');

    const resolvedAccountId = accountId || writableAccounts[0]?.id || '';
    const account = writableAccounts.find(item => item.id === resolvedAccountId);
    const forcedVisibility: TransactionVisibility | undefined =
        account?.visibility === 'Private'
            ? 'Private'
            : account?.scope === 'Household'
                ? 'Household'
                : undefined;
    const effectiveVisibility = forcedVisibility ?? visibility;
    const currency = account?.currency ?? transaction?.currency ?? '';
    const language = i18n.resolvedLanguage ?? i18n.language;

    const categories = useQuery(transactionQueries.categories(userId, householdId, kind));
    const labels = useQuery(transactionQueries.labels(
        userId,
        householdId,
        resolvedAccountId || undefined,
    ));

    const categoriesById = useMemo(
        () => new Map((categories.data ?? []).map(category => [category.id, category])),
        [categories.data],
    );
    const uncategorizedId = useMemo(
        () => (categories.data ?? []).find(
            category => category.systemKey === 'uncategorized',
        )?.id ?? '',
        [categories.data],
    );

    const resolvedItems = useMemo(
        () => items.map(item => {
            const selected = categoriesById.get(item.categoryId);
            const original = transaction?.items.find(old => old.id === item.key);
            const retained = original?.categoryId === item.categoryId && transaction?.kind === kind;
            if (uncategorizedId && (!item.categoryId || (!selected && !retained))) {
                return { ...item, categoryId: uncategorizedId };
            }
            return item;
        }),
        [categoriesById, items, uncategorizedId, transaction, kind],
    );

    const resolvedLabelIds = useMemo(() => {
        if (!labels.data) return selectedLabelIds;
        const available = new Set(labels.data.map(label => label.id));
        return selectedLabelIds.filter(id => available.has(id));
    }, [labels.data, selectedLabelIds]);

    const createLabelMutation = useMutation({
        mutationFn: () => createLabel(householdId, {
            name: newLabelName.trim(),
            scope: newLabelScope,
            accountId: newLabelScope === 'Account' ? resolvedAccountId : null,
        }),
        onSuccess: async result => {
            setSelectedLabelIds(current =>
                current.includes(result.id) ? current : [...current, result.id]);
            setNewLabelName('');
            setNewLabelScope('Household');
            setNewLabelOpen(false);
            await queryClient.invalidateQueries({
                queryKey: transactionQueryKeys.labels(
                    userId,
                    householdId,
                    resolvedAccountId || undefined,
                ),
            });
        },
    });

    const total = useMemo(() => {
        let sum = 0n;
        for (const item of resolvedItems) {
            const value = amountToScaled(item.amount);
            if (value !== null) sum += value;
        }
        return scaledToAmount(sum);
    }, [resolvedItems]);

    function updateItem(key: string, patch: Partial<EditorItem>) {
        setItems(current => current.map(item =>
            item.key === key ? { ...item, ...patch } : item));
    }

    function addItem() {
        setItems(current => [...current, {
            key: itemKey(),
            description: '',
            categoryId: uncategorizedId,
            amount: '',
        }]);
    }

    function removeItem(key: string) {
        setItems(current => current.length === 1
            ? current
            : current.filter(item => item.key !== key));
    }

    function toggleLabel(id: string) {
        setSelectedLabelIds(current => current.includes(id)
            ? current.filter(labelId => labelId !== id)
            : [...current, id]);
    }

    async function submit(event: React.FormEvent) {
        event.preventDefault();
        setError('');

        if (!resolvedAccountId || !occurredOn || resolvedItems.length === 0) {
            setError(t('transactionManagement.formInvalid'));
            return;
        }

        const mappedItems = resolvedItems.map(item => ({
            existingItemId: transaction?.items.some(old => old.id === item.key) ? item.key : null,
            description: item.description.trim(),
            categoryId: item.categoryId,
            amount: normalizeAmount(item.amount),
        }));

        const invalidItem = mappedItems.some(item =>
            !item.description
            || !item.categoryId
            || !/^\d+(?:\.\d{1,4})?$/.test(item.amount)
            || Number(item.amount) <= 0);

        if (invalidItem || amountToScaled(total) === 0n) {
            setError(t('transactionManagement.itemsInvalid'));
            return;
        }

        try {
            await onSave({
                accountId: resolvedAccountId,
                kind,
                visibility: effectiveVisibility,
                merchant: merchant.trim() || null,
                note: note.trim() || null,
                occurredOn,
                items: mappedItems,
                labelIds: resolvedLabelIds,
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
                    <section className="av-tx-form-section av-tx-section-basics">
                        <div className="av-tx-form-section-head">
                            <span className="av-tx-section-number">1</span>
                            <div>
                                <h3>{t('transactionManagement.basics')}</h3>
                                <p>{t('transactionManagement.basicsHelp')}</p>
                            </div>
                        </div>

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
                                <select className="av-select" value={resolvedAccountId} disabled={busy}
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
                    </section>

                    <section className="av-tx-form-section av-tx-section-labels"
                        aria-labelledby="transaction-labels-title">
                        <div className="av-tx-form-section-head">
                            <span className="av-tx-section-number">2</span>
                            <div>
                                <h3 id="transaction-labels-title">
                                    {t('transactionManagement.labels')}
                                </h3>
                                <p>{t('transactionManagement.labelsHelp')}</p>
                            </div>
                        </div>

                        {labels.isPending && (
                            <p className="av-subtle">{t('common.status.loading')}</p>
                        )}

                        {labels.isSuccess
                            && labels.data.length === 0
                            && !newLabelOpen && (
                            <p className="av-subtle">
                                {t('transactionManagement.noLabelsYet')}
                            </p>
                        )}

                        <div className="av-tx-label-catalog">
                            {(labels.data ?? []).map(label => {
                                const selected = resolvedLabelIds.includes(
                                    label.id,
                                );

                                return (
                                    <button
                                        type="button"
                                        key={label.id}
                                        className={
                                            `av-tx-label-choice ${label.scope.toLowerCase()} `
                                            + (selected ? 'selected' : '')
                                        }
                                        aria-pressed={selected}
                                        disabled={busy}
                                        onClick={() => toggleLabel(label.id)}
                                    >
                                        <span className="av-tx-label-check">
                                            {selected ? '✓' : ''}
                                        </span>
                                        <span aria-hidden="true">
                                            {label.scope === 'Household'
                                                ? '⌂'
                                                : '▣'}
                                        </span>
                                        <span>{label.name}</span>
                                    </button>
                                );
                            })}

                            {!newLabelOpen && (
                                <button
                                    type="button"
                                    className="av-tx-label-add"
                                    disabled={
                                        busy
                                        || !resolvedAccountId
                                        || labels.isPending
                                    }
                                    aria-label={t(
                                        'transactionManagement.addNewLabel',
                                    )}
                                    title={t(
                                        'transactionManagement.addNewLabel',
                                    )}
                                    onClick={() => {
                                        createLabelMutation.reset();
                                        setNewLabelOpen(true);
                                    }}
                                >
                                    <ConceptIcon name="plus" />
                                </button>
                            )}
                        </div>

                        {newLabelOpen && (
                            <div className="av-tx-label-create">
                                <label>
                                    <span>
                                        {t('transactionManagement.newLabel')}
                                    </span>
                                    <input
                                        value={newLabelName}
                                        maxLength={40}
                                        autoFocus
                                        disabled={
                                            busy
                                            || createLabelMutation.isPending
                                        }
                                        onChange={event =>
                                            setNewLabelName(
                                                event.target.value,
                                            )}
                                        placeholder={t(
                                            'transactionManagement.labelPlaceholder',
                                        )}
                                    />
                                </label>

                                <label>
                                    <span>
                                        {t('transactionManagement.labelScope')}
                                    </span>
                                    <select
                                        className="av-select"
                                        value={newLabelScope}
                                        disabled={
                                            busy
                                            || createLabelMutation.isPending
                                        }
                                        onChange={event =>
                                            setNewLabelScope(
                                                event.target.value as LabelScope,
                                            )}
                                    >
                                        <option value="Household">
                                            {t(
                                                'transactionManagement.householdLabel',
                                            )}
                                        </option>
                                        <option value="Account">
                                            {t(
                                                'transactionManagement.accountLabel',
                                            )}
                                        </option>
                                    </select>
                                </label>

                                <div className="av-tx-label-create-actions">
                                    <button
                                        type="button"
                                        className="av-btn av-compact"
                                        disabled={
                                            busy
                                            || createLabelMutation.isPending
                                            || !newLabelName.trim()
                                            || !resolvedAccountId
                                        }
                                        onClick={() =>
                                            createLabelMutation.mutate()}
                                    >
                                        + {t(createLabelMutation.isPending
                                            ? 'transactionManagement.creatingLabel'
                                            : 'transactionManagement.createLabel')}
                                    </button>

                                    <button
                                        type="button"
                                        className="av-btn av-compact"
                                        disabled={
                                            busy
                                            || createLabelMutation.isPending
                                        }
                                        onClick={() => {
                                            createLabelMutation.reset();
                                            setNewLabelName('');
                                            setNewLabelScope('Household');
                                            setNewLabelOpen(false);
                                        }}
                                    >
                                        {t('common.actions.cancel')}
                                    </button>
                                </div>
                            </div>
                        )}

                        {newLabelOpen && createLabelMutation.isError && (
                            <p className="av-error" role="alert">
                                {createLabelMutation.error instanceof Error
                                    ? createLabelMutation.error.message
                                    : t('errors.genericDescription')}
                            </p>
                        )}
                    </section>

                    <section className="av-tx-form-section av-tx-section-items"
                        aria-labelledby="transaction-items-title">
                        <div className="av-tx-form-section-head">
                            <span className="av-tx-section-number">3</span>
                            <div>
                                <h3 id="transaction-items-title">
                                    {t('transactionManagement.items')}
                                </h3>
                                <p>{t('transactionManagement.itemsHelp')}</p>
                            </div>
                            <button type="button" className="av-btn av-compact av-tx-section-action"
                                onClick={addItem} disabled={busy || resolvedItems.length >= 100}>
                                + {t('transactionManagement.addItem')}
                            </button>
                        </div>

                        <div className="av-tx-item-list">
                            {resolvedItems.map((item, index) => (
                                <div className="av-tx-item-row" key={item.key}>
                                    <span className="av-tx-item-number">{index + 1}</span>
                                    <label className="av-tx-item-description">
                                        <span>{t('transactionManagement.itemDescription')}</span>
                                        <input value={item.description} maxLength={160} disabled={busy}
                                            onChange={event => updateItem(item.key, {
                                                description: event.target.value,
                                            })}
                                            placeholder={t('transactionManagement.itemDescriptionPlaceholder')}
                                            required />
                                    </label>
                                    <div className="av-tx-category-field">
                                        <span className="av-tx-field-label">
                                            {t('transactionManagement.category')}
                                        </span>
                                        <CategoryPicker
                                            categories={categories.data ?? []}
                                            value={item.categoryId}
                                            fallback={transaction?.items.find(old => old.id === item.key && old.categoryId === item.categoryId)}
                                            disabled={busy || categories.isPending}
                                            label={t('transactionManagement.category')}
                                            onChange={categoryId => updateItem(item.key, {
                                                categoryId,
                                            })}
                                        />
                                    </div>
                                    <label className="av-tx-item-amount-field">
                                        <span>{t('transactionManagement.amount')}</span>
                                        <div className="av-tx-amount-input">
                                            <input inputMode="decimal" value={item.amount} disabled={busy}
                                                onChange={event => updateItem(item.key, {
                                                    amount: event.target.value,
                                                })}
                                                placeholder="0.00" required />
                                            <strong>{currency}</strong>
                                        </div>
                                    </label>
                                    <button type="button" className="av-icon-btn av-tx-remove-item"
                                        disabled={busy || resolvedItems.length === 1}
                                        onClick={() => removeItem(item.key)}
                                        aria-label={t('transactionManagement.removeItem')}>×</button>
                                </div>
                            ))}
                        </div>

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

                    <section className="av-tx-form-section av-tx-section-details">
                        <div className="av-tx-form-section-head">
                            <span className="av-tx-section-number">4</span>
                            <div>
                                <h3>{t('transactionManagement.detailsPrivacy')}</h3>
                                <p>{t('transactionManagement.detailsPrivacyHelp')}</p>
                            </div>
                        </div>

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
                                onChange={event =>
                                    setVisibility(event.target.value as TransactionVisibility)}>
                                <option value="Household">{t('transactionManagement.household')}</option>
                                <option value="Private">{t('transactionManagement.private')}</option>
                            </select>
                        </label>

                        {effectiveVisibility === 'Private' && (
                            <p className="av-tx-private-help">
                                {t('transactionManagement.privateHelp')}
                            </p>
                        )}
                        {currency && (
                            <p className="av-subtle">
                                {t('transactionManagement.currencyLocked')}
                            </p>
                        )}
                    </section>

                    {error && <p className="av-error" role="alert">{error}</p>}

                    <footer className="av-tx-modal-actions">
                        {transaction && onVoid && transaction.status === 'Posted' && (
                            <button type="button" className="av-btn av-danger" disabled={busy}
                                onClick={() => {
                                    if (window.confirm(t('transactionManagement.voidConfirm'))) {
                                        void onVoid().catch(reason => setError(
                                            reason instanceof Error
                                                ? reason.message
                                                : t('errors.genericDescription'),
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
                            disabled={busy || writableAccounts.length === 0
                                || categories.isPending}>
                            {t(busy ? 'transactionManagement.saving' : 'transactionManagement.save')}
                        </button>
                    </footer>
                </form>
            </section>
        </div>
    );
}

