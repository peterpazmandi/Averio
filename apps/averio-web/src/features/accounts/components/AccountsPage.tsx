import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountsQueries } from '../api/accountsQueries';
import { useAccountMutations } from '../api/useAccountMutations';
import { AccountCard } from './AccountCard';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import '../accounts.css';

export function AccountsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t } = useTranslation();
    const [includeArchived, setIncludeArchived] = useState(false);
    const [search, setSearch] = useState('');
    const [scope, setScope] = useState('All');
    const [currency, setCurrency] = useState('All');
    const [filtersOpen, setFiltersOpen] = useState(false);
    const searchInput = useRef<HTMLInputElement>(null);
    const filterToggle = useRef<HTMLButtonElement>(null);
    const filterCount = Number(scope !== 'All') + Number(currency !== 'All') + Number(includeArchived);
    const hasCriteria = search.length > 0 || filterCount > 0;
    const normalize = (value: string) => value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase().trim();
    function reset() { setSearch(''); setScope('All'); setCurrency('All'); setIncludeArchived(false); searchInput.current?.focus(); }
    const accounts = useQuery(accountsQueries.list(userId, householdId, includeArchived));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const mutations = useAccountMutations(userId, householdId);
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.archive.isPending || mutations.restore.isPending;
    const visible = (accounts.data ?? []).filter(account =>
        normalize(account.name).includes(normalize(search))
        && (scope === 'All' || (scope === 'Private' ? account.visibility === 'Private' : account.scope === scope))
        && (currency === 'All' || account.currency === currency));

    return (
        <section className="space-y-6" aria-labelledby="accounts-title">
            <header className="av-page-head">
                <div><h2 id="accounts-title" aria-label={t('app.accounts.title')}>{t('app.accounts.title')} <InfoPopover label={t('app.accounts.title')}>{t('app.accounts.description')}</InfoPopover></h2></div>
                <Link className="av-btn av-primary" to="/app/accounts/new">{t('accountManagement.create')}</Link>
            </header>
            {owners.isError && <div role="alert"><p className="av-error">{t('accountOwnership.loadRequired')}</p>
                <button className="av-btn" disabled={owners.isFetching} onClick={() => { void owners.refetch(); }}>{t('common.actions.retry')}</button></div>}
            <div className="av-account-search" role="search" aria-label={t('accountManagement.search')}>
                <div className="av-search-toolbar">
                    <div className="av-search-input">
                        <ConceptIcon name="search" />
                        <label className="sr-only" htmlFor="account-search">{t('accountManagement.search')}</label>
                        <input ref={searchInput} id="account-search" type="search" autoComplete="off"
                            placeholder={t('accountManagement.searchPlaceholder')} value={search}
                            onChange={event => setSearch(event.target.value)} />
                        {search && <button type="button" className="av-search-clear" aria-label={t('accountManagement.clearSearch')}
                            onClick={() => { setSearch(''); searchInput.current?.focus(); }}><ConceptIcon name="close" /></button>}
                    </div>
                    <button ref={filterToggle} type="button" className="av-btn av-filter-toggle" aria-expanded={filtersOpen}
                        aria-controls="account-filters" onClick={() => setFiltersOpen(open => !open)}>
                        <ConceptIcon name="filters" /><span>{t('accountManagement.filters')}</span>
                        {filterCount > 0 && <span className="av-filter-count">{filterCount}</span>}
                    </button>
                </div>
                <div id="account-filters" className="av-search-panel" hidden={!filtersOpen}>
                    <div className="av-account-filters">
                        <label htmlFor="account-filter-scope" className="space-y-1"><span className="block">{t('accountOwnership.scope')}</span>
                            <select id="account-filter-scope" className="av-select" value={scope} onChange={event => setScope(event.target.value)}>
                                <option value="All">{t('accountManagement.all')}</option>
                                <option value="Household">{t('accountOwnership.household')}</option>
                                <option value="Personal">{t('accountOwnership.personal')}</option>
                                <option value="Private">{t('accountOwnership.private')}</option>
                            </select></label>
                        <label htmlFor="account-filter-currency" className="space-y-1"><span className="block">{t('onboarding.account.currency')}</span>
                            <select id="account-filter-currency" className="av-select" value={currency} onChange={event => setCurrency(event.target.value)}>
                                <option value="All">{t('accountManagement.all')}</option>
                                {['EUR', 'USD', 'HUF'].map(value => <option key={value}>{value}</option>)}
                            </select></label>
                        <label className="av-check-row av-filter-archived"><input type="checkbox" checked={includeArchived}
                            onChange={event => setIncludeArchived(event.target.checked)} /><span>{t('accountManagement.showArchived')}</span></label>
                    </div>
                    <button type="button" className="av-btn av-filter-done" onClick={() => { setFiltersOpen(false); filterToggle.current?.focus(); }}>{t('accountManagement.showResults')}</button>
                </div>
                {(hasCriteria || accounts.isSuccess) && <div className="av-search-summary">
                    <div className="av-search-selection">
                        {accounts.isSuccess && <span role="status">{t('accountManagement.resultCount', { count: visible.length })}</span>}
                        {scope !== 'All' && <span className="av-badge">{t(scope === 'Private' ? 'accountOwnership.private' : scope === 'Personal' ? 'accountOwnership.personal' : 'accountOwnership.household')}</span>}
                        {currency !== 'All' && <span className="av-badge">{currency}</span>}
                        {includeArchived && <span className="av-badge">{t('accountManagement.archivedIncluded')}</span>}
                    </div>
                    {hasCriteria && <button type="button" className="av-search-reset" onClick={reset}>{t('accountManagement.resetSearch')}</button>}
                </div>}
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
