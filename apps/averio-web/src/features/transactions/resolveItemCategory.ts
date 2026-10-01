/**
 * Keep an existing assignment even when it is absent from active choices.
 * The server validates compatibility on save; incompatible assignments require
 * an explicit replacement instead of silently changing financial categorization.
 */
export function resolveItemCategory(categoryId: string, originalCategoryId: string | undefined,
    available: boolean, uncategorizedId: string): string {
    if (categoryId && (available || originalCategoryId === categoryId)) return categoryId;
    return uncategorizedId || categoryId;
}
