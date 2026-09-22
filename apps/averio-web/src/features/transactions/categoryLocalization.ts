import type { TFunction } from 'i18next';

export interface CategoryNameSource {
    systemKey: string | null;
    name: string | null;
}

export function getCategoryDisplayName(
    category: CategoryNameSource,
    t: TFunction,
) {
    if (category.systemKey) {
        return t(`systemCategories.${category.systemKey}`, {
            defaultValue: category.systemKey,
        });
    }

    return category.name ?? '';
}
