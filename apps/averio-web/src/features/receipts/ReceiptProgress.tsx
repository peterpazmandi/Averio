import { useTranslation } from 'react-i18next';

const steps = ['Uploading', 'Queued', 'Processing'];

/** Scan phases only: no elapsed-time estimate or invented percentage. */
export function ReceiptProgress({ status }: { status: string }) {
    const { t } = useTranslation();
    const active = steps.indexOf(status);
    if (active < 0) return null;
    const current = t('receipts.status.' + status);
    return <section className="grid w-full min-w-0 gap-3" aria-label={t('receipts.progress.title')}>
        <p className="text-sm font-semibold" role="status">{t('receipts.progress.current', { status: current })}</p>
        <div role="progressbar" aria-label={t('receipts.progress.title')} aria-valuetext={current} className="flex gap-1 overflow-hidden rounded-full">
            {steps.map((step, index) => <span key={step} className={'h-2 flex-1 ' + (index <= active ? 'bg-emerald-500' : 'bg-slate-200 dark:bg-slate-700')} />)}
        </div>
        <ol className="grid grid-cols-3 gap-2 text-xs" aria-label={t('receipts.progress.steps')}>
            {steps.map((step, index) => <li key={step} aria-current={status === step ? 'step' : undefined}
                className={'min-w-0 break-words ' + (status === step ? 'font-bold text-emerald-800 dark:text-emerald-300' : 'text-slate-600 dark:text-slate-300')}>
                <span aria-hidden="true">{index + 1}. </span>{t('receipts.status.' + step)}
            </li>)}
        </ol>
    </section>;
}
