import { importsEN, importsDE, importsHU } from './locales/imports';
import { receiptsEN, receiptsDE, receiptsHU } from './locales/receipts';
import { landingEN, landingDE, landingHU } from './locales/landing';
import { recurringEN, recurringDE, recurringHU } from './locales/recurring';
import { savedViewsEN, savedViewsDE, savedViewsHU } from './locales/savedViews';
import { transactionLinksEN, transactionLinksDE, transactionLinksHU } from './locales/transactionLinks';
import { invitationsEN, invitationsDE, invitationsHU } from './locales/invitations';
import { categorySpendingEN, categorySpendingDE, categorySpendingHU } from './locales/categorySpending';
import { householdSettingsEN, householdSettingsDE, householdSettingsHU } from './locales/householdSettings';
import { activitiesEN, activitiesDE, activitiesHU } from './locales/activities';
import { iconNamesEN, iconNamesDE, iconNamesHU } from './locales/iconNames';
import { categoryManagementEN, categoryManagementDE, categoryManagementHU } from './locales/categoryManagement';
import { transfersEN, transfersDE, transfersHU } from './locales/transfers';
import { personalMoneyEN, personalMoneyDE, personalMoneyHU } from './locales/personalMoney';
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
                translation: { ...en, imports: importsEN, landing: landingEN, receipts: receiptsEN, transactionLinks: transactionLinksEN,
            recurring: recurringEN, savedViews: savedViewsEN, categorySpending: categorySpendingEN, invitations: invitationsEN, householdSettings: householdSettingsEN, activities: activitiesEN, iconNames: iconNamesEN, accountOwnership: accountOwnershipEN, accountManagement: accountManagementEN, transactionManagement: transactionManagementEN, transfers: transfersEN, personalMoney: personalMoneyEN, categoryManagement: categoryManagementEN, systemCategories: systemCategoriesEN, concept: conceptEN },
            },
            de: {
                translation: { ...de, imports: importsDE, landing: landingDE, receipts: receiptsDE, transactionLinks: transactionLinksDE,
            recurring: recurringDE, savedViews: savedViewsDE, categorySpending: categorySpendingDE, invitations: invitationsDE, householdSettings: householdSettingsDE, activities: activitiesDE, iconNames: iconNamesDE, accountOwnership: accountOwnershipDE, accountManagement: accountManagementDE, transactionManagement: transactionManagementDE, transfers: transfersDE, personalMoney: personalMoneyDE, categoryManagement: categoryManagementDE, systemCategories: systemCategoriesDE, concept: conceptDE },
            },
            hu: {
                translation: { ...hu, imports: importsHU, landing: landingHU, receipts: receiptsHU, transactionLinks: transactionLinksHU,
            recurring: recurringHU, savedViews: savedViewsHU, categorySpending: categorySpendingHU, invitations: invitationsHU, householdSettings: householdSettingsHU, activities: activitiesHU, iconNames: iconNamesHU, accountOwnership: accountOwnershipHU, accountManagement: accountManagementHU, transactionManagement: transactionManagementHU, transfers: transfersHU, personalMoney: personalMoneyHU, categoryManagement: categoryManagementHU, systemCategories: systemCategoriesHU, concept: conceptHU },
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



