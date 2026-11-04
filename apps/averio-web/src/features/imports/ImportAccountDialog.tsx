import { useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '../../shared/api/ApiError';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { normalizeBalanceInput } from '../../shared/money/decimalAmount';
import { useAccountMutations } from '../accounts/api/useAccountMutations';

export function ImportAccountDialog({ userId, householdId, memberId, source, currency, firstDate, onCreated, onClose }: {
    userId: string; householdId: string; memberId: string; source: string; currency: string; firstDate: string;
    onCreated: (id: string, visibility: string) => void; onClose: () => void;
}) {
    const { t } = useTranslation();
    const formId = useId();
    const nameRef = useRef<HTMLInputElement>(null);
    const submitting = useRef(false);
    const { create } = useAccountMutations(userId, householdId);
    const [name, setName] = useState(source);
    const [type, setType] = useState('Checking');
    const [scope, setScope] = useState('Personal');
    const [amount, setAmount] = useState('');
    const [date, setDate] = useState(firstDate);
    const [error, setError] = useState<string>();
    const [discard, setDiscard] = useState(false);
    const dirty = name !== source || type !== 'Checking' || scope !== 'Personal' || amount !== '' || date !== firstDate;
    const dismiss = () => { if (!submitting.current) { if (dirty) setDiscard(true); else onClose(); } };
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (submitting.current) return;
        const balance = normalizeBalanceInput(amount);
        if (balance === null || !amount.trim()) { setError(t('onboarding.account.invalidBalance')); return; }
        submitting.current = true; setError(undefined);
        try {
            const visibility = scope === 'Household' ? 'Household' : 'Private';
            const result = await create.mutateAsync({ name: name.trim(), type, currency, scope, visibility,
                ownerHouseholdMemberIds: [memberId], includeInHouseholdReports: scope === 'Household',
                balanceAnchorAmount: balance, balanceAnchorDate: date });
            onCreated(result.id, visibility);
        } catch (cause) {
            setError(cause instanceof ApiError ? [cause.message, ...new Set(Object.values(cause.validationErrors ?? {}).flat())].join(' ') : t('errors.genericDescription'));
        } finally { submitting.current = false; }
    }
    return <AverioDialog open title={t(discard ? 'accountManagement.unsavedTitle' : 'imports.createAccount')}
        closeLabel={t('accountManagement.cancel')} initialFocusRef={nameRef} busy={create.isPending}
        onDismiss={() => discard ? setDiscard(false) : dismiss()} actions={discard ? <>
            <AverioButton onClick={() => setDiscard(false)}>{t('accountManagement.keepEditing')}</AverioButton>
            <AverioButton variant="danger" onClick={onClose}>{t('accountManagement.discardAndLeave')}</AverioButton>
        </> : <>
            <AverioButton disabled={create.isPending} onClick={dismiss}>{t('accountManagement.cancel')}</AverioButton>
            <AverioButton variant="primary" type="submit" form={formId} disabled={create.isPending}>{t(create.isPending ? 'common.status.loading' : 'accountManagement.save')}</AverioButton>
        </>}>
        {discard ? <p>{t('accountManagement.unsavedDescription')}</p> : <form id={formId} onSubmit={submit}>
            <fieldset disabled={create.isPending} className="grid gap-4">
                <p>{t('imports.createAccountHelp')}</p>
                <AverioField>{t('onboarding.account.name')}<AverioInput ref={nameRef} required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></AverioField>
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('onboarding.account.type')}<AverioSelect value={type} onChange={e => setType(e.target.value)}>
                        {(['Checking', 'Savings', 'Cash', 'CreditCard'] as const).map(value => <option key={value} value={value}>{t('onboarding.account.' + ({ Checking: 'checking', Savings: 'savings', Cash: 'cash', CreditCard: 'creditCard' }[value]))}</option>)}
                    </AverioSelect></AverioField>
                    <AverioField>{t('onboarding.account.currency')}<AverioInput value={currency} readOnly /></AverioField>
                </div>
                <AverioField>{t('imports.accountOwnership')}<AverioSelect value={scope} onChange={e => setScope(e.target.value)}>
                    <option value="Personal">{t('imports.personalAccount')}</option><option value="Household">{t('imports.householdAccount')}</option>
                </AverioSelect></AverioField>
                <p>{t('imports.openingBalanceHelp')}</p>
                <div className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('app.accounts.recordedBalance')}<AverioInput required inputMode="decimal" value={amount} onChange={e => setAmount(e.target.value)} /></AverioField>
                    <AverioField>{t('accountManagement.balanceDate')}<AverioInput required type="date" value={date} onChange={e => setDate(e.target.value)} /></AverioField>
                </div>
                {error && <p role="alert" className="text-red-700 dark:text-red-300">{error}</p>}
            </fieldset>
        </form>}
    </AverioDialog>;
}
