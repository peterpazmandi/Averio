import { useTranslation } from 'react-i18next';
import type { TransferResponse, TransferSideResponse } from '../../shared/api/generated';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';

function TransferSide({ side, label, language }: { side?: TransferSideResponse | null; label: string; language: string }) {
    const { t } = useTranslation();
    return <div className="min-w-0 rounded-[17px] border border-[#E4E7EC] bg-white px-[15px] py-3.5 shadow-[0_10px_30px_rgba(16,24,40,0.06)] dark:border-[#233044] dark:bg-[#0D1726]">
        <div className="flex items-center gap-2 text-[10px] font-semibold text-[#667085] dark:text-[#AAB4C4]">
            <span className="grid size-[30px] shrink-0 place-items-center rounded-[10px] bg-primary-500/10 text-primary-600 dark:text-primary-400"><ConceptIcon name={side ? 'accounts' : 'lock'} /></span>
            {label}
        </div>
        <strong className="mt-2.5 block text-xs wrap-anywhere">{side?.accountName ?? t('transfers.privateAccount')}</strong>
        {side && <strong className="mt-1 block text-[21px] tracking-tight wrap-anywhere tabular-nums group-data-[voided=true]:line-through">
            {formatDecimalAmount(side.amount, side.currency, language)}
        </strong>}
    </div>;
}

export function TransferTimeline({ rows, canEdit, onEdit }: {
    rows: TransferResponse[];
    canEdit: boolean;
    onEdit: (transfer: TransferResponse) => void;
}) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const formatter = new Intl.DateTimeFormat(language, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' });
    return <div aria-label={t('transfers.title')} className="relative mx-auto w-full max-w-[1180px] pb-6 pl-[18px] min-[761px]:pl-0 before:absolute before:inset-y-0 before:left-[5px] before:w-px before:bg-[#E4E7EC] dark:before:bg-[#233044] min-[761px]:before:left-1/2">
        {rows.map((row, index) => <div key={row.id}>
            {(index === 0 || rows[index - 1].occurredOn !== row.occurredOn) && <div className="relative z-10 mt-2.5 mb-3.5 -ml-[13px] flex min-[761px]:ml-0 min-[761px]:justify-center">
                <time dateTime={row.occurredOn} className="rounded-full border border-[#E4E7EC] bg-white px-[11px] py-1.5 text-[10px] font-extrabold tracking-wide text-[#667085] uppercase dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#AAB4C4]">
                    {formatter.format(new Date(row.occurredOn + 'T00:00:00Z'))}
                </time>
            </div>}
            <article data-voided={row.isVoided} className="group relative mb-5 grid min-w-0 grid-cols-1 gap-2 data-[voided=true]:opacity-60 min-[761px]:grid-cols-[minmax(0,1fr)_70px_minmax(0,1fr)] min-[761px]:gap-0">
                <TransferSide side={row.source} label={t('transfers.fromAccount')} language={language} />
                <span aria-hidden="true" className="absolute top-5 -left-[18px] size-[11px] rounded-full border-[3px] border-[#F6F8FB] bg-primary-500 ring-1 ring-[#E4E7EC] dark:border-[#08111F] dark:ring-[#233044] min-[761px]:hidden" />
                <span className="relative z-10 grid size-8 place-items-center justify-self-center rounded-full border border-[#E4E7EC] bg-white text-primary-600 dark:border-[#233044] dark:bg-[#0D1726] dark:text-primary-400 min-[761px]:self-center"><ConceptIcon name="transfers" /></span>
                <TransferSide side={row.destination} label={t('transfers.toAccount')} language={language} />
                <footer className="relative z-10 mt-2 flex min-w-0 flex-wrap items-center justify-between gap-2 rounded-xl border border-[#E4E7EC] bg-white px-3 py-2 text-xs text-[#667085] dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#AAB4C4] min-[761px]:col-span-3">
                    <div className="min-w-0 flex-1 wrap-anywhere">
                        <span className="font-semibold">{t(row.isVoided ? 'transfers.voided' : 'transfers.posted')}</span>
                        {row.note && <p className="mt-1">{row.note}</p>}
                        {(!row.source || !row.destination) && <p className="mt-1">{t('transfers.redactedHelp')}</p>}
                    </div>
                    {row.canModify && !row.isVoided && <AverioButton disabled={!canEdit} onClick={() => onEdit(row)}>{t('transfers.edit')}</AverioButton>}
                </footer>
            </article>
        </div>)}
    </div>;
}
