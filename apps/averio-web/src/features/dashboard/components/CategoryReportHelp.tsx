import { useTranslation } from 'react-i18next';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';

export function CategoryReportHelp({ kind }: { kind: 'Expense' | 'Income' }) {
    const { t } = useTranslation();
    return <InfoPopover label={t(kind === 'Income' ? 'categorySpending.incomeTitle' : 'categorySpending.title')}>
        <span className="block">{t(kind === 'Income' ? 'categorySpending.incomeHelp' : 'categorySpending.help')}</span>
        <span className="mt-2 block">{t('categorySpending.details')}</span>
    </InfoPopover>;
}
