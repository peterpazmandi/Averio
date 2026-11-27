import { useState, useTransition } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { ApiError } from '../../../shared/api/ApiError';
import type { SavedTransactionViewResponse } from '../../../shared/api/generated';
import { transactionSearch, type TransactionSearch } from '../transactionSearch';
import { savedFilters } from '../transactionDates';
import { savedViewQueries, savedViewsApi } from '../api/savedViewQueries';

type Action = 'save' | 'rename' | 'update' | 'delete';
export function SavedViews({ userId, householdId, state, invalid, onApply, controlsVisible = true }: {
    userId: string; householdId: string; state: TransactionSearch; invalid: boolean;
    controlsVisible?: boolean;
    onApply: (filters: Partial<TransactionSearch>) => Promise<void>;
}) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const query = useQuery(savedViewQueries.list(userId, householdId));
    const [isApplying, startApplying] = useTransition();
    const [managing, setManaging] = useState(false);
    const [managedId, setManagedId] = useState('');
    const [selectedId, setSelectedId] = useState('');
    // Snapshot the version and filter payload at dialog opening so background refetches cannot authorize an overwrite.
    const [dialog, setDialog] = useState<{ action: Action; view?: SavedTransactionViewResponse; filters: ReturnType<typeof savedFilters> } | null>(null);
    const [name, setName] = useState('');
    const [notice, setNotice] = useState('');
    const selected = query.data?.find(x => x.id === selectedId);
    const managed = query.data?.find(x => x.id === managedId);
    const current = savedFilters(state);
    const managedModified = managed && JSON.stringify(savedFilters(transactionSearch({ ...managed.filters }))) !== JSON.stringify(current);
    const modified = !isApplying && selected && JSON.stringify(savedFilters(transactionSearch({ ...selected.filters }))) !== JSON.stringify(current);
    const mutation = useMutation({
        mutationFn: async () => {
            if (!dialog) return;
            const view = dialog.view;
            if (dialog.action === 'delete' && view) {
                await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsViewIdDelete({ householdId, viewId: view.id, expectedVersion: view.version });
                setSelectedId(currentId => currentId === view.id ? '' : currentId);
            } else {
                const request = { name: name.trim(), filters: dialog.action === 'rename' && view ? view.filters : dialog.filters, expectedVersion: view?.version ?? null };
                if (view) {
                    await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsViewIdPut({ householdId, viewId: view.id, saveTransactionViewRequest: request });
                    if (dialog.action === 'update') setSelectedId(view.id);
                }
                else setSelectedId(await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsPost({ householdId, saveTransactionViewRequest: request }));
            }
        },
        onSuccess: async () => {
            await client.invalidateQueries({ queryKey: savedViewQueries.list(userId, householdId).queryKey });
            setNotice(t(dialog?.action === 'delete' ? 'savedViews.deleted' : 'savedViews.saved'));
            setDialog(null);
        },
    });
    const open = (action: Action) => { setManaging(false); mutation.reset(); setNotice(''); setName(action === 'save' ? '' : managed?.name ?? ''); setDialog({ action, view: action === 'save' ? undefined : managed, filters: current }); };
    const apply = (id: string) => {
        const view = query.data?.find(x => x.id === id);
        if (!view) return;
        // Selection and URL navigation form one transition, not a manual filter edit.
        startApplying(async () => {
            setSelectedId(id); setNotice('');
            await onApply({ ...transactionSearch({ ...view.filters }), dates: undefined, transaction: undefined, create: undefined, createKind: undefined });
        });
    };
    return <section className={controlsVisible || query.isError || modified || (selected && !selected.available) || notice ? 'my-3 min-w-0' : 'min-w-0'} aria-label={t('savedViews.heading')}>
        <div className={controlsVisible ? 'grid gap-2' : 'hidden'}>
            <div className="flex items-center gap-1">
                <span className="text-sm font-semibold">{t('savedViews.heading')}</span>
                <InfoPopover label={t('savedViews.heading')}>{t('savedViews.help')}</InfoPopover>
                <span className="text-xs text-slate-600 dark:text-slate-300">{t('savedViews.privateStatus')}</span>
            </div>
            {query.isPending && <span role="status" className="text-xs">{t('common.status.loading')}</span>}
            <AverioSelect aria-label={t('savedViews.heading')} value={selectedId} disabled={query.isPending || query.isError || isApplying}
                onChange={event => { if (event.target.value) apply(event.target.value); else setSelectedId(''); }}>
                <option value="">{t('savedViews.choose')}</option>
                {query.data?.map(view => <option key={view.id} value={view.id}>{view.name}</option>)}
            </AverioSelect>
            <div className="flex flex-wrap gap-2">
                <AverioButton disabled={invalid} onClick={() => open('save')}>{t('savedViews.save')}</AverioButton>
                {!!query.data?.length && <AverioButton disabled={isApplying} onClick={() => { setManagedId(selectedId); setManaging(true); }}>{t('savedViews.manage')}</AverioButton>}
            </div>
        </div>
        {modified && <span role="status" className="text-xs text-amber-700 dark:text-amber-300">{selected?.name} · {t('savedViews.modified')}</span>}
        {query.isError && <div role="alert">{t('savedViews.loadError')} <AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
        {selected && !selected.available && <p role="alert" className="mt-2 text-sm text-amber-800 dark:text-amber-200">{t('savedViews.unavailable')}</p>}
        {notice && <p role="status" className="mt-2 text-xs">{notice}</p>}
        {managing && <AverioDialog open title={t('savedViews.manage')} closeLabel={t('common.actions.close')}
            onDismiss={() => setManaging(false)} actions={<AverioButton onClick={() => setManaging(false)}>{t('common.actions.done')}</AverioButton>}>
            <AverioField>{t('savedViews.heading')}<AverioSelect aria-label={t('savedViews.heading')} value={managed?.id ?? ''} onChange={event => setManagedId(event.target.value)}>
                <option value="">{t('savedViews.choose')}</option>
                {query.data?.map(view => <option key={view.id} value={view.id}>{view.name}</option>)}
            </AverioSelect></AverioField>
            {managed && <div className="mt-4 flex flex-wrap gap-2">
                <AverioButton variant="primary" onClick={() => { apply(managed.id); setManaging(false); }}>{t('savedViews.apply')}</AverioButton>
                <AverioButton disabled={invalid || !managedModified} onClick={() => open('update')}>{t('savedViews.update')}</AverioButton>
                <AverioButton onClick={() => open('rename')}>{t('savedViews.rename')}</AverioButton>
                <AverioButton onClick={() => open('delete')}>{t('savedViews.delete')}</AverioButton>
            </div>}
        </AverioDialog>}
        {dialog && <AverioDialog open title={t('savedViews.' + dialog.action)} closeLabel={t('common.actions.close')} busy={mutation.isPending}
            onDismiss={() => setDialog(null)} actions={<>
                <AverioButton disabled={mutation.isPending} onClick={() => setDialog(null)}>{t('common.actions.cancel')}</AverioButton>
                <AverioButton form="saved-view-form" type="submit" variant={dialog.action === 'delete' ? 'danger' : 'primary'} disabled={mutation.isPending || (dialog.action !== 'delete' && !name.trim())}>{t('savedViews.' + dialog.action)}</AverioButton>
            </>}>
            <form id="saved-view-form" onSubmit={event => { event.preventDefault(); if (!mutation.isPending) mutation.mutate(); }} className="space-y-4">
                {dialog.action === 'delete' ? <p>{t('savedViews.confirmDelete', { name: dialog.view?.name })}</p> : dialog.action === 'update' ? <p>{t('savedViews.confirmUpdate')}</p> :
                    <AverioField>{t('savedViews.name')}<AverioInput autoFocus required maxLength={80} value={name} onChange={event => setName(event.target.value)} disabled={mutation.isPending} /></AverioField>}
                {mutation.isError && <div role="alert"><p>{mutation.error instanceof ApiError ? mutation.error.message : t('savedViews.failed')}</p>
                    <AverioButton onClick={async () => { await query.refetch(); setDialog(null); }}>{t('common.actions.retry')}</AverioButton></div>}
            </form>
        </AverioDialog>}
    </section>;
}
