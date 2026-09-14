import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import { en } from './locales/en';
import { de } from './locales/de';
import { hu } from './locales/hu';

import {
    accountOwnershipEN,
    accountOwnershipDE,
    accountOwnershipHU,
} from './locales/accountOwnership';

void i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        resources: {
            en: {
                translation: { ...en, accountOwnership: accountOwnershipEN },
            },
            de: {
                translation: { ...de, accountOwnership: accountOwnershipDE },
            },
            hu: {
                translation: { ...hu, accountOwnership: accountOwnershipHU },
            },
        },

        supportedLngs: [
            'en',
            'de',
            'hu',
        ],

        fallbackLng: 'en',

        load: 'languageOnly',

        detection: {
            order: [
                'localStorage',
                'navigator',
            ],

            caches: [
                'localStorage',
            ],
        },

        interpolation: {
            escapeValue: false,
        },

        react: {
            useSuspense: false,
        },
    });

const syncDocumentLang = (language: string) => {
    document.documentElement.lang = language.split('-')[0];
};

i18n.on('initialized', () => {
    syncDocumentLang(
        i18n.resolvedLanguage ?? i18n.language,
    );
});

i18n.on('languageChanged', syncDocumentLang);

export default i18n;
