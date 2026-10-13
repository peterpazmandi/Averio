import { useTranslation } from 'react-i18next';

export function CategoryReportHelp({ kind }: { kind: 'Expense' | 'Income' }) {
    const { t } = useTranslation();
    return <div className="space-y-2 text-xs leading-relaxed text-slate-600 dark:text-slate-300">
        <p>{t(kind === 'Income' ? 'categorySpending.incomeHelp' : 'categorySpending.help')}</p>
        <details>
            <summary className="cursor-pointer rounded py-2 font-medium focus-visible:outline-2 focus-visible:outline-primary-500">{t('categorySpending.about')}</summary>
            <p className="pt-1">{t('categorySpending.details')}</p>
        </details>
    </div>;
}
