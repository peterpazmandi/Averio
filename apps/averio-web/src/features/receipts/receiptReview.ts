import type { ReceiptDraft } from '../../shared/api/generated';
import { normalizeBalanceInput, sumDecimalAmounts } from '../../shared/money/decimalAmount';

export function receiptItems(draft: ReceiptDraft) {
    const lines = draft.lines.filter(line => line.kind === 'Item' || line.kind === 'Deposit');
    // Never silently truncate a large receipt into a smaller expense.
    return lines.length <= 100 ? lines.map(line => ({
        description: line.description, categoryId: '', amount: line.amount ?? '',
    })) : [];
}

export function receiptReconciles(total: string, items: readonly { amount: string }[]) {
    const reviewed = normalizeBalanceInput(total);
    const amounts = items.map(item => normalizeBalanceInput(item.amount));
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
