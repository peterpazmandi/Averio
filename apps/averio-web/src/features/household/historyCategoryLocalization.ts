import type { TFunction } from 'i18next';
import type { HistoryItem } from '../../shared/api/generated';
import { systemCategoriesEN } from '../../i18n/locales/systemCategories';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';

export function getHistoricalCategoryDisplayName(
    item: Pick<HistoryItem, 'categoryName' | 'isSystemCategory'>,
    t: TFunction,
) {
    // Explicit custom classification wins even when the saved name equals a key.
    if (item.isSystemCategory === false) return item.categoryName;

    // Older snapshots have no discriminator. Resolve only exact known keys;
    // never infer translations from English labels or user-entered descriptions.
    const legacySystemKey = item.isSystemCategory == null
        && Object.prototype.hasOwnProperty.call(systemCategoriesEN, item.categoryName);
    if (item.isSystemCategory === true || legacySystemKey) {
        return getCategoryDisplayName({ systemKey: item.categoryName, name: null }, t);
    }

    return item.categoryName;
}
