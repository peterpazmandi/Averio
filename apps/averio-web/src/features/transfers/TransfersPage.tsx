import { useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CreateTransferRequest, TransferResponse } from '../../shared/api/generated';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { TransferEditor } from './TransferEditor';
import { TransferTimeline } from './TransferTimeline';
import { AverioFilterPanel } from '../../shared/components/ui/AverioFilterPanel';
import { transferApi, transferKeys, useTransferMutations } from './transferQueries';
import { AverioTransferIllustration } from '../../shared/components/ui/AverioTransferIllustration';
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
    const hasFilters = !!(accountId || from || to);
    const canCreate = accounts.isSuccess && owners.isSuccess;
    function resetFilters() { setAccountId(''); setFrom(''); setTo(''); }
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
            <div className="flex shrink-0 flex-wrap items-center gap-2">
                <AverioFilterPanel count={Number(!!accountId) + Number(!!from) + Number(!!to)}
                    labels={{ filters: t('transactionManagement.filters'), showResults: t('transactionManagement.showResults'),
                        reset: t('transactionManagement.reset'), summary: t('transactionManagement.filterSummary'), close: t('transfers.close') }}
                    onReset={resetFilters}
                    summary={<>
                        <span className="rounded-full border border-[#E4E7EC] px-2.5 py-1 dark:border-[#233044]">{accounts.data?.find(a => a.id === accountId)?.name ?? t('transfers.allAccounts')}</span>
                        {!from && !to && <span className="rounded-full border border-[#E4E7EC] px-2.5 py-1 dark:border-[#233044]">{t('transactionManagement.allDates')}</span>}
                        {from && <span>{t('transfers.fromDate')}: {dateFormat.format(new Date(from + 'T00:00:00Z'))}</span>}
                        {to && <span>{t('transfers.toDate')}: {dateFormat.format(new Date(to + 'T00:00:00Z'))}</span>}
                    </>}>
                    <div className="grid grid-cols-1 gap-4">
                        <AverioField>{t('transfers.account')}<AverioSelect value={accountId} onChange={e => setAccountId(e.target.value)}>
                            <option value="">{t('transfers.allAccounts')}</option>
                            {(accounts.data ?? []).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                        </AverioSelect></AverioField>
                        <AverioField>{t('transfers.fromDate')}<AverioInput type="date" value={from} onChange={e => setFrom(e.target.value)} /></AverioField>
                        <AverioField>{t('transfers.toDate')}<AverioInput type="date" value={to} onChange={e => setTo(e.target.value)} /></AverioField>
                        {!validDates && <p role="alert">{t('transfers.invalidDates')}</p>}
                    </div>
                </AverioFilterPanel>
                <AverioButton variant="primary" type="button" disabled={accounts.isPending || owners.isPending || accounts.isError || owners.isError}
                    onClick={() => setEditor('new')}><ConceptIcon name="plus" />{t('transfers.add')}</AverioButton>
            </div>
        </header>
        {notice && <p role="status">{notice}</p>}
        {!validDates && <p role="alert">{t('transfers.invalidDates')}</p>}
        {(accounts.isError || owners.isError || transfers.isError) && <div role="alert">
            <p>{t('transfers.failed')}</p><AverioButton onClick={() => {
                void accounts.refetch(); void owners.refetch(); void transfers.refetch();
            }}>{t('transfers.retry')}</AverioButton></div>}
        {transfers.isPending && validDates && <p role="status">{t('transfers.loading')}</p>}
        {validDates && transfers.isSuccess && rows.length === 0 && !accounts.isError && !owners.isError && <div className="grid min-w-0 justify-items-center gap-4 rounded-[18px] border border-[#E4E7EC] bg-white px-5 py-10 text-center dark:border-[#233044] dark:bg-[#0D1726] min-[621px]:py-14">
            <AverioTransferIllustration />
            <div className="max-w-sm">
                <h3 className="m-0 text-xl font-bold tracking-tight">{t(hasFilters ? 'transfers.noMatches' : 'transfers.empty')}</h3>
                <p className="mt-2 text-sm leading-relaxed text-[#667085] dark:text-[#AAB4C4]">{t(hasFilters ? 'transfers.noMatchesHelp' : 'transfers.emptyHelp')}</p>
            </div>
            {hasFilters
                ? <AverioButton onClick={resetFilters}><ConceptIcon name="filters" />{t('transfers.resetFilters')}</AverioButton>
                : <AverioButton variant="primary" disabled={!canCreate} onClick={() => setEditor('new')}><ConceptIcon name="plus" />{t('transfers.add')}</AverioButton>}
        </div>}
        {rows.length > 0 && <TransferTimeline rows={rows} canEdit={accounts.isSuccess && owners.isSuccess} onEdit={setEditor} />}
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
