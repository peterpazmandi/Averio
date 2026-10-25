import { InfoPopover } from '../../../shared/components/ui/InfoPopover';
import { useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useBlocker } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { format, subDays } from 'date-fns';
import type { AccountOwnerOptionsResponse, AccountResponse, CreateAccountRequest, UpdateAccountRequest } from '../../../shared/api/generated';
import { ApiError } from '../../../shared/api/ApiError';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { FormField } from '../../../shared/components/FormField';
import { normalizeBalanceInput, negateDecimalAmount } from '../../../shared/money/decimalAmount';
import { AccountFieldHelp } from './AccountFieldHelp';
import { AccountOwnershipFields } from './AccountOwnershipFields';
import type { AccountOwnershipValue } from './AccountOwnershipFields';

interface Props {
    account?: AccountResponse;
    options: AccountOwnerOptionsResponse;
    onCreate: (request: CreateAccountRequest) => Promise<unknown>;
    onUpdate: (id: string, request: UpdateAccountRequest) => Promise<unknown>;
    onClose: () => void;
}

export function AccountForm({ account: initialAccount, options, onCreate, onUpdate, onClose }: Props) {
    // Preserve the version alongside the draft, even when a background query refreshes.
    const [account] = useState(initialAccount);
    const { t } = useTranslation();
    const [name, setName] = useState(account?.name ?? '');
    const [type, setType] = useState(account?.type ?? 'Checking');
    const [currency, setCurrency] = useState(account?.currency ?? 'EUR');
    const [amount, setAmount] = useState(account?.balanceAnchorAmount ?? '0');
    const [date, setDate] = useState(() => account?.balanceAnchorDate ?? format(subDays(new Date(), 1), 'yyyy-MM-dd'));
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
    const saved = useRef(false);
    const draft = JSON.stringify({ name, type, currency, amount, date,
        owed: !account && type === 'CreditCard' ? owed : null,
        ...ownership, ownerHouseholdMemberIds: [...ownership.ownerHouseholdMemberIds].sort() });
    const [initialDraft] = useState(draft);
    const dirty = draft !== initialDraft;

    const keepEditingRef = useRef<HTMLButtonElement>(null);
    // Do not offer to discard a request that is already being saved.
    useBlocker({ shouldBlockFn: () => submitting.current && !saved.current, enableBeforeUnload: false });
    const blocker = useBlocker({
        shouldBlockFn: () => dirty && !saved.current && !submitting.current,
        enableBeforeUnload: () => dirty && !saved.current,
        withResolver: true,
    });

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
            saved.current = true;
            onClose();
        } catch (cause) {
            setError(cause instanceof ApiError || cause instanceof RangeError ? cause.message : t('errors.genericDescription'));
            if (cause instanceof ApiError) setValidation(cause.validationErrors);
        } finally { submitting.current = false; setBusy(false); }
    }

    return (
        <>
        <AverioDialog open={blocker.status === 'blocked'} title={t('accountManagement.unsavedTitle')}
            closeLabel={t('accountManagement.keepEditing')} initialFocusRef={keepEditingRef}
            onDismiss={() => blocker.reset?.()}
            actions={<>
                <AverioButton ref={keepEditingRef} onClick={() => blocker.reset?.()}>{t('accountManagement.keepEditing')}</AverioButton>
                <AverioButton variant="danger" onClick={() => blocker.proceed?.()}>{t('accountManagement.discardAndLeave')}</AverioButton>
            </>}>
            <p>{t('accountManagement.unsavedDescription')}</p>
        </AverioDialog>
        <form className="av-account-form" onSubmit={submit} aria-busy={busy} aria-label={t(account ? 'accountManagement.edit' : 'accountManagement.create')}>
            <fieldset disabled={busy} className="av-form-content">
                <section className="av-form-section" aria-labelledby="account-basics-title">
                    <header><h3 id="account-basics-title" aria-label={t('accountManagement.basicsSection')}>{t('accountManagement.basicsSection')} <InfoPopover label={t('accountManagement.basicsSection')}>{t('accountManagement.basicsHelp')}</InfoPopover></h3></header>
                    <div className="av-form-grid">
                        <div className="av-field"><FormField label={t('onboarding.account.name')} autoFocus required maxLength={100} value={name}
                            error={validation?.Name?.[0]} onChange={event => setName(event.target.value)} /></div>
                        <div className="av-field"><label htmlFor="account-type">{t('onboarding.account.type')}</label>
                            <select id="account-type" className="av-control" value={type} onChange={event => setType(event.target.value)}>
                                {(['Checking', 'Savings', 'Cash', 'CreditCard'] as const).map(value => <option key={value} value={value}>
                                    {t('onboarding.account.' + ({ Checking: 'checking', Savings: 'savings', Cash: 'cash', CreditCard: 'creditCard' }[value]))}
                                </option>)}
                            </select>
                        </div>
                    </div>
                </section>
                <AccountOwnershipFields options={options} value={ownership} onChange={setOwnership} lockScope={!!account} />
                <section className="av-form-section" aria-labelledby="account-balance-title">
                    <header><h3 id="account-balance-title">{t('accountManagement.balanceSection')}</h3><p>{t('accountManagement.balanceHelp')}</p></header>
                    <div className="av-form-grid">
                        <div className="av-field" data-locked={!!account}>
                            <label htmlFor="account-currency">{t('onboarding.account.currency')}{account && <span className="av-fixed-label">{t('accountManagement.fixed')}</span>}</label>
                            <select id="account-currency" className="av-control" value={currency} disabled={!!account}
                                aria-describedby={account ? 'currency-help' : undefined} onChange={event => setCurrency(event.target.value)}>
                                {['EUR', 'USD', 'HUF'].map(value => <option key={value}>{value}</option>)}
                            </select>
                            {account && <AccountFieldHelp id="currency-help" field={t('onboarding.account.currency')}>{t('accountManagement.currencyFixed')}</AccountFieldHelp>}
                        </div>
                        {!account && type === 'CreditCard' && <div className="av-field"><label htmlFor="credit-kind">{t('accountManagement.creditKind')}</label>
                            <select id="credit-kind" className="av-control" value={owed ? 'owed' : 'credit'} onChange={event => setOwed(event.target.value === 'owed')}>
                                <option value="owed">{t('accountManagement.owed')}</option><option value="credit">{t('accountManagement.credit')}</option>
                            </select>
                        </div>}
                        <div className="av-field" data-locked={!!account}>
                            <FormField label={t('app.accounts.recordedBalance')} required inputMode="decimal" value={amount} disabled={!!account}
                                aria-describedby={account ? 'balance-help' : undefined}
                                error={validation?.BalanceAnchorAmount?.[0]} onChange={event => setAmount(event.target.value)} />
                            {account && <AccountFieldHelp id="balance-help" field={t('app.accounts.recordedBalance')}>{t('accountManagement.balanceFixed')}</AccountFieldHelp>}
                        </div>
                        <div className="av-field" data-locked={!!account}>
                            <FormField label={t('accountManagement.balanceDate')} type="date" required value={date} disabled={!!account}
                                aria-describedby={account ? 'date-help' : undefined}
                                error={validation?.BalanceAnchorDate?.[0]} onChange={event => setDate(event.target.value)} />
                            {account && <AccountFieldHelp id="date-help" field={t('accountManagement.balanceDate')}>{t('accountManagement.balanceFixed')}</AccountFieldHelp>}
                        </div>
                    </div>
                </section>
                {error && <div role="alert" className="av-card av-error"><p>{error}</p>
                    {validation && <ul>{[...new Set(Object.values(validation).flat())].map(message => <li key={message}>{message}</li>)}</ul>}
                </div>}
                <footer className="av-form-actions">
                    <button className="av-btn" type="button" onClick={onClose}>{t('accountManagement.cancel')}</button>
                    <button className="av-btn av-primary" type="submit">{t(busy ? 'common.status.loading' : 'accountManagement.save')}</button>
                </footer>
            </fieldset>
        </form>
        </>
    );
}
