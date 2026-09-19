import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { format, subDays } from 'date-fns';
import type { AccountOwnerOptionsResponse, AccountResponse, CreateAccountRequest, UpdateAccountRequest } from '../../../shared/api/generated';
import { ApiError } from '../../../shared/api/ApiError';
import { FormField } from '../../../shared/components/FormField';
import { normalizeBalanceInput, negateDecimalAmount } from '../../../shared/money/decimalAmount';
import { AccountOwnershipFields } from './AccountOwnershipFields';
import type { AccountOwnershipValue } from './AccountOwnershipFields';

interface Props {
    account?: AccountResponse;
    options: AccountOwnerOptionsResponse;
    onCreate: (request: CreateAccountRequest) => Promise<unknown>;
    onUpdate: (id: string, request: UpdateAccountRequest) => Promise<unknown>;
    onClose: () => void;
}

export function AccountForm({ account, options, onCreate, onUpdate, onClose }: Props) {
    const { t } = useTranslation();
    const [name, setName] = useState(account?.name ?? '');
    const [type, setType] = useState(account?.type ?? 'Checking');
    const [currency, setCurrency] = useState('EUR');
    const [amount, setAmount] = useState('0');
    const [date, setDate] = useState(() => format(subDays(new Date(), 1), 'yyyy-MM-dd'));
    const [owed, setOwed] = useState(true);
    const [ownership, setOwnership] = useState<AccountOwnershipValue>(() => ({
        scope: account?.scope === 'Personal' ? 'Personal' : 'Household',
        ownerHouseholdMemberIds: account?.owners.map(owner => owner.householdMemberId) ?? [options.currentMemberId],
        visibility: account?.visibility === 'Private' ? 'Private' : 'Household',
        includeInHouseholdReports: account?.includeInHouseholdReports ?? true,
    }));
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [validation, setValidation] = useState<Record<string, string[]> | undefined>();
    const submitting = useRef(false);

    async function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        if (submitting.current) return;
        submitting.current = true;
        setBusy(true); setError(null); setValidation(undefined);
        try {
            if (account) {
                await onUpdate(account.id, {
                    name: name.trim(), type,
                    ownerHouseholdMemberIds: ownership.ownerHouseholdMemberIds,
                    visibility: ownership.visibility,
                    includeInHouseholdReports: ownership.includeInHouseholdReports,
                    expectedUpdatedAt: account.updatedAt,
                });
            } else {
                const normalized = normalizeBalanceInput(amount);
                if (normalized === null) throw new RangeError(t('onboarding.account.invalidBalance'));
                if (type === 'CreditCard' && normalized.startsWith('-'))
                    throw new RangeError(t('onboarding.account.enterPositiveAmount'));
                await onCreate({ name: name.trim(), type, ...ownership, currency,
                    balanceAnchorAmount: type === 'CreditCard' && owed ? negateDecimalAmount(normalized) : normalized,
                    balanceAnchorDate: date });
            }
            onClose();
        } catch (cause) {
            setError(cause instanceof ApiError || cause instanceof RangeError ? cause.message : t('errors.genericDescription'));
            if (cause instanceof ApiError) setValidation(cause.validationErrors);
        } finally { submitting.current = false; setBusy(false); }
    }

    return (
        <form className="av-card space-y-5" onSubmit={submit} aria-busy={busy} aria-labelledby="account-form-title">
            <h2 id="account-form-title" className="text-xl font-semibold">{t(account ? 'accountManagement.edit' : 'accountManagement.create')}</h2>
            <fieldset disabled={busy} className="min-w-0 space-y-5">
                <FormField label={t('onboarding.account.name')} autoFocus required maxLength={100} value={name}
                    error={validation?.Name?.[0]} onChange={event => setName(event.target.value)} />
                <label className="block space-y-2">
                    <span>{t('onboarding.account.type')}</span>
                    <select className="av-select w-full" value={type} onChange={event => setType(event.target.value)}>
                        {(['Checking', 'Savings', 'Cash', 'CreditCard'] as const).map(value => (
                            <option key={value} value={value}>{t('onboarding.account.' + ({ Checking: 'checking', Savings: 'savings', Cash: 'cash', CreditCard: 'creditCard' }[value]))}</option>
                        ))}
                    </select>
                </label>
                <AccountOwnershipFields options={options} value={ownership} onChange={setOwnership} lockScope={!!account} />
                {account ? <p className="av-subtle">{t('accountManagement.immutable', { currency: account.currency })}</p> : (
                    <>
                        <label className="block space-y-2"><span>{t('onboarding.account.currency')}</span>
                            <select className="av-select w-full" value={currency} onChange={event => setCurrency(event.target.value)}>
                                {['EUR', 'USD', 'HUF'].map(value => <option key={value}>{value}</option>)}
                            </select>
                        </label>
                        {type === 'CreditCard' && <label className="block space-y-2"><span>{t('accountManagement.creditKind')}</span>
                            <select className="av-select w-full" value={owed ? 'owed' : 'credit'} onChange={event => setOwed(event.target.value === 'owed')}>
                                <option value="owed">{t('accountManagement.owed')}</option><option value="credit">{t('accountManagement.credit')}</option>
                            </select>
                        </label>}
                        <FormField label={t('app.accounts.recordedBalance')} required inputMode="decimal" value={amount}
                            error={validation?.BalanceAnchorAmount?.[0]} onChange={event => setAmount(event.target.value)} />
                        <FormField label={t('accountManagement.balanceDate')} type="date" required value={date}
                            error={validation?.BalanceAnchorDate?.[0]} onChange={event => setDate(event.target.value)} />
                    </>
                )}
                {error && <div role="alert" className="av-error"><p>{error}</p>
                    {validation && <ul>{[...new Set(Object.values(validation).flat())].map(message => <li key={message}>{message}</li>)}</ul>}
                </div>}
                <div className="flex flex-wrap gap-3">
                    <button className="av-btn av-primary" type="submit">{t(busy ? 'common.status.loading' : 'accountManagement.save')}</button>
                    <button className="av-btn" type="button" onClick={onClose}>{t('accountManagement.cancel')}</button>
                </div>
            </fieldset>
        </form>
    );
}
