import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import i18next from 'i18next';
import * as jsxRuntime from 'react/jsx-runtime';

function load(path, imports = {}) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, require: name => {
        if (name in imports) return imports[name];
        throw new Error('Unexpected import: ' + name);
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}
function elements(tree) {
    if (!tree || typeof tree !== 'object') return [];
    if (Array.isArray(tree)) return tree.flatMap(elements);
    return [tree, ...elements(tree.props?.children)];
}
function text(tree) {
    if (tree == null || typeof tree === 'boolean') return '';
    if (typeof tree !== 'object') return String(tree);
    if (Array.isArray(tree)) return tree.map(text).join('');
    return text(tree.props?.children);
}
const resources = load('../src/i18n/locales/systemCategories.ts');
const categoryLocalization = load('../src/features/transactions/categoryLocalization.ts');
const { HistoryItemFromJSON } = load('../src/shared/api/generated/models/HistoryItem.ts', { '../runtime': {} });
const historyLocalization = load('../src/features/household/historyCategoryLocalization.ts', {
    '../../i18n/locales/systemCategories': resources,
    '../transactions/categoryLocalization': categoryLocalization,
});
async function translations() {
    const i18n = i18next.createInstance();
    await i18n.init({ lng: 'en', fallbackLng: 'en', resources: {
        en: { translation: { systemCategories: resources.systemCategoriesEN } },
        de: { translation: { systemCategories: resources.systemCategoriesDE } },
        hu: { translation: { systemCategories: resources.systemCategoriesHU } },
    } });
    return i18n;
}

test('frozen category references follow EN HU DE without translating custom names or English descriptions', async () => {
    const i18n = await translations();
    const { getHistoricalCategoryDisplayName: name } = historyLocalization;
    const apiItem = fields => Object.freeze(HistoryItemFromJSON({ description: 'Salary', amount: '20000', ...fields }));
    const system = apiItem({ categoryName: 'income-salary', isSystemCategory: true });
    const legacy = apiItem({ categoryName: 'income-salary' });
    const legacyNull = apiItem({ categoryName: 'income-salary', isSystemCategory: null });
    const customKey = apiItem({ categoryName: 'income-salary', isSystemCategory: false });
    const customText = Object.freeze({ categoryName: 'Salary', isSystemCategory: false });
    const unknown = Object.freeze({ categoryName: 'future-system-key', isSystemCategory: true });
    for (const [language, expected] of [['en', 'Salary'], ['hu', 'Fizetés'], ['de', 'Gehalt']]) {
        await i18n.changeLanguage(language);
        assert.equal(name(system, i18n.t), expected);
        assert.equal(name(legacy, i18n.t), expected, 'Pre-fix snapshots resolve exact built-in keys');
        assert.equal(name(legacyNull, i18n.t), expected, 'Unknown classification also accepts explicit null');
        assert.equal(name(customKey, i18n.t), 'income-salary', 'Custom name wins over a coincidentally identical key');
        assert.equal(name(customText, i18n.t), 'Salary', 'No reverse lookup of an English label');
        assert.equal(name({ categoryName: 'Salary' }, i18n.t), 'Salary');
        assert.equal(name({ categoryName: 'family-gift' }, i18n.t), 'family-gift');
        assert.equal(name(unknown, i18n.t), 'future-system-key', 'Unknown references remain readable');
    }
});

test('open previous-household card rerenders cached English-created history when the UI language changes', async () => {
    const i18n = await translations();
    const summary = { id: 'history', householdName: 'Previous home', leftAtUtc: '2026-10-10T12:00:00Z' };
    const snapshot = { summary, accounts: [{ id: 'account', name: 'Bank of America', balance: '60000', currency: 'HUF' }],
        cashFlow: [], transfers: [], transactions: [{ id: 'transaction', accountId: 'account', description: 'Usual salary',
            note: 'My English note', occurredOn: '2026-10-10', amount: '60000', currency: 'HUF', kind: 'Income',
            items: [{ description: 'Salary', categoryName: 'income-salary', amount: '20000', isSystemCategory: true },
                { description: 'Custom key', categoryName: 'income-salary', amount: '20000', isSystemCategory: false },
                { description: 'Earlier entry', categoryName: 'income-salary', amount: '20000' }] }] };
    const original = JSON.stringify(snapshot);
    const queryKeys = [];
    const { PreviousHouseholds } = load('../src/features/household/PreviousHouseholds.tsx', {
        react: { useState: initial => [initial === null ? 'history' : initial, () => {}] },
        'react/jsx-runtime': jsxRuntime,
        '@tanstack/react-query': { useQuery: options => {
            queryKeys.push(JSON.stringify(options.queryKey));
            return { isSuccess: true, data: options.queryKey[0] === 'history' ? [summary]
                : options.queryKey[0] === 'former' ? [] : snapshot };
        } },
        'react-i18next': { useTranslation: () => ({ t: i18n.t, i18n }) },
        '../../shared/components/ui/AverioCard': { AverioCard: 'section' },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioFormControls': { AverioField: 'label', AverioInput: 'input' },
        '../../shared/money/decimalAmount': { formatDecimalAmount: amount => amount, subtractDecimalAmounts() {} },
        './historyCategoryLocalization': historyLocalization,
        './invitationQueries': { invitationApi: {}, invitationKeys: {
            history: user => ['history', user], former: (user, household) => ['former', user, household],
            snapshot: (user, id, from, to) => ['snapshot', user, id, from, to],
        } },
    });
    for (const [language, categoryName] of [['en', 'Salary'], ['hu', 'Fizetés'], ['de', 'Gehalt']]) {
        await i18n.changeLanguage(language);
        const outer = PreviousHouseholds({ userId: 'member', householdId: 'destination' });
        const card = elements(outer).find(node => typeof node.type === 'function');
        assert.ok(card, 'The existing snapshot stays open');
        const tree = card.type(card.props);
        const rows = elements(tree).filter(node => node.type === 'li').map(text);
        assert.ok(rows.includes('Salary · ' + categoryName + ' · 20000'));
        assert.ok(rows.includes('Custom key · income-salary · 20000'));
        assert.ok(rows.includes('Earlier entry · ' + categoryName + ' · 20000'));
        for (const userContent of ['Usual salary', 'Bank of America', 'My English note']) {
            assert.ok(text(tree).includes(userContent), 'Preserve ' + userContent);
        }
        assert.equal(JSON.stringify(snapshot), original, 'Changing language must not mutate the snapshot');
    }
    assert.equal(new Set(queryKeys.filter(key => key.startsWith('["snapshot"'))).size, 1,
        'Language switching renders the same cached snapshot and needs no live source-category request');
});
