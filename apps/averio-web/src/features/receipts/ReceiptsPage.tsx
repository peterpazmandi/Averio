import { useEffect, useRef, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ReceiptScanResult, RememberReceiptCategoryRequest } from '../../shared/api/generated';
import { API_BASE_URL } from '../../shared/api/apiConfiguration';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { TransactionEditor } from '../transactions/components/TransactionEditor';
import { createTransaction } from '../transactions/api/transactionApi';
import type { TransactionInputDto } from '../transactions/api/transactionApi';
import { receiptKeys, receiptQueries, receiptsApi } from './receiptQueries';

const pending = new Set(['Uploading', 'Queued', 'Processing']);

export function ReceiptsPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const cache = useQueryClient();
    const input = useRef<HTMLInputElement>(null);
    const camera = useRef<HTMLInputElement>(null);
    const [selection, setSelection] = useState<{ file: File; operationId: string } | null>(null);
    const [fileError, setFileError] = useState('');
    const [notice, setNotice] = useState('');
    const [review, setReview] = useState<ReceiptScanResult | null>(null);
    const [manual, setManual] = useState<ReceiptScanResult | null>(null);
    const [matching, setMatching] = useState<string | null>(null);
    const [photo, setPhoto] = useState<ReceiptScanResult | null>(null);
    const [discard, setDiscard] = useState<ReceiptScanResult | null>(null);
    const capabilities = useQuery(receiptQueries.capabilities(userId, householdId));
    const inbox = useInfiniteQuery({
        queryKey: [...receiptKeys.all(userId, householdId), 'inbox'],
        initialPageParam: 0,
        queryFn: ({ pageParam, signal }) => receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansGet({ householdId, skip: pageParam }, { signal }),
        getNextPageParam: (last, pages) => last.length === 50 && pages.length * 50 <= 10000 ? pages.length * 50 : undefined,
        refetchInterval: query => query.state.data?.pages.some(page => page.some(scan => pending.has(scan.status))) ? 3000 : false,
    });
    const refresh = () => cache.invalidateQueries({ queryKey: receiptKeys.all(userId, householdId) });
    const upload = useMutation({
        mutationFn: (value: { file: File; operationId: string }) => receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansPost({ householdId, ...value }),
        onSuccess: async () => {
            setSelection(null); if (input.current) input.current.value = '';
            if (camera.current) camera.current.value = '';
            setNotice(t('receipts.uploaded')); await refresh();
        },
        // A lost response may still represent an accepted upload. Refresh the
        // private inbox and retain the same operation ID for a deliberate retry.
        onError: refresh,
    });
    const remove = useMutation({
        mutationFn: (scan: ReceiptScanResult) => scan.status === 'Posted'
            ? receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdImageDelete({ householdId, id: scan.id, expectedVersion: scan.version })
            : receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdDelete({ householdId, id: scan.id, expectedVersion: scan.version }),
        onSuccess: async () => { setDiscard(null); await refresh(); },
        onError: refresh,
    });
    const post = useMutation({
        mutationFn: ({ scan, request, currency, reviewedTotal, rememberCategoryRules }: { scan: ReceiptScanResult; request: TransactionInputDto; currency: string; reviewedTotal: string; rememberCategoryRules: RememberReceiptCategoryRequest[] }) =>
            receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdTransactionPost({ householdId, id: scan.id,
                postReceiptRequest: { expectedVersion: scan.version, currency, reviewedTotal, transaction: request, rememberCategoryRules } }),
        onSuccess: async () => {
            setReview(null); setNotice(t('receipts.posted'));
            // Match ordinary posting invalidation, including account balances,
            // wallet history, activity totals, statistics and the dashboard.
            await Promise.all([
                refresh(),
                ...['transactions', 'accounts', 'dashboard', 'statistics', 'personal-money', 'activities']
                    .map(key => cache.invalidateQueries({ predicate: query => query.queryKey[0] === key && query.queryKey.includes(userId) && query.queryKey.includes(householdId) })),
            ]);
        },
    });
    const manualSave = useMutation({
        mutationFn: (request: TransactionInputDto) => createTransaction(householdId, request),
        onSuccess: async () => {
            setManual(null); setNotice(t('receipts.posted'));
            await Promise.all(['transactions', 'accounts', 'dashboard', 'statistics', 'personal-money', 'activities']
                .map(key => cache.invalidateQueries({ predicate: query => query.queryKey[0] === key && query.queryKey.includes(userId) && query.queryKey.includes(householdId) })));
        },
    });
    const quota = capabilities.data;
    const exhausted = quota?.monthlyLimit !== undefined && quota.monthlyUsed !== undefined && quota.monthlyUsed >= quota.monthlyLimit;
    // An ambiguous upload response must remain retryable with its original operation ID.
    const canStartUpload = capabilities.isSuccess && quota?.enabled && !exhausted;
    const canRetryUpload = selection !== null && upload.isError;
    const canSubmitUpload = canStartUpload || canRetryUpload;
    const scans = inbox.data?.pages.flat() ?? [];
    const orderedScans = [...scans].sort((a, b) => Number(b.status === 'Ready') - Number(a.status === 'Ready'));
    function choose(file?: File) {
        if (!file) return;
        upload.reset(); setFileError(''); setNotice('');
        if (!['image/jpeg', 'image/png'].includes(file.type) || file.size === 0 || file.size > (capabilities.data?.maxUploadBytes ?? 10485760)) {
            setSelection(null); setFileError(t('receipts.fileInvalid')); return;
        }
        setSelection({ file, operationId: crypto.randomUUID() });
    }
    return <section className="grid gap-4" aria-labelledby="receipts-title">
        <header className="flex flex-wrap items-center justify-between gap-3">
            <div><h2 id="receipts-title" className="text-xl font-bold">{t('receipts.title')}</h2><p className="mt-1 text-sm">{t('receipts.privateHelp')}</p></div>
            <AverioButton disabled={inbox.isFetching} onClick={() => void refresh()}>{t('receipts.refresh')}</AverioButton>
        </header>
        <ol className="grid grid-cols-3 gap-2 text-sm" aria-label={t('receipts.howItWorks')}>
            {['photoStep', 'reviewStep', 'saveStep'].map((step, index) => <li key={step} className="flex flex-col gap-2 rounded-xl bg-emerald-50 p-3 dark:bg-emerald-950/30 sm:flex-row sm:items-center">
                <span aria-hidden="true" className="flex size-7 shrink-0 items-center justify-center rounded-full bg-emerald-600 font-bold text-white">{index + 1}</span>
                <span className="font-semibold">{t('receipts.' + step)}</span>
            </li>)}
        </ol>
        <AverioCard className="grid gap-4">
            <h3 className="font-bold">{t('receipts.capture')}</h3>
            {quota && quota.resetsAt && <div className="grid gap-1 text-sm" role="status">
                <p className="font-semibold">{t('receipts.quota', { used: quota.monthlyUsed, limit: quota.monthlyLimit })}</p>
                <p>{t('receipts.quotaReset', { date: new Date(quota.resetsAt).toLocaleString(i18n.resolvedLanguage ?? i18n.language) })}</p>
                {exhausted && <p className="text-amber-900 dark:text-amber-200">{t('receipts.quotaExhausted')}</p>}
                <p className="text-xs text-slate-600 dark:text-slate-300">{t('receipts.quotaHelp')}</p>
            </div>}
            {quota && <p className="text-xs text-slate-600 dark:text-slate-300">{t((quota.postedImageRetentionDays ?? 0) > 0 ? 'receipts.retentionHelp' : 'receipts.temporaryHelp', { days: quota.postedImageRetentionDays })}</p>}
            <p className="text-sm text-slate-600 dark:text-slate-300">{t('receipts.photoHelp')}</p>
            {capabilities.isPending && <p role="status">{t('common.status.loading')}</p>}
            {capabilities.isError && <p role="alert">{capabilities.error.message} <AverioButton onClick={() => void capabilities.refetch()}>{t('receipts.retry')}</AverioButton></p>}
            {capabilities.isSuccess && !capabilities.data.enabled && <p role="status">{t('receipts.disabled')}</p>}
                <input ref={input} type="file" accept="image/jpeg,image/png" hidden aria-label={t('receipts.chooseFile')}
                    disabled={!canStartUpload || upload.isPending} onChange={event => choose(event.target.files?.[0])} />
            <input ref={camera} type="file" accept="image/jpeg,image/png" capture="environment" hidden aria-label={t('receipts.takePhoto')}
                disabled={!canStartUpload || upload.isPending} onChange={event => choose(event.target.files?.[0])} />
            <div className="grid gap-2 sm:grid-cols-2">
                <AverioButton variant={selection ? 'secondary' : 'primary'} disabled={!canStartUpload || upload.isPending} onClick={() => camera.current?.click()}>{t('receipts.takePhoto')}</AverioButton>
                <AverioButton disabled={!canStartUpload || upload.isPending} onClick={() => input.current?.click()}>{t('receipts.chooseFile')}</AverioButton>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-300">{t('receipts.formats')}</p>
            {selection && <div className="grid gap-3 rounded-xl border border-emerald-200 bg-emerald-50/50 p-3 dark:border-emerald-900 dark:bg-emerald-950/20">
                <SelectedReceipt file={selection.file} />
                <div className="flex flex-wrap gap-2">
                    <AverioButton variant="primary" disabled={!canSubmitUpload || upload.isPending}
                        onClick={() => upload.mutate(selection)}>{t(upload.isPending ? 'receipts.uploading' : upload.isError ? 'receipts.retryUpload' : 'receipts.upload')}</AverioButton>
                    <AverioButton disabled={upload.isPending} onClick={() => { setSelection(null); upload.reset(); if (input.current) input.current.value = ''; if (camera.current) camera.current.value = ''; }}>{t('receipts.removePhoto')}</AverioButton>
                </div>
            </div>}
            {fileError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{fileError}</p>}
            {upload.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{upload.error.message} {t('receipts.uploadRecovery')}</p>}
        </AverioCard>
        {notice && <p role="status" className="text-sm text-emerald-800 dark:text-emerald-200">{notice}</p>}
        {inbox.isPending && <p role="status">{t('common.status.loading')}</p>}
        {inbox.isError && <p role="alert">{inbox.error.message} <AverioButton onClick={() => void inbox.refetch()}>{t('receipts.retry')}</AverioButton></p>}
        {inbox.isSuccess && scans.length === 0 && <AverioCard className="grid justify-items-center gap-3 py-10 text-center">
            <svg aria-hidden="true" focusable="false" viewBox="0 0 120 100" className="h-28 w-36 text-emerald-600 dark:text-emerald-400" fill="none" stroke="currentColor" strokeWidth="3"><path d="M35 10h50v80l-10-6-10 6-10-6-10 6-10-6V10Z"/><path d="M45 28h30M45 42h30M45 56h20"/><circle cx="90" cy="68" r="18" className="fill-white dark:fill-slate-950"/><path d="m81 68 6 6 12-13"/></svg>
            <h3 className="font-bold">{t('receipts.emptyTitle')}</h3><p className="max-w-md text-sm">{t('receipts.emptyHelp')}</p>
            <AverioButton variant="primary" disabled={!canStartUpload} onClick={() => input.current?.click()}>{t('receipts.chooseFile')}</AverioButton>
        </AverioCard>}
        {scans.length > 0 && <h3 className="font-bold">{t('receipts.inboxTitle')}</h3>}
        <ul className="grid gap-3" aria-label={t('receipts.title')}>
            {orderedScans.map(scan => <li key={scan.id}><AverioCard className="grid gap-3">
                <div className="flex flex-wrap items-start justify-between gap-2"><div className="min-w-0"><h3 className="break-words font-bold">{scan.draft?.merchant || t('receipts.receipt')}</h3>
                    <p className="text-xs text-slate-600 dark:text-slate-300">{new Date(scan.createdAt).toLocaleString(i18n.resolvedLanguage ?? i18n.language)}</p></div>
                    <span className={'rounded-full px-3 py-1 text-xs font-semibold ' + (scan.status === 'Ready' ? 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200' : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200')}>{t('receipts.status.' + scan.status, { defaultValue: t('receipts.status.Unknown') })}</span></div>
                {scan.possibleDuplicate && <p role="status" className="text-sm text-amber-900 dark:text-amber-200">{t('receipts.duplicate')} <AverioButton onClick={() => setMatching(scan.id)}>{t('receipts.viewMatch')}</AverioButton></p>}
                {scan.status === 'Ready' && scan.draft && <p className="text-lg font-bold">{scan.draft.total ?? '—'} {scan.draft.currency}</p>}
                {['Queued', 'Processing'].includes(scan.status) && <p className="text-sm" role="status">{t('receipts.processingHelp')}</p>}
                {scan.status === 'Ready' && <p className="text-sm">{t('receipts.expires', { date: new Date(scan.expiresAt).toLocaleDateString(i18n.resolvedLanguage ?? i18n.language) })}</p>}
                {scan.status === 'Failed' && <div className="grid gap-2">
                    <p className="text-sm">{t(['provider_unavailable', 'attempts_exhausted', 'timeout', 'processing_failed'].includes(scan.failureCode ?? '') ? 'receipts.providerFailed' : 'receipts.failedHelp')}</p>
                    <p className="text-xs">{t('receipts.newScanCharge')}</p>
                    <div className="flex flex-wrap gap-2">
                        <AverioButton disabled={!canStartUpload || upload.isPending} onClick={() => input.current?.click()}>{t('receipts.anotherPhoto')}</AverioButton>
                        <AverioButton onClick={() => { manualSave.reset(); setManual(scan); }}>{t('receipts.enterManually')}</AverioButton>
                    </div>
                </div>}
                {scan.status === 'Uploading' && <p className="text-sm">{t('receipts.uploadRecovery')}</p>}
                <div className="flex flex-wrap gap-2">
                    {scan.status === 'Ready' && scan.draft && <AverioButton variant="primary" onClick={() => { post.reset(); setReview(scan); }}>{t('receipts.review')}</AverioButton>}
                    {scan.status === 'Posted' && scan.imageAvailable && <>
                        <AverioButton onClick={() => setPhoto(scan)}>{t('receipts.viewPhoto')}</AverioButton>
                        <AverioButton disabled={remove.isPending} onClick={() => { remove.reset(); setDiscard(scan); }}>{t('receipts.deletePhoto')}</AverioButton>
                    </>}
                    {scan.status === 'Posted' && scan.postedTransactionId && <Link to="/app/transactions" search={{ transaction: scan.postedTransactionId }} className="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-semibold text-emerald-700 focus-visible:outline-2 dark:text-emerald-300"><ConceptIcon name="transactions" />{t('receipts.openTransaction')}</Link>}
                    {!['Posted', 'Discarded', 'Expired'].includes(scan.status) && <AverioButton disabled={remove.isPending} onClick={() => { remove.reset(); setDiscard(scan); }}>{t('receipts.discard')}</AverioButton>}
                </div>
            </AverioCard></li>)}
        </ul>
        {inbox.hasNextPage && <AverioButton disabled={inbox.isFetchingNextPage} onClick={() => void inbox.fetchNextPage()}>{t('receipts.loadMore')}</AverioButton>}
        {review?.draft && <TransactionEditor key={review.id} userId={userId} householdId={householdId} transaction={null}
            receiptDraft={review.draft} receiptEvidence={<ReceiptEvidence householdId={householdId} scan={review} onMatch={() => setMatching(review.id)} />}
            receiptPhotoAction={<ReceiptPhoto householdId={householdId} scan={review} buttonOnly />}
            onSaveReceipt={(request, currency, reviewedTotal, rememberCategoryRules) => post.mutateAsync({ scan: review, request, currency, reviewedTotal, rememberCategoryRules }).then(() => undefined)}
            busy={post.isPending} onCancel={() => setReview(null)} />}
        {manual && <TransactionEditor key={'manual-' + manual.id} userId={userId} householdId={householdId} transaction={null}
            receiptEvidence={<><p>{t('receipts.manualHelp')}</p><ReceiptPhoto householdId={householdId} scan={manual} /></>}
            receiptPhotoAction={<ReceiptPhoto householdId={householdId} scan={manual} buttonOnly />}
            onSave={request => manualSave.mutateAsync(request).then(() => undefined)} busy={manualSave.isPending} onCancel={() => setManual(null)} />}
        {matching && <MatchingReceipt userId={userId} householdId={householdId} id={matching} onClose={() => setMatching(null)} />}
        {photo && <AverioDialog open title={t('receipts.viewPhoto')} closeLabel={t('common.actions.close')} onDismiss={() => setPhoto(null)} actions={<AverioButton onClick={() => setPhoto(null)}>{t('common.actions.close')}</AverioButton>}>
            <p className="mb-3 text-sm">{t('receipts.imageExpires', { date: new Date(photo.expiresAt).toLocaleDateString(i18n.resolvedLanguage ?? i18n.language) })}</p>
            <ReceiptPhoto householdId={householdId} scan={photo} />
        </AverioDialog>}
        {discard && <AverioDialog open title={t(discard.status === 'Posted' ? 'receipts.deletePhoto' : 'receipts.discard')} closeLabel={t('common.actions.close')} busy={remove.isPending} onDismiss={() => setDiscard(null)}
            actions={<><AverioButton disabled={remove.isPending} onClick={() => setDiscard(null)}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="danger" disabled={remove.isPending} onClick={() => remove.mutate(discard)}>{t(discard.status === 'Posted' ? 'receipts.deletePhoto' : 'receipts.discard')}</AverioButton></>}>
            <p>{t(discard.status === 'Posted' ? 'receipts.deletePhotoHelp' : 'receipts.discardHelp')}</p>{remove.isError && <p role="alert">{remove.error.message}</p>}
        </AverioDialog>}
    </section>;
}

function SelectedReceipt({ file }: { file: File }) {
    const { t } = useTranslation();
    const preview = useRef<HTMLImageElement>(null);
    useEffect(() => {
        const url = URL.createObjectURL(file);
        if (preview.current) preview.current.src = url;
        return () => URL.revokeObjectURL(url);
    }, [file]);
    return <div className="flex min-w-0 items-center gap-3">
        <img ref={preview} alt={t('receipts.selectedPhoto')} className="h-24 w-20 shrink-0 rounded-lg bg-white object-contain" />
        <div className="min-w-0"><p className="font-semibold">{t('receipts.readyToScan')}</p><p className="break-all text-sm text-slate-600 dark:text-slate-300">{file.name}</p></div>
    </div>;
}

function ReceiptEvidence({ householdId, scan, onMatch }: { householdId: string; scan: ReceiptScanResult; onMatch: () => void }) {
    const { t } = useTranslation();
    return <div className="grid gap-3">
        {scan.possibleDuplicate && <p role="alert" className="text-sm text-amber-900 dark:text-amber-200">{t('receipts.duplicate')} <AverioButton onClick={onMatch}>{t('receipts.viewMatch')}</AverioButton></p>}
        <ul className="space-y-1 text-sm text-amber-900 dark:text-amber-200">{scan.draft?.warnings.filter(warning => warning !== 'review_required').map(warning => <li key={warning}>{t('receipts.warnings.' + warning, { defaultValue: t('receipts.warnings.review_required') })}</li>)}</ul>
        <details className="rounded-xl border border-slate-200 p-3 dark:border-slate-700"><summary className="min-h-11 cursor-pointer py-3 font-semibold">{t('receipts.viewPhoto')}</summary>
        <ReceiptPhoto householdId={householdId} scan={scan} />
        </details>
        <details className="rounded-xl border border-slate-200 p-3 dark:border-slate-700"><summary className="min-h-11 cursor-pointer font-semibold">{t('receipts.observations')}</summary>
            <p className="mb-3 text-sm">{t('receipts.observationsHelp')}</p>
            <ul className="grid gap-2 text-sm">{scan.draft?.lines.map((line, index) => <li key={index} className="flex flex-wrap justify-between gap-2 border-b border-slate-200 pb-2 dark:border-slate-700">
                <span className="min-w-0 break-words">{line.description} <span className="text-xs">({t('receipts.kinds.' + line.kind)})</span></span><span>{line.amount ?? t('receipts.unknownAmount')}</span>
            </li>)}</ul>
        </details>
    </div>;
}


function ReceiptPhoto({ householdId, scan, buttonOnly = false }: { householdId: string; scan: ReceiptScanResult; buttonOnly?: boolean }) {
    const { t } = useTranslation();
    const [imageFailed, setImageFailed] = useState(false);
    const [expanded, setExpanded] = useState(false);
    const [zoom, setZoom] = useState(100);
    const src = `${API_BASE_URL}/api/v1/households/${encodeURIComponent(householdId)}/receipt-scans/${encodeURIComponent(scan.id)}/image`;
    const retry = <p role="alert">{t('receipts.imageFailed')} <AverioButton onClick={() => setImageFailed(false)}>{t('receipts.retry')}</AverioButton></p>;
    return <>
        {!buttonOnly && (!imageFailed ? <img src={src} alt={t('receipts.imageAlt')} className="max-h-80 w-full rounded-xl bg-slate-100 object-contain dark:bg-slate-900" onError={() => setImageFailed(true)} /> : retry)}
        <AverioButton onClick={() => { setZoom(100); setExpanded(true); }}>{t('receipts.expandPhoto')}</AverioButton>
        {expanded && <AverioDialog open wide title={t('receipts.expandPhoto')} closeLabel={t('common.actions.close')} onDismiss={() => setExpanded(false)}
            actions={<AverioButton onClick={() => setExpanded(false)}>{t('common.actions.close')}</AverioButton>}>
            <p>{t('receipts.zoomHelp')}</p>
            <label className="my-3 flex flex-wrap items-center gap-3">{t('receipts.zoom')} · {zoom}%
                <input type="range" min="100" max="400" step="25" value={zoom} aria-label={t('receipts.zoom')} onChange={event => setZoom(Number(event.target.value))} />
            </label>
            {!imageFailed ? <div className="max-h-[65dvh] overflow-auto" tabIndex={0} role="region" aria-label={t('receipts.imageAlt')}>
                <img src={src} alt={t('receipts.imageAlt')} style={{ width: zoom + '%', maxWidth: 'none' }} onError={() => setImageFailed(true)} />
            </div> : retry}
        </AverioDialog>}
    </>;
}

function MatchingReceipt({ userId, householdId, id, onClose }: { userId: string; householdId: string; id: string; onClose: () => void }) {
    const { t, i18n } = useTranslation();
    const match = useQuery({
        queryKey: [...receiptKeys.all(userId, householdId), 'match', id],
        staleTime: 0,
        queryFn: async ({ signal }) => {
            const source = await receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdGet({ householdId, id }, { signal });
            return source.matchingReceiptId ? receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdGet({ householdId, id: source.matchingReceiptId }, { signal }) : null;
        },
    });
    return <AverioDialog open title={t('receipts.matchTitle')} closeLabel={t('common.actions.close')} onDismiss={onClose}
        actions={<AverioButton onClick={onClose}>{t('common.actions.close')}</AverioButton>}>
        {match.isPending && <p role="status">{t('common.status.loading')}</p>}
        {match.isError && <p role="alert">{match.error.message} <AverioButton onClick={() => void match.refetch()}>{t('receipts.retry')}</AverioButton></p>}
        {match.isSuccess && !match.data && <p>{t('receipts.noMatch')}</p>}
        {match.data && <div className="grid gap-3">
            <p className="font-bold">{match.data.draft?.merchant || t('receipts.receipt')}</p>
            <p>{new Date(match.data.createdAt).toLocaleString(i18n.resolvedLanguage ?? i18n.language)} · {t('receipts.status.' + match.data.status, { defaultValue: t('receipts.status.Unknown') })}</p>
            {match.data.draft && <p>{match.data.draft.total} {match.data.draft.currency}</p>}
            {match.data.imageAvailable && <ReceiptPhoto householdId={householdId} scan={match.data} />}
            {match.data.postedTransactionId && <Link to="/app/transactions" search={{ transaction: match.data.postedTransactionId }} className="inline-flex min-h-11 items-center font-semibold text-emerald-700 dark:text-emerald-300">{t('receipts.openTransaction')}</Link>}
        </div>}
    </AverioDialog>;
}

