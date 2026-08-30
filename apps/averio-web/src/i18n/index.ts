import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import { en } from './locales/en';
import { de } from './locales/de';
import { hu } from './locales/hu';

void i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        resources: {
            en: {
                translation: en,
            },
            de: {
                translation: de,
            },
            hu: {
                translation: hu,
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

export default i18n;