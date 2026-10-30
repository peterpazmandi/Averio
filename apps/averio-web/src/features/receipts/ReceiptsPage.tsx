import { useRef, useState } from 'react';
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
        mutationFn: (scan: ReceiptScanResult) => receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansIdDelete({ householdId, id: scan.id, expectedVersion: scan.version }),
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
    const scans = inbox.data?.pages.flat() ?? [];
    function choose(file?: File) {
        upload.reset(); setFileError(''); setNotice('');
        if (!file) { setSelection(null); return; }
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
        <AverioCard className="grid gap-3">
            <h3 className="font-bold">{t('receipts.capture')}</h3>
            <p className="text-sm">{t('receipts.formats')}</p>
            {capabilities.isPending && <p role="status">{t('common.status.loading')}</p>}
            {capabilities.isError && <p role="alert">{capabilities.error.message} <AverioButton onClick={() => void capabilities.refetch()}>{t('receipts.retry')}</AverioButton></p>}
            {capabilities.isSuccess && !capabilities.data.enabled && <p role="status">{t('receipts.disabled')}</p>}
            <label className="grid gap-2 text-sm font-semibold">{t('receipts.chooseFile')}
                <input ref={input} type="file" accept="image/jpeg,image/png" className="min-h-11 min-w-0 max-w-full rounded-xl border border-slate-300 p-2 dark:border-slate-700"
                    disabled={!capabilities.data?.enabled || upload.isPending} onChange={event => choose(event.target.files?.[0])} />
            </label>
            <input ref={camera} type="file" accept="image/jpeg,image/png" capture="environment" hidden aria-label={t('receipts.takePhoto')}
                disabled={!capabilities.data?.enabled || upload.isPending} onChange={event => choose(event.target.files?.[0])} />
            <div className="flex flex-wrap gap-2"><AverioButton disabled={!capabilities.data?.enabled || upload.isPending} onClick={() => camera.current?.click()}>{t('receipts.takePhoto')}</AverioButton>
            <AverioButton variant="primary" disabled={!selection || !capabilities.data?.enabled || upload.isPending}
                onClick={() => selection && upload.mutate(selection)}>{t(upload.isPending ? 'receipts.uploading' : upload.isError ? 'receipts.retryUpload' : 'receipts.upload')}</AverioButton></div>
            {fileError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{fileError}</p>}
            {upload.isError && <p role="alert" className="text-sm text-red-700 dark:text-red-300">{upload.error.message} {t('receipts.uploadRecovery')}</p>}
        </AverioCard>
        {notice && <p role="status" className="text-sm text-emerald-800 dark:text-emerald-200">{notice}</p>}
        {inbox.isPending && <p role="status">{t('common.status.loading')}</p>}
        {inbox.isError && <p role="alert">{inbox.error.message} <AverioButton onClick={() => void inbox.refetch()}>{t('receipts.retry')}</AverioButton></p>}
        {inbox.isSuccess && scans.length === 0 && <AverioCard className="grid justify-items-center gap-3 py-10 text-center">
            <svg aria-hidden="true" focusable="false" viewBox="0 0 120 100" className="h-28 w-36 text-emerald-600 dark:text-emerald-400" fill="none" stroke="currentColor" strokeWidth="3"><path d="M35 10h50v80l-10-6-10 6-10-6-10 6-10-6V10Z"/><path d="M45 28h30M45 42h30M45 56h20"/><circle cx="90" cy="68" r="18" className="fill-white dark:fill-slate-950"/><path d="m81 68 6 6 12-13"/></svg>
            <h3 className="font-bold">{t('receipts.emptyTitle')}</h3><p className="max-w-md text-sm">{t('receipts.emptyHelp')}</p>
            <AverioButton variant="primary" disabled={!capabilities.data?.enabled} onClick={() => input.current?.click()}>{t('receipts.chooseFile')}</AverioButton>
        </AverioCard>}
        <ul className="grid gap-3" aria-label={t('receipts.title')}>
            {scans.map(scan => <li key={scan.id}><AverioCard className="grid gap-3">
                <div className="flex flex-wrap items-start justify-between gap-2"><div className="min-w-0"><h3 className="break-words font-bold">{scan.draft?.merchant || t('receipts.receipt')}</h3>
                    <p className="text-xs text-slate-600 dark:text-slate-300">{new Date(scan.createdAt).toLocaleString(i18n.resolvedLanguage ?? i18n.language)}</p></div>
                    <span className="text-sm font-semibold">{t('receipts.status.' + scan.status, { defaultValue: t('receipts.status.Unknown') })}</span></div>
                {scan.status === 'Ready' && <p className="text-sm">{t('receipts.expires', { date: new Date(scan.expiresAt).toLocaleDateString(i18n.resolvedLanguage ?? i18n.language) })}</p>}
                {scan.status === 'Failed' && <p className="text-sm">{t('receipts.failedHelp')}</p>}
                {scan.status === 'Uploading' && <p className="text-sm">{t('receipts.uploadRecovery')}</p>}
                <div className="flex flex-wrap gap-2">
                    {scan.status === 'Ready' && scan.draft && <AverioButton variant="primary" onClick={() => { post.reset(); setReview(scan); }}>{t('receipts.review')}</AverioButton>}
                    {scan.status === 'Posted' && scan.postedTransactionId && <Link to="/app/transactions" search={{ transaction: scan.postedTransactionId }} className="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-semibold text-emerald-700 focus-visible:outline-2 dark:text-emerald-300"><ConceptIcon name="transactions" />{t('receipts.openTransaction')}</Link>}
                    {!['Posted', 'Discarded', 'Expired'].includes(scan.status) && <AverioButton disabled={remove.isPending} onClick={() => { remove.reset(); setDiscard(scan); }}>{t('receipts.discard')}</AverioButton>}
                </div>
            </AverioCard></li>)}
        </ul>
        {inbox.hasNextPage && <AverioButton disabled={inbox.isFetchingNextPage} onClick={() => void inbox.fetchNextPage()}>{t('receipts.loadMore')}</AverioButton>}
        {review?.draft && <TransactionEditor key={review.id} userId={userId} householdId={householdId} transaction={null}
            receiptDraft={review.draft} receiptEvidence={<ReceiptEvidence householdId={householdId} scan={review} />}
            onSaveReceipt={(request, currency, reviewedTotal, rememberCategoryRules) => post.mutateAsync({ scan: review, request, currency, reviewedTotal, rememberCategoryRules }).then(() => undefined)}
            busy={post.isPending} onCancel={() => setReview(null)} />}
        {discard && <AverioDialog open title={t('receipts.discard')} closeLabel={t('common.actions.close')} busy={remove.isPending} onDismiss={() => setDiscard(null)}
            actions={<><AverioButton disabled={remove.isPending} onClick={() => setDiscard(null)}>{t('common.actions.cancel')}</AverioButton><AverioButton variant="danger" disabled={remove.isPending} onClick={() => remove.mutate(discard)}>{t('receipts.discard')}</AverioButton></>}>
            <p>{t('receipts.discardHelp')}</p>{remove.isError && <p role="alert">{remove.error.message}</p>}
        </AverioDialog>}
    </section>;
}

function ReceiptEvidence({ householdId, scan }: { householdId: string; scan: ReceiptScanResult }) {
    const { t } = useTranslation();
    const [imageFailed, setImageFailed] = useState(false);
    return <div className="grid gap-3">
        {!imageFailed ? <img src={`${API_BASE_URL}/api/v1/households/${encodeURIComponent(householdId)}/receipt-scans/${encodeURIComponent(scan.id)}/image`}
            alt={t('receipts.imageAlt')} className="max-h-80 w-full rounded-xl bg-slate-100 object-contain dark:bg-slate-900" onError={() => setImageFailed(true)} />
            : <p role="alert">{t('receipts.imageFailed')} <AverioButton onClick={() => setImageFailed(false)}>{t('receipts.retry')}</AverioButton></p>}
        <ul className="list-disc space-y-1 pl-5 text-sm">{scan.draft?.warnings.map(warning => <li key={warning}>{t('receipts.warnings.' + warning, { defaultValue: t('receipts.warnings.review_required') })}</li>)}</ul>
        <details className="rounded-xl border border-slate-200 p-3 dark:border-slate-700"><summary className="min-h-11 cursor-pointer font-semibold">{t('receipts.observations')}</summary>
            <p className="mb-3 text-sm">{t('receipts.observationsHelp')}</p>
            <ul className="grid gap-2 text-sm">{scan.draft?.lines.map((line, index) => <li key={index} className="flex flex-wrap justify-between gap-2 border-b border-slate-200 pb-2 dark:border-slate-700">
                <span className="min-w-0 break-words">{line.description} <span className="text-xs">({t('receipts.kinds.' + line.kind)})</span></span><span>{line.amount ?? t('receipts.unknownAmount')}</span>
            </li>)}</ul>
        </details>
    </div>;
}

