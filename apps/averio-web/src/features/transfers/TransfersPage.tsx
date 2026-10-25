import { InfoPopover } from '../../shared/components/ui/InfoPopover';
import { useMemo, useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CreateTransferRequest, TransferResponse } from '../../shared/api/generated';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { householdQueries } from '../household/householdQueries';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { TransferEditor } from './TransferEditor';
import { TransferTimeline } from './TransferTimeline';
import { personalMoneyQueries } from '../personal-money/personalMoneyQueries';
import { personalMoneyRelations } from '../personal-money/personalMoneyRelations';
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
    const [editorRevision, setEditorRevision] = useState(0);
    const [notice, setNotice] = useState('');
    const [personalMoneyOnly, setPersonalMoneyOnly] = useState(false);
    const accounts = useQuery(accountsQueries.list(userId, householdId, true));
    const household = useQuery(householdQueries.detail(userId, householdId));
    const personalMoney = useQuery(personalMoneyQueries.mine(userId, householdId));
    const relations = useMemo(() => personalMoneyRelations(personalMoney.data), [personalMoney.data]);
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const writable = (accounts.data ?? []).filter(a => a.scope === 'Household'
        || a.owners.some(o => o.householdMemberId === owners.data?.currentMemberId));
    const mutations = useTransferMutations(userId, householdId);
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.void.isPending;
    const hasFilters = !!(accountId || from || to || personalMoneyOnly);
    const canCreate = accounts.isSuccess && owners.isSuccess;
    function resetFilters() { setAccountId(''); setFrom(''); setTo(''); setPersonalMoneyOnly(false); }
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
    const visibleRows = personalMoneyOnly ? rows.filter(row => relations.transfers.has(row.id)) : rows;
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
    async function reloadCurrent() {
        if (!editor) return;
        if (editor === 'new') {
            await Promise.all([accounts.refetch({ throwOnError: true }), owners.refetch({ throwOnError: true })]);
            return;
        }
        const latest = await transferApi.apiV1HouseholdsHouseholdIdTransfersTransferIdGet({ householdId, transferId: editor.id });
        await Promise.all([accounts.refetch({ throwOnError: true }), owners.refetch({ throwOnError: true })]);
        setEditor(latest);
        // Explicitly discard stale fields along with their concurrency token.
        setEditorRevision(value => value + 1);
    }
    return <section className="grid min-w-0 gap-5" aria-labelledby="transfer-title">
        <header className="flex flex-col items-start justify-between gap-4 min-[621px]:flex-row min-[621px]:items-end"><div><h2 id="transfer-title" className="m-0 text-[27px] font-bold tracking-tight" aria-label={t('transfers.title')}>{t('transfers.title')} <InfoPopover label={t('transfers.title')}>{t('transfers.description')}</InfoPopover></h2></div>
            <div className="flex shrink-0 flex-wrap items-center gap-2">
                <AverioFilterPanel count={Number(!!accountId) + Number(!!from) + Number(!!to) + Number(personalMoneyOnly)}
                    labels={{ filters: t('transactionManagement.filters'), showResults: t('transactionManagement.showResults'),
                        reset: t('transactionManagement.reset'), summary: t('transactionManagement.filterSummary'), close: t('transfers.close') }}
                    onReset={resetFilters}
                    summary={<>
                        <span className="rounded-full border border-[#E4E7EC] px-2.5 py-1 dark:border-[#233044]">{accounts.data?.find(a => a.id === accountId)?.name ?? t('transfers.allAccounts')}</span>
                        {personalMoneyOnly && <span className="rounded-full border border-emerald-300 px-2.5 py-1 dark:border-emerald-700">{t('personalMoney.filterOnly')}</span>}
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
                        <label className="flex min-h-11 items-center gap-3 rounded-xl border border-emerald-300/40 bg-emerald-50 p-3 text-sm dark:bg-emerald-950/30">
                            <input type="checkbox" checked={personalMoneyOnly}
                                disabled={personalMoney.isPending || personalMoney.isError}
                                onChange={e => setPersonalMoneyOnly(e.target.checked)}
                                className="size-4 accent-emerald-500" />
                            {t('personalMoney.filterOnly')}
                        </label>
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
        {personalMoneyOnly && personalMoney.isError && <p role="alert">{t('personalMoney.loadError')}</p>}
        {personalMoneyOnly && rows.length > 0 && visibleRows.length === 0 && <p role="status" className="rounded-xl border border-dashed border-slate-300 p-4 text-sm dark:border-slate-700">{t('personalMoney.noLoadedRelations')}</p>}
        {personalMoneyOnly && <p className="text-xs text-slate-600 dark:text-slate-300">{t('personalMoney.loadedFilterHelp')}</p>}
        {visibleRows.length > 0 && <TransferTimeline rows={visibleRows} relations={relations.transfers}
            canEdit={accounts.isSuccess && owners.isSuccess} onEdit={setEditor} />}
        {transfers.hasNextPage && <AverioButton disabled={transfers.isFetchingNextPage}
            onClick={() => { void transfers.fetchNextPage(); }}>{t('transfers.loadMore')}</AverioButton>}
        {editor && <TransferEditor key={`${editor === 'new' ? 'new' : editor.id}:${editorRevision}`}
                transfer={editor === 'new' ? null : editor} accounts={writable}
                currentMemberId={owners.data?.currentMemberId}
                canCreateGrant={household.data?.role === 'owner'} busy={busy}
                personalRelation={editor === 'new' ? undefined : relations.transfers.get(editor.id)}
                onClose={() => setEditor(null)} onSave={save} onVoid={voidCurrent} onReload={reloadCurrent} />}
    </section>;
}
