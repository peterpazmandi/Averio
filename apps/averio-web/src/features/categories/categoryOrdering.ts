import type { TFunction } from 'i18next';
import type { CategoryResponse } from '../../shared/api/generated';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';

/** Sort displayed names, not untranslated keys. Never mutate the query cache. */
export function sortCategories<T extends Pick<CategoryResponse, 'id' | 'name' | 'systemKey' | 'isPinned'> & { pinOrder?: number | null }>(
    categories: readonly T[], t: TFunction, language: string, pinnedFirst = true,
): T[] {
    const collator = new Intl.Collator(language, { usage: 'sort', sensitivity: 'base', numeric: true });
    return [...categories].sort((a, b) =>
        (pinnedFirst ? Number(Boolean(b.isPinned)) - Number(Boolean(a.isPinned)) : 0)
        || (pinnedFirst && a.isPinned && b.isPinned ? (a.pinOrder ?? 0) - (b.pinOrder ?? 0) : 0)
        || collator.compare(getCategoryDisplayName(a, t), getCategoryDisplayName(b, t))
        || a.id.localeCompare(b.id));
}

export function normalizeSearch(value: string, language: string) {
    return value.toLocaleLowerCase(language).normalize('NFD').replace(/\p{M}/gu, '').trim();
}
