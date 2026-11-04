import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export function ImportedBadge({ batchId }: { batchId?: string | null }) {
    const { t } = useTranslation();
    const style = 'mt-2 inline-flex min-h-8 items-center rounded-full border border-slate-200 bg-slate-50 px-2.5 text-xs font-medium text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300';
    return batchId ? <Link to="/app/import-history" search={{ batch: batchId }}
        title={t('importHistory.openBatch')} className={style + ' hover:border-primary-400 hover:text-primary-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:hover:text-primary-300'}>
        {t('importHistory.imported')}
    </Link> : <span className={style}>{t('importHistory.imported')}</span>;
}