import { useTranslation } from 'react-i18next';
import { useTheme } from '../../theme/ThemeProvider';
import { AverioSelect } from '../ui/AverioFormControls';
import { ConceptIcon } from './ConceptIcon';

export function ConceptPreferences() {
    const { t, i18n } = useTranslation();
    const { theme, toggleTheme } = useTheme();
    return (
        <div className="flex shrink-0 items-center gap-2">
            <AverioSelect className="min-h-[38px]! w-[66px]! px-[7px]! py-0! text-xs!" aria-label={t('preferences.language.label')}
                value={(i18n.resolvedLanguage ?? i18n.language).split('-')[0]}
                onChange={(event) => { void i18n.changeLanguage(event.target.value); }}>
                <option value="en">EN</option>
                <option value="de">DE</option>
                <option value="hu">HU</option>
            </AverioSelect>
            <button type="button" className="inline-flex size-[38px] shrink-0 items-center justify-center rounded-[11px] border border-[#E4E7EC] bg-white text-[#152033] shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA]" onClick={toggleTheme}
                aria-label={t(theme === 'dark' ? 'preferences.theme.light' : 'preferences.theme.dark')}
                title={t(theme === 'dark' ? 'preferences.theme.light' : 'preferences.theme.dark')}>
                <ConceptIcon name="theme" />
            </button>
        </div>
    );
}
