import { useTranslation } from 'react-i18next';
export function ReimbursementBadge({ status }: { status?: string | null }) {
    const { t } = useTranslation();
    if (!status || !['NeedsSubmission', 'Submitted', 'Rejected', 'Cancelled', 'PartiallyReceived', 'Received'].includes(status)) return null;
    const tone = status === 'NeedsSubmission' ? 'bg-amber-50 text-amber-900 dark:bg-amber-950 dark:text-amber-200'
        : status === 'Submitted' ? 'bg-emerald-50 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200'
        : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200';
    return <span className={'mt-2 inline-flex max-w-full items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ' + tone}>
        <span aria-hidden="true">↻</span><span>{t('reimbursements.badge', { status: t('reimbursements.status.' + status) })}</span>
    </span>;
}
