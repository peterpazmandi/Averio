import { useTranslation } from 'react-i18next';
import { useTheme } from '../../theme/ThemeProvider';
import '../../styles/concept.css';
import { ConceptIcon } from './ConceptIcon';

export function ConceptPreferences() {
    const { t, i18n } = useTranslation();
    const { theme, toggleTheme } = useTheme();
    return (
        <div className="av-ui" style={{ minHeight: 0, background: 'transparent' }}>
            <div className="av-preferences">
                <select className="av-language" aria-label={t('preferences.language.label')}
                    value={(i18n.resolvedLanguage ?? i18n.language).split('-')[0]}
                    onChange={(event) => { void i18n.changeLanguage(event.target.value); }}>
                    <option value="en">EN</option>
                    <option value="de">DE</option>
                    <option value="hu">HU</option>
                </select>
                <button type="button" className="av-icon-btn" onClick={toggleTheme}
                    aria-label={t(theme === 'dark' ? 'preferences.theme.light' : 'preferences.theme.dark')}
                    title={t(theme === 'dark' ? 'preferences.theme.light' : 'preferences.theme.dark')}>
                    <ConceptIcon name="theme" />
                </button>
            </div>
        </div>
    );
}
