import { useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioInput } from '../../shared/components/ui/AverioFormControls';
import { activityQueries } from './activityQueries';
import { ActivityEditor } from './ActivityEditor';

export function ActivitySelect({ userId, householdId, value, onChange, disabled, includeClosed = false, allowCreate = true, emptyLabel }: {
    userId: string; householdId: string; value: string; onChange: (id: string) => void;
    disabled?: boolean; includeClosed?: boolean; allowCreate?: boolean; emptyLabel?: string;
}) {
    const { t, i18n } = useTranslation();
    const id = useId();
    const query = useQuery(activityQueries.list(userId, householdId));
    const [open, setOpen] = useState(false);
    const [creating, setCreating] = useState(false);
    const [search, setSearch] = useState('');
    const selected = query.data?.find(x => x.id === value);
    const options = (query.data ?? []).filter(x => (includeClosed || !x.isClosed || x.id === value)
        && x.name.toLocaleLowerCase(i18n.language).includes(search.toLocaleLowerCase(i18n.language)))
        .sort((a, b) => a.name.localeCompare(b.name, i18n.language));
    return <div className="grid min-w-0 gap-2">
        <span id={id} className="text-sm font-semibold">{t('activities.activity')}</span>
        <AverioButton aria-labelledby={id + ' ' + id + '-value'} aria-haspopup="dialog" disabled={disabled}
            onClick={() => { setSearch(''); setOpen(true); }} className="w-full justify-between!">
            <span id={id + '-value'} className="truncate">{selected?.name ?? (value ? t('activities.unavailable') : emptyLabel ?? t('activities.none'))}</span>
            <span aria-hidden="true">⌄</span>
        </AverioButton>
        {open && <AverioDialog open title={t('activities.choose')} closeLabel={t('common.actions.close')}
            onDismiss={() => setOpen(false)} actions={<>
                {allowCreate && <AverioButton onClick={() => setCreating(true)}>{t('activities.create')}</AverioButton>}
                <AverioButton onClick={() => setOpen(false)}>{t('common.actions.done')}</AverioButton></>}>
            <div className="grid gap-3">
                <AverioInput type="search" aria-label={t('activities.search')} placeholder={t('activities.search')}
                    value={search} onChange={e => setSearch(e.target.value)} />
                <AverioButton onClick={() => { onChange(''); setOpen(false); }}>{emptyLabel ?? t('activities.none')}</AverioButton>
                {query.isPending && <p role="status">{t('common.status.loading')}</p>}
                {query.isError && <div role="alert"><p>{query.error.message}</p><AverioButton onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
                {query.isSuccess && options.length === 0 && <p>{t('activities.noChoices')}</p>}
                {query.isSuccess && options.map(x => <AverioButton key={x.id} aria-pressed={value === x.id} onClick={() => { onChange(x.id); setOpen(false); }}>
                    <span className="wrap-anywhere">{x.name}{x.isClosed ? ` · ${t('activities.closed')}` : ''}</span>
                </AverioButton>)}
            </div>
        </AverioDialog>}
        {creating && <ActivityEditor userId={userId} householdId={householdId} onClose={() => setCreating(false)}
            onCreated={newId => { onChange(newId); setOpen(false); }} />}
    </div>;
}
