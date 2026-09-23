import { useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CreateTransferRequest, TransferResponse, TransferSideResponse } from '../../shared/api/generated';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { TransferEditor } from './TransferEditor';
import { transferApi, transferKeys, useTransferMutations } from './transferQueries';
import './transfers.css';

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
        return <div className="av-transfer-side">
            <span>{value?.accountName ?? t('transfers.privateAccount')}</span>
            {value && <strong>{formatDecimalAmount(value.amount, value.currency, language)}</strong>}
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
    return <section className="av-transfer-page" aria-labelledby="transfer-title">
        <header className="av-page-head"><div><h2 id="transfer-title">{t('transfers.title')}</h2>
            <p>{t('transfers.description')}</p></div>
            <button className="av-btn av-primary" type="button" disabled={accounts.isPending || owners.isPending || accounts.isError || owners.isError}
                onClick={() => setEditor('new')}><ConceptIcon name="plus" />{t('transfers.add')}</button>
        </header>
        <div className="av-card av-transfer-filters">
            <label>{t('transfers.account')}<select className="av-select" value={accountId} onChange={e => setAccountId(e.target.value)}>
                <option value="">{t('transfers.allAccounts')}</option>
                {(accounts.data ?? []).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select></label>
            <label>{t('transfers.fromDate')}<input className="av-input" type="date" value={from} onChange={e => setFrom(e.target.value)} /></label>
            <label>{t('transfers.toDate')}<input className="av-input" type="date" value={to} onChange={e => setTo(e.target.value)} /></label>
        </div>
        {notice && <p role="status">{notice}</p>}
        {!validDates && <p role="alert">{t('transfers.invalidDates')}</p>}
        {(accounts.isError || owners.isError || transfers.isError) && <div role="alert">
            <p>{t('transfers.failed')}</p><button className="av-btn" onClick={() => {
                void accounts.refetch(); void owners.refetch(); void transfers.refetch();
            }}>{t('transfers.retry')}</button></div>}
        {transfers.isPending && validDates && <p role="status">{t('transfers.loading')}</p>}
        {transfers.isSuccess && rows.length === 0 && <div className="av-card av-transfer-empty">
            <ConceptIcon name="transfers" /><h3>{t('transfers.empty')}</h3><p>{t('transfers.emptyHelp')}</p>
        </div>}
        <div className="av-transfer-list">
            {rows.map(row => <article className={'av-card av-transfer-card' + (row.isVoided ? ' is-voided' : '')} key={row.id}>
                <header><time dateTime={row.occurredOn}>{dateFormat.format(new Date(row.occurredOn + 'T12:00:00Z'))}</time>
                    <span className="av-badge">{t(row.isVoided ? 'transfers.voided' : 'transfers.posted')}</span></header>
                <div className="av-transfer-movement">{side(row.source)}<ConceptIcon name="transfers" />{side(row.destination)}</div>
                {row.note && <p>{row.note}</p>}
                {(!row.source || !row.destination) && <p>{t('transfers.redactedHelp')}</p>}
                {row.canModify && !row.isVoided && <button className="av-btn" disabled={!accounts.isSuccess || !owners.isSuccess}
                    onClick={() => setEditor(row)}>{t('transfers.edit')}</button>}
            </article>)}
        </div>
        {transfers.hasNextPage && <button className="av-btn" disabled={transfers.isFetchingNextPage}
            onClick={() => { void transfers.fetchNextPage(); }}>{t('transfers.loadMore')}</button>}
        {editor === 'new' && writable.filter(a => a.status === 'Active').length < 2
            ? <div className="av-card av-transfer-empty" role="status"><p>{t('transfers.needAccounts')}</p>
                <button className="av-btn" onClick={() => setEditor(null)}>{t('transfers.close')}</button></div>
            : editor && <TransferEditor key={editor === 'new' ? 'new' : editor.id}
                transfer={editor === 'new' ? null : editor} accounts={writable} busy={busy}
                onClose={() => setEditor(null)} onSave={save} onVoid={voidCurrent} />}
    </section>;
}
