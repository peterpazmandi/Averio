import { useEffect, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../authentication/context/AuthProvider';
import { logoutUser } from '../authentication/api/authApi';
import { saveOnboardingProfile } from '../onboarding/api/onboardingApi';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { ApiError } from '../../shared/api/ApiError';
import type { InvitationTokenRequest } from '../../shared/api/generated';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { invitationApi, invitationKeys } from './invitationQueries';
import { pendingInvitation, rememberInvitation, forgetInvitation } from './invitationContext';
import { refreshHouseholdContext } from './householdTransition';

export function InvitationPage() {
    const [token, setToken] = useState(() => window.location.hash.slice(1) || pendingInvitation() || '');
    useEffect(() => {
        // Opening a replacement email link in this tab can be hash navigation,
        // which keeps the React page mounted. Capture it as a new invitation.
        const receiveLink = () => {
            const incoming = window.location.hash.slice(1);
            if (incoming) setToken(incoming);
        };
        window.addEventListener('hashchange', receiveLink);
        return () => window.removeEventListener('hashchange', receiveLink);
    }, []);
    useEffect(() => {
        rememberInvitation(token);
        window.history.replaceState(window.history.state, '', window.location.pathname);
    }, [token]);
    const auth = useAuth();
    const { t } = useTranslation();
    if (auth.status === 'loading') return <p role="status">{t('common.status.loading')}</p>;
    return <InvitationForUser key={`${auth.user?.userId ?? 'anonymous'}:${token}`} token={token} onClearToken={() => setToken('')} />;
}
function InvitationForUser({ token, onClearToken }: { token: string; onClearToken: () => void }) {
    const auth = useAuth();
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const navigate = useNavigate();
    const [confirmed, setConfirmed] = useState(false);
    const [declining, setDeclining] = useState(false);
    const [selectedId, setSelectedId] = useState('');
    const [handling, setHandling] = useState<Record<string, string>>({});
    const [successor, setSuccessor] = useState('');
    const [displayName, setDisplayName] = useState('');
    const pending = useQuery({ queryKey: invitationKeys.pending(auth.user?.userId),
        queryFn: ({ signal }) => invitationApi.apiV1InvitationsPendingGet({ signal }),
        enabled: auth.status === 'authenticated' && !token });
    const invitationId = pending.data?.some(row => row.id === selectedId) ? selectedId
        : pending.data?.length === 1 ? pending.data[0].id : undefined;
    const preview = useMutation({ mutationFn: (request: InvitationTokenRequest) => invitationApi.apiV1InvitationsPreviewPost({ invitationTokenRequest: request }),
        onSuccess: () => { setConfirmed(false); setHandling({}); setSuccessor(''); } });
    // Consent belongs to the exact invitation reviewed, even if discovery refreshes.
    const reviewCurrent = !!preview.data && preview.variables?.token === (token || null)
        && preview.variables?.invitationId === invitationId;
    const review = reviewCurrent ? preview.data : undefined;
    const departure = review?.departure;
    const accept = useMutation({
        mutationFn: async () => {
            if (!reviewCurrent || !preview.variables) throw new Error(t('invitations.review'));
            if (!auth.user?.displayName && displayName.trim()) await saveOnboardingProfile({ displayName: displayName.trim() });
            return invitationApi.apiV1InvitationsAcceptancesPost({ acceptInvitationRequest: {
                token: preview.variables.token, invitationId: preview.variables.invitationId ?? null, expectedSourceVersion: departure?.version ?? '00000000-0000-0000-0000-000000000000',
                confirmed, successorMemberId: successor || null,
                accounts: departure?.accounts.map(account => ({ accountId: account.id, expectedVersion: account.version,
                    openingBalance: account.balance, handling: handling[account.id] })) ?? [],
            } });
        },
        onSuccess: async () => {
            await refreshHouseholdContext(client, auth.refresh);
            await navigate({ to: '/app/household' });
            forgetInvitation();
        },
    });
    const signOut = useMutation({ mutationFn: async () => { await logoutUser(); await auth.clear(); } });
    const decline = useMutation({ mutationFn: () => {
        if (!reviewCurrent || !preview.variables) throw new Error(t('invitations.review'));
        return invitationApi.apiV1InvitationsDeclinationsPost({ invitationTokenRequest: preview.variables });
    },
        onSuccess: async () => {
            forgetInvitation();
            onClearToken();
            setSelectedId(''); setConfirmed(false); setHandling({}); setSuccessor(''); setDeclining(false);
            preview.reset(); accept.reset(); decline.reset();
            await client.invalidateQueries({ queryKey: invitationKeys.pending(auth.user?.userId) });
            await navigate({ to: auth.user?.onboardingRequired ? '/onboarding' : '/app/household' });
        } });
    const busy = preview.isPending || accept.isPending || signOut.isPending || decline.isPending;
    const error = accept.error ?? preview.error ?? signOut.error ?? decline.error;
    const choicesComplete = !departure || (departure.accounts.every(account => !!handling[account.id]) && (!departure.requiresSuccessor || !!successor));
    return <AverioCard className="mx-auto w-full max-w-xl space-y-5 p-5 sm:p-6">
        <h1 className="text-2xl font-bold">{t('invitations.review')}</h1>
        <p>{t('invitations.joinHelp')}</p>
        {auth.status === 'authenticated' ? <div className="space-y-5">
            <p className="break-all text-sm">{t('invitations.signedIn', { email: auth.user?.email })}</p>
            {!token && pending.isPending && <p role="status">{t('common.status.loading')}</p>}
            {!token && pending.isError && <><p role="alert">{t('invitations.loadError')}</p><AverioButton onClick={() => void pending.refetch()}>{t('common.actions.retry')}</AverioButton></>}
            {!token && pending.isSuccess && pending.data.length === 0 && <p role="alert">{t('invitations.missing')}</p>}
            {!token && (pending.data?.length ?? 0) > 1 && <AverioField>{t('invitations.migration.destination')}<AverioSelect disabled={busy} value={selectedId} onChange={event => { setSelectedId(event.target.value); preview.reset(); accept.reset(); }}>
                <option value="">{t('invitations.migration.choose')}</option>{pending.data?.map(row => <option key={row.id} value={row.id}>{row.householdName}</option>)}
            </AverioSelect></AverioField>}
            <div className="flex flex-wrap gap-3">
                {!!(token || invitationId) && <AverioButton variant="primary" disabled={busy} onClick={() => { accept.reset(); decline.reset(); setDeclining(false); preview.mutate({ token: token || null, invitationId }); }}>{t('invitations.review')}</AverioButton>}
                <AverioButton disabled={busy} onClick={() => signOut.mutate()}>{t('invitations.switchUser')}</AverioButton>
            </div>
            {review && <section className="space-y-5" aria-label={t('invitations.review')}>
                <h2 className="break-words text-xl font-semibold">{t('invitations.migration.joining', { name: review.householdName })}</h2>
                {review.alreadyJoined ? <p>{t('invitations.alreadyJoined')}</p> : <>
                    {!auth.user?.displayName && <AverioField>{t('invitations.migration.displayName')}<AverioInput maxLength={100} value={displayName} disabled={busy} onChange={event => setDisplayName(event.target.value)} /></AverioField>}
                    {departure && <>
                        {departure.householdName && <AverioCard className="space-y-3 p-4"><h3 className="font-semibold">{t('invitations.migration.leaving', { name: departure.householdName })}</h3>
                            <p>{t(departure.willArchive ? 'invitations.migration.archive' : 'invitations.migration.history')}</p></AverioCard>}
                        <p>{t('invitations.migration.noHistory')}</p>
                        {departure.accounts.map(account => <AverioCard key={account.id} className="space-y-3 p-4">
                            <h3 className="break-words font-semibold">{account.name}</h3><p className="text-sm">{t('invitations.migration.balance', { amount: formatDecimalAmount(account.balance, account.currency, i18n.language) })}</p>
                            <AverioField>{t('invitations.migration.handling')}<AverioSelect value={handling[account.id] ?? ''} disabled={busy} onChange={event => setHandling(old => ({ ...old, [account.id]: event.target.value }))}>
                                <option value="">{t('invitations.migration.choose')}</option><option value="private">{t('invitations.migration.private')}</option><option value="shared">{t('invitations.migration.shared')}</option><option value="household">{t('invitations.migration.household')}</option>
                            </AverioSelect></AverioField>
                            <p className="text-xs text-slate-600 dark:text-slate-300">{t('invitations.migration.' + (handling[account.id] === 'household' ? 'householdHelp' : handling[account.id] === 'shared' ? 'sharedHelp' : 'privateHelp'))}</p>
                        </AverioCard>)}
                        {departure.accountsStaying > 0 && <p>{t('invitations.migration.staying', { count: departure.accountsStaying })}</p>}
                        {departure.requiresSuccessor && <AverioField>{t('invitations.migration.successor')}<AverioSelect value={successor} disabled={busy} onChange={event => setSuccessor(event.target.value)}>
                            <option value="">{t('invitations.migration.choose')}</option>{departure.successors.map(member => <option key={member.id} value={member.id}>{member.displayName || t('householdSettings.unnamedMember')}</option>)}
                        </AverioSelect></AverioField>}
                    </>}
                    <label className="flex items-start gap-3"><input type="checkbox" checked={confirmed} disabled={busy} onChange={event => setConfirmed(event.target.checked)} className="mt-1 size-5 shrink-0" /><span>{t('invitations.confirm')}</span></label>
                </>}
                <div className="flex flex-wrap gap-3">
                    <AverioButton variant="primary" disabled={busy || !review.canJoin || (!review.alreadyJoined && (!confirmed || !choicesComplete))} onClick={() => accept.mutate()}>{t(review.alreadyJoined ? 'invitations.continue' : 'invitations.accept')}</AverioButton>
                    {!review.alreadyJoined && <AverioButton disabled={busy} onClick={() => setDeclining(true)}>{t('invitations.decline')}</AverioButton>}
                </div>
            </section>}
            <AverioDialog open={declining && reviewCurrent} title={t('invitations.decline')} closeLabel={t('householdSettings.cancel')} busy={busy} onDismiss={() => setDeclining(false)}
                actions={<AverioButton variant="danger" disabled={busy || !reviewCurrent} onClick={() => decline.mutate()}>{t('invitations.decline')}</AverioButton>}>
                <p>{t('invitations.declineHelp')}</p>{decline.error && <p role="alert">{decline.error instanceof ApiError ? decline.error.message : t('errors.genericDescription')}</p>}
            </AverioDialog>
        </div> : auth.status === 'error' ? <AverioButton onClick={() => void auth.refresh().catch(() => undefined)}>{t('common.actions.retry')}</AverioButton> : !token ? <p role="alert">{t('invitations.missing')}</p> : <div className="flex flex-wrap gap-4">
            <Link to="/login" search={{ redirect: '/invitation' }} className="font-semibold text-emerald-700 underline dark:text-emerald-300">{t('invitations.login')}</Link>
            <Link to="/register" className="font-semibold text-emerald-700 underline dark:text-emerald-300">{t('invitations.register')}</Link>
        </div>}
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error instanceof ApiError ? error.message : t('errors.genericDescription')}</p>}
        <Link to="/" onClick={forgetInvitation} className="block text-sm underline">{t('invitations.notNow')}</Link>
    </AverioCard>;
}
