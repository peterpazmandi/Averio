import { useEffect, useMemo, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ImportAccountMapping, ImportPreviewRow } from '../../shared/api/generated';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount, normalizeBalanceInput, subtractDecimalAmounts } from '../../shared/money/decimalAmount';
import { importsApi } from './importApi';

export type ReviewDecision = { token: string; skippedRows: number[]; ready: boolean };
export function ImportOverlapReview({ userId, householdId, batchId, mappings, rows, onChange }: {
    userId: string; householdId: string; batchId: string; mappings: ImportAccountMapping[]; rows: ImportPreviewRow[]; onChange: (value: ReviewDecision | null) => void;
}) {
    const { t, i18n } = useTranslation();
    const query = useQuery({ queryKey: ['imports', userId, householdId, batchId, 'overlap', mappings], retry: false,
        queryFn: ({ signal }) => importsApi.apiV1HouseholdsHouseholdIdImportsIdReviewPost({ householdId, id: batchId, importReviewRequest: { accounts: mappings } }, { signal }) });
    const [selection, setSelection] = useState<{ token: string; choices: Record<number, string> }>({ token: '', choices: {} });
    const [page, setPage] = useState(0);
    const decisions = useMemo(() => {
        if (!query.data || query.isFetching || query.isError) return null;
        const choices = selection.token === query.data.token ? selection.choices : {};
        return { token: query.data.token, skippedRows: query.data.overlaps.filter(o => choices[o.rowIndex] === 'skip').map(o => o.rowIndex),
            ready: query.data.overlaps.every(o => ['keep', 'skip'].includes(choices[o.rowIndex])) };
    }, [query.data, query.isFetching, query.isError, selection]);
    useEffect(() => { onChange(decisions); }, [onChange, decisions]);
    const choose = (index: number, value: string) => setSelection(old => ({ token: query.data!.token, choices: { ...(old.token === query.data!.token ? old.choices : {}), [index]: value } }));
    return <AverioCard><h3 className="font-bold">{t('importTools.overlap')}</h3><p className="my-3 text-sm">{t('importTools.overlapHelp')}</p>
        {query.isPending || query.isFetching ? <p role="status">{t('importTools.checking')}</p> : query.isError ? <p role="alert">{t('importTools.reviewError')}</p>
            : <><p role="status">{t('importTools.matches', { count: query.data.overlaps.length })}</p>
                {query.data.overlaps.length > 0 && <div className="my-3 flex flex-wrap gap-2">
                    <AverioButton onClick={() => setSelection({ token: query.data.token, choices: Object.fromEntries(query.data.overlaps.map(o => [o.rowIndex, 'keep'])) })}>{t('importTools.keepAll')}</AverioButton>
                </div>}
                <ol className="grid gap-4">{query.data.overlaps.slice(page * 20, (page + 1) * 20).map(o => <li key={o.rowIndex} className="border-t border-slate-200 pt-3 dark:border-slate-700">
                    <p className="font-semibold">{t('importTools.sourceRow', { row: o.rowIndex + 1, count: o.matchCount })}</p>
                    <p className="mt-2 text-sm">{rows[o.rowIndex]?.sheet} #{rows[o.rowIndex]?.rowNumber} · {rows[o.rowIndex]?.date} · {rows[o.rowIndex]?.account} · {rows[o.rowIndex]?.amount} {rows[o.rowIndex]?.currency} · {rows[o.rowIndex]?.tags}</p>
                    <ul className="my-2 text-sm">{o.matches.map(m => <li key={m.id}>
                        {m.date} · {m.description} · {formatDecimalAmount(m.amount, rows[o.rowIndex]?.currency ?? 'EUR', i18n.resolvedLanguage ?? 'en')}
                        {m.kind === 'Transfer' && m.receivedAmount && m.receivedCurrency && <> · {t('importTools.received')}: {formatDecimalAmount(m.receivedAmount, m.receivedCurrency, i18n.resolvedLanguage ?? 'en')}</>}
                    </li>)}</ul>
                    <AverioField>{t('importTools.decision')}<AverioSelect aria-label={t('importTools.decision')} value={selection.token === query.data.token ? selection.choices[o.rowIndex] ?? '' : ''} onChange={e => choose(o.rowIndex, e.target.value)}>
                        <option value="">{t('importTools.choose')}</option><option value="keep">{t('importTools.keep')}</option><option value="skip">{t('importTools.skip')}</option>
                    </AverioSelect></AverioField></li>)}</ol>
                {query.data.overlaps.length > 20 && <div className="mt-3 flex gap-3"><AverioButton disabled={page === 0} onClick={() => setPage(p => p - 1)}>{t('common.actions.back')}</AverioButton>
                    <AverioButton disabled={(page + 1) * 20 >= query.data.overlaps.length} onClick={() => setPage(p => p + 1)}>{t('common.actions.next')}</AverioButton></div>}
            </>}
        <AverioButton className="mt-3" disabled={query.isFetching} onClick={() => void query.refetch()}>{t('importTools.refresh')}</AverioButton>
    </AverioCard>;
}

export function ImportReconciliation({ userId, householdId, batchId }: { userId: string; householdId: string; batchId: string }) {
    const { t, i18n } = useTranslation();
    const [asOf, setAsOf] = useState(new Date().toLocaleDateString('en-CA'));
    const [expected, setExpected] = useState<Record<string, string>>({});
    const validDate = /^\d{4}-\d{2}-\d{2}$/.test(asOf);
    const query = useQuery({ queryKey: ['imports', userId, householdId, batchId, 'balances', asOf], enabled: validDate,
        queryFn: ({ signal }) => importsApi.apiV1HouseholdsHouseholdIdImportsIdBalancesGet({ householdId, id: batchId, asOf }, { signal }) });
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? 'en');
    return <AverioCard><h3 className="text-lg font-bold">{t('importTools.balances')}</h3><p className="my-3 text-sm">{t('importTools.balanceHelp')}</p>
        <AverioField>{t('importTools.asOf')}<AverioInput type="date" value={asOf} onChange={e => { setAsOf(e.target.value); setExpected({}); }} /></AverioField>
        {query.isError ? <p role="alert">{t('importTools.reviewError')}</p> : query.isPending || query.isFetching ? <p role="status">{t('importTools.checking')}</p>
            : query.data.map(a => {
                const value = expected[a.accountId] ?? ''; const normalized = normalizeBalanceInput(value);
                const difference = normalized === null ? null : subtractDecimalAmounts(normalized, a.currentBalance);
                return <div key={a.accountId} className="mt-4 grid gap-2 border-t border-slate-200 pt-4 dark:border-slate-700">
                    <h4 className="font-semibold">{a.name} · {a.currency}</h4><p>{t('importTools.current', { amount: money(a.currentBalance, a.currency) })}</p>
                    <p className="text-sm">{t('imports.anchor', { date: a.anchorDate, amount: money(a.anchorAmount, a.currency) })}</p>
                    {asOf < a.anchorDate ? <p role="alert">{t('importTools.beforeAnchor')}</p> : <>
                        <AverioField>{t('importTools.expected')}<AverioInput inputMode="decimal" value={value} onChange={e => setExpected(old => ({ ...old, [a.accountId]: e.target.value }))} /></AverioField>
                        {value && normalized === null && <p role="alert">{t('importTools.invalidAmount')}</p>}
                        {difference !== null && <p role="status" className={difference === '0' ? 'font-semibold text-emerald-700 dark:text-emerald-300' : 'font-semibold text-amber-800 dark:text-amber-200'}>
                            {difference === '0' ? t('importTools.matchesBalance') : t('importTools.difference', { amount: money(difference, a.currency) })}</p>}
                    </>}
                    <Link to="/app/accounts/$accountId/edit" params={{ accountId: a.accountId }} className="font-semibold text-primary-700 dark:text-primary-300">{t('importTools.reviewAnchor')}</Link>
                </div>;
            })}
        {query.isSuccess && !query.data.length && <p>{t('importTools.noAccounts')}</p>}
        <AverioButton className="mt-3" disabled={query.isFetching || !validDate} onClick={() => void query.refetch()}>{t('importTools.refresh')}</AverioButton>
    </AverioCard>;
}

export function ImportUndo({ userId, householdId, batchId }: { userId: string; householdId: string; batchId: string }) {
    const { t, i18n } = useTranslation(); const cache = useQueryClient();
    const [acceptedToken, setAcceptedToken] = useState<string>();
    const plan = useQuery({ queryKey: ['imports', userId, householdId, batchId, 'undo-plan'], retry: false,
        queryFn: ({ signal }) => importsApi.apiV1HouseholdsHouseholdIdImportsIdUndoPlanGet({ householdId, id: batchId }, { signal }) });
    const undo = useMutation({ mutationFn: () => importsApi.apiV1HouseholdsHouseholdIdImportsIdUndoPost({ householdId, id: batchId, importUndoRequest: { token: plan.data!.token, confirm: true } }),
        onSuccess: async () => { setAcceptedToken(undefined); await cache.invalidateQueries({ predicate: q => q.queryKey.includes(userId) && q.queryKey.includes(householdId) }); },
        onError: () => { setAcceptedToken(undefined); void plan.refetch(); } });
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? 'en');
    return <AverioCard><h3 className="text-lg font-bold">{t('importTools.undo')}</h3><p className="my-3 text-sm">{t('importTools.undoHelp')}</p>
        {undo.isError && <p role="alert">{t('importTools.undoError')}</p>}
        {undo.isSuccess && <p role="status">{t('importTools.undone', { count: undo.data.voided })}</p>}
        {plan.isPending || plan.isFetching ? <p role="status">{t('importTools.checking')}</p> : plan.isError ? <p role="alert">{t('importTools.reviewError')}</p> : <>
            <p className="font-semibold">{t('importTools.undoCount', { count: plan.data.eligible, retained: plan.data.retained })}</p>
            <ul className="my-3 text-sm">{['Edited', 'Linked', 'ReadOnly', 'Unavailable', 'Untracked', 'Voided', 'Skipped'].map(state => {
                const count = plan.data.records.filter(r => r.state === state).length;
                return count > 0 ? <li key={state}>{t('importTools.' + state)}: {count}</li> : null;
            })}</ul>
            <ul className="my-3">{plan.data.effects.map(e => <li key={e.accountId}>{e.name}: {money(e.before, e.currency)} → {money(e.after, e.currency)}</li>)}</ul>
            {plan.data.eligible > 0 && <label className="my-4 flex items-start gap-3 text-sm"><input type="checkbox" className="mt-1 size-5 accent-emerald-500" disabled={undo.isPending} checked={acceptedToken === plan.data.token} onChange={e => setAcceptedToken(e.target.checked ? plan.data.token : undefined)} />{t('importTools.undoAck')}</label>}
            <AverioButton disabled={!plan.data.eligible || acceptedToken !== plan.data.token || undo.isPending || plan.isFetching} onClick={() => undo.mutate()}>{t(undo.isPending ? 'imports.saving' : 'importTools.undoAction')}</AverioButton>
        </>}
        <AverioButton className="mt-3" disabled={undo.isPending || plan.isFetching} onClick={() => { setAcceptedToken(undefined); void plan.refetch(); }}>{t('importTools.refresh')}</AverioButton>
    </AverioCard>;
}