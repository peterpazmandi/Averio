import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { notificationsApi, notificationKeys, type NotificationItem } from './notificationsApi';

function notificationDestination(row: NotificationItem): '/app/money-back' | '/app/recurring' {
    return row.type.startsWith('Reimbursement') ? '/app/money-back' : '/app/recurring';
}

export function NotificationsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const list = useQuery({ queryKey: notificationKeys.list(userId, householdId), queryFn: () => notificationsApi.list(householdId),
        refetchInterval: 60_000, refetchOnWindowFocus: true });
    const refresh = () => client.invalidateQueries({ queryKey: ['notifications', userId] });
    const read = useMutation({ mutationFn: notificationsApi.read, onSuccess: refresh });
    const dismiss = useMutation({ mutationFn: notificationsApi.dismiss, onSuccess: refresh });
    const readAll = useMutation({ mutationFn: () => notificationsApi.readAll(householdId), onSuccess: refresh });
    return <section className="grid gap-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="text-2xl font-bold">{t('inbox.title')}</h2>
            <AverioButton disabled={!list.data?.some(n => !n.readAt) || readAll.isPending}
                onClick={() => readAll.mutate()}>{t('inbox.readAll')}</AverioButton>
        </div>
        {list.isPending && <p role="status">{t('common.status.loading')}</p>}
        {list.isError && <p role="alert">{t('inbox.error')} <AverioButton onClick={() => void list.refetch()}>{t('common.actions.retry')}</AverioButton></p>}
        {(read.isError || dismiss.isError || readAll.isError) && <p role="alert">{t('inbox.error')}</p>}
        {list.isSuccess && list.data.length === 0 && <AverioCard className="py-10 text-center">
            <span aria-hidden="true" className="text-4xl text-emerald-500">✓</span>
            <h3 className="mt-3 text-xl font-bold">{t('inbox.empty')}</h3>
            <p className="mt-2">{t('inbox.emptyHelp')}</p>
        </AverioCard>}
        {list.data?.map(row => <AverioCard key={row.id} className="flex flex-wrap items-center justify-between gap-3">
            <div className="min-w-0">
                <div className="flex items-center gap-2">
                    {!row.readAt && <span className="size-2 rounded-full bg-emerald-500" aria-label={t('inbox.unread')} />}
                    <h3 className="font-semibold">{t(`inbox.types.${row.type}`, { defaultValue: t('inbox.generic') })}</h3>
                </div>
                <time className="text-sm text-slate-500 dark:text-slate-400" dateTime={row.createdAt}>
                    {new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(row.createdAt))}
                </time>
            </div>
            <div className="flex flex-wrap items-center gap-2">
                <Link to={notificationDestination(row)} onClick={() => { if (!row.readAt) read.mutate(row.id); }}
                    className="inline-flex min-h-11 items-center rounded-xl px-3 font-semibold text-emerald-700 focus-visible:outline-2 dark:text-emerald-300">
                    {t('inbox.review')}
                </Link>
                {!row.readAt && <AverioButton disabled={read.isPending} onClick={() => read.mutate(row.id)}>{t('inbox.markRead')}</AverioButton>}
                <AverioButton disabled={dismiss.isPending} onClick={() => dismiss.mutate(row.id)}>{t('inbox.dismiss')}</AverioButton>
            </div>
        </AverioCard>)}
    </section>;
}
