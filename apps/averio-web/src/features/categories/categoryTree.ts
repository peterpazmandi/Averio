import type { TFunction } from 'i18next';
import type { CategoryResponse } from '../../shared/api/generated';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';
import { sortCategories } from './categoryOrdering';

export function categoryPath(category: CategoryResponse, categories: readonly CategoryResponse[], t: TFunction) {
    const byId = new Map(categories.map(c => [c.id, c]));
    const names: string[] = [];
    const seen = new Set<string>();
    let node: CategoryResponse | undefined = category;
    while (node && !seen.has(node.id)) {
        seen.add(node.id); names.unshift(getCategoryDisplayName(node, t));
        node = node.parentId ? byId.get(node.parentId) : undefined;
    }
    return names.join(' / ');
}

export function descendantsOf(id: string, categories: readonly CategoryResponse[]) {
    const excluded = new Set([id]);
    let changed = true;
    while (changed) {
        changed = false;
        for (const c of categories) if (c.parentId && excluded.has(c.parentId) && !excluded.has(c.id)) {
            excluded.add(c.id); changed = true;
        }
    }
    return excluded;
}

/** Parent-before-child traversal, retaining ancestor context for filtered matches. */
export function categoryTree(categories: readonly CategoryResponse[], t: TFunction, language: string,
    matches: (category: CategoryResponse) => boolean = () => true) {
    const sorted = sortCategories(categories, t, language, false);
    const byId = new Map(sorted.map(c => [c.id, c]));
    const visible = new Set<string>();
    for (const c of sorted.filter(matches)) {
        let node: CategoryResponse | undefined = c;
        const seen = new Set<string>();
        while (node && !seen.has(node.id)) {
            seen.add(node.id); visible.add(node.id);
            node = node.parentId ? byId.get(node.parentId) : undefined;
        }
    }
    const result: { category: CategoryResponse; depth: number; rootId: string; contextOnly: boolean }[] = [];
    const seen = new Set<string>();
    function visit(c: CategoryResponse, depth: number, rootId: string) {
        if (seen.has(c.id) || !visible.has(c.id)) return;
        seen.add(c.id); result.push({ category: c, depth, rootId, contextOnly: !matches(c) });
        for (const child of sorted.filter(child => child.parentId === c.id)) visit(child, depth + 1, rootId);
    }
    for (const root of sorted.filter(c => !c.parentId || !byId.has(c.parentId))) visit(root, 0, root.id);
    return result;
}

export const categoryIndent = ['pl-0', 'pl-4', 'pl-8', 'pl-12', 'pl-16'];
