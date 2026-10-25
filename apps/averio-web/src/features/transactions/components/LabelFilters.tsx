import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { accountsQueries } from '../../accounts/api/accountsQueries';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { savedViewQueries } from '../api/savedViewQueries';
import { AverioButton } from '../../../shared/components/ui/AverioButton';

export function LabelFilters({ userId, householdId, value, onChange }: {
    userId: string; householdId: string; value: string[]; onChange: (ids: string[]) => void;
}) {
    const { t } = useTranslation();
    const query = useQuery(savedViewQueries.labels(userId, householdId));
    const accounts = useQuery(accountsQueries.list(userId, householdId, true));
    const options = query.data ?? [];
    return <fieldset className="min-w-0 space-y-2 text-sm">
        <legend className="text-[13px] font-semibold">{t('savedViews.labels')} <InfoPopover label={t('savedViews.labels')}>{t('savedViews.labelsHelp')}</InfoPopover></legend>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <div role="alert">{t('savedViews.labelError')} <AverioButton onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
        {query.isSuccess && !options.length && <p>{t('savedViews.noLabels')}</p>}
        <div className="grid max-h-48 grid-cols-1 gap-2 overflow-y-auto sm:grid-cols-2 lg:grid-cols-3">
            {[...options, ...value.filter(id => !options.some(x => x.id === id)).map(id => ({ id, name: t('savedViews.unavailableLabel'), accountId: null }))].map(label => <label key={label.id} className="flex min-h-11 items-center gap-2 rounded-lg border border-slate-200 p-2 dark:border-slate-700 has-checked:border-emerald-500 has-checked:bg-emerald-50 dark:has-checked:bg-emerald-950">
                <input type="checkbox" checked={value.includes(label.id)} disabled={!value.includes(label.id) && value.length >= 20}
                    onChange={event => onChange(event.target.checked ? [...value, label.id].sort() : value.filter(id => id !== label.id))}
                    className="size-4 shrink-0 accent-emerald-500" />
                <span className="min-w-0 break-words">{label.name}{label.accountId && <span className="block text-xs text-slate-500 dark:text-slate-400">{accounts.data?.find(account => account.id === label.accountId)?.name}</span>}</span>
            </label>)}
        </div>
    </fieldset>;
}
