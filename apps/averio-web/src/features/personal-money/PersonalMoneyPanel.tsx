import { Activity, useState } from 'react';
import { format } from 'date-fns';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { HouseholdResponse, PersonalMoneyChargeResponse } from '../../shared/api/generated';
import { TransactionsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioSectionSelect } from '../../shared/components/ui/AverioSectionSelect';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { personalMoneyApi, personalMoneyQueries, personalMoneyKeys } from './personalMoneyQueries';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { PersonalMoneyReconciliationPanel, HouseholdBoundaryFlowPanel } from './PersonalMoneyReconciliationPanel';

const transactionsApi = new TransactionsApi(generatedApiConfiguration);
const decimalPattern = /^[0-9]+([.,][0-9]{1,4})?$/;
const normalized = (value: string) => value.trim().replace(',', '.');

export function PersonalMoneyPanel({ userId, householdId, household }: {
    userId: string;
    householdId: string;
    household: HouseholdResponse;
}) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const mine = useQuery(personalMoneyQueries.mine(userId, householdId));
    const accounts = useQuery(accountsQueries.list(userId, householdId, false));
    const privateAccounts = (accounts.data ?? []).filter(account =>
        account.visibility === 'Private' && account.scope === 'Personal'
        && account.owners.some(owner => household.members.some(member =>
            member.isCurrentUser && member.id === owner.householdMemberId)));
    const transactions = useQuery({
        queryKey: ['personal-money', userId, householdId, 'eligible-expenses'],
        queryFn: ({ signal }) => transactionsApi.apiV1HouseholdsHouseholdIdTransactionsGet(
            { householdId, kind: 'Expense', pageSize: 50 }, { signal }),
    });
    const eligibleIds = new Set([
        ...privateAccounts.map(a => a.id),
        ...(accounts.data ?? []).filter(a => a.scope === 'Household'
            && a.visibility === 'Household' && a.includeInHouseholdReports).map(a => a.id),
    ]);
    // Consumption occurs only after explicit selection; never infer it from an account.
    const existingCharges = mine.data?.charges ?? [];
    const chargedIds = new Set(existingCharges.map(c => c.transactionId));
    const purchases = (transactions.data?.items ?? []).filter(transaction =>
        transaction.status === 'Posted' && eligibleIds.has(transaction.accountId)
        && (!mine.data || transaction.currency === mine.data.currency)
        && !chargedIds.has(transaction.id));
    type MoneyTab = 'mine' | 'charges' | 'reconcile' | 'owner' | 'boundary';
    const [tab, setTab] = useState<MoneyTab>('mine');
    const tabs: MoneyTab[] = household.role === 'owner'
        ? ['mine', 'charges', 'reconcile', 'owner', 'boundary']
        : ['mine', 'charges', 'reconcile', 'boundary'];
    const tabIcons = { mine: 'savings', charges: 'receipt', reconcile: 'transfers',
        owner: 'group', boundary: 'statistics' } as const;
    const [recipient, setRecipient] = useState(household.members.find(m => m.isCurrentUser)?.id ?? '');
    const [grantAmount, setGrantAmount] = useState('');
    // Reuse this operation ID after network errors; only form edits start a new grant intent.
    const [grantOperationId, setGrantOperationId] = useState(() => crypto.randomUUID());
    const resetGrantIntent = () => setGrantOperationId(crypto.randomUUID());
    const [currency, setCurrency] = useState(household.preferredCurrency || 'EUR');
    const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'));
    const [purchaseId, setPurchaseId] = useState('');
    const [chargeAmount, setChargeAmount] = useState('');
    const [chargeOperationId, setChargeOperationId] = useState(() => crypto.randomUUID());
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');
    const locale = i18n.resolvedLanguage ?? i18n.language;
    const money = (value: string, code: string) => {
        try { return new Intl.NumberFormat(locale, { style: 'currency', currency: code }).format(Number(value)); }
        catch { return value + ' ' + code; }
    };
    const refresh = async () => {
        await client.invalidateQueries({ queryKey: personalMoneyKeys.mine(userId, householdId) });
        await client.invalidateQueries({ queryKey: ['personal-money', userId, householdId, 'eligible-expenses'] });
    };
    const grant = useMutation({
        mutationFn: () => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyGrantsPost({
            householdId,
            grantPersonalMoneyRequest: {
                recipientMemberId: recipient, currency, amount: normalized(grantAmount),
                occurredOn: date, operationId: grantOperationId,
            },
        }),
        onSuccess: async () => { await refresh(); setGrantAmount(''); resetGrantIntent(); setMessage(t('personalMoney.grantSuccess')); setError(''); },
        onError: (failure: Error) => { setError(failure.message); setMessage(''); },
    });
    const charge = useMutation({
        mutationFn: () => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeChargesPost({
            householdId,
            chargePersonalMoneyRequest: { transactionId: purchaseId, amount: normalized(chargeAmount), operationId: chargeOperationId },
        }),
        onSuccess: async () => { await refresh(); setPurchaseId(''); setChargeAmount(''); setChargeOperationId(crypto.randomUUID()); setMessage(t('personalMoney.chargeSuccess')); setError(''); },
        onError: (failure: Error) => { setError(failure.message); setMessage(''); },
    });
    const canGrant = household.role === 'owner';
    const validGrant = !!recipient && !!date && decimalPattern.test(grantAmount) && Number(normalized(grantAmount)) > 0;
    const selectedPurchase = purchases.find(p => p.id === purchaseId);
    const validCharge = !!selectedPurchase && decimalPattern.test(chargeAmount) && Number(normalized(chargeAmount)) > 0
        && Number(normalized(chargeAmount)) <= Number(selectedPurchase.amount);

    return <section id="household-personal-money" aria-labelledby="personal-money-heading" className="scroll-mt-24 space-y-5">
        <header className="space-y-1">
            <h3 id="personal-money-heading" className="text-lg font-semibold">{t('personalMoney.title')}</h3>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.description')}</p>
        </header>
        <nav aria-label={t('personalMoney.sections')}>
            <div className="lg:hidden">
                <AverioSectionSelect label={t('personalMoney.sections')} value={tab}
                    items={tabs.map(item => ({ value: item, label: t('personalMoney.compactTabs.' + item), icon: tabIcons[item] }))}
                    onChange={setTab} />
            </div>
            <div className="hidden gap-2 lg:grid lg:grid-cols-2 xl:grid-cols-3">
            {tabs.map(item => <button key={item} type="button" aria-current={tab === item ? 'page' : undefined}
                onClick={() => setTab(item)}
                className={`flex min-h-14 min-w-0 items-center gap-3 rounded-xl border-l-[3px] px-4 py-3 text-left text-sm font-semibold transition focus-visible:outline-2 focus-visible:outline-primary-500 ${tab === item
                    ? 'border-l-emerald-500 bg-emerald-100 text-emerald-900 ring-1 ring-emerald-300 dark:bg-emerald-950 dark:text-emerald-100 dark:ring-emerald-800'
                    : 'border-l-transparent bg-white hover:bg-emerald-50 dark:bg-[#0D1726] dark:hover:bg-emerald-950/30'}`}>
                <ConceptIcon name={tabIcons[item]} />
                <span className="min-w-0 wrap-normal">{t('personalMoney.tabs.' + item)}</span>
            </button>)}
            </div>
        </nav>
        {message && <p role="status" className="text-sm font-medium text-emerald-700 dark:text-emerald-300">{message}</p>}
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error}</p>}
        {canGrant && tab === 'owner' && <AverioCard className="space-y-4 p-5 sm:p-6">
            <header className="space-y-2">
                <span className="inline-flex rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-900 dark:bg-emerald-900 dark:text-emerald-100">{t('personalMoney.ownerArea')}</span>
                <h4 className="text-xl font-semibold">{t('personalMoney.newGrant')}</h4>
                <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.ownerHelp')}</p>
            </header>
            <form className="grid gap-4"
            onSubmit={event => { event.preventDefault(); if (validGrant && !grant.isPending) grant.mutate(); }}>
            <div className="grid gap-4 sm:grid-cols-2">
                <AverioField>{t('personalMoney.recipient')}
                    <AverioSelect value={recipient} onChange={e => { setRecipient(e.target.value); resetGrantIntent(); }} required>
                        {household.members.map(member => <option key={member.id} value={member.id}>{member.displayName || t('householdSettings.unnamedMember')}</option>)}
                    </AverioSelect>
                </AverioField>
                <AverioField>{t('personalMoney.amount')}
                    <AverioInput inputMode="decimal" value={grantAmount} required pattern="[0-9]+([.,][0-9]{1,4})?"
                        onChange={e => { setGrantAmount(e.target.value); resetGrantIntent(); }} />
                </AverioField>
                <AverioField>{t('personalMoney.currency')}
                    <AverioSelect value={currency} onChange={e => { setCurrency(e.target.value); resetGrantIntent(); }}>
                        {['EUR', 'USD', 'HUF'].map(code => <option key={code} value={code}>{code}</option>)}
                    </AverioSelect>
                </AverioField>
                <AverioField>{t('personalMoney.date')}
                    <AverioInput type="date" value={date} required onChange={e => { setDate(e.target.value); resetGrantIntent(); }} />
                </AverioField>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.grantHelp')}</p>
            <div><AverioButton type="submit" variant="primary" disabled={grant.isPending || !validGrant}>{t(grant.isPending ? 'common.actions.saving' : 'personalMoney.grant')}</AverioButton></div>
        </form></AverioCard>}
        <Activity mode={tab !== 'boundary' && tab !== 'owner' ? 'visible' : 'hidden'}>
        <AverioCard className="@container space-y-5 p-5 sm:p-6">
        {tab !== 'mine' && mine.isPending && <p role="status">{t('common.status.loading')}</p>}
        {tab !== 'mine' && mine.isError && <div role="alert" className="space-y-2">
            <p>{t('personalMoney.loadError')}</p>
            <AverioButton onClick={() => void mine.refetch()}>{t('common.actions.retry')}</AverioButton>
        </div>}
        {tab !== 'mine' && mine.isSuccess && !mine.data &&
            <p className="rounded-xl border border-dashed border-slate-300 p-5 text-sm dark:border-slate-700">{t('personalMoney.empty')}</p>}
        {tab === 'mine' && <header className="space-y-1">
            <h4 className="text-xl font-semibold">{t('personalMoney.myWallet')}</h4>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.walletHelp')}</p>
        </header>}
        {tab === 'mine' && (mine.isPending ? <p role="status">{t('common.status.loading')}</p> : mine.isError
            ? <div role="alert" className="space-y-2"><p>{t('personalMoney.loadError')}</p>
                <AverioButton onClick={() => { void mine.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>
            : mine.data ? <div className="grid grid-cols-1 gap-3 @min-[36rem]:grid-cols-3">
                <div className="rounded-xl border border-emerald-500/20 bg-emerald-50 p-4 dark:bg-emerald-950/20">
                    <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.available')}</p>
                    <p className="mt-2 break-words text-2xl font-bold">{money(mine.data.available, mine.data.currency)}</p>
                </div>
                <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                    <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.funded')}</p>
                    <p className="mt-2 break-words text-lg font-semibold">{money(mine.data.attributedPrivateFunding, mine.data.currency)}</p>
                </div>
                <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                    <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.difference')}</p>
                    <p className="mt-2 break-words text-lg font-semibold">{money(mine.data.fundingDifference, mine.data.currency)}</p>
                </div>
            </div> : <p className="rounded-xl border border-dashed border-slate-300 p-5 text-sm text-slate-600 dark:border-slate-700 dark:text-slate-300">{t('personalMoney.empty')}</p>)}
        {tab === 'charges' && mine.data && <form className="grid gap-4 rounded-xl border border-slate-200 p-4 dark:border-slate-700"
            onSubmit={event => { event.preventDefault(); if (validCharge && !charge.isPending) charge.mutate(); }}>
            <h4 className="font-semibold">{t('personalMoney.chargeTitle')}</h4>
            <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.chargeHelp')}</p>
            <AverioField>{t('personalMoney.purchase')}
                <AverioSelect value={purchaseId} disabled={charge.isPending} onChange={e => {
                    const id = e.target.value; setPurchaseId(id); setChargeOperationId(crypto.randomUUID());
                    setChargeAmount(purchases.find(p => p.id === id)?.amount ?? '');
                }}>
                    <option value="">{t('personalMoney.selectPurchase')}</option>
                    {purchases.map(p => <option key={p.id} value={p.id}>
                        {p.occurredOn} · {p.merchant || p.description} · {money(p.amount, p.currency)}
                    </option>)}
                </AverioSelect>
            </AverioField>
            <AverioField>{t('personalMoney.amount')}
                <AverioInput value={chargeAmount} disabled={charge.isPending} inputMode="decimal" pattern="[0-9]+([.,][0-9]{1,4})?"
                    onChange={e => { setChargeAmount(e.target.value); setChargeOperationId(crypto.randomUUID()); }} />
            </AverioField>
            <div><AverioButton type="submit" variant="primary" disabled={!validCharge || charge.isPending}>
                {t(charge.isPending ? 'common.actions.saving' : 'personalMoney.charge')}</AverioButton></div>
        </form>}
        <Activity mode={tab === 'charges' ? 'visible' : 'hidden'}>
        {mine.data && <section className="space-y-3" aria-label={t('personalMoney.existingCharges')}>
            <h4 className="font-semibold">{t('personalMoney.existingCharges')}</h4>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.correctionHelp')}</p>
            {existingCharges.length === 0 ? <p className="text-sm">{t('personalMoney.noCharges')}</p>
                : existingCharges.map(item => <ChargeEditor key={item.transactionId + ':' + item.version}
                    item={item} householdId={householdId} refresh={refresh} onError={setError}
                    label={transactions.data?.items.find(p => p.id === item.transactionId)?.merchant || t('personalMoney.purchase')}
                    formattedAmount={money(item.amount, mine.data!.currency)} />)}
        </section>}
        </Activity>
        <Activity mode={tab === 'reconcile' ? 'visible' : 'hidden'}>
        {mine.data && <PersonalMoneyReconciliationPanel userId={userId}
            householdId={householdId} wallet={mine.data} />}
        </Activity>
        {tab === 'mine' && mine.data && <div className="space-y-2">
            <h4 className="font-semibold">{t('personalMoney.history')}</h4>
            {mine.data.entries.length === 0
                ? <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.noEntries')}</p>
                : <ul className="divide-y divide-slate-200 dark:divide-slate-700">
                    {mine.data.entries.slice(0, 20).map(entry => <li key={entry.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                        <span>{entry.occurredOn} · {t('personalMoney.kind.' + entry.kind)}</span>
                        <strong>{money(entry.amount, mine.data!.currency)}</strong>
                    </li>)}
                </ul>}
        </div>}
    </AverioCard>
    </Activity>
    {tab === 'boundary' && <HouseholdBoundaryFlowPanel householdId={householdId} />}
    </section>;
}

function ChargeEditor({ item, householdId, refresh, label, formattedAmount, onError }: {
    item: PersonalMoneyChargeResponse; householdId: string; refresh: () => Promise<void>;
    label: string; formattedAmount: string; onError: (message: string) => void;
}) {
    const { t } = useTranslation();
    const [amount, setAmount] = useState(item.amount);
    const [operationId, setOperationId] = useState(() => crypto.randomUUID());
    const [intent, setIntent] = useState<'save' | 'remove'>('save');
    const correction = useMutation({
        mutationFn: (target: string) => personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeChargesTransactionIdPut({
            householdId, transactionId: item.transactionId,
            correctPersonalMoneyChargeRequest: { amount: target, expectedAmount: item.amount,
                expectedVersion: item.version, operationId },
        }),
        onSuccess: async () => { onError(''); await refresh(); },
        // Refresh stale versions after a conflict while retaining retry IDs on network failures.
        onError: async (failure: Error) => { onError(failure.message); await refresh(); },
    });
    const valid = decimalPattern.test(amount) && Number(normalized(amount)) > 0
        && normalized(amount) !== item.amount;
    return <form className="space-y-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700"
        onSubmit={event => { event.preventDefault(); if (valid && !correction.isPending) correction.mutate(normalized(amount)); }}>
        <p className="text-sm font-semibold">{label} · {formattedAmount}</p>
        <a className="inline-block text-sm font-semibold text-emerald-700 underline focus-visible:outline-2 dark:text-emerald-300"
            href={'/app/transactions?transaction=' + encodeURIComponent(item.transactionId)}>{t('transactionLinks.details')}</a>
        <AverioField>{t('personalMoney.changedAmount')}
            <AverioInput inputMode="decimal" value={amount} disabled={correction.isPending}
                onChange={e => { setAmount(e.target.value); setIntent('save'); setOperationId(crypto.randomUUID()); }} />
        </AverioField>
        <div className="flex flex-wrap gap-2">
            <AverioButton type="submit" disabled={!valid || correction.isPending || intent === 'remove'}>{t('personalMoney.saveCharge')}</AverioButton>
            <AverioButton type="button" disabled={correction.isPending} onClick={() => {
                // A fresh intent gets a fresh ID; repeated remove retries reuse it.
                if (intent !== 'remove') { setIntent('remove'); setOperationId(crypto.randomUUID()); }
            }}>{t('personalMoney.removeCharge')}</AverioButton>
            {intent === 'remove' && <AverioButton type="button" disabled={correction.isPending}
                onClick={() => correction.mutate('0')}>{t('personalMoney.confirmUnlink')}</AverioButton>}
            {intent === 'remove' && <AverioButton type="button" disabled={correction.isPending} onClick={() => { setIntent('save'); setOperationId(crypto.randomUUID()); }}>{t('common.actions.cancel')}</AverioButton>}
        </div>
        {intent === 'remove' && <p className="text-sm">{t('personalMoney.unlinkHelp')}</p>}
        {correction.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{correction.error.message}</p>}
    </form>;
}
