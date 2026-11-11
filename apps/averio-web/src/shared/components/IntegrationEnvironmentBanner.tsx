import { useTranslation } from 'react-i18next';
import { runtimeConfiguration } from '../runtimeConfiguration';

export function IntegrationEnvironmentBanner() {
    const { t } = useTranslation();

    if (runtimeConfiguration.environment !== 'integration') {
        return null;
    }

    return (
        <div
            role="status"
            className="flex min-h-8 items-center justify-center gap-2 border-b border-amber-300 bg-amber-50 px-4 py-1 text-center text-xs font-bold uppercase tracking-[0.12em] text-amber-950 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100"
        >
            <span aria-hidden="true" className="size-2 rounded-full bg-amber-600 dark:bg-amber-300" />
            {t('environment.integration')}
        </div>
    );
}

