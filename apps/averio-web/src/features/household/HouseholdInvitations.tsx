import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { InvitationResponse } from '../../shared/api/generated';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { ApiError } from '../../shared/api/ApiError';
import { invitationApi, invitationKeys } from './invitationQueries';

export function HouseholdInvitations({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const [email, setEmail] = useState('');
    const [open, setOpen] = useState(false);
    const [revoke, setRevoke] = useState<InvitationResponse | null>(null);
    const key = invitationKeys.list(userId, householdId);
    const query = useQuery({ queryKey: key, queryFn: ({ signal }) => invitationApi.apiV1HouseholdsHouseholdIdInvitationsGet({ householdId }, { signal }) });
    const mutation = useMutation({
        mutationFn: async (action: { type: 'create' | 'resend' | 'revoke'; row?: InvitationResponse }) => {
            if (action.type === 'create') return invitationApi.apiV1HouseholdsHouseholdIdInvitationsPost({ householdId, createInvitationRequest: { email: email.trim() } });
            const args = { householdId, invitationId: action.row!.id, invitationVersionRequest: { expectedVersion: action.row!.version } };
            return action.type === 'resend' ? invitationApi.apiV1HouseholdsHouseholdIdInvitationsInvitationIdResendsPost(args)
                : invitationApi.apiV1HouseholdsHouseholdIdInvitationsInvitationIdRevocationsPost(args);
        },
        onSuccess: () => { setOpen(false); setRevoke(null); setEmail(''); },
        onSettled: async () => { await client.invalidateQueries({ queryKey: key }); },
    });
    const error = mutation.error && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{mutation.error instanceof ApiError ? mutation.error.message : t('errors.genericDescription')}</p>;
    return <AverioCard className="space-y-4 p-5 sm:p-6">
        <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-lg font-semibold">{t('invitations.title')}</h3>
            <AverioButton onClick={() => { mutation.reset(); setOpen(true); }}>{t('invitations.invite')}</AverioButton></div>
        <p className="text-sm text-slate-600 dark:text-slate-300">{t('invitations.help')}</p>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <div role="alert"><p>{t('invitations.loadError')}</p><AverioButton disabled={query.isFetching} onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton></div>}
        {query.isSuccess && query.data.length === 0 && <div className="space-y-2 py-5 text-center"><span aria-hidden="true" className="mx-auto flex size-16 items-center justify-center rounded-full bg-emerald-100 text-emerald-800"><ConceptIcon name="household" /></span><h4 className="font-semibold">{t('invitations.empty')}</h4><p className="text-sm">{t('invitations.help')}</p></div>}
        {!query.isError && <ul className="divide-y divide-slate-200 dark:divide-slate-700">{query.data?.map(row => <li key={row.id} className="space-y-2 py-4">
            <p className="break-all font-semibold">{row.email}</p>
            <p className="text-sm">{t('invitations.states.' + row.state)} · {t('invitations.delivery.' + row.deliveryStatus)}</p>
            <p className="text-xs text-slate-600 dark:text-slate-300">{t('invitations.expires', { date: new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(row.expiresAt)) })}</p>
            {(row.state === 'Pending' || row.state === 'Expired') && <div className="flex flex-wrap gap-2">
                <AverioButton disabled={mutation.isPending} onClick={() => mutation.mutate({ type: 'resend', row })}>{t('invitations.resend')}</AverioButton>
                <AverioButton disabled={mutation.isPending} onClick={() => { mutation.reset(); setRevoke(row); }}>{t('invitations.revoke')}</AverioButton>
            </div>}
        </li>)}</ul>}
        {!open && !revoke && error}
        <AverioDialog open={open} title={t('invitations.invite')} closeLabel={t('householdSettings.cancel')} onDismiss={() => setOpen(false)} busy={mutation.isPending}
            actions={<AverioButton form="invite-form" type="submit" variant="primary" disabled={mutation.isPending}>{t('invitations.send')}</AverioButton>}>
            <form id="invite-form" onSubmit={event => { event.preventDefault(); if (!mutation.isPending) mutation.mutate({ type: 'create' }); }}>
                <AverioCard className="space-y-4"><AverioField>{t('invitations.email')}<AverioInput type="email" required maxLength={254} value={email} disabled={mutation.isPending} onChange={event => setEmail(event.target.value)} /></AverioField><p className="text-sm">{t('invitations.help')}</p>{error}</AverioCard>
            </form>
        </AverioDialog>
        <AverioDialog open={revoke !== null} title={t('invitations.revoke')} closeLabel={t('householdSettings.cancel')} onDismiss={() => setRevoke(null)} busy={mutation.isPending}
            actions={<AverioButton variant="danger" disabled={mutation.isPending} onClick={() => revoke && mutation.mutate({ type: 'revoke', row: revoke })}>{t('invitations.revoke')}</AverioButton>}>
            <p>{t('invitations.revokeHelp')}</p>{error}
        </AverioDialog>
    </AverioCard>;
}
