import { useCallback, useMemo, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ImportAccountMapping, ImportPreview, AccountResponse } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { AverioCategorySelect } from '../../shared/components/ui/AverioCategorySelect';
import { formatDecimalAmount, sumDecimalAmounts } from '../../shared/money/decimalAmount';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { transactionQueries } from '../transactions/api/transactionQueries';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';
import { importsApi } from './importApi';
import { ImportOverlapReview, ImportReconciliation, type ReviewDecision } from './ImportTools';
import { ImportAccountDialog } from './ImportAccountDialog';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';

export function ImportPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const cache = useQueryClient();
    const accounts = useQuery(accountsQueries.list(userId, householdId));
    const owners = useQuery(accountsQueries.ownerOptions(userId, householdId));
    const categories = useQuery(transactionQueries.categories(userId, householdId));
    const [preview, setPreview] = useState<ImportPreview>();
    const [step, setStep] = useState(0);
    const [mappings, setMappings] = useState<Record<string, ImportAccountMapping>>({});
    const [categoryMap, setCategoryMap] = useState<Record<string, string>>({});
    const [review, setReview] = useState<ReviewDecision | null>(null);
    const onReviewChange = useCallback((value: ReviewDecision | null) => setReview(value), []);
    const reviewMappings = useMemo(() => preview?.accounts.map(a => mappings[a]) ?? [], [preview, mappings]);
    const [ack, setAck] = useState(false);
    const [page, setPage] = useState(0);
    const [search, setSearch] = useState('');
    const [fileError, setFileError] = useState(false);
    const [source, setSource] = useState<'moneyManager' | 'ynab'>('moneyManager');
    const [ynabCurrency, setYnabCurrency] = useState('EUR');
    const [ynabDateFormat, setYnabDateFormat] = useState('MDY');
    const [ynabNumberFormat, setYnabNumberFormat] = useState('DotDecimal');
    const [creatingSource, setCreatingSource] = useState<string>();
    const key = (kind: string, name: string) => JSON.stringify([kind, name]);
    const writable = (accounts.data ?? []).filter(a => a.status === 'Active' && !a.cutoverAtUtc &&
        (a.scope === 'Household' || a.owners.some(o => o.householdMemberId === owners.data?.currentMemberId)));
    const defaultVisibility = (a: AccountResponse) => a.scope === 'Household' ? 'Household' : 'Private';
    const sourceAccountFor = (source: string, data = preview) =>
        data?.sourceAccounts?.find(account => account.source === source);
    const currencyFor = (source: string, data = preview) => {
        const metadata = sourceAccountFor(source, data);
        if (metadata) return metadata.currency;
        const values = new Set((data?.rows ?? []).flatMap(r => r.account === source ? [r.currency]
            : r.destination === source ? [r.receivedCurrency ?? r.currency] : []));
        return values.size === 1 ? [...values][0] : undefined;
    };
    const upload = useMutation({
        mutationFn: (file: File) => source === 'ynab'
            ? importsApi.apiV1HouseholdsHouseholdIdImportsYnabPost({ householdId, file, currency: ynabCurrency,
                dateFormat: ynabDateFormat, numberFormat: ynabNumberFormat })
            : importsApi.apiV1HouseholdsHouseholdIdImportsPost({ householdId, file }),
        onSuccess: data => {
            setPreview(data); setPage(0); setAck(false); setStep(1); setSearch(''); setReview(null);
            setMappings(Object.fromEntries(data.accounts.flatMap(source => {
                const remembered = data.defaults?.accounts.find(m => m.source === source);
                const saved = writable.find(a => a.id === remembered?.accountId && a.currency === currencyFor(source, data));
                if (saved) return [[source, { source, accountId: saved.id, visibility: saved.scope === 'Household' ? 'Household' : saved.visibility === 'Private' ? 'Private' : remembered!.visibility }]];
                const matches = writable.filter(a => a.name.trim() === source && a.currency === currencyFor(source, data));
                return matches.length === 1 ? [[source, { source, accountId: matches[0].id, visibility: defaultVisibility(matches[0]) }]] : [];
            })));
            setCategoryMap(Object.fromEntries(data.categories.map(c => {
                const saved = data.defaults?.categories.find(m => m.kind === c.kind && m.source === c.name);
                if (saved?.categoryId && categories.data?.some(a => a.id === saved.categoryId && (a.kind === c.kind || a.kind === 'Both'))) return [key(c.kind, c.name), saved.categoryId];
                const matches = (categories.data ?? []).filter(a => (a.kind === c.kind || a.kind === 'Both') && getCategoryDisplayName(a, t) === c.name);
                return [key(c.kind, c.name), matches.length === 1 ? matches[0].id : ''];
            })));
        },
    });
    const confirm = useMutation({
        mutationFn: () => importsApi.apiV1HouseholdsHouseholdIdImportsIdConfirmationPost({ householdId, id: preview!.id,
            confirmImportRequest: { accounts: preview!.accounts.map(a => mappings[a]),
                categories: preview!.categories.map(c => ({ kind: c.kind, source: c.name, categoryId: categoryMap[key(c.kind, c.name)] || null })),
                acknowledgeBalancesAndOverlap: ack, reviewToken: review?.token, skippedRows: review?.skippedRows ?? [] } }),
        onSuccess: async result => {
            setPreview(p => p ? { ...p, status: 'Completed', completion: result } : p);
            await cache.invalidateQueries({ predicate: q => q.queryKey.includes(userId) && q.queryKey.includes(householdId) });
        },
    });
    const includedRows = preview?.rows.filter((_, i) => !review?.skippedRows.includes(i)) ?? [];
    const includedTotals = (preview?.totals ?? []).map(total => {
        const rows = includedRows.filter(r => r.kind === total.kind && r.currency === total.currency);
        return { ...total, amount: sumDecimalAmounts(rows.map(r => r.amount)), count: rows.length };
    });
    const busy = upload.isPending || confirm.isPending;
    const loaded = accounts.isSuccess && owners.isSuccess && categories.isSuccess;
    const mappingsValid = preview?.accounts.every(source => {
        const m = mappings[source];
        return m && writable.some(a => a.id === m.accountId && a.currency === currencyFor(source));
    }) && new Set(Object.values(mappings).map(m => m.accountId)).size === preview?.accounts.length;
    const clearImportDraft = () => {
        setPreview(undefined);
        setMappings({});
        setCategoryMap({});
        setReview(null);
        setAck(false);
        setPage(0);
        setSearch('');
        confirm.reset();
    };
    const failure = upload.error ?? confirm.error;
    const completed = preview?.status === 'Completed';
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? 'en');
    return <section className="grid min-w-0 gap-5 text-[#152033] dark:text-[#F3F6FA]" aria-labelledby="import-title">
        <header className="flex flex-wrap items-center justify-between gap-3"><div>
            <h2 id="import-title" className="text-2xl font-bold">{t('imports.title')}</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">{t('imports.intro')}</p>
        </div><Link to="/app/import-history" className="text-sm font-semibold text-primary-700 dark:text-primary-300">{t('importHistory.title')}</Link><Link to="/app/transactions" className="text-sm font-semibold text-primary-700 dark:text-primary-300">{t('imports.back')}</Link></header>
        {!loaded && <AverioCard><p role="status">{t(accounts.isError || owners.isError || categories.isError ? 'imports.loadError' : 'imports.loading')}</p>
            <AverioButton onClick={() => { void accounts.refetch(); void owners.refetch(); void categories.refetch(); }}>{t('common.actions.retry')}</AverioButton></AverioCard>}
        {failure && <p role="alert" className="rounded-xl border border-red-300 p-4 text-red-700 dark:text-red-300">{failure instanceof ApiError ? failure.message : t('errors.genericDescription')}
            {confirm.isError && <span className="block">{t('imports.retrySafe')}</span>}</p>}
        {busy && <div role="status" className="grid gap-2"><p>{t(upload.isPending ? 'imports.reading' : 'imports.saving')}</p>
            <progress aria-label={t(upload.isPending ? 'imports.reading' : 'imports.saving')} className="h-2 w-full accent-emerald-500" /></div>}
        {completed ? <><AverioCard><h3 className="text-xl font-bold">{t('imports.complete')}</h3><p className="my-3">{t('importTools.complete', { transactions: preview.completion?.transactions ?? preview.rows.filter(r => r.kind !== 'Transfer').length, transfers: preview.completion?.transfers ?? preview.rows.filter(r => r.kind === 'Transfer').length, accounts: preview.completion?.accounts ?? preview.accounts.length, skipped: preview.completion?.skipped ?? 0 })}</p>
            <Link to="/app/import-history" search={{ batch: preview.id }} className="mr-4 font-semibold text-primary-700 dark:text-primary-300">{t('importHistory.records')}</Link><Link to="/app/transactions" className="font-semibold text-primary-700 dark:text-primary-300">{t('imports.back')}</Link></AverioCard><ImportReconciliation userId={userId} householdId={householdId} batchId={preview.id} /></> : <>
            <ol className="flex flex-wrap gap-3 text-sm" aria-label={t('imports.steps')}>
                {['upload', 'accounts', 'categories', 'review'].map((s, i) => <li key={s} aria-current={step === i ? 'step' : undefined}
                    className={step === i ? 'font-bold text-primary-700 dark:text-primary-300' : 'text-slate-500 dark:text-slate-400'}>{i + 1}. {t('imports.' + s)}</li>)}
            </ol>
            {step === 0 && <AverioCard>
                <div className="grid gap-4">
                    <AverioField>{t('imports.source')}
                        <AverioSelect value={source} disabled={busy} onChange={e => {
                            const nextSource = e.target.value as 'moneyManager' | 'ynab';
                            if (nextSource === source) return;
                            setSource(nextSource);
                            setFileError(false);
                            upload.reset();
                            clearImportDraft();
                        }}>
                            <option value="moneyManager">{t('imports.sourceMoneyManager')}</option>
                            <option value="ynab">{t('imports.sourceYnab')}</option>
                        </AverioSelect>
                    </AverioField>
                    {source === 'ynab' && <>
                        <div className="flex items-center text-sm font-semibold">{t('imports.sourceYnab')} <InfoPopover label={t('imports.sourceYnab')}>{t('imports.ynabSettingsHelp')}</InfoPopover></div>
                        <div className="grid gap-4 sm:grid-cols-3">
                            <AverioField>{t('imports.ynabCurrency')}
                                <AverioSelect value={ynabCurrency} disabled={busy} onChange={e => setYnabCurrency(e.target.value)}>
                                    <option value="EUR">EUR</option><option value="HUF">HUF</option><option value="USD">USD</option>
                                </AverioSelect>
                            </AverioField>
                            <AverioField>{t('imports.ynabDateFormat')}
                                <AverioSelect value={ynabDateFormat} disabled={busy} onChange={e => setYnabDateFormat(e.target.value)}>
                                    <option value="MDY">{t('imports.ynabDateMdy')}</option>
                                    <option value="DMY">{t('imports.ynabDateDmy')}</option>
                                    <option value="YMD">{t('imports.ynabDateYmd')}</option>
                                </AverioSelect>
                            </AverioField>
                            <AverioField>{t('imports.ynabDecimal')}
                                <AverioSelect value={ynabNumberFormat} disabled={busy} onChange={e => setYnabNumberFormat(e.target.value)}>
                                    <option value="DotDecimal">{t('imports.ynabDotDecimal')}</option>
                                    <option value="CommaDecimal">{t('imports.ynabCommaDecimal')}</option>
                                </AverioSelect>
                            </AverioField>
                        </div>
                    </>}
                    <AverioField><span className="inline-flex items-center">{t(source === 'ynab' ? 'imports.ynabFile' : 'imports.file')} <InfoPopover label={t(source === 'ynab' ? 'imports.ynabFile' : 'imports.file')}>{t(source === 'ynab' ? 'imports.ynabFileHelp' : 'imports.fileHelp')}</InfoPopover></span>
                        <AverioInput key={source} type="file" accept={source === 'ynab' ? '.csv,.tsv,.zip' : '.xlsx'}
                            disabled={!loaded || busy} onChange={e => {
                                const file = e.target.files?.[0]; setFileError(false); upload.reset(); confirm.reset();
                                if (!file) return;
                                const ext = file.name.toLowerCase().split('.').pop();
                                if (file.size > 5 * 1024 * 1024 || !ext ||
                                    !(source === 'ynab' ? ['csv', 'tsv', 'zip'].includes(ext) : ext === 'xlsx')) {
                                    setFileError(true); return;
                                }
                                upload.mutate(file);
                            }} />
                    </AverioField>
                    <p className="text-sm text-slate-600 dark:text-slate-300">{t(source === 'ynab' ? 'imports.ynabFileHelp' : 'imports.fileHelp')}</p>
                    {fileError && <p role="alert" className="text-red-600">{t(source === 'ynab' ? 'imports.ynabFileError' : 'imports.fileError')}</p>}
                </div>
            </AverioCard>}
            {preview && step === 1 && <AverioCard><h3 className="mb-3 font-bold">{t('imports.accounts')}</h3>
                <p className="mb-4 text-sm">{t('imports.accountsHelp')}</p><p className="mb-3 text-sm">{t('importTools.remembered')}</p>
                <AverioButton onClick={() => void accounts.refetch()}>{t('imports.refresh')}</AverioButton>
                <div className="mt-4 grid gap-4">{preview.accounts.map(source => {
                    const m = mappings[source]; const a = writable.find(a => a.id === m?.accountId);
                    const metadata = sourceAccountFor(source); const currency = currencyFor(source);
                    return <div key={source} className="grid gap-3 border-t border-slate-200 pt-4 dark:border-slate-700 sm:grid-cols-2">
                        <AverioField>{source} · {currency ?? t('imports.currencyConflict')}<AverioSelect value={m?.accountId ?? ''} disabled={busy} onChange={e => {
                            const a = writable.find(a => a.id === e.target.value); setAck(false);
                            setMappings(old => { const next = { ...old }; if (a) next[source] = { source, accountId: a.id, visibility: defaultVisibility(a) }; else delete next[source]; return next; });
                        }}><option value="">{t('imports.choose')}</option>{writable.filter(a => a.currency === currency).map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}</AverioSelect>
                        {metadata?.openingBalance !== undefined && metadata.openingBalance !== null && <span className="text-xs font-normal">{t('imports.anchor', {
                            date: metadata.openingBalanceDate ?? preview.from,
                            amount: money(metadata.openingBalance, metadata.currency)
                        })}</span>}</AverioField>
                        {a && <AverioField>{t('imports.visibility')}<AverioSelect value={m.visibility} disabled={busy || a.scope === 'Household' || a.visibility === 'Private'}
                            onChange={e => setMappings(old => ({ ...old, [source]: { ...m, visibility: e.target.value } }))}>
                            <option value="Private">{t('imports.private')}</option><option value="Household">{t('imports.household')}</option></AverioSelect>
                            <span className="text-xs font-normal">{t('imports.anchor', { date: a.balanceAnchorDate, amount: money(a.balanceAnchorAmount, a.currency) })}</span></AverioField>}
                        <AverioButton className="justify-self-start sm:col-span-2" disabled={busy || !currency || !owners.data}
                            aria-label={t('imports.createFor', { name: source })} onClick={() => setCreatingSource(source)}>{t('imports.createAccount')}</AverioButton>
                    </div>;
                })}</div>{!mappingsValid && <p className="mt-3 text-sm">{t('imports.mappingHelp')}</p>}</AverioCard>}
            {preview && step === 2 && <AverioCard><h3 className="font-bold" aria-label={t('imports.categories')}>{t('imports.categories')} <InfoPopover label={t('imports.categories')}>{t('imports.categoriesHelp')}</InfoPopover></h3>
                <AverioInput type="search" aria-label={t('imports.search')} placeholder={t('imports.search')} value={search} onChange={e => setSearch(e.target.value)} />
                <div className="mt-4 grid gap-3 sm:grid-cols-2">{preview.categories.filter(c => c.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())).map(c =>
                    <div key={key(c.kind, c.name)} className="min-w-0"><p className="mb-1 text-sm font-semibold">{c.name} · {t('imports.' + c.kind)}</p>
                    <AverioCategorySelect categories={(categories.data ?? []).filter(x => x.kind === c.kind || x.kind === 'Both')} contextCategories={categories.data}
                        value={categoryMap[key(c.kind, c.name)] ?? ''} onChange={id => setCategoryMap(old => ({ ...old, [key(c.kind, c.name)]: id }))}
                        noneLabel={t('imports.createCategory', { name: c.name })} label={t('imports.mapCategory', { name: c.name })} disabled={busy} /></div>)}</div></AverioCard>}
            {preview && step === 3 && <>
                <ImportOverlapReview userId={userId} householdId={householdId} batchId={preview.id} mappings={reviewMappings} rows={preview.rows} onChange={onReviewChange} />
                <AverioCard><h3 className="font-bold" aria-label={t('imports.ready', { count: includedRows.length })}>{t('imports.ready', { count: includedRows.length })} <InfoPopover label={t('imports.ready', { count: includedRows.length })}><span className="block">{t('imports.repeated', { count: preview.repeatedRows })}</span><span className="mt-2 block">{t(source === 'ynab' ? 'imports.ynabMerchantHelp' : 'imports.tagsHelp')}</span><span className="mt-2 block">{t('imports.warning')}</span></InfoPopover></h3><p>{preview.from} – {preview.to}</p>
                    <div className="my-4 grid gap-3 sm:grid-cols-3">{includedTotals.map(total => <div key={total.kind + total.currency}>
                        <p className="text-sm">{t('imports.' + total.kind)} · {total.count}</p><strong>{money(total.amount, total.currency)}</strong></div>)}</div>
                    <p className="text-sm">{t('imports.repeated', { count: preview.repeatedRows })}</p>
                    <p className="mt-3 text-sm">{t(source === 'ynab' ? 'imports.ynabMerchantHelp' : 'imports.tagsHelp')}</p>
                    <p className="mt-3 text-sm">{t('imports.warning')}</p>
                    <label className="mt-4 flex items-start gap-3 text-sm font-semibold"><input className="mt-1 size-5 shrink-0 accent-emerald-500" type="checkbox" checked={ack} disabled={busy} onChange={e => setAck(e.target.checked)} />{t('imports.ack')}</label>
                </AverioCard>
                <AverioCard><div className="max-h-[28rem] overflow-auto" tabIndex={0} role="region" aria-label={t('imports.preview')}><table className="w-full text-left text-sm"><caption className="mb-3 text-left font-bold">{t('imports.preview')}</caption>
                    <thead className="sticky top-0 bg-slate-50 dark:bg-slate-900"><tr>{['date', 'kind', 'account', 'category', 'amount', 'merchant', 'note'].map(c => <th key={c} className="p-2">{t('imports.' + c)}</th>)}</tr></thead>
                    <tbody>{preview.rows.slice(page * 50, (page + 1) * 50).map((r, i) => <tr key={r.sheet + r.rowNumber} className="border-t border-slate-200 dark:border-slate-700">
                        <td className="whitespace-nowrap p-2">{r.date}</td><td className="p-2">{t('imports.' + r.kind)}{review?.skippedRows.includes(page * 50 + i) && <span className="block text-xs font-bold">{t('importTools.Skipped')}</span>}</td><td className="p-2">{r.account}{r.destination && ` → ${r.destination}`}</td>
                        <td className="p-2">{r.category}</td><td className="whitespace-nowrap p-2">{money(r.amount, r.currency)}</td><td className="p-2">{r.tags}</td><td className="min-w-40 p-2">{r.note}</td></tr>)}</tbody></table></div>
                    <div className="mt-3 flex items-center justify-between gap-2"><AverioButton disabled={page === 0} onClick={() => setPage(p => p - 1)}>{t('common.actions.back')}</AverioButton>
                        <span className="text-sm">{t('imports.page', { page: page + 1, pages: Math.ceil(preview.count / 50) })}</span>
                        <AverioButton disabled={(page + 1) * 50 >= preview.count} onClick={() => setPage(p => p + 1)}>{t('common.actions.next')}</AverioButton></div>
                </AverioCard>
            </>}
            {preview && <footer className="flex justify-between gap-3"><AverioButton disabled={busy} onClick={() => { setStep(s => Math.max(0, s - 1)); setReview(null); setAck(false); confirm.reset(); }}>{t('common.actions.back')}</AverioButton>
                {step < 3 ? <AverioButton variant="primary" disabled={busy || !loaded || (step === 1 && !mappingsValid)} onClick={() => setStep(s => s + 1)}>{t('common.actions.continue')}</AverioButton>
                    : <AverioButton variant="primary" disabled={busy || !ack || !review?.ready || !mappingsValid || !loaded} onClick={() => confirm.mutate()}>{t('imports.confirm', { count: preview.count - (review?.skippedRows.length ?? 0) })}</AverioButton>}</footer>}
        </>}
        {creatingSource && owners.data && preview && (sourceAccountFor(creatingSource) || currencyFor(creatingSource)) && <ImportAccountDialog
            key={creatingSource} userId={userId} householdId={householdId} memberId={owners.data.currentMemberId}
            source={creatingSource} currency={sourceAccountFor(creatingSource)?.currency ?? currencyFor(creatingSource)!}
            openingBalance={sourceAccountFor(creatingSource)?.openingBalance}
            openingBalanceDate={sourceAccountFor(creatingSource)?.openingBalanceDate}
            firstDate={sourceAccountFor(creatingSource)?.openingBalanceDate ?? preview.rows.filter(r => r.account === creatingSource || r.destination === creatingSource).map(r => r.date).sort()[0] ?? preview.from}
            onClose={() => setCreatingSource(undefined)}
            onCreated={(id, visibility) => {
                setMappings(old => ({ ...old, [creatingSource]: { source: creatingSource, accountId: id, visibility } }));
                setAck(false); setCreatingSource(undefined);
            }} />}
    </section>;
}
