import { useTranslation } from 'react-i18next';

import {
    useTheme,
} from '../../theme/ThemeProvider';

export function ThemeToggle() {
    const { t } =
        useTranslation();

    const {
        theme,
        setTheme,
    } = useTheme();

    return (
        <div
            className="
                flex gap-1 rounded-2xl
                border border-slate-200
                bg-white/80 p-1
                shadow-sm backdrop-blur
                dark:border-slate-800
                dark:bg-slate-900/80
            "
        >
            <ThemeButton
                active={theme === 'light'}
                label={t('preferences.theme.light')}
                onClick={() =>
                    setTheme('light')
                }
            >
                <SunIcon />
            </ThemeButton>

            <ThemeButton
                active={theme === 'dark'}
                label={t('preferences.theme.dark')}
                onClick={() =>
                    setTheme('dark')
                }
            >
                <MoonIcon />
            </ThemeButton>
        </div>
    );
}

interface ThemeButtonProps {
    active: boolean;
    label: string;
    onClick: () => void;
    children: React.ReactNode;
}

function ThemeButton({
    active,
    label,
    onClick,
    children,
}: ThemeButtonProps) {
    return (
        <button
            type="button"
            aria-label={label}
            aria-pressed={active}
            onClick={onClick}
            className={`
                flex size-9 items-center justify-center
                rounded-xl transition
                ${
                    active
                        ? `
                            bg-slate-950 text-white
                            shadow-sm
                            dark:bg-primary-500
                            dark:text-slate-950
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
            {children}
        </button>
    );
}

function SunIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
        >
            <circle cx="12" cy="12" r="4" />
            <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
        </svg>
    );
}

function MoonIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-4"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
        >
            <path d="M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8Z" />
        </svg>
    );
}