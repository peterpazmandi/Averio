import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../authentication/context/AuthProvider';
import { accountsQueries } from '../api/accountsQueries';
import { useAccountMutations } from '../api/useAccountMutations';
import { ApiError } from '../../../shared/api/ApiError';
import { AccountForm } from './AccountForm';
import '../accounts.css';

export function AccountEditorPage({ accountId }: { accountId?: string }) {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <AccountEditor key={auth.user.userId + ':' + auth.user.household.householdId + ':' + (accountId ?? 'new')}
        userId={auth.user.userId} householdId={auth.user.household.householdId} accountId={accountId} />;
}

function AccountEditor({ userId, householdId, accountId }: { userId: string; householdId: string; accountId?: string }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const detail = useQuery({ ...accountsQueries.detail(userId, householdId, accountId ?? ''), enabled: !!accountId, retry: false });
    const mutations = useAccountMutations(userId, householdId);
    const pending = owners.isPending || (!!accountId && detail.isPending);
    const error = owners.error || (accountId ? detail.error : null);
    const account = accountId ? detail.data : undefined;
    const canEdit = !account || (account.status === 'Active' && (account.scope === 'Household'
        || account.owners.some(owner => owner.householdMemberId === owners.data?.currentMemberId)));
    const busy = mutations.create.isPending || mutations.update.isPending;
    function close() { void navigate({ to: '/app/accounts' }); }
    return (
        <section className="av-account-editor" aria-labelledby="account-editor-title">
            <header className="av-editor-heading">
                <Link to="/app/accounts" className="av-back-link" disabled={busy}>← {t('accountManagement.back')}</Link>
                <h2 id="account-editor-title">{t(accountId ? 'accountManagement.edit' : 'accountManagement.create')}</h2>
                <p>{account ? account.name : t('accountManagement.editorHelp')}</p>
            </header>
            {pending ? <p role="status">{t('common.status.loading')}</p> : error ? <div className="av-card space-y-3">
                <p role="alert" className="av-error">{error instanceof ApiError ? error.message : t('errors.genericDescription')}</p>
                <button className="av-btn" disabled={owners.isFetching || detail.isFetching} onClick={() => { void owners.refetch(); if (accountId) void detail.refetch(); }}>{t('common.actions.retry')}</button>
            </div> : !canEdit ? <p role="alert" className="av-card">{t(account?.status === 'Archived' ? 'accountManagement.restoreBeforeEdit' : 'accountManagement.readOnly')}</p>
                : owners.data && (!accountId || account) && <AccountForm account={account} options={owners.data}
                    onCreate={request => mutations.create.mutateAsync(request)}
                    onUpdate={(id, request) => mutations.update.mutateAsync({ id, request })} onClose={close} />}
        </section>
    );
}
