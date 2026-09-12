import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

const linkClassName = `
    block rounded-xl px-4 py-3
    text-sm font-medium
    transition-colors
    focus-visible:outline
    focus-visible:outline-2
    focus-visible:outline-offset-2
    focus-visible:outline-primary-600
`;

const activeClassName = `
    bg-primary-100 text-primary-800
    dark:bg-primary-950 dark:text-primary-200
`;

const inactiveClassName = `
    text-slate-600
    hover:bg-slate-100 hover:text-slate-950
    dark:text-slate-300
    dark:hover:bg-slate-900 dark:hover:text-white
`;

export function AppNavigation() {
    const { t } = useTranslation();

    return (
        <nav aria-label={t('app.navigation.label')}>
            <ul className="flex gap-2 lg:flex-col">
                <li className="flex-1 lg:flex-none">
                    <Link
                        to="/app"
                        activeOptions={{
                            exact: true,
                        }}
                        className={linkClassName}
                        activeProps={{
                            className: activeClassName,
                        }}
                        inactiveProps={{
                            className: inactiveClassName,
                        }}
                    >
                        {t('app.navigation.dashboard')}
                    </Link>
                </li>

                <li className="flex-1 lg:flex-none">
                    <Link
                        to="/app/accounts"
                        className={linkClassName}
                        activeProps={{
                            className: activeClassName,
                        }}
                        inactiveProps={{
                            className: inactiveClassName,
                        }}
                    >
                        {t('app.navigation.accounts')}
                    </Link>
                </li>
            </ul>
        </nav>
    );
}