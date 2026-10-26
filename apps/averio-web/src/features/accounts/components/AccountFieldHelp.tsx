import { useTranslation } from 'react-i18next';
import { InfoPopover } from '../../../shared/components/ui/InfoPopover';

/** Keep the locked-field cue visible; share the same help interaction as other pages. */
export function AccountFieldHelp({ id, field, children }: { id: string; field: string; children: string }) {
    const { t } = useTranslation();
    return <span className="inline-flex items-center text-xs text-slate-600 dark:text-slate-300">
        {t('accountManagement.whyFixed')}
        <InfoPopover id={id} label={field}>{children}</InfoPopover>
    </span>;
}
