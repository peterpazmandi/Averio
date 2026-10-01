import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import i18next from 'i18next';

function load(path, imports = {}) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS },
    }).outputText;
    const context = { exports: {}, Intl, require: name => {
        if (name in imports) return imports[name];
        throw new Error('Unexpected import: ' + name);
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}
const localization = load('../src/features/transactions/categoryLocalization.ts');
const { sortCategories, normalizeSearch } = load('../src/features/categories/categoryOrdering.ts', {
    '../transactions/categoryLocalization': localization,
});
const category = (id, name, isPinned = false, systemKey = null) => ({ id, name, systemKey, isPinned });

test('pins including children precede unpinned categories without mutating cached data', async () => {
    const i18n = i18next.createInstance();
    await i18n.init({ lng: 'en', resources: {} });
    const items = [
        category('3', 'zebra'), category('2', 'Apple'),
        { ...category('4', 'Zoo', true), parentId: '3' }, category('1', 'Bread', true),
    ];
    assert.equal(sortCategories(items, i18n.t, 'en').map(c => c.id).join(), '1,4,2,3');
    assert.equal(items.map(c => c.id).join(), '3,2,4,1');
    assert.equal(sortCategories(items, i18n.t, 'en', false).map(c => c.id).join(), '2,1,3,4');
});

test('language changes sort translated names, including Hungarian digraphs', async () => {
    const i18n = i18next.createInstance();
    await i18n.init({ lng: 'en', resources: {
        en: { translation: { systemCategories: { one: 'Zoo', two: 'Apple' } } },
        de: { translation: { systemCategories: { one: 'Apfel', two: 'Zoo' } } },
        hu: { translation: { systemCategories: { one: 'Cukor', two: 'Csoki' } } },
    } });
    const items = [category('1', null, false, 'one'), category('2', null, false, 'two')];
    assert.equal(sortCategories(items, i18n.t, 'en').map(c => c.id).join(), '2,1');
    await i18n.changeLanguage('de');
    assert.equal(sortCategories(items, i18n.t, 'de').map(c => c.id).join(), '1,2');
    await i18n.changeLanguage('hu');
    assert.equal(sortCategories(items, i18n.t, 'hu').map(c => c.id).join(), '1,2');
    assert.equal(normalizeSearch('  KÁVÉ  ', 'hu'), 'kave');
});

test('all 240 seeded icons have meaningful EN DE HU names', () => {
    const names = load('../src/i18n/locales/iconNames.ts');
    for (const language of ['EN', 'DE', 'HU']) {
        const entries = names['iconNames' + language];
        assert.equal(Object.keys(entries).length, 240);
        for (let id = 1; id <= 240; id++) {
            assert(entries[id]?.trim());
            assert(!/^icon-/.test(entries[id]));
        }
    }
    assert.equal(names.iconNamesEN['21'], 'Shopping cart');
    assert.equal(names.iconNamesDE['103'], 'Kaffee');
    assert.equal(names.iconNamesHU['103'], 'Kávé');
});

test('saved pin sequence overrides alphabetical ordering', async () => {
    const i18n = i18next.createInstance(); await i18n.init({ lng: 'en', resources: {} });
    const items = [{ ...category('1', 'Apple', true), pinOrder: 3 }, { ...category('2', 'Zoo', true), pinOrder: 0 }, category('3', 'Aardvark')];
    assert.equal(sortCategories(items, i18n.t, 'en').map(c => c.id).join(), '2,1,3');
});

test('deep tree keeps ancestor context and excludes all descendants from parent choices', async () => {
    const i18n = i18next.createInstance(); await i18n.init({ lng: 'en', resources: {} });
    const { categoryTree, categoryPath, descendantsOf } = load('../src/features/categories/categoryTree.ts', {
        '../transactions/categoryLocalization': localization, './categoryOrdering': { sortCategories },
    });
    const items = [
        { ...category('c', 'Leaf'), parentId: 'b' }, { ...category('a', 'Root'), parentId: null },
        { ...category('b', 'Branch'), parentId: 'a' }, { ...category('d', 'Other'), parentId: null },
    ];
    const tree = categoryTree(items, i18n.t, 'en', c => c.id === 'c');
    assert.equal(tree.map(n => n.category.id).join(), 'a,b,c');
    assert.equal(tree.map(n => n.depth).join(), '0,1,2');
    assert.equal(categoryPath(items[0], items, i18n.t), 'Root / Branch / Leaf');
    assert.equal([...descendantsOf('a', items)].sort().join(), 'a,b,c');
});
