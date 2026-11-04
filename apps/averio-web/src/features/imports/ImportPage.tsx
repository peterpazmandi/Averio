import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ImportAccountMapping, ImportPreview, AccountResponse } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { AverioCategorySelect } from '../../shared/components/ui/AverioCategorySelect';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import { accountsQueries } from '../accounts/api/accountsQueries';
import { transactionQueries } from '../transactions/api/transactionQueries';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';
import { importsApi } from './importApi';
import { ImportAccountDialog } from './ImportAccountDialog';

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
    const [ack, setAck] = useState(false);
    const [page, setPage] = useState(0);
    const [search, setSearch] = useState('');
    const [fileError, setFileError] = useState(false);
    const [creatingSource, setCreatingSource] = useState<string>();
    const key = (kind: string, name: string) => JSON.stringify([kind, name]);
    const writable = (accounts.data ?? []).filter(a => a.status === 'Active' && !a.cutoverAtUtc &&
        (a.scope === 'Household' || a.owners.some(o => o.householdMemberId === owners.data?.currentMemberId)));
    const defaultVisibility = (a: AccountResponse) => a.scope === 'Household' ? 'Household' : 'Private';
    const currencyFor = (source: string, data = preview) => {
        const values = new Set((data?.rows ?? []).flatMap(r => r.account === source ? [r.currency]
            : r.destination === source ? [r.receivedCurrency ?? r.currency] : []));
        return values.size === 1 ? [...values][0] : undefined;
    };
    const upload = useMutation({
        mutationFn: (file: File) => importsApi.apiV1HouseholdsHouseholdIdImportsPost({ householdId, file }),
        onSuccess: data => {
            setPreview(data); setPage(0); setAck(false); setStep(1); setSearch('');
            setMappings(Object.fromEntries(data.accounts.flatMap(source => {
                const matches = writable.filter(a => a.name.trim() === source && a.currency === currencyFor(source, data));
                return matches.length === 1 ? [[source, { source, accountId: matches[0].id, visibility: defaultVisibility(matches[0]) }]] : [];
            })));
            setCategoryMap(Object.fromEntries(data.categories.map(c => {
                const matches = (categories.data ?? []).filter(a => (a.kind === c.kind || a.kind === 'Both') && getCategoryDisplayName(a, t) === c.name);
                return [key(c.kind, c.name), matches.length === 1 ? matches[0].id : ''];
            })));
        },
    });
    const confirm = useMutation({
        mutationFn: () => importsApi.apiV1HouseholdsHouseholdIdImportsIdConfirmationPost({ householdId, id: preview!.id,
            confirmImportRequest: { accounts: preview!.accounts.map(a => mappings[a]),
                categories: preview!.categories.map(c => ({ kind: c.kind, source: c.name, categoryId: categoryMap[key(c.kind, c.name)] || null })),
                acknowledgeBalancesAndOverlap: ack } }),
        onSuccess: async () => {
            setPreview(p => p ? { ...p, status: 'Completed' } : p);
            await cache.invalidateQueries({ predicate: q => q.queryKey.includes(userId) && q.queryKey.includes(householdId) });
        },
    });
    const busy = upload.isPending || confirm.isPending;
    const loaded = accounts.isSuccess && owners.isSuccess && categories.isSuccess;
    const mappingsValid = preview?.accounts.every(source => {
        const m = mappings[source];
        return m && writable.some(a => a.id === m.accountId && a.currency === currencyFor(source));
    }) && new Set(Object.values(mappings).map(m => m.accountId)).size === preview?.accounts.length;
    const failure = upload.error ?? confirm.error;
    const completed = preview?.status === 'Completed';
    const money = (amount: string, currency: string) => formatDecimalAmount(amount, currency, i18n.resolvedLanguage ?? 'en');
    return <section className="grid min-w-0 gap-5 text-[#152033] dark:text-[#F3F6FA]" aria-labelledby="import-title">
        <header className="flex flex-wrap items-center justify-between gap-3"><div>
            <h2 id="import-title" className="text-2xl font-bold">{t('imports.title')}</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">{t('imports.intro')}</p>
        </div><Link to="/app/transactions" className="text-sm font-semibold text-primary-700 dark:text-primary-300">{t('imports.back')}</Link></header>
        {!loaded && <AverioCard><p role="status">{t(accounts.isError || owners.isError || categories.isError ? 'imports.loadError' : 'imports.loading')}</p>
            <AverioButton onClick={() => { void accounts.refetch(); void owners.refetch(); void categories.refetch(); }}>{t('common.actions.retry')}</AverioButton></AverioCard>}
        {failure && <p role="alert" className="rounded-xl border border-red-300 p-4 text-red-700 dark:text-red-300">{failure instanceof ApiError ? failure.message : t('errors.genericDescription')}
            {confirm.isError && <span className="block">{t('imports.retrySafe')}</span>}</p>}
        {busy && <div role="status" className="grid gap-2"><p>{t(upload.isPending ? 'imports.reading' : 'imports.saving')}</p>
            <progress aria-label={t(upload.isPending ? 'imports.reading' : 'imports.saving')} className="h-2 w-full accent-emerald-500" /></div>}
        {completed ? <AverioCard><h3 className="text-xl font-bold">{t('imports.complete')}</h3><p className="my-3">{t('imports.completeHelp', { count: preview.count })}</p>
            <Link to="/app/transactions" className="font-semibold text-primary-700 dark:text-primary-300">{t('imports.back')}</Link></AverioCard> : <>
            <ol className="flex flex-wrap gap-3 text-sm" aria-label={t('imports.steps')}>
                {['upload', 'accounts', 'categories', 'review'].map((s, i) => <li key={s} aria-current={step === i ? 'step' : undefined}
                    className={step === i ? 'font-bold text-primary-700 dark:text-primary-300' : 'text-slate-500 dark:text-slate-400'}>{i + 1}. {t('imports.' + s)}</li>)}
            </ol>
            {step === 0 && <AverioCard><AverioField>{t('imports.file')}<AverioInput type="file" accept=".xlsx" disabled={!loaded || busy}
                onChange={e => { const f = e.target.files?.[0]; setFileError(false); upload.reset(); confirm.reset();
                    if (!f) return; if (!f.name.toLowerCase().endsWith('.xlsx') || f.size > 5 * 1024 * 1024) { setFileError(true); return; } upload.mutate(f); }} /></AverioField>
                <p className="mt-3 text-sm">{t('imports.fileHelp')}</p>{fileError && <p role="alert" className="text-red-600">{t('imports.fileError')}</p>}</AverioCard>}
            {preview && step === 1 && <AverioCard><h3 className="mb-3 font-bold">{t('imports.accounts')}</h3>
                <p className="mb-4 text-sm">{t('imports.accountsHelp')}</p>
                <AverioButton onClick={() => void accounts.refetch()}>{t('imports.refresh')}</AverioButton>
                <div className="mt-4 grid gap-4">{preview.accounts.map(source => {
                    const m = mappings[source]; const a = writable.find(a => a.id === m?.accountId); const currency = currencyFor(source);
                    return <div key={source} className="grid gap-3 border-t border-slate-200 pt-4 dark:border-slate-700 sm:grid-cols-2">
                        <AverioField>{source} · {currency ?? t('imports.currencyConflict')}<AverioSelect value={m?.accountId ?? ''} disabled={busy} onChange={e => {
                            const a = writable.find(a => a.id === e.target.value); setAck(false);
                            setMappings(old => { const next = { ...old }; if (a) next[source] = { source, accountId: a.id, visibility: defaultVisibility(a) }; else delete next[source]; return next; });
                        }}><option value="">{t('imports.choose')}</option>{writable.filter(a => a.currency === currency).map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}</AverioSelect></AverioField>
                        {a && <AverioField>{t('imports.visibility')}<AverioSelect value={m.visibility} disabled={busy || a.scope === 'Household' || a.visibility === 'Private'}
                            onChange={e => setMappings(old => ({ ...old, [source]: { ...m, visibility: e.target.value } }))}>
                            <option value="Private">{t('imports.private')}</option><option value="Household">{t('imports.household')}</option></AverioSelect>
                            <span className="text-xs font-normal">{t('imports.anchor', { date: a.balanceAnchorDate, amount: money(a.balanceAnchorAmount, a.currency) })}</span></AverioField>}
                        <AverioButton className="justify-self-start sm:col-span-2" disabled={busy || !currency || !owners.data}
                            aria-label={t('imports.createFor', { name: source })} onClick={() => setCreatingSource(source)}>{t('imports.createAccount')}</AverioButton>
                    </div>;
                })}</div>{!mappingsValid && <p className="mt-3 text-sm">{t('imports.mappingHelp')}</p>}</AverioCard>}
            {preview && step === 2 && <AverioCard><h3 className="font-bold">{t('imports.categories')}</h3><p className="my-3 text-sm">{t('imports.categoriesHelp')}</p>
                <AverioInput type="search" aria-label={t('imports.search')} placeholder={t('imports.search')} value={search} onChange={e => setSearch(e.target.value)} />
                <div className="mt-4 grid gap-3 sm:grid-cols-2">{preview.categories.filter(c => c.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())).map(c =>
                    <div key={key(c.kind, c.name)} className="min-w-0"><p className="mb-1 text-sm font-semibold">{c.name} · {t('imports.' + c.kind)}</p>
                    <AverioCategorySelect categories={(categories.data ?? []).filter(x => x.kind === c.kind || x.kind === 'Both')} contextCategories={categories.data}
                        value={categoryMap[key(c.kind, c.name)] ?? ''} onChange={id => setCategoryMap(old => ({ ...old, [key(c.kind, c.name)]: id }))}
                        noneLabel={t('imports.createCategory', { name: c.name })} label={t('imports.mapCategory', { name: c.name })} disabled={busy} /></div>)}</div></AverioCard>}
            {preview && step === 3 && <>
                <AverioCard><h3 className="font-bold">{t('imports.ready', { count: preview.count })}</h3><p>{preview.from} – {preview.to}</p>
                    <div className="my-4 grid gap-3 sm:grid-cols-3">{preview.totals.map(total => <div key={total.kind + total.currency}>
                        <p className="text-sm">{t('imports.' + total.kind)} · {total.count}</p><strong>{money(total.amount, total.currency)}</strong></div>)}</div>
                    <p className="text-sm">{t('imports.repeated', { count: preview.repeatedRows })}</p>
                    <p className="mt-3 text-sm">{t('imports.tagsHelp')}</p>
                    <p className="mt-3 text-sm">{t('imports.warning')}</p>
                    <label className="mt-4 flex items-start gap-3 text-sm font-semibold"><input className="mt-1 size-5 shrink-0 accent-emerald-500" type="checkbox" checked={ack} disabled={busy} onChange={e => setAck(e.target.checked)} />{t('imports.ack')}</label>
                </AverioCard>
                <AverioCard><div className="max-h-[28rem] overflow-auto" tabIndex={0} role="region" aria-label={t('imports.preview')}><table className="w-full text-left text-sm"><caption className="mb-3 text-left font-bold">{t('imports.preview')}</caption>
                    <thead className="sticky top-0 bg-slate-50 dark:bg-slate-900"><tr>{['date', 'kind', 'account', 'category', 'amount', 'merchant', 'note'].map(c => <th key={c} className="p-2">{t('imports.' + c)}</th>)}</tr></thead>
                    <tbody>{preview.rows.slice(page * 50, (page + 1) * 50).map(r => <tr key={r.sheet + r.rowNumber} className="border-t border-slate-200 dark:border-slate-700">
                        <td className="whitespace-nowrap p-2">{r.date}</td><td className="p-2">{t('imports.' + r.kind)}</td><td className="p-2">{r.account}{r.destination && ` → ${r.destination}`}</td>
                        <td className="p-2">{r.category}</td><td className="whitespace-nowrap p-2">{money(r.amount, r.currency)}</td><td className="p-2">{r.tags}</td><td className="min-w-40 p-2">{r.note}</td></tr>)}</tbody></table></div>
                    <div className="mt-3 flex items-center justify-between gap-2"><AverioButton disabled={page === 0} onClick={() => setPage(p => p - 1)}>{t('common.actions.back')}</AverioButton>
                        <span className="text-sm">{t('imports.page', { page: page + 1, pages: Math.ceil(preview.count / 50) })}</span>
                        <AverioButton disabled={(page + 1) * 50 >= preview.count} onClick={() => setPage(p => p + 1)}>{t('common.actions.next')}</AverioButton></div>
                </AverioCard>
            </>}
            {preview && <footer className="flex justify-between gap-3"><AverioButton disabled={busy} onClick={() => { setStep(s => Math.max(0, s - 1)); setAck(false); confirm.reset(); }}>{t('common.actions.back')}</AverioButton>
                {step < 3 ? <AverioButton variant="primary" disabled={busy || !loaded || (step === 1 && !mappingsValid)} onClick={() => setStep(s => s + 1)}>{t('common.actions.continue')}</AverioButton>
                    : <AverioButton variant="primary" disabled={busy || !ack || !mappingsValid || !loaded} onClick={() => confirm.mutate()}>{t('imports.confirm', { count: preview.count })}</AverioButton>}</footer>}
        </>}
        {creatingSource && owners.data && preview && currencyFor(creatingSource) && <ImportAccountDialog
            key={creatingSource} userId={userId} householdId={householdId} memberId={owners.data.currentMemberId}
            source={creatingSource} currency={currencyFor(creatingSource)!}
            firstDate={preview.rows.filter(r => r.account === creatingSource || r.destination === creatingSource).map(r => r.date).sort()[0]}
            onClose={() => setCreatingSource(undefined)}
            onCreated={(id, visibility) => {
                setMappings(old => ({ ...old, [creatingSource]: { source: creatingSource, accountId: id, visibility } }));
                setAck(false); setCreatingSource(undefined);
            }} />}
    </section>;
}
