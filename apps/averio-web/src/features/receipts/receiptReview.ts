import type { ReceiptDraft } from '../../shared/api/generated';
import { normalizeBalanceInput, sumDecimalAmounts } from '../../shared/money/decimalAmount';

// Compare printed evidence, never AI-renamed descriptions. Require two meaningful
// matching words and a unique candidate; short codes alone cannot select an item.
function discountWords(value: string) {
    return [...new Set(value.normalize('NFKD').replace(/\p{M}/gu, '').toLowerCase()
        .match(/[a-z0-9]+/g)?.filter(word => word.length >= 3) ?? [])];
}
function matchesDiscount(discount: string, description: string) {
    const words = discountWords(discount);
    const itemWords = discountWords(description);
    const shared = words.filter(word => itemWords.some(item => item === word
        || (Math.min(item.length, word.length) >= 4 && (item.startsWith(word) || word.startsWith(item)))));
    if (shared.length >= 2 && shared.length / words.length >= 0.6) return true;
    // OCR can corrupt the suffix while preserving a product name and model
    // (e.g. LANDL A60). Require both exact anchors for this fallback, never
    // a price/quantity or fuzzy model prefix. The caller still requires one
    // candidate and exact reconciliation of the complete discounted receipt.
    const exact = words.filter(word => itemWords.includes(word));
    return exact.some(word => /^[a-z]{4,}$/.test(word))
        && exact.some(word => /[a-z]/.test(word) && /[0-9]/.test(word));
}

export function receiptItems(draft: ReceiptDraft) {
    const indexed = draft.lines.map((line, lineIndex) => ({ ...line, lineIndex }));
    const lines = indexed.filter(line => line.kind === 'Item' || line.kind === 'Deposit');
    // Never silently truncate a large receipt into a smaller expense.
    if (lines.length > 100) return [];
    const items = lines.map(line => ({
        receiptLineIndex: line.lineIndex, canRememberCategory: line.kind === 'Item', rememberCategory: false,
        suggestedCategoryId: line.suggestedCategoryId ?? undefined,
        categorySuggestionSource: line.categorySuggestionSource ?? undefined,
        description: line.suggestedDescription?.trim() || line.description,
        originalDescription: line.description, originalAmount: line.amount ?? '',
        discountAmount: '', categoryId: '', amount: line.amount ?? '',
    }));
    const discounts = draft.lines.filter(line => line.kind === 'Discount');
    // A printed total that already matches must not have its discounts applied twice.
    if (!discounts.length || receiptReconciles(draft.total ?? '', items)) return items;
    const proposed = items.map(item => ({ ...item }));
    for (const discount of discounts) {
        const amount = normalizeReceiptAmount(discount.amount ?? '');
        if (!amount?.startsWith('-')) return items;
        const candidates = lines.flatMap((line, index) => line.kind === 'Item'
            && matchesDiscount(discount.description, line.description) ? [index] : []);
        if (candidates.length !== 1) return items;
        const target = proposed[candidates[0]];
        const gross = normalizeReceiptAmount(target.amount);
        if (gross === null) return items;
        target.amount = sumDecimalAmounts([gross, amount]);
        target.discountAmount = sumDecimalAmounts([target.discountAmount || '0', amount]);
    }
    // Suggestions must reconcile as a whole. Ambiguity, free items and unmatched
    // discounts remain manual review; do not spread an unexplained discrepancy.
    return receiptReconciles(draft.total ?? '', proposed) ? proposed : items;
}

export function normalizeReceiptAmount(value: string) {
    return normalizeBalanceInput(value);
}

// Explicit review assignments use current edited values, never mutate evidence,
// and only commit as one fully reconciled proposal. Callers prevent reapplication.
export function assignReceiptDiscounts<T extends { amount: string; discountAmount?: string; originalAmount?: string }>(
    items: readonly T[], discounts: readonly { amount: string | null }[], targets: readonly number[], total: string,
): T[] | null {
    if (!discounts.length || targets.length !== discounts.length || receiptReconciles(total, items)) return null;
    const proposal = items.map(item => ({ ...item }));
    for (let index = 0; index < discounts.length; index++) {
        const target = targets[index];
        const discount = normalizeReceiptAmount(discounts[index].amount ?? '');
        if (!Number.isInteger(target) || target < 0 || target >= proposal.length || !discount?.startsWith('-')) return null;
        const item = proposal[target];
        const amount = normalizeReceiptAmount(item.amount);
        if (amount === null) return null;
        item.originalAmount = item.discountAmount ? item.originalAmount : amount;
        item.amount = sumDecimalAmounts([amount, discount]);
        item.discountAmount = sumDecimalAmounts([item.discountAmount || '0', discount]);
    }
    return receiptReconciles(total, proposal) ? proposal : null;
}

export function receiptReconciles(total: string, items: readonly { amount: string }[]) {
    const reviewed = normalizeReceiptAmount(total);
    const amounts = items.map(item => normalizeReceiptAmount(item.amount));
    return reviewed !== null && !reviewed.startsWith('-') && reviewed !== '0'
        && items.length > 0 && items.length <= 100
        && amounts.every(amount => amount !== null && amount !== '0' && !amount.startsWith('-'))
        && sumDecimalAmounts(amounts as string[]) === reviewed;
}

// Suggestions only use the authorized category catalogue already visible to the
// user. Exact whole-name matches, with ambiguous matches left for human review.
export function suggestReceiptCategory(description: string, categories: readonly { id: string; displayName: string }[]) {
    const words = (value: string) => value.toLocaleLowerCase().normalize('NFKC').replace(/[^\p{L}\p{N}]+/gu, ' ').trim();
    const text = ' ' + words(description) + ' ';
    const matches = categories.filter(category => {
        const name = words(category.displayName);
        return name.length >= 3 && text.includes(' ' + name + ' ');
    });
    return matches.length === 1 ? matches[0] : undefined;
}

