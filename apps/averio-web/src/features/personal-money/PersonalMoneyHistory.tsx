import { useTranslation } from 'react-i18next';
import type { PersonalMoneyEventResponse } from '../../shared/api/generated';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';

export function PersonalMoneyHistory({ entries, currency }: {
    entries: PersonalMoneyEventResponse[];
    currency: string;
}) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;

    return <section className="min-w-0 space-y-2" aria-label={t('personalMoney.history')}>
        <h4 className="font-semibold">{t('personalMoney.history')}</h4>
        {entries.length === 0
            ? <p className="text-sm text-slate-600 dark:text-slate-300">{t('personalMoney.noEntries')}</p>
            : <ul className="divide-y divide-slate-200 dark:divide-slate-700">
                {entries.map(entry => {
                    const nonzero = /[1-9]/.test(entry.amount);
                    const negative = nonzero && entry.amount.startsWith('-');
                    const positive = nonzero && !negative;
                    const kind = t('personalMoney.kind.' + (entry.recurringAllowanceRuleId ? 'RecurringGrant' : entry.kind));
                    return <li key={entry.id} className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-3 text-sm">
                        <div className="min-w-0 flex-1 basis-36">
                            <p className="break-words font-medium">{entry.transactionName || kind}</p>
                            <p className="text-xs text-slate-600 dark:text-slate-300">
                                <time dateTime={entry.occurredOn}>{entry.occurredOn}</time>
                                {entry.transactionName && <> · {kind}</>}
                            </p>
                        </div>
                        <strong className={'max-w-full break-words tabular-nums ' + (negative
                            ? 'text-red-700 dark:text-red-300'
                            : positive ? 'text-emerald-700 dark:text-emerald-300' : 'text-slate-600 dark:text-slate-300')}>
                            {positive ? '+' : ''}{formatDecimalAmount(entry.amount, currency, language)}
                        </strong>
                    </li>;
                })}
            </ul>}
    </section>;
}
