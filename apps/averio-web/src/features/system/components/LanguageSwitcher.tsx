import {
    useTranslation,
} from 'react-i18next';

interface LanguageSwitcherProps {
    fullWidth?: boolean;
}

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

export function LanguageSwitcher({
    fullWidth = false,
}: LanguageSwitcherProps) {
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
                t(
                    'preferences.language.label',
                )
            }
            className={`
                rounded-2xl

                border
                border-slate-200

                bg-white/80

                p-1

                shadow-sm
                backdrop-blur

                dark:border-slate-800
                dark:bg-slate-900/80

                ${fullWidth
                    ? `
                            grid
                            w-full
                            min-w-0
                            grid-cols-3
                            gap-1
                        `
                    : `
                            flex
                            gap-1
                        `
                }
            `}
        >
            {languages.map(
                language => {
                    const active =
                        current ===
                        language.code;

                    return (
                        <button
                            key={language.code}
                            type="button"
                            aria-pressed={active}
                            onClick={() =>
                                void i18n.changeLanguage(
                                    language.code,
                                )
                            }
                            className={`
                                min-w-0

                                rounded-xl

                                px-3
                                py-2

                                text-center
                                text-xs
                                font-semibold

                                transition

                                ${active
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
                            {language.label}
                        </button>
                    );
                },
            )}
        </div>
    );
}