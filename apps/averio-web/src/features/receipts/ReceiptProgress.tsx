import { useState } from 'react';
import { useTranslation } from 'react-i18next';

const steps = ['Photo', 'Uploading', 'Queued', 'Processing', 'Ready', 'Saving', 'Posted'];
const outcomes = ['Failed', 'Discarded', 'Expired'];

export function ReceiptProgressDetails({ status }: { status: string }) {
    const { t } = useTranslation();
    const [open, setOpen] = useState(false);
    return <details className="min-w-0 sm:col-span-2" onToggle={event => setOpen(event.currentTarget.open)}>
        <summary className="min-h-11 cursor-pointer py-3 text-sm font-semibold">{t('receipts.progress.title')}</summary>
        {open && <ReceiptProgress status={status} />}
    </details>;
}

/** Milestones, not a time estimate. Terminal outcomes branch off the normal flow. */
export function ReceiptProgress({ status }: { status: string }) {
    const { t } = useTranslation();
    const active = steps.indexOf(status);
    const label = (value: string) => value === 'Photo' ? t('receipts.photoStep')
        : value === 'Saving' ? t('transactionManagement.saving')
        : t('receipts.status.' + value, { defaultValue: t('receipts.status.Unknown') });
    return <section className="grid w-full min-w-0 gap-3" aria-label={t('receipts.progress.title')}>
        <p className="text-sm font-semibold" role="status">{t('receipts.progress.current', { status: label(status) })}</p>
        <ol className="grid grid-cols-2 gap-2 sm:grid-cols-7" aria-label={t('receipts.progress.steps')}>
            {steps.map((step, index) => <li key={step} aria-current={status === step ? 'step' : undefined}
                className={'min-w-0 rounded-lg border p-2 text-xs ' + (status === step
                    ? 'border-emerald-600 bg-emerald-100 font-bold text-emerald-950 ring-1 ring-emerald-600 dark:border-emerald-400 dark:bg-emerald-950 dark:text-emerald-100 dark:ring-emerald-400'
                    : active > index ? 'border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/30 dark:text-emerald-200'
                        : 'border-slate-200 text-slate-600 dark:border-slate-700 dark:text-slate-300')}>
                <span aria-hidden="true" className={'mb-2 block h-1 rounded-full ' + (active >= index ? 'bg-emerald-500' : 'bg-slate-200 dark:bg-slate-700')} />
                <span aria-hidden="true" className="mr-1">{active > index ? '✓' : `${index + 1}.`}</span><span className="break-words">{label(step)}</span>
            </li>)}
        </ol>
        <div className="flex flex-wrap items-center gap-2 text-xs">
            <span>{t('receipts.progress.outcomes')}</span>
            {outcomes.map(outcome => <span key={outcome} aria-current={status === outcome ? 'step' : undefined}
                className={'rounded-full border px-3 py-1 ' + (status === outcome
                    ? 'border-amber-600 bg-amber-100 font-bold text-amber-950 dark:border-amber-400 dark:bg-amber-950 dark:text-amber-100'
                    : 'border-slate-200 text-slate-600 dark:text-slate-300 dark:border-slate-700')}>{label(outcome)}</span>)}
        </div>
    </section>;
}
