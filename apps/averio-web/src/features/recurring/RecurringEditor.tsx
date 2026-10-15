import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { AccountResponse, RecurringRuleResponse, SaveRecurringRuleRequest } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { AverioCategorySelect } from '../../shared/components/ui/AverioCategorySelect';
import { transactionQueries } from '../transactions/api/transactionQueries';

export function RecurringEditor({ userId, householdId, rule, accounts, onSave, onClose }: {
    userId: string; householdId: string; rule: RecurringRuleResponse | null; accounts: AccountResponse[];
    onSave: (value: SaveRecurringRuleRequest) => Promise<void>; onClose: () => void;
}) {
    const { t } = useTranslation();
    const [draft, setDraft] = useState<SaveRecurringRuleRequest>(() => rule ? { ...rule, expectedVersion: rule.version } : {
        accountId: '', categoryId: '', name: '', amount: '', kind: 'Expense', visibility: 'Household',
        frequency: 'Monthly', startsOn: new Date().toISOString().slice(0, 10), endsOn: null,
        automatic: false, isPaused: false, isStopped: false,
    });
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [stopConsent, setStopConsent] = useState(false);
    const categories = useQuery(transactionQueries.categories(userId, householdId, draft.kind === 'Income' ? 'Income' : 'Expense'));
    const account = accounts.find(a => a.id === draft.accountId);
    function change<K extends keyof SaveRecurringRuleRequest>(key: K, value: SaveRecurringRuleRequest[K]) {
        setDraft(d => ({ ...d, [key]: value }));
    }
    async function submit(event: React.FormEvent) {
        event.preventDefault(); setBusy(true); setError('');
        try { await onSave(draft); } catch (e) { setError(e instanceof ApiError ? e.message : t('errors.genericDescription')); }
        finally { setBusy(false); }
    }
    return <AverioDialog open title={t(rule ? 'recurring.edit' : 'recurring.add')} closeLabel={t('recurring.cancel')} busy={busy} onDismiss={onClose}
        actions={<><AverioButton disabled={busy} onClick={onClose}>{t('recurring.cancel')}</AverioButton>
            <AverioButton form="recurring-editor" type="submit" variant={draft.isStopped ? 'danger' : 'primary'}
                disabled={busy || !draft.categoryId || !draft.accountId || (draft.isStopped && !stopConsent)}>{t(busy ? 'recurring.pending' : 'recurring.save')}</AverioButton></>}>
        <form id="recurring-editor" className="grid gap-4" onSubmit={e => { void submit(e); }}>
            {error && <p role="alert">{error} {t('recurring.updatedElsewhere')}</p>}
            <AverioCard><h3 className="mb-4 font-bold">{t('recurring.details')}</h3><div className="grid gap-4 sm:grid-cols-2">
                <AverioField>{t('recurring.name')}<AverioInput required maxLength={100} value={draft.name} disabled={busy} onChange={e => change('name', e.target.value)} /></AverioField>
                <AverioField>{t('recurring.kind')}<AverioSelect value={draft.kind} disabled={busy || !!rule} onChange={e => { change('kind', e.target.value); change('categoryId', ''); }}>
                    <option value="Expense">{t('recurring.Expense')}</option><option value="Income">{t('recurring.Income')}</option></AverioSelect></AverioField>
                <AverioField>{t('recurring.account')}<AverioSelect required value={draft.accountId} disabled={busy || !!rule} onChange={e => {
                    const a = accounts.find(a => a.id === e.target.value); change('accountId', e.target.value); change('visibility', a?.visibility === 'Private' ? 'Private' : 'Household');
                }}><option value="">—</option>{accounts.map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}</AverioSelect></AverioField>
                <AverioField>{t('recurring.amount')}{account ? ' · ' + account.currency : ''}<AverioInput required inputMode="decimal" pattern="[0-9]+([.,][0-9]{1,4})?" value={draft.amount} disabled={busy}
                    onChange={e => change('amount', e.target.value.replace(',', '.'))} /></AverioField>
                <div><span className="mb-2 block text-[13px] font-semibold">{t('recurring.category')}</span>
                    {categories.isPending ? <p role="status">{t('recurring.loading')}</p> : categories.isError ? <AverioButton onClick={() => { void categories.refetch(); }}>{t('recurring.retry')}</AverioButton> :
                    <AverioCategorySelect label={t('recurring.category')} categories={(categories.data ?? []).filter(c => !c.isArchived)} contextCategories={categories.data ?? []}
                        value={draft.categoryId} onChange={id => change('categoryId', id)} disabled={busy} />}</div>
                <AverioField>{t('recurring.visibility')}<AverioSelect value={draft.visibility} disabled={busy || account?.scope === 'Household' || account?.visibility === 'Private'} onChange={e => change('visibility', e.target.value)}>
                    <option value="Household">{t('recurring.Household')}</option><option value="Private">{t('recurring.Private')}</option></AverioSelect></AverioField>
            </div></AverioCard>
            <AverioCard><h3 className="mb-4 font-bold">{t('recurring.timing')}</h3><div className="grid gap-4 sm:grid-cols-2">
                <AverioField>{t('recurring.frequency')}<AverioSelect value={draft.frequency} disabled={busy || !!rule} onChange={e => change('frequency', e.target.value)}>
                    {['Weekly', 'Monthly', 'Yearly'].map(f => <option key={f} value={f}>{t('recurring.' + f)}</option>)}</AverioSelect></AverioField>
                <AverioField>{t('recurring.starts')}<AverioInput required type="date" value={draft.startsOn} disabled={busy || !!rule} min={!rule ? new Date().toISOString().slice(0, 10) : undefined} onChange={e => change('startsOn', e.target.value)} /></AverioField>
                <AverioField>{t('recurring.ends')}<AverioInput type="date" min={draft.startsOn} value={draft.endsOn ?? ''} disabled={busy} onChange={e => change('endsOn', e.target.value || null)} /></AverioField>
            </div><p className="mt-3 text-xs">{t('recurring.datesHelp')}</p>
            <label className="mt-4 flex min-h-11 items-center gap-3 font-semibold"><input type="checkbox" className="size-4 accent-emerald-500" checked={draft.automatic ?? false} disabled={busy} onChange={e => change('automatic', e.target.checked)} />{t('recurring.automatic')}</label>
            <p className="text-xs">{t(draft.automatic ? 'recurring.automaticHelp' : 'recurring.manualHelp')}</p>
            {rule && <><label className="mt-3 flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 accent-emerald-500" checked={draft.isPaused ?? false} disabled={busy} onChange={e => change('isPaused', e.target.checked)} />{t('recurring.pause')}</label>
                <label className="flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 accent-red-600" checked={draft.isStopped ?? false} disabled={busy} onChange={e => { change('isStopped', e.target.checked); setStopConsent(false); }} />{t('recurring.stop')}</label>
                {draft.isStopped && <div className="rounded-xl border border-red-300 p-3"><p>{t('recurring.stopHelp')}</p><label className="mt-2 flex min-h-11 items-center gap-3"><input required type="checkbox" checked={stopConsent} onChange={e => setStopConsent(e.target.checked)} />{t('recurring.stopConfirm')}</label></div>}</>}
            </AverioCard><p className="text-xs">{t('recurring.privacyHelp')}</p>
        </form>
    </AverioDialog>;
}
