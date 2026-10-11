import { invitationsEN, invitationsDE, invitationsHU } from './locales/invitations';
import { householdSettingsEN, householdSettingsDE, householdSettingsHU } from './locales/householdSettings';
import { activitiesEN, activitiesDE, activitiesHU } from './locales/activities';
import { iconNamesEN, iconNamesDE, iconNamesHU } from './locales/iconNames';
import { categoryManagementEN, categoryManagementDE, categoryManagementHU } from './locales/categoryManagement';
import { transfersEN, transfersDE, transfersHU } from './locales/transfers';
import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import { accountManagementEN, accountManagementDE, accountManagementHU } from './locales/accountManagement';
import { transactionManagementEN, transactionManagementDE, transactionManagementHU } from './locales/transactionManagement';
import { systemCategoriesEN, systemCategoriesDE, systemCategoriesHU } from './locales/systemCategories';

import { conceptEN, conceptDE, conceptHU } from './locales/concept';

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
                translation: { ...en, invitations: invitationsEN, householdSettings: householdSettingsEN, activities: activitiesEN, iconNames: iconNamesEN, accountOwnership: accountOwnershipEN, accountManagement: accountManagementEN, transactionManagement: transactionManagementEN, transfers: transfersEN, categoryManagement: categoryManagementEN, systemCategories: systemCategoriesEN, concept: conceptEN },
            },
            de: {
                translation: { ...de, invitations: invitationsDE, householdSettings: householdSettingsDE, activities: activitiesDE, iconNames: iconNamesDE, accountOwnership: accountOwnershipDE, accountManagement: accountManagementDE, transactionManagement: transactionManagementDE, transfers: transfersDE, categoryManagement: categoryManagementDE, systemCategories: systemCategoriesDE, concept: conceptDE },
            },
            hu: {
                translation: { ...hu, invitations: invitationsHU, householdSettings: householdSettingsHU, activities: activitiesHU, iconNames: iconNamesHU, accountOwnership: accountOwnershipHU, accountManagement: accountManagementHU, transactionManagement: transactionManagementHU, transfers: transfersHU, categoryManagement: categoryManagementHU, systemCategories: systemCategoriesHU, concept: conceptHU },
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


