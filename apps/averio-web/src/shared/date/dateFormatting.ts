import {
    format,
} from 'date-fns';

import {
    de,
    enUS,
    hu,
} from 'date-fns/locale';

import i18n from '../../i18n';

const locales = {
    en: enUS,
    de,
    hu,
};

export const formatAppDateTime = (
    value: string | Date,
): string => {
    const language =
        (
            i18n.resolvedLanguage
            ?? i18n.language
            ?? 'en'
        ).split('-')[0];

    const locale =
        locales[
            language as keyof typeof locales
        ] ?? enUS;

    const date =
        value instanceof Date
            ? value
            : new Date(value);

    return format(
        date,
        'PPpp',
        {
            locale,
        },
    );
};