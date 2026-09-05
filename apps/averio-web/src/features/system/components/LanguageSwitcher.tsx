import {
    useTranslation,
} from 'react-i18next';

const languages = [
    {
        code: 'en',
        label: 'EN',
    },
    {
        code: 'de',
        label: 'DE',
    },
    {
        code: 'hu',
        label: 'HU',
    },
] as const;

export function LanguageSwitcher() {
    const {
        t,
        i18n,
    } = useTranslation();

    const current =
        (
            i18n.resolvedLanguage
            ?? i18n.language
            ?? 'en'
        ).split('-')[0];

    return (
        <div
            role="group"
            aria-label={
                t('preferences.language.label')
            }
            className="
                flex gap-1 rounded-2xl
                border border-slate-200
                bg-white/80 p-1
                shadow-sm backdrop-blur
                dark:border-slate-800
                dark:bg-slate-900/80
            "
        >
            {languages.map(
                language => {
                    const active =
                        current ===
                        language.code;

                    return (
                        <button
                            key={
                                language.code
                            }
                            type="button"
                            aria-pressed={
                                active
                            }
                            title={t(
                                `preferences.language.${language.code}`,
                            )}
                            onClick={() =>
                                void i18n
                                    .changeLanguage(
                                        language.code,
                                    )
                            }
                            className={`
                                min-w-10
                                rounded-xl
                                px-2.5 py-2
                                text-xs
                                font-semibold
                                transition

                                ${
                                    active
                                        ? `
                                            bg-primary-500
                                            text-slate-950
                                            shadow-sm
                                        `
                                        : `
                                            text-slate-500
                                            hover:bg-slate-100
                                            hover:text-slate-900

                                            dark:text-slate-400
                                            dark:hover:bg-slate-800
                                            dark:hover:text-white
                                        `
                                }
                            `}
                        >
                            {
                                language.label
                            }
                        </button>
                    );
                },
            )}
        </div>
    );
}