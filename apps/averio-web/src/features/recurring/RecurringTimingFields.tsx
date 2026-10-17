import { useTranslation } from 'react-i18next';
import type { SaveRecurringRuleRequest } from '../../shared/api/generated';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';

export type RecurringTiming = Pick<SaveRecurringRuleRequest, 'frequency' | 'startsOn' | 'endsOn' | 'automatic'>;

/** Shared schedule controls for transaction creation and schedule maintenance. */
export function RecurringTimingFields({ value, onChange, busy = false, anchorLocked = false, showStart = true }: {
    value: RecurringTiming;
    onChange: (patch: Partial<RecurringTiming>) => void;
    busy?: boolean;
    anchorLocked?: boolean;
    showStart?: boolean;
}) {
    const { t } = useTranslation();
    return <>
        <div className="grid gap-4 sm:grid-cols-2">
            <AverioField>{t('recurring.frequency')}
                <AverioSelect aria-label={t('recurring.frequency')} value={value.frequency} disabled={busy || anchorLocked} onChange={e => onChange({ frequency: e.target.value })}>
                    {['Daily', 'Weekly', 'Monthly', 'Yearly'].map(f => <option key={f} value={f}>{t('recurring.' + f)}</option>)}
                </AverioSelect>
            </AverioField>
            {showStart && <AverioField>{t('recurring.starts')}
                <AverioInput required type="date" value={value.startsOn} disabled={busy || anchorLocked}
                    min={anchorLocked ? undefined : new Date().toISOString().slice(0, 10)} onChange={e => onChange({ startsOn: e.target.value })} />
            </AverioField>}
            <AverioField>{t('recurring.ends')}
                <AverioInput type="date" min={value.startsOn} value={value.endsOn ?? ''} disabled={busy}
                    onChange={e => onChange({ endsOn: e.target.value || null })} />
            </AverioField>
        </div>
        <p className="mt-3 text-xs">{t('recurring.datesHelp')}</p>
        <label className="mt-4 flex min-h-11 items-center gap-3 font-semibold">
            <input type="checkbox" className="size-4 accent-emerald-500" checked={value.automatic ?? false} disabled={busy}
                onChange={e => onChange({ automatic: e.target.checked })} />{t('recurring.automatic')}
        </label>
        <p className="text-xs">{t(value.automatic ? 'recurring.automaticHelp' : 'recurring.manualHelp')}</p>
    </>;
}
