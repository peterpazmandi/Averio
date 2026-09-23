import { useId, useState } from 'react';
import { format } from 'date-fns';
import { useTranslation } from 'react-i18next';
import type { AccountResponse, CreateTransferRequest, TransferResponse } from '../../shared/api/generated';
import { Popup } from '../../shared/components/Popup';

interface Props {
    transfer: TransferResponse | null;
    accounts: AccountResponse[];
    busy: boolean;
    onClose: () => void;
    onSave: (request: CreateTransferRequest) => Promise<void>;
    onVoid: () => Promise<void>;
}

export function TransferEditor({ transfer, accounts, busy, onClose, onSave, onVoid }: Props) {
    const { t } = useTranslation();
    const formId = useId();
    const [sourceId, setSourceId] = useState(transfer?.source?.accountId ?? accounts[0]?.id ?? '');
    const [destinationId, setDestinationId] = useState(transfer?.destination?.accountId ?? '');
    const [sent, setSent] = useState(transfer?.source?.amount ?? '');
    const [received, setReceived] = useState(transfer?.destination?.amount ?? '');
    const [date, setDate] = useState(transfer?.occurredOn ?? format(new Date(), 'yyyy-MM-dd'));
    const [note, setNote] = useState(transfer?.note ?? '');
    const [error, setError] = useState('');
    const [confirmVoid, setConfirmVoid] = useState(false);
    const source = accounts.find(a => a.id === sourceId);
    const destination = accounts.find(a => a.id === destinationId);
    const sameCurrency = source?.currency === destination?.currency;
    const activePair = source?.status === 'Active' && destination?.status === 'Active';
    const editable = !transfer || (transfer.canModify && !transfer.isVoided);
    const normalize = (amount: string) => amount.trim().replace(',', '.');

    async function submit() {
        setError('');
        try {
            await onSave({ sourceAccountId: sourceId, destinationAccountId: destinationId,
                sentAmount: normalize(sent), receivedAmount: normalize(sameCurrency ? sent : received),
                occurredOn: date, note: note.trim() || null });
        } catch (failure) {
            setError(failure instanceof Error ? failure.message : t('transfers.failed'));
        }
    }

    async function voidTransfer() {
        setError('');
        try { await onVoid(); }
        catch (failure) { setError(failure instanceof Error ? failure.message : t('transfers.failed')); }
    }

    return <Popup open title={t(transfer ? 'transfers.edit' : 'transfers.add')}
        closeLabel={t('transfers.close')} onDismiss={() => { if (!busy) onClose(); }}
        actions={<>
            {editable && transfer && <button type="button" className="av-btn" disabled={busy}
                onClick={() => setConfirmVoid(true)}>{t('transfers.void')}</button>}
            <button type="button" className="av-btn" disabled={busy} onClick={onClose}>{t('transfers.close')}</button>
            {editable && <button form={formId} type="submit" className="av-btn av-primary"
                disabled={busy || !activePair || sourceId === destinationId}>{t(busy ? 'transfers.saving' : 'transfers.save')}</button>}
        </>}>
        <form id={formId} className="av-transfer-form" onSubmit={event => { event.preventDefault(); void submit(); }}>
            <p>{t('transfers.balanceHelp')}</p>
            {error && <p className="av-transfer-error" role="alert">{error}</p>}
            {transfer && <p>{t('transfers.fixedAccounts')}</p>}
            {transfer && !activePair && <p role="status">{t('transfers.archivedHelp')}</p>}
            <fieldset disabled={busy || !editable}>
                <div className="av-transfer-grid">
                    <label>{t('transfers.fromAccount')}
                        <select className="av-select" required value={sourceId} disabled={!!transfer}
                            onChange={e => { setSourceId(e.target.value); setReceived(''); }}>
                            <option value="">{t('transfers.chooseAccount')}</option>
                            {accounts.filter(a => a.status === 'Active' || a.id === sourceId).map(a =>
                                <option key={a.id} value={a.id} disabled={a.id === destinationId}>{a.name} · {a.currency}</option>)}
                        </select>
                    </label>
                    <label>{t('transfers.toAccount')}
                        <select className="av-select" required value={destinationId} disabled={!!transfer}
                            onChange={e => { setDestinationId(e.target.value); setReceived(''); }}>
                            <option value="">{t('transfers.chooseAccount')}</option>
                            {accounts.filter(a => a.status === 'Active' || a.id === destinationId).map(a =>
                                <option key={a.id} value={a.id} disabled={a.id === sourceId}>{a.name} · {a.currency}</option>)}
                        </select>
                    </label>
                    <label>{t('transfers.sent')} {source?.currency}
                        <input className="av-input" inputMode="decimal" required maxLength={20}
                            pattern="[0-9]+([.,][0-9]{1,4})?" value={sent} onChange={e => setSent(e.target.value)} />
                    </label>
                    {!sameCurrency && <label>{t('transfers.received')} {destination?.currency}
                        <input className="av-input" inputMode="decimal" required maxLength={20}
                            pattern="[0-9]+([.,][0-9]{1,4})?" value={received} onChange={e => setReceived(e.target.value)} />
                    </label>}
                    <label>{t('transfers.date')}
                        <input className="av-input" type="date" required value={date} onChange={e => setDate(e.target.value)} />
                    </label>
                </div>
                {source && destination && !sameCurrency && <p>{t('transfers.fxHelp')}</p>}
                <label>{t('transfers.note')}
                    <textarea className="av-input" maxLength={500} value={note} onChange={e => setNote(e.target.value)} />
                </label>
            </fieldset>
            <p>{t('transfers.privacyHelp')}</p>
            {confirmVoid && <div className="av-transfer-confirm" role="group" aria-label={t('transfers.void')}>
                <p>{t('transfers.voidHelp')}</p>
                <button className="av-btn" type="button" disabled={busy} onClick={() => setConfirmVoid(false)}>{t('transfers.cancel')}</button>
                <button className="av-btn" type="button" disabled={busy} onClick={() => { void voidTransfer(); }}>{t('transfers.confirmVoid')}</button>
            </div>}
        </form>
    </Popup>;
}
