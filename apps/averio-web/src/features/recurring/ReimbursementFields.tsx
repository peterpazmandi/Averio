import { useTranslation } from 'react-i18next';
import type { RecurringReimbursementConfiguration } from '../../shared/api/generated';
import { AverioDisclosure } from '../../shared/components/ui/AverioDisclosure';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';

export function ReimbursementFields({ value, onChange, accounts, currency, busy }: {
    value: RecurringReimbursementConfiguration | null;
    onChange: (value: RecurringReimbursementConfiguration | null) => void;
    accounts: { id: string; name: string; currency: string; status: string }[]; currency: string; busy: boolean;
}) {
    const { t } = useTranslation();
    const eligible = accounts.filter(a => a.currency === currency && a.status === 'Active');
    const patch = (change: Partial<RecurringReimbursementConfiguration>) => value && onChange({ ...value, ...change });
    return <AverioDisclosure title={t('reimbursements.settings')} defaultOpen={!!value}>
        <label className="flex min-h-11 items-center gap-3"><input type="checkbox" className="size-4 accent-emerald-500" checked={!!value} disabled={busy}
            onChange={e => onChange(e.target.checked ? { provider: '', calculation: 'Fixed', value: '', maximumAmount: null,
                destinationAccountId: eligible[0]?.id ?? '', paymentDelayDays: 7, submissionReminderDays: 2 } : null)} />{t('reimbursements.enable')}</label>
        {value && <div className="grid gap-3 sm:grid-cols-2">
            <p className="text-sm sm:col-span-2">{t('reimbursements.externalHelp')}</p>
            <AverioField>{t('reimbursements.provider')}<AverioInput required maxLength={160} value={value.provider} disabled={busy} onChange={e => patch({ provider: e.target.value })} /></AverioField>
            <AverioField>{t('reimbursements.calculation')}<AverioSelect aria-label={t('reimbursements.calculation')} value={value.calculation} disabled={busy} onChange={e => patch({ calculation: e.target.value })}>
                <option value="Fixed">{t('reimbursements.fixed')}</option><option value="Percentage">{t('reimbursements.percentage')}</option>
            </AverioSelect></AverioField>
            <AverioField>{t(value.calculation === 'Percentage' ? 'reimbursements.percentage' : 'reimbursements.estimate')} · {value.calculation === 'Percentage' ? '%' : currency}
                <AverioInput required inputMode="decimal" value={value.value} disabled={busy} onChange={e => patch({ value: e.target.value.replace(',', '.') })} /></AverioField>
            <AverioField>{t('reimbursements.maximum')} · {currency}<AverioInput inputMode="decimal" value={value.maximumAmount ?? ''} disabled={busy} onChange={e => patch({ maximumAmount: e.target.value.replace(',', '.') || null })} /></AverioField>
            <AverioField>{t('reimbursements.destination')}<AverioSelect aria-label={t('reimbursements.destination')} required value={value.destinationAccountId} disabled={busy} onChange={e => patch({ destinationAccountId: e.target.value })}>
                <option value="">{t('reimbursements.chooseAccount')}</option>
                {!eligible.some(a => a.id === value.destinationAccountId) && value.destinationAccountId && <option value={value.destinationAccountId} disabled>{t('reimbursements.unavailableAccount')}</option>}
                {eligible.map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}
            </AverioSelect></AverioField>
            <AverioField>{t('reimbursements.delay')}<AverioInput required type="number" min={0} max={365} step={1} value={value.paymentDelayDays} disabled={busy} onChange={e => patch({ paymentDelayDays: Number(e.target.value) })} /></AverioField>
            <AverioField>{t('reimbursements.reminderDays')}<AverioInput type="number" min={0} max={365} step={1} value={value.submissionReminderDays ?? ''} disabled={busy} onChange={e => patch({ submissionReminderDays: e.target.value === '' ? null : Number(e.target.value) })} /></AverioField>
            <p className="text-xs sm:col-span-2">{t('reimbursements.estimateHelp')}</p>
        </div>}
    </AverioDisclosure>;
}
