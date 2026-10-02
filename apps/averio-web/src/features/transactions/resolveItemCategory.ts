/**
 * Default only an uninitialized choice. Never replace an explicit draft selection
 * because refreshed choices omit it; the selector shows it as unavailable and
 * server validation requires an explicit compatible replacement before saving.
 */
export function resolveItemCategory(categoryId: string, uncategorizedId: string): string {
    return categoryId || uncategorizedId;
}
