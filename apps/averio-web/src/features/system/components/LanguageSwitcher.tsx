import {
    useTranslation,
} from 'react-i18next';

export function LanguageSwitcher() {
    const {
        t,
        i18n,
    } = useTranslation();

    const language =
        (
            i18n.resolvedLanguage
            ?? i18n.language
            ?? 'en'
        ).split('-')[0];

    return (
        <label className="flex items-center gap-3 text-sm text-slate-600">
            <span>
                {t('language.label')}
            </span>

            <select
                value={language}
                onChange={(event) =>
                    void i18n.changeLanguage(
                        event.target.value,
                    )
                }
                className="
                    rounded-lg
                    border
                    border-slate-300
                    bg-white
                    px-3
                    py-2
                    text-slate-900
                    outline-none
                    transition
                    focus:border-slate-500
                "
            >
                <option value="en">
                    {t('language.en')}
                </option>

                <option value="de">
                    {t('language.de')}
                </option>

                <option value="hu">
                    {t('language.hu')}
                </option>
            </select>
        </label>
    );
}