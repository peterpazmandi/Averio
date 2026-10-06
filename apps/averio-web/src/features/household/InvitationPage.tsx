import { useEffect, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../authentication/context/AuthProvider';
import { logoutUser } from '../authentication/api/authApi';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { invitationApi } from './invitationQueries';
import { pendingInvitation, rememberInvitation, forgetInvitation } from './invitationContext';

export function InvitationPage() {
    const [token] = useState(() => window.location.hash.slice(1) || pendingInvitation() || '');
    useEffect(() => {
        rememberInvitation(token);
        window.history.replaceState(window.history.state, '', window.location.pathname);
    }, [token]);
    const auth = useAuth();
    const { t } = useTranslation();
    if (auth.status === 'loading') return <p role="status">{t('common.status.loading')}</p>;
    return <InvitationForUser key={auth.user?.userId ?? 'anonymous'} token={token} />;
}
function InvitationForUser({ token }: { token: string }) {
    const auth = useAuth();
    const { t } = useTranslation();
    const client = useQueryClient();
    const navigate = useNavigate();
    const [confirmed, setConfirmed] = useState(false);
    const preview = useMutation({ mutationFn: () => invitationApi.apiV1InvitationsPreviewPost({ invitationTokenRequest: { token } }) });
    const accept = useMutation({
        mutationFn: () => invitationApi.apiV1InvitationsAcceptancesPost({ invitationTokenRequest: { token } }),
        onSuccess: async () => {
            forgetInvitation();
            await client.cancelQueries();
            client.clear();
            await auth.refresh();
            await navigate({ to: '/app/household' });
        },
    });
    const signOut = useMutation({ mutationFn: async () => { await logoutUser(); await auth.clear(); await client.cancelQueries(); client.clear(); } });
    const busy = preview.isPending || accept.isPending || signOut.isPending;
    const error = accept.error ?? preview.error ?? signOut.error;
    return <AverioCard className="mx-auto max-w-xl space-y-5 p-6">
        <h1 className="text-2xl font-bold">{t('invitations.review')}</h1>
        <p>{t('invitations.joinHelp')}</p>
        {!token ? <p role="alert">{t('invitations.missing')}</p> : auth.status === 'authenticated' ? <>
            <p className="break-all text-sm">{t('invitations.signedIn', { email: auth.user?.email })}</p>
            {!preview.data && <AverioButton variant="primary" disabled={busy} onClick={() => preview.mutate()}>{t('invitations.review')}</AverioButton>}
            {preview.data && <>
                <h2 className="break-words text-xl font-semibold">{preview.data.householdName}</h2>
                {preview.data.alreadyJoined ? <p>{t('invitations.alreadyJoined')}</p> : preview.data.canJoin ? <label className="flex items-start gap-3"><input type="checkbox" checked={confirmed} disabled={busy} onChange={event => setConfirmed(event.target.checked)} className="mt-1 size-5 shrink-0" /><span>{t('invitations.confirm')}</span></label> : <p role="alert">{t('invitations.blocked')}</p>}
                <AverioButton variant="primary" disabled={busy || !preview.data.canJoin || (!preview.data.alreadyJoined && !confirmed)} onClick={() => accept.mutate()}>{t(preview.data.alreadyJoined ? 'invitations.continue' : 'invitations.accept')}</AverioButton>
            </>}
            <AverioButton disabled={busy} onClick={() => signOut.mutate()}>{t('invitations.switchUser')}</AverioButton>
        </> : auth.status === 'error' ? <AverioButton onClick={() => void auth.refresh().catch(() => undefined)}>{t('common.actions.retry')}</AverioButton> : <div className="flex flex-wrap gap-4">
            <Link to="/login" search={{ redirect: '/invitation' }} className="font-semibold text-emerald-700 underline dark:text-emerald-300">{t('invitations.login')}</Link>
            <Link to="/register" className="font-semibold text-emerald-700 underline dark:text-emerald-300">{t('invitations.register')}</Link>
        </div>}
        {error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{error instanceof ApiError ? error.message : t('errors.genericDescription')}</p>}
        <Link to="/" onClick={forgetInvitation} className="block text-sm underline">{t('invitations.notNow')}</Link>
    </AverioCard>;
}
