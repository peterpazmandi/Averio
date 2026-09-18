import { useTranslation } from 'react-i18next';

export function ConceptBrand() {
    const { t } = useTranslation();
    return (
        <div className="av-brand">
            <div className="av-brand-mark" aria-hidden="true" />
            <div><strong>Averio</strong><small>{t('concept.tagline')}</small></div>
        </div>
    );
}
