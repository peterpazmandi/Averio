import i18n from '../../i18n';
import { runtimeConfiguration } from '../runtimeConfiguration';

const cultureMapping: Record<string, string> = {
    en: 'en-US',
    de: 'de-DE',
    hu: 'hu-HU',
};

export const API_BASE_URL =
    (runtimeConfiguration.apiUrl || 'http://localhost:5180')
        .replace(/\/$/, '');

export const getApiCulture = (): string => {
    const language =
        i18n.resolvedLanguage
        ?? i18n.language
        ?? 'en';

    const normalizedLanguage =
        language.split('-')[0];

    return cultureMapping[normalizedLanguage]
        ?? 'en-US';
};
