import { useTranslation } from 'react-i18next';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';

export type RecurringType = 'expense' | 'income' | 'reimbursement';
export function RecurringTypeDialog({ onChoose, onClose }: { onChoose: (type: RecurringType) => void; onClose: () => void }) {
    const { t } = useTranslation();
    return <AverioDialog open title={t('reimbursements.creation.title')} closeLabel={t('common.actions.close')} onDismiss={onClose} actions={<AverioButton onClick={onClose}>{t('common.actions.cancel')}</AverioButton>}>
        <div className="grid gap-3">
            {(['expense', 'income', 'reimbursement'] as const).map((type, index) => <button key={type} type="button" onClick={() => onChoose(type)}
                className="flex min-h-24 items-start gap-4 rounded-[17px] border border-l-[3px] border-slate-200 border-l-primary-500 bg-white p-5 text-left hover:bg-emerald-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-slate-700 dark:border-l-primary-500 dark:bg-[#0D1726] dark:hover:bg-emerald-950/40">
                <span aria-hidden="true" className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-50 text-xl text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300">{['↗', '↙', '↻'][index]}</span>
                <span className="grid min-w-0 gap-1"><span className="font-semibold">{t('reimbursements.creation.' + type)}</span><span className="text-sm text-slate-600 dark:text-slate-300">{t('reimbursements.creation.' + type + 'Help')}</span></span>
            </button>)}
        </div>
    </AverioDialog>;
}
