import { useTranslation } from 'react-i18next';
import type { TransferResponse, TransferSideResponse } from '../../shared/api/generated';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import type { PersonalMoneyRelation } from '../personal-money/personalMoneyRelations';
import { PersonalMoneyRelationBadge } from '../personal-money/PersonalMoneyRelationBadge';

function TransferSide({ side, label, language }: { side?: TransferSideResponse | null; label: string; language: string }) {
    const { t } = useTranslation();
    return <div className="min-w-0 p-4 min-[761px]:p-5">
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

export function TransferTimeline({ rows, canEdit, onEdit, relations }: {
    rows: TransferResponse[];
    relations: Map<string, PersonalMoneyRelation>;
    canEdit: boolean;
    onEdit: (transfer: TransferResponse) => void;
}) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const formatter = new Intl.DateTimeFormat(language, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric', timeZone: 'UTC' });
    return <div aria-label={t('transfers.title')} className="mx-auto w-full max-w-[1180px] pb-4">
        {rows.map((row, index) => <div key={row.id}>
            {(index === 0 || rows[index - 1].occurredOn !== row.occurredOn) && <div className="mt-2 mb-3 flex">
                <time dateTime={row.occurredOn} className="rounded-full border border-[#E4E7EC] bg-white px-[11px] py-1.5 text-[10px] font-extrabold tracking-wide text-[#667085] uppercase dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#AAB4C4]">
                    {formatter.format(new Date(row.occurredOn + 'T00:00:00Z'))}
                </time>
            </div>}
            <article data-voided={row.isVoided} className="group mb-4 grid min-w-0 grid-cols-1 overflow-hidden rounded-[17px] border border-[#E4E7EC] border-l-[3px] border-l-primary-500 bg-[#F2FAF7] shadow-sm data-[voided=true]:border-l-slate-400 data-[voided=true]:opacity-60 dark:border-[#233044] dark:border-l-primary-500 dark:bg-[#112B36] dark:data-[voided=true]:border-l-slate-500 min-[761px]:grid-cols-[minmax(0,1fr)_48px_minmax(0,1fr)]">
                <TransferSide side={row.source} label={t('transfers.fromAccount')} language={language} />
                <span className="grid size-8 place-items-center justify-self-center rounded-full bg-primary-500/10 text-primary-600 dark:text-primary-400 min-[761px]:self-center"><ConceptIcon name="transfers" /></span>
                <TransferSide side={row.destination} label={t('transfers.toAccount')} language={language} />
                <footer className="mx-4 flex min-w-0 flex-wrap items-center justify-between gap-2 border-t border-[#D9E9E2] py-3 text-xs text-[#667085] dark:border-[#25404B] dark:text-[#AAB4C4] min-[761px]:col-span-3 min-[761px]:mx-5">
                    <div className="min-w-0 flex-1 wrap-anywhere">
                        <span className="font-semibold">{t(row.isVoided ? 'transfers.voided' : 'transfers.posted')}</span>
                        {relations.get(row.id) && <span className="mt-2 block">
                            <PersonalMoneyRelationBadge relation={relations.get(row.id)!} detail />
                        </span>}
                        {row.note && <p className="mt-1">{row.note}</p>}
                        {(!row.source || !row.destination) && <p className="mt-1">{t('transfers.redactedHelp')}</p>}
                    </div>
                    {row.canModify && !row.isVoided && <AverioButton disabled={!canEdit} onClick={() => onEdit(row)}>{t('transfers.edit')}</AverioButton>}
                </footer>
            </article>
        </div>)}
    </div>;
}
