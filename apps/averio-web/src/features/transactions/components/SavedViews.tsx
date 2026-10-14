import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../../shared/components/ui/AverioFormControls';
import { AverioCard } from '../../../shared/components/ui/AverioCard';
import { ApiError } from '../../../shared/api/ApiError';
import type { SavedTransactionViewResponse } from '../../../shared/api/generated';
import { transactionSearch, type TransactionSearch } from '../transactionSearch';
import { savedFilters } from '../transactionDates';
import { savedViewQueries, savedViewsApi } from '../api/savedViewQueries';

type Action = 'save' | 'rename' | 'update' | 'delete';
export function SavedViews({ userId, householdId, state, invalid, onApply }: {
    userId: string; householdId: string; state: TransactionSearch; invalid: boolean;
    onApply: (filters: Partial<TransactionSearch>) => void;
}) {
    const { t } = useTranslation();
    const client = useQueryClient();
    const query = useQuery(savedViewQueries.list(userId, householdId));
    const [selectedId, setSelectedId] = useState('');
    // Snapshot the version and filter payload at dialog opening so background refetches cannot authorize an overwrite.
    const [dialog, setDialog] = useState<{ action: Action; view?: SavedTransactionViewResponse; filters: ReturnType<typeof savedFilters> } | null>(null);
    const [name, setName] = useState('');
    const [notice, setNotice] = useState('');
    const selected = query.data?.find(x => x.id === selectedId);
    const current = savedFilters(state);
    const modified = selected && JSON.stringify(savedFilters(transactionSearch({ ...selected.filters }))) !== JSON.stringify(current);
    const mutation = useMutation({
        mutationFn: async () => {
            if (!dialog) return;
            const view = dialog.view;
            if (dialog.action === 'delete' && view) {
                await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsViewIdDelete({ householdId, viewId: view.id, expectedVersion: view.version });
                setSelectedId('');
            } else {
                const request = { name: name.trim(), filters: dialog.action === 'rename' && view ? view.filters : dialog.filters, expectedVersion: view?.version ?? null };
                if (view) await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsViewIdPut({ householdId, viewId: view.id, saveTransactionViewRequest: request });
                else setSelectedId(await savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsPost({ householdId, saveTransactionViewRequest: request }));
            }
        },
        onSuccess: async () => {
            setNotice(t(dialog?.action === 'delete' ? 'savedViews.deleted' : 'savedViews.saved'));
            setDialog(null);
            await client.invalidateQueries({ queryKey: savedViewQueries.list(userId, householdId).queryKey });
        },
    });
    const open = (action: Action) => { mutation.reset(); setNotice(''); setName(action === 'save' ? '' : selected?.name ?? ''); setDialog({ action, view: action === 'save' ? undefined : selected, filters: current }); };
    const apply = (id: string) => {
        const view = query.data?.find(x => x.id === id);
        setSelectedId(id); setNotice('');
        if (view) onApply({ ...transactionSearch({ ...view.filters }), dates: undefined, transaction: undefined, create: undefined, createKind: undefined });
    };
    return <AverioCard className="my-4 space-y-3 p-4" aria-label={t('savedViews.heading')}>
        <h2 className="font-semibold">{t('savedViews.heading')}</h2>
        <p className="text-xs text-slate-500 dark:text-slate-400">{t('savedViews.help')}</p>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <div role="alert">{t('savedViews.loadError')} <AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
        {query.isSuccess && <>
            {!query.data.length && <p className="text-sm">{t('savedViews.empty')}</p>}
            {!!query.data.length && <AverioField>{t('savedViews.heading')}<AverioSelect value={selected?.id ?? ''} onChange={event => apply(event.target.value)}>
                <option value="">{t('savedViews.choose')}</option>
                {query.data.map(view => <option key={view.id} value={view.id}>{view.name}</option>)}
            </AverioSelect></AverioField>}
            {modified && <p role="status" className="text-xs font-semibold">{t('savedViews.modified')}</p>}
            {selected && !selected.available && <p role="alert" className="text-sm text-amber-800 dark:text-amber-200">{t('savedViews.unavailable')}</p>}
            <div className="flex flex-wrap gap-2">
                <AverioButton disabled={invalid} onClick={() => open('save')}>{t('savedViews.save')}</AverioButton>
                {selected && <>
                    <AverioButton disabled={invalid || !modified} onClick={() => open('update')}>{t('savedViews.update')}</AverioButton>
                    <AverioButton onClick={() => open('rename')}>{t('savedViews.rename')}</AverioButton>
                    <AverioButton onClick={() => open('delete')}>{t('savedViews.delete')}</AverioButton>
                </>}
            </div>
        </>}
        {notice && <p role="status">{notice}</p>}
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
    </AverioCard>;
}
