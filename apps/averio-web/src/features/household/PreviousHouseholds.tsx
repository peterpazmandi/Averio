import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount, subtractDecimalAmounts } from '../../shared/money/decimalAmount';
import { invitationApi, invitationKeys } from './invitationQueries';

export function PreviousHouseholds({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const [selected, setSelected] = useState<string | null>(null);
    const history = useQuery({ queryKey: invitationKeys.history(userId),
        queryFn: ({ signal }) => invitationApi.apiV1HouseholdHistoryGet({ signal }) });
    const former = useQuery({ queryKey: invitationKeys.former(userId, householdId),
        queryFn: ({ signal }) => invitationApi.apiV1HouseholdsHouseholdIdFormerMembersGet({ householdId }, { signal }) });
    const date = (value: string) => new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(value));
    return <>
        {(former.isError || (former.data?.length ?? 0) > 0) && <AverioCard className="space-y-3 p-5">
            <h3 className="text-lg font-semibold">{t('invitations.history.former')}</h3>
            {former.isError ? <><p role="alert">{t('invitations.loadError')}</p><AverioButton onClick={() => void former.refetch()}>{t('common.actions.retry')}</AverioButton></> : <ul className="space-y-2">{former.data?.map(member => <li key={member.id} className="break-words">{member.displayName || t('householdSettings.unnamedMember')} — {t('invitations.history.left', { date: date(member.leftAtUtc) })}</li>)}</ul>}
            <p className="text-sm">{t('invitations.history.attribution')}</p>
        </AverioCard>}
        <AverioCard className="space-y-4 p-5 sm:p-6"><h3 className="text-lg font-semibold">{t('invitations.history.title')}</h3>
            <p className="text-sm">{t('invitations.history.help')}</p>
            {history.isPending && <p role="status">{t('common.status.loading')}</p>}
            {history.isError && <><p role="alert">{t('invitations.loadError')}</p><AverioButton onClick={() => void history.refetch()}>{t('common.actions.retry')}</AverioButton></>}
            {history.isSuccess && history.data.length === 0 && <p>{t('invitations.history.empty')}</p>}
            <ul className="space-y-3">{history.data?.map(row => <li key={row.id} className="flex flex-wrap items-center justify-between gap-3">
                <div className="min-w-0"><p className="break-words font-semibold">{row.householdName}</p><p className="text-xs">{t('invitations.history.left', { date: date(row.leftAtUtc) })}</p></div>
                <AverioButton aria-expanded={selected === row.id} onClick={() => setSelected(old => old === row.id ? null : row.id)}>{t(selected === row.id ? 'invitations.history.close' : 'invitations.history.open')}</AverioButton>
            </li>)}</ul>
        </AverioCard>
        {selected && <HistoricalHousehold key={selected} userId={userId} id={selected} />}
    </>;
}
function HistoricalHousehold({ userId, id }: { userId: string; id: string }) {
    const { t, i18n } = useTranslation();
    const [from, setFrom] = useState('');
    const [to, setTo] = useState('');
    const query = useQuery({ queryKey: invitationKeys.snapshot(userId, id, from, to), queryFn: ({ signal }) => invitationApi.apiV1HouseholdHistoryIdGet({ id, from: from || undefined, to: to || undefined }, { signal }), placeholderData: old => old });
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.language);
    if (query.isPending) return <p role="status">{t('common.status.loading')}</p>;
    if (query.isError) return <AverioCard className="space-y-3 p-5"><p role="alert">{t('invitations.loadError')}</p><AverioButton onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton></AverioCard>;
    const snapshot = query.data;
    const accountNames = new Map(snapshot.accounts.map(account => [account.id, account.name]));
    const transactions = snapshot.transactions;
    const transfers = snapshot.transfers;
    return <section className="min-w-0 space-y-4" aria-label={t('invitations.history.title')}>
        <AverioCard className="space-y-4 p-5"><h3 className="break-words text-lg font-semibold">{snapshot.summary.householdName}</h3>
            <p role="status">{t('invitations.history.readOnly', { date: new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(snapshot.summary.leftAtUtc)) })}</p>
            <p className="text-sm">{t('invitations.history.frozen')}</p>
            <div className="grid gap-3 sm:grid-cols-2"><AverioField>{t('invitations.history.from')}<AverioInput type="date" value={from} max={to || undefined} onChange={event => setFrom(event.target.value)} /></AverioField>
                <AverioField>{t('invitations.history.to')}<AverioInput type="date" value={to} min={from || undefined} onChange={event => setTo(event.target.value)} /></AverioField></div>
            {query.isFetching && <p role="status">{t('common.status.loading')}</p>}
            <h4 className="font-semibold">{t('invitations.history.cashFlow')}</h4><p className="text-xs">{t('invitations.history.reportHelp')}</p>
            {snapshot.cashFlow.map(total => <dl key={total.currency} className="grid gap-2 sm:grid-cols-3">
                <div><dt>{t('invitations.history.income')}</dt><dd className="font-semibold">{money(total.income, total.currency)}</dd></div>
                <div><dt>{t('invitations.history.expense')}</dt><dd className="font-semibold">{money(total.expense, total.currency)}</dd></div>
                <div><dt>{t('invitations.history.net')}</dt><dd className="font-semibold">{money(subtractDecimalAmounts(total.income, total.expense), total.currency)}</dd></div>
            </dl>)}
            {snapshot.cashFlow.length === 0 && <p>{t('invitations.history.noEntries')}</p>}
        </AverioCard>
        <AverioCard className="space-y-3 p-5"><h4 className="font-semibold">{t('invitations.history.accounts')}</h4><ul className="divide-y divide-slate-200 dark:divide-slate-700">{snapshot.accounts.map(account => <li key={account.id} className="space-y-1 py-3">
            <p className="break-words font-semibold">{account.name}</p><p>{money(account.balance, account.currency)}</p>
            {account.wasCopied && <p className="text-xs">{t('invitations.history.copied')}</p>}
        </li>)}</ul></AverioCard>
        <AverioCard className="space-y-3 p-5"><h4 className="font-semibold">{t('invitations.history.transactions')}</h4>
            {transactions.length === 0 && <p>{t('invitations.history.noEntries')}</p>}
            {transactions.map(row => <details key={row.id} className="min-w-0 border-b border-slate-200 py-3 dark:border-slate-700"><summary className="cursor-pointer break-words">{row.occurredOn} · {row.description} · {row.kind === 'Income' ? '+' : '−'}{money(row.amount, row.currency)} {row.isVoided && t('invitations.history.voided')}</summary>
                <div className="space-y-2 pt-3 text-sm"><p className="break-words">{accountNames.get(row.accountId)}</p>{row.note && <p className="whitespace-pre-wrap break-words">{row.note}</p>}
                    <ul className="space-y-1">{row.items.map((item, i) => <li key={i} className="break-words">{item.description} · {item.categoryName} · {money(item.amount, row.currency)}</li>)}</ul></div>
            </details>)}
        </AverioCard>
        <AverioCard className="space-y-3 p-5"><h4 className="font-semibold">{t('invitations.history.transfers')}</h4>
            {transfers.length === 0 && <p>{t('invitations.history.noEntries')}</p>}
            {transfers.map(row => <div key={row.id} className="space-y-1 border-b border-slate-200 py-3 text-sm dark:border-slate-700"><p>{row.occurredOn} {row.isVoided && t('invitations.history.voided')}</p>
                <p className="break-words">{row.source ? `${row.source.accountName}: ${money(row.source.amount, row.source.currency)}` : t('invitations.history.hidden')} → {row.destination ? `${row.destination.accountName}: ${money(row.destination.amount, row.destination.currency)}` : t('invitations.history.hidden')}</p>
                {row.note && <p className="whitespace-pre-wrap break-words">{row.note}</p>}
            </div>)}
        </AverioCard>
    </section>;
}
