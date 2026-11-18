import { useTranslation } from 'react-i18next';
import { useId } from 'react';
import type { RecurringReimbursementConfiguration } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDisclosure } from '../../shared/components/ui/AverioDisclosure';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';

function estimate(payment: string, config: RecurringReimbursementConfiguration): string | null {
    const scaled = (value: string) => {
        if (!/^\d{1,15}(\.\d{1,4})?$/.test(value)) return null;
        const [whole, fraction = ''] = value.split('.');
        return BigInt(whole) * 10000n + BigInt(fraction.padEnd(4, '0'));
    };
    const amount = scaled(payment), value = scaled(config.value), cap = config.maximumAmount ? scaled(config.maximumAmount) : null;
    if (!amount || !value || (config.maximumAmount && !cap) || (config.calculation === 'Percentage' && value > 1000000n)) return null;
    let result = config.calculation === 'Percentage' ? (amount * value + 500000n) / 1000000n : value;
    if (cap !== null && result > cap) result = cap;
    if (result > amount) result = amount;
    return result > 0n ? `${result / 10000n}.${(result % 10000n).toString().padStart(4, '0')}` : null;
}

export function ReimbursementFields({ value, onChange, accounts, currency, paymentAmount, sourceAccountId, busy }: {
    value: RecurringReimbursementConfiguration | null;
    onChange: (value: RecurringReimbursementConfiguration | null) => void;
    accounts: { id: string; name: string; currency: string; status: string; cutoverAtUtc?: string | null }[];
    currency: string; paymentAmount: string; sourceAccountId: string; busy: boolean;
}) {
    const { t, i18n } = useTranslation();
    const id = useId();
    const eligible = accounts.filter(a => a.currency === currency && a.status === 'Active' && !a.cutoverAtUtc);
    const patch = (change: Partial<RecurringReimbursementConfiguration>) => value && onChange({ ...value, ...change });
    const destination = value?.destinationAccountId || (eligible.some(a => a.id === sourceAccountId) ? sourceAccountId : '');
    const preview = value ? estimate(paymentAmount, value) : null;
    const heading = (key: string, help: string) => <h4 className="flex items-center justify-between gap-2 font-semibold" aria-label={t(key)}>{t(key)} <InfoPopover label={t(key)}>{t(help)}</InfoPopover></h4>;
    return <AverioCard className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="font-bold" aria-label={t('reimbursements.settings')}>{t('reimbursements.settings')} <InfoPopover label={t('reimbursements.settings')}>{t('reimbursements.externalHelp')}</InfoPopover></h3>
            <label className="flex min-h-11 items-center gap-3 text-sm font-medium"><input type="checkbox" className="size-4 accent-emerald-500" checked={!!value} disabled={busy}
                onChange={e => onChange(e.target.checked ? { provider: '', calculation: 'Fixed', value: '', maximumAmount: null,
                    destinationAccountId: eligible.some(a => a.id === sourceAccountId) ? sourceAccountId : '', paymentDelayDays: 7, submissionReminderDays: 2 } : null)} />{t('reimbursements.enable')}</label>
        </div>
        {value && <>
            <div className="rounded-xl bg-emerald-50 p-4 dark:bg-emerald-950/40">
                <ol className="flex flex-wrap gap-x-5 gap-y-2 text-sm font-semibold text-emerald-900 dark:text-emerald-200">
                    {['recordStep', 'submitStep', 'awaitStep'].map((step, index) => <li key={step}>{index + 1}. {t('reimbursements.design.' + step)}</li>)}
                </ol>
                <p className="mt-2 text-sm">{t('reimbursements.design.requiredSubmission')}</p>
            </div>
            <div className="grid gap-3">
                {heading('reimbursements.design.who', 'reimbursements.design.whoHelp')}
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('reimbursements.provider')}<AverioInput required maxLength={160} value={value.provider} disabled={busy} onChange={e => patch({ provider: e.target.value })} /></AverioField>
                    <AverioField>{t('reimbursements.destination')}<AverioSelect aria-label={t('reimbursements.destination')} required value={destination} disabled={busy} onChange={e => patch({ destinationAccountId: e.target.value })}>
                        <option value="">{t('reimbursements.chooseAccount')}</option>
                        {!eligible.some(a => a.id === destination) && destination && <option value={destination} disabled>{t('reimbursements.unavailableAccount')}</option>}
                        {eligible.map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}
                    </AverioSelect></AverioField>
                </div>
            </div>
            <div className="grid gap-3 border-t border-slate-200 pt-3 dark:border-slate-700">
                {heading('reimbursements.design.howMuch', 'reimbursements.estimateHelp')}
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('reimbursements.calculation')}<AverioSelect aria-label={t('reimbursements.calculation')} value={value.calculation} disabled={busy} onChange={e => patch({ calculation: e.target.value })}>
                        <option value="Fixed">{t('reimbursements.fixed')}</option><option value="Percentage">{t('reimbursements.percentage')}</option>
                    </AverioSelect></AverioField>
                    <AverioField>{t(value.calculation === 'Percentage' ? 'reimbursements.percentage' : 'reimbursements.estimate')} · {value.calculation === 'Percentage' ? '%' : currency}
                        <AverioInput required inputMode="decimal" value={value.value} disabled={busy} onChange={e => patch({ value: e.target.value.replace(',', '.') })} /></AverioField>
                </div>
                <AverioDisclosure title={t('reimbursements.maximum')} defaultOpen={!!value.maximumAmount} summary={value.maximumAmount ? `${value.maximumAmount} ${currency}` : undefined}>
                    <AverioField>{t('reimbursements.maximum')} · {currency}<AverioInput inputMode="decimal" value={value.maximumAmount ?? ''} disabled={busy} onChange={e => patch({ maximumAmount: e.target.value.replace(',', '.') || null })} /></AverioField>
                </AverioDisclosure>
                {preview && <p role="status" className="rounded-xl bg-slate-50 p-3 text-sm dark:bg-slate-900">{t('reimbursements.design.preview')} <strong>{formatDecimalAmount(preview, currency, i18n.resolvedLanguage ?? i18n.language)}</strong></p>}
            </div>
            <div className="grid gap-3 border-t border-slate-200 pt-3 dark:border-slate-700">
                {heading('reimbursements.design.when', 'reimbursements.design.whenHelp')}
                <div className="grid items-start gap-4 sm:grid-cols-2">
                    <AverioField>{t('reimbursements.delay')}<AverioInput required type="number" min={0} max={365} step={1} value={value.paymentDelayDays} disabled={busy} onChange={e => patch({ paymentDelayDays: Number(e.target.value) })} /></AverioField>
                    <div className="grid gap-2">
                        <label className="flex min-h-11 items-center gap-3 text-sm font-semibold"><input type="checkbox" className="size-4 accent-emerald-500" checked={value.submissionReminderDays !== null} disabled={busy}
                            onChange={e => patch({ submissionReminderDays: e.target.checked ? 2 : null })} />{t('reimbursements.design.remind')}</label>
                        {value.submissionReminderDays !== null && <div className="grid gap-1">
                            <AverioField htmlFor={id}>{t('reimbursements.design.reminderDays')}</AverioField>
                            <AverioInput id={id} required type="number" min={0} max={365} step={1} value={value.submissionReminderDays} disabled={busy} onChange={e => patch({ submissionReminderDays: Number(e.target.value) })} />
                        </div>}
                    </div>
                </div>
            </div>
        </>}
    </AverioCard>;
}
