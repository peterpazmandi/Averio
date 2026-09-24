import { useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CreateTransferRequest, TransferResponse, TransferSideResponse } from '../../shared/api/generated';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { TransferEditor } from './TransferEditor';
import { transferApi, transferKeys, useTransferMutations } from './transferQueries';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';

export function TransfersPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const [accountId, setAccountId] = useState('');
    const [from, setFrom] = useState('');
    const [to, setTo] = useState('');
    const [editor, setEditor] = useState<TransferResponse | 'new' | null>(null);
    const [notice, setNotice] = useState('');
    const accounts = useQuery(accountsQueries.list(userId, householdId, true));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const writable = (accounts.data ?? []).filter(a => a.scope === 'Household'
        || a.owners.some(o => o.householdMemberId === owners.data?.currentMemberId));
    const mutations = useTransferMutations(userId, householdId);
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.void.isPending;
    const validDates = !from || !to || from <= to;
    const transfers = useInfiniteQuery({
        queryKey: transferKeys.list(userId, householdId, accountId, from, to),
        enabled: validDates,
        initialPageParam: undefined as { cursorDate: string; cursorCreatedAt: string; cursorId: string } | undefined,
        queryFn: ({ pageParam, signal }) => transferApi.apiV1HouseholdsHouseholdIdTransfersGet({ householdId,
            accountId: accountId || undefined, from: from || undefined, to: to || undefined,
            pageSize: 40, ...pageParam }, { signal }),
        getNextPageParam: page => {
            const last = page.items.at(-1);
            return page.hasMore && last ? { cursorDate: last.occurredOn, cursorCreatedAt: last.createdAt, cursorId: last.id } : undefined;
        },
    });
    const rows = transfers.data?.pages.flatMap(p => p.items) ?? [];
    const language = i18n.resolvedLanguage ?? i18n.language;
    const dateFormat = new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeZone: 'UTC' });
    function side(value: TransferSideResponse | null | undefined) {
        return <div className="grid min-w-0 gap-2 wrap-anywhere last:text-right">
            <span>{value?.accountName ?? t('transfers.privateAccount')}</span>
            {value && <strong className="text-[clamp(16px,3vw,23px)] tabular-nums group-data-[voided=true]:line-through">{formatDecimalAmount(value.amount, value.currency, language)}</strong>}
        </div>;
    }
    async function save(request: CreateTransferRequest) {
        if (editor === 'new') await mutations.create.mutateAsync(request);
        else if (editor) await mutations.update.mutateAsync({ id: editor.id, request: {
            sentAmount: request.sentAmount, receivedAmount: request.receivedAmount,
            occurredOn: request.occurredOn, note: request.note, expectedUpdatedAt: editor.updatedAt,
        } });
        setEditor(null); setNotice(t('transfers.saved'));
    }
    async function voidCurrent() {
        if (!editor || editor === 'new') return;
        await mutations.void.mutateAsync({ id: editor.id, expectedUpdatedAt: editor.updatedAt });
        setEditor(null); setNotice(t('transfers.voidedNotice'));
    }
    return <section className="grid min-w-0 gap-5" aria-labelledby="transfer-title">
        <header className="flex flex-col items-start justify-between gap-4 min-[621px]:flex-row min-[621px]:items-end"><div><h2 id="transfer-title" className="m-0 text-[27px] font-bold tracking-tight">{t('transfers.title')}</h2>
            <p className="mt-1.5 max-w-[780px] text-xs leading-relaxed text-[#667085] dark:text-[#AAB4C4]">{t('transfers.description')}</p></div>
            <AverioButton variant="primary" type="button" disabled={accounts.isPending || owners.isPending || accounts.isError || owners.isError}
                onClick={() => setEditor('new')}><ConceptIcon name="plus" />{t('transfers.add')}</AverioButton>
        </header>
        <div className="min-w-0 rounded-[18px] border border-[#E4E7EC] bg-white shadow-[0_10px_30px_rgba(16,24,40,0.06)] dark:border-[#233044] dark:bg-[#0D1726] dark:shadow-[0_10px_34px_rgba(0,0,0,0.2)] grid grid-cols-1 gap-4 p-5 min-[621px]:grid-cols-3">
            <AverioField>{t('transfers.account')}<AverioSelect value={accountId} onChange={e => setAccountId(e.target.value)}>
                <option value="">{t('transfers.allAccounts')}</option>
                {(accounts.data ?? []).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </AverioSelect></AverioField>
            <AverioField>{t('transfers.fromDate')}<AverioInput type="date" value={from} onChange={e => setFrom(e.target.value)} /></AverioField>
            <AverioField>{t('transfers.toDate')}<AverioInput type="date" value={to} onChange={e => setTo(e.target.value)} /></AverioField>
        </div>
        {notice && <p role="status">{notice}</p>}
        {!validDates && <p role="alert">{t('transfers.invalidDates')}</p>}
        {(accounts.isError || owners.isError || transfers.isError) && <div role="alert">
            <p>{t('transfers.failed')}</p><AverioButton onClick={() => {
                void accounts.refetch(); void owners.refetch(); void transfers.refetch();
            }}>{t('transfers.retry')}</AverioButton></div>}
        {transfers.isPending && validDates && <p role="status">{t('transfers.loading')}</p>}
        {transfers.isSuccess && rows.length === 0 && <div className="min-w-0 rounded-[18px] border border-[#E4E7EC] bg-white shadow-[0_10px_30px_rgba(16,24,40,0.06)] dark:border-[#233044] dark:bg-[#0D1726] dark:shadow-[0_10px_34px_rgba(0,0,0,0.2)] grid justify-items-start gap-3 p-[22px]">
            <ConceptIcon name="transfers" /><h3>{t('transfers.empty')}</h3><p>{t('transfers.emptyHelp')}</p>
        </div>}
        <div className="grid gap-3.5">
            {rows.map(row => <article data-voided={row.isVoided} className="group min-w-0 rounded-[18px] border border-[#E4E7EC] bg-white shadow-[0_10px_30px_rgba(16,24,40,0.06)] dark:border-[#233044] dark:bg-[#0D1726] dark:shadow-[0_10px_34px_rgba(0,0,0,0.2)] p-4 min-[621px]:p-[22px] [&_p]:text-[13px] [&_p]:leading-relaxed [&_p]:wrap-anywhere" key={row.id}>
                <header className="flex items-center justify-between gap-3 text-[13px]"><time dateTime={row.occurredOn}>{dateFormat.format(new Date(row.occurredOn + 'T12:00:00Z'))}</time>
                    <span className="rounded-full border border-[#E4E7EC] px-2 py-1 text-xs dark:border-[#233044]">{t(row.isVoided ? 'transfers.voided' : 'transfers.posted')}</span></header>
                <div className="my-[18px] grid grid-cols-[minmax(0,1fr)_24px_minmax(0,1fr)] items-center gap-2.5 min-[621px]:gap-[18px] [&>svg]:text-primary-500">{side(row.source)}<ConceptIcon name="transfers" />{side(row.destination)}</div>
                {row.note && <p>{row.note}</p>}
                {(!row.source || !row.destination) && <p>{t('transfers.redactedHelp')}</p>}
                {row.canModify && !row.isVoided && <AverioButton disabled={!accounts.isSuccess || !owners.isSuccess}
                    onClick={() => setEditor(row)}>{t('transfers.edit')}</AverioButton>}
            </article>)}
        </div>
        {transfers.hasNextPage && <AverioButton disabled={transfers.isFetchingNextPage}
            onClick={() => { void transfers.fetchNextPage(); }}>{t('transfers.loadMore')}</AverioButton>}
        {editor === 'new' && writable.filter(a => a.status === 'Active').length < 2
            ? <div className="min-w-0 rounded-[18px] border border-[#E4E7EC] bg-white shadow-[0_10px_30px_rgba(16,24,40,0.06)] dark:border-[#233044] dark:bg-[#0D1726] dark:shadow-[0_10px_34px_rgba(0,0,0,0.2)] grid justify-items-start gap-3 p-[22px]" role="status"><p>{t('transfers.needAccounts')}</p>
                <AverioButton onClick={() => setEditor(null)}>{t('transfers.close')}</AverioButton></div>
            : editor && <TransferEditor key={editor === 'new' ? 'new' : editor.id}
                transfer={editor === 'new' ? null : editor} accounts={writable} busy={busy}
                onClose={() => setEditor(null)} onSave={save} onVoid={voidCurrent} />}
    </section>;
}
