import { ApiError } from '../../shared/api/ApiError';
import { useId, useState } from 'react';
import { format } from 'date-fns';
import { useTranslation } from 'react-i18next';
import type { AccountResponse, CreateTransferRequest, TransferResponse } from '../../shared/api/generated';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioField, AverioInput, AverioSelect, AverioTextarea } from '../../shared/components/ui/AverioFormControls';

interface Props {
    transfer: TransferResponse | null;
    accounts: AccountResponse[];
    busy: boolean;
    onClose: () => void;
    onSave: (request: CreateTransferRequest) => Promise<void>;
    onVoid: () => Promise<void>;
    onReload: () => Promise<void>;
}

export function TransferEditor({ transfer, accounts, busy: mutationBusy, onClose, onSave, onVoid, onReload }: Props) {
    const { t } = useTranslation();
    const formId = useId();
    const [sourceId, setSourceId] = useState(transfer ? transfer.source?.accountId ?? '' : accounts.find(a => a.status === 'Active')?.id ?? '');
    const [destinationId, setDestinationId] = useState(transfer?.destination?.accountId ?? '');
    const [sent, setSent] = useState(transfer?.source?.amount ?? '');
    const [received, setReceived] = useState(transfer?.destination?.amount ?? '');
    const [date, setDate] = useState(transfer?.occurredOn ?? format(new Date(), 'yyyy-MM-dd'));
    const [note, setNote] = useState(transfer?.note ?? '');
    const [error, setError] = useState('');
    const [conflict, setConflict] = useState(false);
    const [reloading, setReloading] = useState(false);
    const busy = mutationBusy || reloading;
    const [confirmVoid, setConfirmVoid] = useState(false);
    const source = accounts.find(a => a.id === sourceId);
    const destination = accounts.find(a => a.id === destinationId);
    const sameCurrency = source?.currency === destination?.currency;
    const activePair = source?.status === 'Active' && destination?.status === 'Active';
    const editable = !transfer || (transfer.canModify && !transfer.isVoided);
    const normalize = (amount: string) => amount.trim().replace(',', '.');

    async function submit() {
        if (busy || conflict || !editable || !activePair || sourceId === destinationId) return;
        setError('');
        try {
            await onSave({ sourceAccountId: sourceId, destinationAccountId: destinationId,
                sentAmount: normalize(sent), receivedAmount: normalize(sameCurrency ? sent : received),
                occurredOn: date, note: note.trim() || null });
        } catch (failure) {
            setError(failure instanceof Error ? failure.message : t('transfers.failed'));
            if (transfer && failure instanceof ApiError && failure.status === 409) setConflict(true);
        }
    }

    async function voidTransfer() {
        if (busy || conflict || !editable) return;
        setError('');
        try { await onVoid(); }
        catch (failure) {
            setError(failure instanceof Error ? failure.message : t('transfers.failed'));
            if (transfer && failure instanceof ApiError && failure.status === 409) setConflict(true);
        }
    }

    async function reload() {
        setReloading(true);
        try { await onReload(); }
        catch (failure) {
            setError(failure instanceof Error ? failure.message : t('transfers.failed'));
        } finally { setReloading(false); }
    }

    return <AverioDialog open busy={busy} title={t(transfer ? 'transfers.edit' : 'transfers.add')}
        closeLabel={t('transfers.close')} onDismiss={() => { if (!busy) onClose(); }}
        actions={<>
            {editable && transfer && <AverioButton type="button" disabled={busy || conflict}
                onClick={() => setConfirmVoid(true)}>{t('transfers.void')}</AverioButton>}
            <AverioButton type="button" disabled={busy} onClick={onClose}>{t('transfers.close')}</AverioButton>
            {editable && <AverioButton form={formId} type="submit" variant="primary"
                disabled={busy || conflict || !activePair || sourceId === destinationId}>{t(busy ? 'transfers.saving' : 'transfers.save')}</AverioButton>}
        </>}>
        <form id={formId} className="grid gap-4 [&_p]:text-[13px] [&_p]:leading-relaxed [&_p]:wrap-anywhere" onSubmit={event => { event.preventDefault(); void submit(); }}>
            <p>{t('transfers.balanceHelp')}</p>
            {error && <p className="text-red-600 dark:text-red-400" role="alert">{error}</p>}
            {conflict && <div role="alert" className="grid justify-items-start gap-2">
                <p>{t('transfers.conflictHelp')}</p>
                <AverioButton type="button" disabled={busy} onClick={() => { void reload(); }}>{t('transfers.reloadLatest')}</AverioButton>
            </div>}
            {transfer && <p>{t('transfers.fixedAccounts')}</p>}
            {transfer && !activePair && <p role="status">{t('transfers.archivedHelp')}</p>}
            <fieldset disabled={busy || conflict || !editable} className="grid min-w-0 gap-4 border-0 p-0 disabled:opacity-65">
                <AverioCard role="group" aria-labelledby={`${formId}-movement`}>
                    <h3 id={`${formId}-movement`} className="mb-4 flex items-center gap-2 text-sm font-bold"><ConceptIcon name="transfers" />{t('transfers.movementSection')}</h3>
                    <div className="grid grid-cols-1 gap-4 min-[621px]:grid-cols-2">
                        <AverioField>{t('transfers.fromAccount')}
                            <AverioSelect required value={sourceId} disabled={!!transfer}
                                onChange={e => { setSourceId(e.target.value); setReceived(''); }}>
                                <option value="">{t('transfers.chooseAccount')}</option>
                                {accounts.filter(a => a.status === 'Active' || (!!transfer && a.id === sourceId)).map(a =>
                                    <option key={a.id} value={a.id} disabled={a.id === destinationId}>{a.name} · {a.currency}</option>)}
                            </AverioSelect>
                        </AverioField>
                        <AverioField>{t('transfers.toAccount')}
                            <AverioSelect required value={destinationId} disabled={!!transfer}
                                onChange={e => { setDestinationId(e.target.value); setReceived(''); }}>
                                <option value="">{t('transfers.chooseAccount')}</option>
                                {accounts.filter(a => a.status === 'Active' || (!!transfer && a.id === destinationId)).map(a =>
                                    <option key={a.id} value={a.id} disabled={a.id === sourceId}>{a.name} · {a.currency}</option>)}
                            </AverioSelect>
                        </AverioField>
                        <AverioField>{t('transfers.sent')} {source?.currency}
                            <AverioInput inputMode="decimal" required maxLength={20}
                                pattern="[0-9]+([.,][0-9]{1,4})?" value={sent} onChange={e => setSent(e.target.value)} />
                        </AverioField>
                        {!sameCurrency && <AverioField>{t('transfers.received')} {destination?.currency}
                            <AverioInput inputMode="decimal" required maxLength={20}
                                pattern="[0-9]+([.,][0-9]{1,4})?" value={received} onChange={e => setReceived(e.target.value)} />
                        </AverioField>}
                    </div>
                    {source && destination && !sameCurrency && <p className="mt-3 text-[#667085] dark:text-[#AAB4C4]">{t('transfers.fxHelp')}</p>}
                </AverioCard>
                <AverioCard role="group" aria-labelledby={`${formId}-details`}>
                    <h3 id={`${formId}-details`} className="mb-4 flex items-center gap-2 text-sm font-bold"><ConceptIcon name="receipt" />{t('transfers.detailsSection')}</h3>
                    <div className="grid gap-4">
                        <AverioField>{t('transfers.date')}
                            <AverioInput type="date" required value={date} onChange={e => setDate(e.target.value)} />
                        </AverioField>
                        <AverioField>{t('transfers.note')}
                            <AverioTextarea maxLength={500} value={note} onChange={e => setNote(e.target.value)} />
                        </AverioField>
                    </div>
                </AverioCard>
            </fieldset>
            <p>{t('transfers.privacyHelp')}</p>
            {confirmVoid && <div className="flex flex-wrap gap-2 rounded-[14px] border border-red-500 p-3.5 [&>p]:w-full" role="group" aria-label={t('transfers.void')}>
                <p>{t('transfers.voidHelp')}</p>
                <AverioButton type="button" disabled={busy} onClick={() => setConfirmVoid(false)}>{t('transfers.cancel')}</AverioButton>
                <AverioButton variant="danger" type="button" disabled={busy || conflict} onClick={() => { void voidTransfer(); }}>{t('transfers.confirmVoid')}</AverioButton>
            </div>}
        </form>
    </AverioDialog>;
}
