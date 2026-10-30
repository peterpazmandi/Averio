import type { ReceiptDraft, RememberReceiptCategoryRequest } from '../../../shared/api/generated';
import { normalizeReceiptAmount, receiptItems, receiptReconciles, suggestReceiptCategory } from '../../receipts/receiptReview';
import { getCategoryDisplayName } from '../categoryLocalization';
import type { ReactNode } from 'react';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { useMemo, useState } from 'react';
import type { RecurringRuleResponse, SaveRecurringRuleRequest } from '../../../shared/api/generated';
import { RecurringTimingFields } from '../../recurring/RecurringTimingFields';
import { AverioField, AverioInput, AverioSelect, AverioTextarea } from '../../../shared/components/ui/AverioFormControls';
import { ActivitySelect } from '../../activities/ActivitySelect';
import { emptyAssignmentVersion } from '../../activities/activityQueries';
import { AverioDisclosure } from '../../../shared/components/ui/AverioDisclosure';
import { personalMoneyQueries } from '../../personal-money/personalMoneyQueries';
import type { PersonalMoneyRelation } from '../../personal-money/personalMoneyRelations';
import { PersonalMoneyRelationBadge } from '../../personal-money/PersonalMoneyRelationBadge';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { formatDecimalAmount, subtractDecimalAmounts, sumDecimalAmounts } from '../../../shared/money/decimalAmount';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { createLabel } from '../api/transactionApi';
import { transactionQueries, transactionQueryKeys } from '../api/transactionQueries';
import { CategoryPicker } from './CategoryPicker';
import { resolveItemCategory } from '../resolveItemCategory';
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
    personalRelation?: PersonalMoneyRelation;
    initialKind?: TransactionKind;
    receiptDraft?: ReceiptDraft;
    receiptEvidence?: ReactNode;
    onSaveReceipt?: (request: TransactionInputDto, currency: string, reviewedTotal: string, rememberCategoryRules: RememberReceiptCategoryRequest[]) => Promise<void>;
    onCancel: () => void;
    recurringRule?: RecurringRuleResponse;
    initialRecurring?: boolean;
    onSaveRecurring?: (request: SaveRecurringRuleRequest) => Promise<void>;
    onSave?: (request: TransactionInputDto, expectedUpdatedAt?: string) => Promise<void>;
    onVoid?: (expectedUpdatedAt: string) => Promise<void>;
    onArchive?: (archived: boolean, expectedUpdatedAt: string) => Promise<void>;
    busy: boolean;
}

interface EditorItem {
    receiptLineIndex?: number;
    canRememberCategory?: boolean;
    rememberCategory?: boolean;
    suggestedCategoryId?: string;
    categorySuggestionSource?: string;
    originalDescription?: string;
    originalAmount?: string;
    discountAmount?: string;
    key: string;
    description: string;
    categoryId: string;
    amount: string;
    personalAmount: string | null;
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
    transaction: initialTransaction,
    personalRelation,
    initialKind = 'Expense',
    receiptDraft, receiptEvidence, onSaveReceipt,
    recurringRule: initialRule,
    initialRecurring = false,
    onSaveRecurring,
    onCancel,
    onSave,
    onVoid,
    onArchive,
    busy: parentBusy,
}: Props) {
    // Keep the draft and all concurrency tokens from the same editing session.
    // The parent keys this component by transaction ID and unmounts it on close.
    const [transaction] = useState(initialTransaction);
    const [rule] = useState(initialRule);
    const [recurring, setRecurring] = useState(!!initialRule || initialRecurring);
    const [scheduleName, setScheduleName] = useState(initialRule?.name ?? '');
    const [timing, setTiming] = useState({ frequency: initialRule?.frequency ?? 'Monthly', endsOn: initialRule?.endsOn ?? null as string | null, automatic: initialRule?.automatic ?? false });
    const [paused, setPaused] = useState(initialRule?.isPaused ?? false);
    const [stopped, setStopped] = useState(initialRule?.isStopped ?? false);
    const [stopConsent, setStopConsent] = useState(false);
    const [submitting, setSubmitting] = useState(false);
    const busy = parentBusy || submitting;
    const { t, i18n } = useTranslation();
    const queryClient = useQueryClient();
    const [receiptCurrency, setReceiptCurrency] = useState(receiptDraft?.currency ?? '');
    const [reviewedTotal, setReviewedTotal] = useState(receiptDraft?.total ?? '');
    const [reviewConfirmed, setReviewConfirmed] = useState(false);
    const accounts = useQuery(accountsQueries.list(userId, householdId, !!rule));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const writableAccounts = useMemo(() => (accounts.data ?? []).filter(account =>
        account.id === rule?.accountId || (account.status === 'Active'
        && (!transaction || account.currency === transaction.currency)
        && (!receiptDraft || account.currency === receiptCurrency)
        && (
            account.scope === 'Household'
            || account.owners.some(owner =>
                owner.householdMemberId === owners.data?.currentMemberId)
        ))), [accounts.data, owners.data?.currentMemberId, transaction, rule, receiptDraft, receiptCurrency]);

    const firstAccountId = rule?.accountId ?? transaction?.accountId ?? (receiptDraft ? '' : writableAccounts[0]?.id) ?? '';
    const [accountId, setAccountId] = useState(firstAccountId);
    const [kind, setKind] = useState<TransactionKind>((rule?.kind as TransactionKind | undefined) ?? transaction?.kind ?? initialKind);
    const [visibility, setVisibility] = useState<TransactionVisibility>(
        (rule?.visibility as TransactionVisibility | undefined) ?? transaction?.visibility ?? 'Household',
    );
    const [merchant, setMerchant] = useState(rule?.merchant ?? transaction?.merchant ?? receiptDraft?.merchant ?? '');
    const [note, setNote] = useState(rule?.note ?? transaction?.note ?? '');
    const [activityId, setActivityId] = useState(rule?.activityId ?? transaction?.activityId ?? '');
    const [occurredOn, setOccurredOn] = useState(rule?.startsOn ?? transaction?.occurredOn ?? receiptDraft?.date ?? (receiptDraft ? '' : initialRecurring ? new Date().toISOString().slice(0, 10) : todayIso()));
    const [items, setItems] = useState<EditorItem[]>(() =>
        receiptDraft && receiptItems(receiptDraft).length ? receiptItems(receiptDraft).map(item => ({ ...item, key: itemKey(), personalAmount: null })) : rule ? rule.items.map(item => ({ key: itemKey(), ...item, personalAmount: null })) : transaction?.items.length
            ? [...transaction.items]
                .sort((a, b) => a.position - b.position)
                .map(item => ({
                    key: item.id || itemKey(),
                    description: item.description,
                    categoryId: item.categoryId,
                    amount: item.amount,
                    personalAmount: null,
                }))
            : [{
                key: itemKey(),
                description: '',
                categoryId: '',
                amount: '',
                personalAmount: null,
            }],
    );
    const [selectedLabelIds, setSelectedLabelIds] = useState<string[]>(
        () => rule?.labelIds ?? transaction?.labels.map(label => label.id) ?? [],
    );
    const [newLabelName, setNewLabelName] = useState('');
    const [newLabelScope, setNewLabelScope] = useState<LabelScope>('Household');
    const [newLabelOpen, setNewLabelOpen] = useState(false);
    const [error, setError] = useState('');
    const [allowanceItemKey, setAllowanceItemKey] = useState<string | null>(null);

    const resolvedAccountId = receiptDraft ? (writableAccounts.some(a => a.id === accountId) ? accountId : '') : accountId || writableAccounts[0]?.id || '';
    const account = writableAccounts.find(item => item.id === resolvedAccountId);
    const forcedVisibility: TransactionVisibility | undefined =
        account?.visibility === 'Private'
            ? 'Private'
            : account?.scope === 'Household'
                ? 'Household'
                : undefined;
    const effectiveVisibility = rule ? visibility : forcedVisibility ?? visibility;
    const currency = account?.currency ?? rule?.currency ?? transaction?.currency ?? '';
    const language = i18n.resolvedLanguage ?? i18n.language;

    const wallet = useQuery({ ...personalMoneyQueries.mine(userId, householdId),
        enabled: !rule && kind === 'Expense' });
    const canChargePersonalMoney = !recurring && kind === 'Expense'
        && ((account?.scope === 'Household' && account.visibility === 'Household'
            && account.includeInHouseholdReports && effectiveVisibility === 'Household')
            || (account?.scope === 'Personal' && account.visibility === 'Private'
                && !account.includeInHouseholdReports && effectiveVisibility === 'Private'))
        && wallet.data?.currency === currency;

    function storedPersonalAmount(item: EditorItem): string {
        if (!transaction) return '0';
        const current = sumDecimalAmounts((wallet.data?.entries ?? []).filter(entry =>
            entry.transactionItemId === item.key
            && entry.transactionId === transaction.id
            && (entry.kind === 'PrivateCharge' || entry.kind === 'SharedCharge'))
            .map(entry => entry.amount));
        return current.startsWith('-') ? subtractDecimalAmounts('0', current) : '0';
    }
    function personalAmount(item: EditorItem): string {
        return item.personalAmount ?? storedPersonalAmount(item);
    }
    function canManagePersonalMoney(item: EditorItem): boolean {
        return canChargePersonalMoney || (!recurring && kind === 'Expense'
            && (amountToScaled(storedPersonalAmount(item)) ?? 0n) > 0n);
    }
    function chargeAllItems() {
        setItems(current => current.map(item => ({
            ...item, personalAmount: canChargePersonalMoney ? normalizeAmount(item.amount) : '0',
        })));
    }

    const categories = useQuery(transactionQueries.categories(userId, householdId, kind));
    const labels = useQuery(transactionQueries.labels(
        userId,
        householdId,
        resolvedAccountId || undefined,
    ));

    const uncategorizedId = useMemo(
        () => (categories.data ?? []).find(
            category => category.systemKey === 'uncategorized',
        )?.id ?? '',
        [categories.data],
    );

    const resolvedItems = useMemo(
        () => items.map(item => ({
            ...item, categoryId: resolveItemCategory(item.categoryId, uncategorizedId),
        })),
        [items, uncategorizedId],
    );

    const resolvedLabelIds = useMemo(() => {
        if (rule || !labels.data) return selectedLabelIds;
        const available = new Set(labels.data.map(label => label.id));
        return selectedLabelIds.filter(id => available.has(id));
    }, [labels.data, selectedLabelIds, rule]);

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

    const legacyPersonalTotal = transaction ? (amountToScaled(subtractDecimalAmounts('0', sumDecimalAmounts((wallet.data?.entries ?? [])
        .filter(entry => entry.transactionId === transaction.id && !entry.transactionItemId
            && (entry.kind === 'PrivateCharge' || entry.kind === 'SharedCharge'))
        .map(entry => entry.amount)))) ?? 0n) : 0n;
    const personalTotal = resolvedItems.reduce((sum, item) =>
        sum + (canManagePersonalMoney(item) ? (amountToScaled(personalAmount(item)) ?? 0n) : 0n), legacyPersonalTotal);
    const allowanceItem = resolvedItems.find(item => item.key === allowanceItemKey);

    function updateItem(key: string, patch: Partial<EditorItem>) {
        if (receiptDraft) setReviewConfirmed(false);
        setItems(current => current.map(item =>
            item.key === key ? { ...item, ...patch } : item));
    }

    function addItem() {
        setItems(current => [...current, {
            key: itemKey(),
            description: '',
            categoryId: uncategorizedId,
            amount: '',
            personalAmount: null,
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

    const receiptValid = !receiptDraft || (reviewConfirmed && receiptCurrency === currency && receiptReconciles(reviewedTotal, resolvedItems));

    function submittedAmount(value: string) {
        // Keep receipt validation and all monetary request fields on the same
        // canonical representation, including item-level allowance allocations.
        return receiptDraft ? normalizeReceiptAmount(value) ?? normalizeAmount(value) : normalizeAmount(value);
    }

    async function submit(event: React.FormEvent) {
        event.preventDefault();
        if (busy) return;
        setError('');

        if (!receiptValid) { setError(t('receipts.reconcileHelp')); return; }
        if (!resolvedAccountId || !occurredOn || resolvedItems.length === 0) {
            setError(t('transactionManagement.formInvalid'));
            return;
        }

        const mappedItems = resolvedItems.map(item => ({
            existingItemId: transaction?.items.some(old => old.id === item.key) ? item.key : null,
            description: item.description.trim(),
            categoryId: item.categoryId,
            amount: submittedAmount(item.amount),
            personalMoneyAmount: canManagePersonalMoney(item) ? item.personalAmount === null
                ? null : submittedAmount(item.personalAmount) : null,
        }));

        const invalidItem = mappedItems.some(item =>
            !item.description
            || !item.categoryId
            || !/^\d+(?:\.\d{1,4})?$/.test(item.amount)
            || Number(item.amount) <= 0);

        if (invalidItem || amountToScaled(total) === 0n ||
            mappedItems.some(item => item.personalMoneyAmount !== null &&
                (amountToScaled(item.personalMoneyAmount) === null ||
                (amountToScaled(item.personalMoneyAmount) ?? 0n) > (amountToScaled(item.amount) ?? 0n)))) {
            setError(t('transactionManagement.itemsInvalid'));
            return;
        }

        setSubmitting(true);
        try {
            if (recurring && onSaveRecurring) {
                await onSaveRecurring({
                    accountId: resolvedAccountId, kind, visibility: effectiveVisibility,
                    name: scheduleName.trim(), categoryId: mappedItems[0].categoryId, amount: total,
                    items: mappedItems.map(({ description, categoryId, amount }) => ({ description, categoryId, amount })),
                    labelIds: resolvedLabelIds, merchant: merchant.trim() || null, note: note.trim() || null,
                    activityId: activityId || null, startsOn: occurredOn, ...timing,
                    isPaused: paused, isStopped: stopped, expectedVersion: rule?.version,
                });
                return;
            }
            const remembered = resolvedItems.flatMap((item, itemIndex) => item.rememberCategory && item.canRememberCategory
                && item.receiptLineIndex !== undefined && item.categoryId !== uncategorizedId
                ? [{ lineIndex: item.receiptLineIndex, itemIndex }] : []);
            const save = onSaveReceipt ? (request: TransactionInputDto) => onSaveReceipt(request, receiptCurrency, submittedAmount(reviewedTotal), remembered) : onSave;
            await save?.({
                accountId: resolvedAccountId,
                kind,
                visibility: effectiveVisibility,
                merchant: merchant.trim() || null,
                note: note.trim() || null,
                occurredOn,
                items: mappedItems,
                labelIds: resolvedLabelIds,
                chargePersonalMoney: false,
                activityAssignment: activityId !== (transaction?.activityId ?? '')
                    ? { activityId: activityId || null, expectedVersion: transaction?.activityAssignmentVersion ?? emptyAssignmentVersion }
                    : null,
            }, transaction?.updatedAt);
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : t('errors.genericDescription'));
        } finally { setSubmitting(false); }
    }


    return <><AverioDialog open wide title={t(receiptDraft ? 'receipts.review' : rule ? 'recurring.edit' : transaction ? 'transactionManagement.edit' : 'transactionManagement.add')}
        closeLabel={t('common.actions.close')} busy={busy} onDismiss={onCancel}
        actions={<>
            {transaction && onArchive && transaction.status === 'Posted' && <AverioButton disabled={busy}
                onClick={() => {
                    if (window.confirm(t(transaction.isArchived ? 'transactionManagement.restoreConfirm' : 'transactionManagement.archiveConfirm')))
                        void onArchive(!transaction.isArchived, transaction.updatedAt).catch(reason =>
                            setError(reason instanceof Error ? reason.message : t('errors.genericDescription')));
                }}>{t(transaction.isArchived ? 'transactionManagement.restore' : 'transactionManagement.archive')}</AverioButton>}
            {transaction && onVoid && transaction.status === 'Posted' && <AverioButton variant="danger" disabled={busy} onClick={() => {
                if (window.confirm(t('transactionManagement.voidConfirm'))) {
                    void onVoid(transaction.updatedAt).catch(reason => setError(reason instanceof Error ? reason.message : t('errors.genericDescription')));
                }
            }}>{t('transactionManagement.void')}</AverioButton>}
            <AverioButton disabled={busy} onClick={onCancel}>{t('common.actions.cancel')}</AverioButton>
            <AverioButton type="submit" form="transaction-editor" variant={stopped ? 'danger' : 'primary'}
                disabled={busy || !receiptValid || !resolvedAccountId || (Boolean(receiptDraft) && (accounts.isError || owners.isError || categories.isError)) || writableAccounts.length === 0 || categories.isPending || (stopped && !stopConsent)}>
                {t(busy ? 'transactionManagement.saving' : receiptDraft ? 'receipts.confirm' : recurring ? 'recurring.save' : 'transactionManagement.save')}
            </AverioButton>
        </>}>
        <form id="transaction-editor" className="grid gap-4" onSubmit={submit}>
            {receiptDraft && <AverioCard className="grid gap-3">
                <h3 className="font-bold">{t('receipts.review')}</h3>
                <p className="text-sm">{t('receipts.reviewHelp')}</p>
                {receiptEvidence}
                <div className="grid gap-3 sm:grid-cols-2">
                    <AverioField>{t('receipts.currency')}<AverioSelect required aria-label={t('receipts.currency')} value={receiptCurrency} disabled={busy}
                        onChange={event => { setReceiptCurrency(event.target.value); setAccountId(''); setReviewConfirmed(false); setItems(current => current.map(i => ({ ...i, personalAmount: null }))); }}>
                        <option value="">{t('receipts.chooseCurrency')}</option>
                        {!['EUR', 'USD', 'HUF'].includes(receiptCurrency) && receiptCurrency && <option value={receiptCurrency} disabled>{receiptCurrency}</option>}
                        {['EUR', 'USD', 'HUF'].map(code => <option key={code}>{code}</option>)}
                    </AverioSelect></AverioField>
                    <AverioField>{t('receipts.reviewedTotal')}<AverioInput required inputMode="decimal" aria-label={t('receipts.reviewedTotal')}
                        value={reviewedTotal} disabled={busy} onChange={event => { setReviewedTotal(event.target.value); setReviewConfirmed(false); }} /></AverioField>
                </div>
                <p className="text-sm" role="status">{t(receiptReconciles(reviewedTotal, resolvedItems) ? 'receipts.reconciled' : 'receipts.reconcileHelp')}</p>
                <label className="flex min-h-11 items-center gap-3 text-sm"><input type="checkbox" className="size-4 shrink-0 accent-emerald-500" checked={reviewConfirmed} disabled={busy}
                    onChange={event => setReviewConfirmed(event.target.checked)} />{t('receipts.reviewConsent')}</label>
            </AverioCard>}
            {receiptDraft && [accounts, owners, categories].map((query, index) => query.isError && <AverioCard key={index} className="grid gap-2">
                <p role="alert" className="text-sm text-red-700 dark:text-red-300">{query.error.message}</p>
                <div><AverioButton disabled={query.isFetching} onClick={() => void query.refetch()}>{t('receipts.retry')}</AverioButton></div>
            </AverioCard>)}
            {transaction && personalRelation && <div className="rounded-xl border border-emerald-400/30 bg-emerald-50 p-3 dark:bg-emerald-950/30">
                <p className="mb-2 text-xs font-semibold">{t('personalMoney.relationDetails')}</p>
                <PersonalMoneyRelationBadge relation={personalRelation} detail />
                <p className="mt-2 text-xs">{t('personalMoney.transactionLinkHelp')}</p>
            </div>}

            <AverioCard className="grid gap-4">
                <div><h3 className="font-bold" aria-label={t('transactionManagement.basics')}>{t('transactionManagement.basics')} <InfoPopover label={t('transactionManagement.basics')}>{t('transactionManagement.basicsHelp')}</InfoPopover></h3></div>
                <fieldset className="flex gap-3" disabled={busy || !!rule || !!receiptDraft}>
                    <legend className="sr-only">{t('transactionManagement.type')}</legend>
                    {(['Income', 'Expense'] as const).map(value => <label key={value}
                        className={'flex min-h-11 flex-1 items-center justify-center gap-2 rounded-xl border px-3 py-2 font-semibold ' + (kind === value ? 'border-emerald-500 bg-emerald-500/10' : 'border-slate-200 dark:border-slate-700')}>
                        <input type="radio" name="kind" value={value} checked={kind === value} className="accent-emerald-500" onChange={() => { setKind(value); setItems(current => current.map(i => ({ ...i, personalAmount: null }))); }} />
                        {t(value === 'Income' ? 'transactionManagement.income' : 'transactionManagement.expense')}
                    </label>)}
                </fieldset>
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('transactionManagement.account')}<AverioSelect aria-label={t('transactionManagement.account')} value={resolvedAccountId} disabled={busy || !!rule} required onChange={e => { setAccountId(e.target.value); setItems(current => current.map(i => ({ ...i, personalAmount: null }))); }}>
                        <option value="" disabled>{t('transactionManagement.account')}</option>
                        {writableAccounts.map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}
                    </AverioSelect>{receiptDraft && accounts.isSuccess && owners.isSuccess && writableAccounts.length === 0 && <span className="text-sm">{t('receipts.noAccount')}</span>}</AverioField>
                    <AverioField>{t(recurring ? 'recurring.starts' : 'transactionManagement.date')}<AverioInput type="date" value={occurredOn} required disabled={busy || !!rule}
                        min={recurring && !rule ? new Date().toISOString().slice(0, 10) : undefined} onChange={e => setOccurredOn(e.target.value)} /></AverioField>
                </div>
                <AverioField>{t('transactionManagement.merchant')}<AverioInput value={merchant} maxLength={160} disabled={busy}
                    placeholder={t('transactionManagement.merchantPlaceholder')} onChange={e => setMerchant(e.target.value)} /></AverioField>
            </AverioCard>
            <AverioCard className="grid gap-3">
                <div><h3 className="font-bold" aria-label={t('transactionManagement.labels')}>{t('transactionManagement.labels')} <InfoPopover label={t('transactionManagement.labels')}>{t('transactionManagement.labelsHelp')}</InfoPopover></h3></div>
                {!resolvedAccountId && <p className="text-sm">{t('receipts.labelsChooseAccount')}</p>}
                {labels.isLoading && <p role="status">{t('common.status.loading')}</p>}
                {labels.isError && <div role="alert"><p>{labels.error.message}</p><AverioButton disabled={busy || labels.isFetching} onClick={() => void labels.refetch()}>{t('receipts.retry')}</AverioButton></div>}
                {labels.isSuccess && !labels.data.length && <p className="text-xs">{t('transactionManagement.noLabelsYet')}</p>}
                <div className="flex flex-wrap gap-2">
                    {(labels.data ?? []).map(label => <AverioButton key={label.id} aria-pressed={resolvedLabelIds.includes(label.id)}
                        disabled={busy} onClick={() => toggleLabel(label.id)}
                        className={resolvedLabelIds.includes(label.id) ? 'border-emerald-500! bg-emerald-500/10!' : ''}>
                        {resolvedLabelIds.includes(label.id) ? '✓ ' : ''}{label.name}
                    </AverioButton>)}
                    {!newLabelOpen && <AverioButton disabled={busy || !resolvedAccountId} onClick={() => { createLabelMutation.reset(); setNewLabelOpen(true); }}>
                        + {t('transactionManagement.addNewLabel')}
                    </AverioButton>}
                </div>
                {newLabelOpen && <div className="grid gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-700">
                    <AverioField>{t('transactionManagement.newLabel')}<AverioInput value={newLabelName} maxLength={40} autoFocus
                        disabled={busy || createLabelMutation.isPending} onChange={e => setNewLabelName(e.target.value)} placeholder={t('transactionManagement.labelPlaceholder')} /></AverioField>
                    <AverioField>{t('transactionManagement.labelScope')}<AverioSelect aria-label={t('transactionManagement.labelScope')} value={newLabelScope} disabled={busy || createLabelMutation.isPending} onChange={e => setNewLabelScope(e.target.value as LabelScope)}>
                        <option value="Household">{t('transactionManagement.householdLabel')}</option><option value="Account">{t('transactionManagement.accountLabel')}</option>
                    </AverioSelect></AverioField>
                    <div className="flex flex-wrap gap-2">
                        <AverioButton disabled={busy || createLabelMutation.isPending || !newLabelName.trim() || !resolvedAccountId} onClick={() => createLabelMutation.mutate()}>
                            {t(createLabelMutation.isPending ? 'transactionManagement.creatingLabel' : 'transactionManagement.createLabel')}
                        </AverioButton>
                        <AverioButton disabled={busy || createLabelMutation.isPending} onClick={() => { createLabelMutation.reset(); setNewLabelName(''); setNewLabelScope('Household'); setNewLabelOpen(false); }}>{t('common.actions.cancel')}</AverioButton>
                    </div>
                    {createLabelMutation.isError && <p role="alert">{createLabelMutation.error.message}</p>}
                </div>}
            </AverioCard>
            <AverioCard className="grid gap-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div><h3 className="font-bold" aria-label={t('transactionManagement.items')}>{t('transactionManagement.items')} <InfoPopover label={t('transactionManagement.items')}>{t('transactionManagement.itemsHelp')}</InfoPopover></h3></div>
                    <div className="flex flex-wrap gap-2">
                        <AverioButton disabled={busy || resolvedItems.length >= 100} onClick={addItem}>+ {t('transactionManagement.addItem')}</AverioButton>
                    </div>
                </div>
                {receiptDraft && receiptDraft.lines.some(line => line.kind === 'Discount') && <p className="text-sm" role="status">{t(items.some(item => item.discountAmount) ? 'receipts.discountsSuggested' : 'receipts.discountsManual')}</p>}
                <div aria-hidden="true" className="hidden gap-3 text-[13px] font-semibold sm:grid sm:grid-cols-[minmax(0,1.4fr)_minmax(0,1.2fr)_minmax(0,.8fr)_44px]">
                    <span>{t('transactionManagement.itemDescription')}</span>
                    <span>{t('transactionManagement.category')}</span>
                    <span>{t('transactionManagement.amount')} · {currency}</span>
                </div>
                {resolvedItems.map((item, index) => <div key={item.key} className="grid grid-cols-[minmax(0,1fr)_44px] items-end gap-3 border-b border-slate-200 pb-3 dark:border-slate-700 sm:grid-cols-[minmax(0,1.4fr)_minmax(0,1.2fr)_minmax(0,.8fr)_44px]">
                    <div className="col-span-2 sm:col-span-1"><label htmlFor={item.key + '-description'} className="mb-[7px] block text-[13px] font-semibold sm:sr-only">{t('transactionManagement.itemDescription')}</label><AverioInput id={item.key + '-description'} required value={item.description} maxLength={160} disabled={busy}
                        onChange={e => updateItem(item.key, { description: e.target.value })} placeholder={t('transactionManagement.itemDescriptionPlaceholder')} />
                        {item.originalDescription && <p className="mt-1 break-words text-xs text-slate-600 dark:text-slate-300">{t('receipts.originalLine', { description: item.originalDescription })}</p>}
                        {item.originalDescription && item.description !== item.originalDescription && <p className="mt-1 text-xs">{t('receipts.nameSuggestion')}</p>}
                        {item.discountAmount && <p className="mt-1 text-xs text-emerald-700 dark:text-emerald-300">{t('receipts.discountApplied', { gross: item.originalAmount, discount: item.discountAmount })}</p>}
                    </div>
                    <div className="col-span-2 min-w-0 sm:col-span-1"><span className="mb-[7px] block text-[13px] font-semibold sm:hidden">{t('transactionManagement.category')}</span>
                        <CategoryPicker categories={categories.data ?? []} value={item.categoryId}
                            fallback={transaction?.items.find(old => old.id === item.key && old.categoryId === item.categoryId)}
                            disabled={busy || categories.isPending} label={t('transactionManagement.category')} onChange={categoryId => updateItem(item.key, { categoryId })} />
                        {receiptDraft && (() => {
                            const available = (categories.data ?? []).filter(c => !c.isArchived && c.systemKey !== 'uncategorized')
                                .map(c => ({ id: c.id, displayName: getCategoryDisplayName(c, t) }));
                            const supplied = available.find(c => c.id === item.suggestedCategoryId);
                            const suggestion = supplied ?? suggestReceiptCategory(item.description, available);
                            return <>
                            {suggestion && suggestion.id !== item.categoryId ? <AverioButton disabled={busy} className="mt-2 text-xs"
                                onClick={() => updateItem(item.key, { categoryId: suggestion.id })}>{t('receipts.suggestion', { category: suggestion.displayName })}</AverioButton> : null}
                            {supplied && <p className="mt-1 text-xs text-slate-600 dark:text-slate-300">{t(item.categorySuggestionSource === 'HouseholdRule' ? 'receipts.householdRuleSuggestion' : 'receipts.modelCategorySuggestion')}</p>}
                            {item.canRememberCategory && receiptDraft.merchant?.trim() && <label className="mt-2 flex min-h-11 items-start gap-2 text-xs">
                                <input type="checkbox" className="mt-1 size-4 shrink-0 accent-emerald-500" checked={!!item.rememberCategory && item.categoryId !== uncategorizedId}
                                    disabled={busy || !item.categoryId || item.categoryId === uncategorizedId || !available.some(c => c.id === item.categoryId)}
                                    onChange={event => updateItem(item.key, { rememberCategory: event.target.checked })} />
                                <span>{t('receipts.rememberCategory')}<span className="mt-1 block text-slate-600 dark:text-slate-300">{t('receipts.rememberCategoryHelp')}</span></span>
                            </label>}
                            </>;
                        })()}
                    </div>
                    <div className="min-w-0">
                        <label htmlFor={item.key + '-amount'} className="mb-[7px] block text-[13px] font-semibold sm:sr-only">{t('transactionManagement.amount')} · {currency}</label>
                        <div className="flex min-w-0 gap-1">
                            <AverioInput id={item.key + '-amount'} className="min-w-0" required inputMode="decimal" value={item.amount} disabled={busy}
                                onChange={e => updateItem(item.key, { amount: e.target.value })} placeholder="0.00" />
                            {canManagePersonalMoney(item) && <AverioButton size="icon" disabled={busy} aria-haspopup="dialog"
                                aria-label={t('personalMoney.itemCharge') + ' · ' + (item.description || index + 1)}
                                title={t('personalMoney.itemCharge') + ' · ' + formatDecimalAmount(scaledToAmount(amountToScaled(personalAmount(item)) ?? 0n), currency, language)}
                                className={(amountToScaled(personalAmount(item)) ?? 0n) > 0n
                                    ? 'shrink-0 border-emerald-500! bg-emerald-100! text-emerald-800! dark:bg-emerald-950! dark:text-emerald-200!'
                                    : 'shrink-0'}
                                onClick={() => setAllowanceItemKey(item.key)}><ConceptIcon name="savings" /></AverioButton>}
                        </div>
                    </div>
                    <div className="flex items-end justify-end"><AverioButton size="icon" className="text-red-700! hover:bg-red-50! dark:text-red-400! dark:hover:bg-red-950/40!" title={t('transactionManagement.removeItem')} disabled={busy || resolvedItems.length === 1} onClick={() => removeItem(item.key)}
                        aria-label={t('transactionManagement.removeItem') + ' ' + (index + 1)}><ConceptIcon name="close" /></AverioButton></div>
                </div>)}
                {!recurring && kind === 'Expense' && wallet.isError && <p role="alert" className="text-xs text-red-700 dark:text-red-300">{t('personalMoney.loadError')}</p>}
                <div className={'flex flex-wrap justify-between gap-3 font-bold ' + (kind === 'Income' ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400')}>
                    <span>{t(recurring ? 'recurring.amount' : 'transactionManagement.total')}</span>
                    <strong>{kind === 'Income' ? '+' : '−'}{currency ? formatDecimalAmount(total, currency, language) : total}</strong>
                </div>
                {!recurring && kind === 'Expense' && (legacyPersonalTotal > 0n || canChargePersonalMoney || resolvedItems.some(canManagePersonalMoney)) &&
                    <div className="flex flex-wrap justify-between gap-3 text-sm font-semibold text-emerald-800 dark:text-emerald-200">
                        <span>{t('personalMoney.totalMyAllocations')}</span>
                        <output aria-label={t('personalMoney.totalMyAllocations')} aria-live="polite">{formatDecimalAmount(scaledToAmount(personalTotal), currency, language)}</output>
                    </div>}
            </AverioCard>
            {!transaction && onSaveRecurring && <AverioDisclosure title={t('recurring.makeRecurring')} defaultOpen={recurring} summary={recurring ? scheduleName || t('recurring.creationHelp') : undefined}>
                        {!rule && onSave && <label className="flex min-h-11 items-center gap-3 font-semibold">
                            <input type="checkbox" className="size-4 accent-emerald-500" checked={recurring} disabled={busy}
                                onChange={e => { setRecurring(e.target.checked); setItems(current => current.map(i => ({ ...i, personalAmount: null }))); }} />{t('recurring.makeRecurring')}
                        </label>}
                        {recurring && <>
                            <p className="text-sm">{t('recurring.creationHelp')}</p>
                            <AverioField>{t('recurring.name')}<AverioInput required maxLength={100} value={scheduleName} disabled={busy} onChange={e => setScheduleName(e.target.value)} /></AverioField>
                            <RecurringTimingFields value={{ ...timing, startsOn: occurredOn }} onChange={patch => setTiming(current => ({ ...current, ...patch }))} busy={busy} anchorLocked={!!rule} showStart={false} />
                            {rule && <>
                                <label className="flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 accent-emerald-500" checked={paused} disabled={busy} onChange={e => setPaused(e.target.checked)} />{t('recurring.pause')}</label>
                                <label className="flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 accent-red-600" checked={stopped} disabled={busy} onChange={e => { setStopped(e.target.checked); setStopConsent(false); }} />{t('recurring.stop')}</label>
                                {stopped && <div className="rounded-xl border border-red-300 p-3"><p>{t('recurring.stopHelp')}</p><label className="mt-2 flex min-h-11 items-center gap-3"><input type="checkbox" required checked={stopConsent} disabled={busy} onChange={e => setStopConsent(e.target.checked)} />{t('recurring.stopConfirm')}</label></div>}
                            </>}
                        </>}
                    </AverioDisclosure>}

            <AverioDisclosure title={t('activities.activity')} defaultOpen={!!activityId} summary={activityId ? t('personalMoney.extraConfigured') : t('activities.none')}>
                <ActivitySelect userId={userId} householdId={householdId} value={activityId} onChange={setActivityId} disabled={busy} />
                <p className="text-xs">{t('activities.privateHelp')}</p>
            </AverioDisclosure>
            <AverioDisclosure title={t('transactionManagement.detailsPrivacy')} summary={t(effectiveVisibility === 'Private' ? 'transactionManagement.private' : 'transactionManagement.household') + (note ? ' · ' + note : '')}>
                <p className="text-xs">{t('transactionManagement.detailsPrivacyHelp')}</p>
                <AverioField>{t('transactionManagement.note')}<AverioTextarea aria-label={t('transactionManagement.note')} value={note} maxLength={500} disabled={busy} onChange={e => setNote(e.target.value)} placeholder={t('transactionManagement.notePlaceholder')} /></AverioField>
                <AverioField>{t('transactionManagement.visibility')}<AverioSelect aria-label={t('transactionManagement.visibility')} value={effectiveVisibility}
                    disabled={busy || (forcedVisibility !== undefined && (!rule || visibility === forcedVisibility))} onChange={e => setVisibility(e.target.value as TransactionVisibility)}>
                    <option value="Household" disabled={forcedVisibility === 'Private'}>{t('transactionManagement.household')}</option>
                    <option value="Private" disabled={forcedVisibility === 'Household'}>{t('transactionManagement.private')}</option>
                </AverioSelect></AverioField>
                {effectiveVisibility === 'Private' && <p className="text-xs">{t('transactionManagement.privateHelp')}</p>}
                {recurring && <p className="text-xs">{t('recurring.privacyHelp')}</p>}
                <p className="text-xs">{t('transactionManagement.currencyLocked')}</p>
            </AverioDisclosure>
            {error && <p role="alert" className="text-red-700 dark:text-red-400">{error} {rule && t('recurring.updatedElsewhere')}</p>}
        </form>
    </AverioDialog>
    {allowanceItem && canManagePersonalMoney(allowanceItem) && <AverioDialog open title={t('personalMoney.itemAllocationTitle')}
        closeLabel={t('common.actions.close')} onDismiss={() => setAllowanceItemKey(null)} busy={busy}
        actions={<AverioButton variant="primary" onClick={() => setAllowanceItemKey(null)}>{t('common.actions.close')}</AverioButton>}>
        <div className="grid gap-3">
            <p className="font-semibold">{allowanceItem.description || t('transactionManagement.itemDescription')}</p>
            {!canChargePersonalMoney ? <AverioButton disabled={busy} onClick={() => updateItem(allowanceItem.key, { personalAmount: '0' })}>
                {t('personalMoney.removeCharge')}
            </AverioButton> : <><label className="flex min-h-11 items-center gap-2 text-sm font-medium">
                <input type="checkbox" className="size-4 accent-emerald-500" disabled={busy}
                    checked={(amountToScaled(personalAmount(allowanceItem)) ?? 0n) > 0n}
                    onChange={event => updateItem(allowanceItem.key, { personalAmount: event.target.checked ? normalizeAmount(allowanceItem.amount) : '0' })} />
                {t('personalMoney.itemCharge')}
            </label>
            <AverioField>{t('personalMoney.itemChargeAmount')} · {currency}
                <AverioInput inputMode="decimal" value={personalAmount(allowanceItem)} disabled={busy}
                    onChange={event => updateItem(allowanceItem.key, { personalAmount: event.target.value })} />
            </AverioField></>}
            <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs">
                <span>{t('personalMoney.itemSplitHousehold')} <strong>{formatDecimalAmount(subtractDecimalAmounts(total, scaledToAmount(personalTotal)), currency, language)}</strong></span>
                <span>{t('personalMoney.itemSplitPersonal')} <strong>{formatDecimalAmount(scaledToAmount(personalTotal), currency, language)}</strong></span>
            </div>
            <p className="text-xs leading-relaxed">{t('personalMoney.itemAllocationHelp')}</p>
            {canChargePersonalMoney && <AverioButton disabled={busy} onClick={chargeAllItems}>{t('personalMoney.chargeAllItems')}</AverioButton>}
        </div>
    </AverioDialog>}</>;
}

