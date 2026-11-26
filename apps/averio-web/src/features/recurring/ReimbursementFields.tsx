import { useTranslation } from 'react-i18next';
import { useId } from 'react';
import { useQuery } from '@tanstack/react-query';
import { accountsQueries } from '../accounts/api/accountsQueries';
import type { AccountResponse } from '../../shared/api/generated';
import type { RecurringReimbursementConfiguration } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioDisclosure } from '../../shared/components/ui/AverioDisclosure';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';

function scaled(value: string): bigint | null {
    if (!/^\d{1,15}(\.\d{1,4})?$/.test(value)) return null;
    const [whole, fraction = ''] = value.split('.');
    return BigInt(whole) * 10000n + BigInt(fraction.padEnd(4, '0'));
}

function estimate(payment: string, config: RecurringReimbursementConfiguration): string | null {
    const amount = scaled(payment), value = scaled(config.value), cap = config.maximumAmount ? scaled(config.maximumAmount) : null;
    if (!amount || !value || (config.maximumAmount && !cap) || (config.calculation === 'Percentage' && value > 1000000n)) return null;
    let result = config.calculation === 'Percentage' ? (amount * value + 500000n) / 1000000n : value;
    if (cap !== null && result > cap) result = cap;
    if (result > amount) result = amount;
    return result > 0n ? `${result / 10000n}.${(result % 10000n).toString().padStart(4, '0')}` : null;
}

export function ReimbursementFields({ userId, householdId, value, onChange, accounts, currency, paymentAmount, sourceAccountId, busy, allowDisable = true, limitToPayment = true, allowUnavailableDestination = false, embedded = false }: {
    userId: string; householdId: string;
    allowDisable?: boolean;
    limitToPayment?: boolean;
    allowUnavailableDestination?: boolean;
    embedded?: boolean;
    value: RecurringReimbursementConfiguration | null;
    onChange: (value: RecurringReimbursementConfiguration | null) => void;
    accounts: AccountResponse[];
    currency: string; paymentAmount: string; sourceAccountId: string; busy: boolean;
}) {
    const { t, i18n } = useTranslation();
    const id = useId();
    const owners = useQuery({ ...accountsQueries.ownerOptions(userId, householdId), enabled: !!value });
    const eligible = accounts.filter(a => a.currency === currency && a.status === 'Active' && !a.cutoverAtUtc
        && !!owners.data?.currentMemberId && (a.scope === 'Household' || a.owners.some(o => o.householdMemberId === owners.data.currentMemberId)));
    const patch = (change: Partial<RecurringReimbursementConfiguration>) => value && onChange({ ...value, ...change });
    const destination = value?.destinationAccountId || (eligible.some(a => a.id === sourceAccountId) ? sourceAccountId : '');
    const preview = value ? estimate(paymentAmount, value) : null;
    const payment = scaled(paymentAmount), entered = value ? scaled(value.value) : null;
    const exceedsPayment = limitToPayment && value?.calculation === 'Fixed' && payment !== null && entered !== null && entered > payment;
    const paymentLimit = limitToPayment && payment !== null && payment > 0n
        ? t('reimbursements.estimateLimit', { amount: formatDecimalAmount(paymentAmount, currency, i18n.resolvedLanguage ?? i18n.language) }) : '';
    const heading = (key: string, help: string) => <h4 className="flex items-center justify-between gap-2 font-semibold" aria-label={t(key)}>{t(key)} <InfoPopover label={t(key)}>{t(help)}</InfoPopover></h4>;
    const Container = embedded ? 'div' : AverioCard;
    return <Container className="grid gap-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
            {!embedded && <h3 className="font-semibold" aria-label={t('reimbursements.settings')}>{t('reimbursements.settings')} <InfoPopover label={t('reimbursements.settings')}>{t('reimbursements.externalHelp')}</InfoPopover></h3>}
            {allowDisable && <label className="flex min-h-11 items-center gap-3 text-sm font-medium"><input type="checkbox" className="size-4 accent-emerald-500" checked={!!value} disabled={busy}
                onChange={e => onChange(e.target.checked ? { provider: '', calculation: 'Fixed', value: '', maximumAmount: null,
                    destinationAccountId: eligible.some(a => a.id === sourceAccountId) ? sourceAccountId : '', paymentDelayDays: 7, submissionReminderDays: 2 } : null)} />{t('reimbursements.enable')}</label>}
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
                    <div className="grid content-start gap-[7px]">
                        <div className="flex min-h-11 items-center gap-2"><AverioField htmlFor={id + '-provider'}>{t('reimbursements.provider')}</AverioField>
                            <InfoPopover label={t('reimbursements.provider')} id={id + '-provider-help'}>{t('reimbursements.providerHelp')}</InfoPopover></div>
                        <AverioInput id={id + '-provider'} aria-describedby={id + '-provider-help'} required maxLength={160} value={value.provider} disabled={busy} onChange={e => patch({ provider: e.target.value })} />
                    </div>
                    <div className="grid content-start gap-[7px]"><AverioField className="min-h-11 content-center" htmlFor={id + '-destination'}>{t('reimbursements.destination')}</AverioField><AverioSelect id={id + '-destination'} required value={destination} disabled={busy} aria-invalid={!!destination && !eligible.some(a => a.id === destination) || owners.isError || undefined}
                        ref={select => { select?.setCustomValidity(!allowUnavailableDestination && (owners.isError || !eligible.some(a => a.id === destination)) ? t('reimbursements.unavailableAccount') : ''); }}
                        onChange={e => patch({ destinationAccountId: e.target.value })}>
                        <option value="">{t('reimbursements.chooseAccount')}</option>
                        {!eligible.some(a => a.id === destination) && destination && <option value={destination} disabled>{t('reimbursements.unavailableAccount')}</option>}
                        {eligible.map(a => <option key={a.id} value={a.id}>{a.name} · {a.currency}</option>)}
                    </AverioSelect></div>
                </div>
                {owners.isPending && <p role="status">{t('common.status.loading')}</p>}
                {owners.isError && <div role="alert"><p>{t('reimbursements.loadError')}</p><AverioButton onClick={() => { void owners.refetch(); }}>{t('common.actions.retry')}</AverioButton></div>}
            </div>
            <div className="grid gap-3 border-t border-slate-200 pt-3 dark:border-slate-700">
                {heading('reimbursements.design.howMuch', 'reimbursements.estimateHelp')}
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('reimbursements.calculation')}<AverioSelect aria-label={t('reimbursements.calculation')} value={value.calculation} disabled={busy} onChange={e => patch({ calculation: e.target.value })}>
                        <option value="Fixed">{t('reimbursements.fixed')}</option><option value="Percentage">{t('reimbursements.percentage')}</option>
                    </AverioSelect></AverioField>
                    <AverioField>{t(value.calculation === 'Percentage' ? 'reimbursements.percentage' : 'reimbursements.estimate')} · {value.calculation === 'Percentage' ? '%' : currency}
                        <AverioInput required inputMode="decimal" value={value.value} disabled={busy} aria-invalid={exceedsPayment || undefined}
                            aria-describedby={value.calculation === 'Fixed' && paymentLimit ? id + '-estimate-limit' : undefined}
                            ref={input => { input?.setCustomValidity(exceedsPayment ? paymentLimit : ''); }}
                            onChange={e => patch({ value: e.target.value.replace(',', '.') })} /></AverioField>
                </div>
                {value.calculation === 'Fixed' && paymentLimit && <p id={id + '-estimate-limit'} role={exceedsPayment ? 'alert' : undefined}
                    className={`text-sm ${exceedsPayment ? 'text-red-700 dark:text-red-300' : 'text-slate-600 dark:text-slate-300'}`}>{paymentLimit}</p>}
                <AverioDisclosure title={t('reimbursements.maximum')} defaultOpen={!!value.maximumAmount} summary={value.maximumAmount ? `${value.maximumAmount} ${currency}` : undefined}>
                    <div className="grid gap-2"><div className="flex items-center justify-between gap-2">
                        <AverioField htmlFor={id + '-cap'}>{t('reimbursements.maximum')} · {currency}</AverioField>
                        <InfoPopover label={t('reimbursements.maximum')} id={id + '-cap-help'}>{t('reimbursements.capHelp', {
                            payment: formatDecimalAmount('150000', currency || 'EUR', i18n.resolvedLanguage ?? i18n.language),
                            estimate: formatDecimalAmount('30000', currency || 'EUR', i18n.resolvedLanguage ?? i18n.language),
                            cap: formatDecimalAmount('25000', currency || 'EUR', i18n.resolvedLanguage ?? i18n.language) })}</InfoPopover>
                    </div><AverioInput id={id + '-cap'} aria-describedby={id + '-cap-help'} inputMode="decimal" value={value.maximumAmount ?? ''} disabled={busy} onChange={e => patch({ maximumAmount: e.target.value.replace(',', '.') || null })} /></div>
                </AverioDisclosure>
                {preview && <p role="status" className="rounded-xl bg-slate-50 p-3 text-sm dark:bg-slate-900">{t('reimbursements.design.preview')} <strong>{formatDecimalAmount(preview, currency, i18n.resolvedLanguage ?? i18n.language)}</strong></p>}
            </div>
            <div className="grid gap-3 border-t border-slate-200 pt-3 dark:border-slate-700">
                {heading('reimbursements.design.when', 'reimbursements.design.whenHelp')}
                <div className="grid items-stretch gap-4 sm:grid-cols-2">
                    <div className="grid content-start gap-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                        <h5 className="flex min-h-11 items-center gap-2 text-sm font-semibold" aria-label={t('reimbursements.edit.paymentTiming')}>{t('reimbursements.edit.paymentTiming')}
                            <InfoPopover label={t('reimbursements.edit.paymentTiming')} id={id + '-timing-help'}>{t('reimbursements.edit.paymentTimingHelp')}</InfoPopover></h5>
                        <AverioField>{t('reimbursements.delay')}<AverioInput aria-describedby={id + '-timing-help'} required type="number" min={0} max={365} step={1} value={value.paymentDelayDays} disabled={busy} onChange={e => patch({ paymentDelayDays: Number(e.target.value) })} /></AverioField>
                    </div>
                    <div className="grid content-start gap-3 rounded-xl border border-slate-200 p-4 dark:border-slate-700">
                        <label className="flex min-h-11 items-center gap-3 text-sm font-semibold"><input type="checkbox" className="size-4 accent-emerald-500" checked={value.submissionReminderDays !== null} disabled={busy}
                            onChange={e => patch({ submissionReminderDays: e.target.checked ? 2 : null })} />{t('reimbursements.design.remind')}</label>
                        {value.submissionReminderDays !== null ? <div className="grid gap-[7px]">
                            <AverioField htmlFor={id}>{t('reimbursements.design.reminderDays')}</AverioField>
                            <AverioInput id={id} required type="number" min={0} max={365} step={1} value={value.submissionReminderDays} disabled={busy} onChange={e => patch({ submissionReminderDays: Number(e.target.value) })} />
                        </div> : <p className="text-sm text-slate-600 dark:text-slate-300">{t('reimbursements.edit.reminderOff')}</p>}
                    </div>
                </div>
            </div>
        </>}
    </Container>;
}
