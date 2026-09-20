import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../api/accountsQueries';
import { useAccountMutations } from '../api/useAccountMutations';
import { AccountCard } from './AccountCard';

export function AccountsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t } = useTranslation();
    const [includeArchived, setIncludeArchived] = useState(false);
    const [search, setSearch] = useState('');
    const [scope, setScope] = useState('All');
    const [currency, setCurrency] = useState('All');
    const accounts = useQuery(accountsQueries.list(userId, householdId, includeArchived));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const mutations = useAccountMutations(userId, householdId);
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.archive.isPending || mutations.restore.isPending;
    const visible = (accounts.data ?? []).filter(account =>
        account.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())
        && (scope === 'All' || (scope === 'Private' ? account.visibility === 'Private' : account.scope === scope))
        && (currency === 'All' || account.currency === currency));

    return (
        <section className="space-y-6" aria-labelledby="accounts-title">
            <header className="av-page-head">
                <div><h2 id="accounts-title">{t('app.accounts.title')}</h2>
                    <p>{t('app.accounts.description')}</p></div>
                <Link className="av-btn av-primary" to="/app/accounts/new">{t('accountManagement.create')}</Link>
            </header>
            {owners.isError && <div role="alert"><p className="av-error">{t('accountOwnership.loadRequired')}</p>
                <button className="av-btn" disabled={owners.isFetching} onClick={() => { void owners.refetch(); }}>{t('common.actions.retry')}</button></div>}
            <div className="av-account-filters">
                <label className="space-y-1"><span className="block">{t('accountManagement.search')}</span>
                    <input className="av-select" type="search" value={search} onChange={event => setSearch(event.target.value)} /></label>
                <label className="space-y-1"><span className="block">{t('accountOwnership.scope')}</span>
                    <select className="av-select" value={scope} onChange={event => setScope(event.target.value)}>
                        <option value="All">{t('accountManagement.all')}</option>
                        <option value="Household">{t('accountOwnership.household')}</option>
                        <option value="Personal">{t('accountOwnership.personal')}</option>
                        <option value="Private">{t('accountOwnership.private')}</option>
                    </select></label>
                <label className="space-y-1"><span className="block">{t('onboarding.account.currency')}</span>
                    <select className="av-select" value={currency} onChange={event => setCurrency(event.target.value)}>
                        <option value="All">{t('accountManagement.all')}</option>
                        {['EUR', 'USD', 'HUF'].map(value => <option key={value}>{value}</option>)}
                    </select></label>
                <label className="av-check-row av-filter-archived"><input type="checkbox" checked={includeArchived}
                    onChange={event => setIncludeArchived(event.target.checked)} />{t('accountManagement.showArchived')}</label>
            </div>
            {accounts.isPending && <p role="status">{t('common.status.loading')}</p>}
            {accounts.isError && <div role="alert"><p className="av-error">{t('errors.genericDescription')}</p>
                <button className="av-btn" disabled={accounts.isFetching} onClick={() => { void accounts.refetch(); }}>{t('common.actions.retry')}</button></div>}
            {accounts.isSuccess && (visible.length === 0 ? <p className="av-subtle">{t('accountManagement.empty')}</p> :
                <ul className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    {visible.map(account => <li key={account.id} className="min-w-0"><AccountCard account={account}
                        currentMemberId={owners.data?.currentMemberId} busy={busy}
                        onArchive={() => mutations.archive.mutateAsync(account.id)}
                        onRestore={() => mutations.restore.mutateAsync(account.id)} /></li>)}
                </ul>)}
        </section>
    );
}
