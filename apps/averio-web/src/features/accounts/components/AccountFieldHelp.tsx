import { useTranslation } from 'react-i18next';

/** Native disclosure: usable by keyboard and touch, without hover-only instructions. */
export function AccountFieldHelp({ id, field, children }: { id: string; field: string; children: string }) {
    const { t } = useTranslation();
    return (
        <details className="av-field-help">
            <summary aria-label={t('accountManagement.helpFor', { field })}>
                <span className="av-info-mark" aria-hidden="true">i</span>
                <span>{t('accountManagement.whyFixed')}</span>
            </summary>
            <p id={id}>{children}</p>
        </details>
    );
}
