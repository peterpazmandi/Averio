import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';

function load(path, imports) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, require: name => {
        if (name in imports) return imports[name];
        throw new Error(`Unexpected import: ${name}`);
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}

test('household drafts keep their original version and reject duplicate/conflicted submissions', async () => {
    const state = [];
    let cursor = 0;
    let options;
    let error = null;
    let calls = 0;
    let request;
    const invalidations = [];
    class ApiError extends Error { constructor(status) { super('Conflict'); this.status = status; } }
    const { HouseholdEditor } = load('../src/features/household/HouseholdEditor.tsx', {
        react: {
            useState: initial => { const i = cursor++; if (!(i in state)) state[i] = initial; return [state[i], v => { state[i] = v; }]; },
            useRef: initial => { const i = cursor++; if (!(i in state)) state[i] = { current: initial }; return state[i]; },
        },
        'react/jsx-runtime': jsxRuntime,
        'react-i18next': { useTranslation: () => ({ t: key => key }) },
        '@tanstack/react-query': {
            useQueryClient: () => ({ invalidateQueries: async ({ queryKey }) => { invalidations.push(queryKey); } }),
            useMutation: opts => { options = opts; return { error, isPending: false, mutate() { calls++; } }; },
        },
        '../../shared/api/ApiError': { ApiError },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioFormControls': { AverioField: 'label', AverioInput: 'input', AverioSelect: 'select' },
        './householdQueries': {
            householdApi: { apiV1HouseholdsHouseholdIdPatch: async input => { request = input; } },
            householdKeys: { detail: (u, h) => ['household-settings', u, h] },
        },
        '../onboarding/api/onboardingQueryKeys': { onboardingQueryKeys: { all: ['onboarding'] } },
        '../dashboard/api/dashboardQueryKeys': { dashboardQueryKeys: { household: (u, h) => ['dashboard', u, h] } },
        '../authentication/api/authQueryKeys': { authQueryKeys: { all: ['auth'] } },
    });
    let saved = 0;
    const household = { id: 'h', name: 'Original', countryCode: 'HU', preferredCurrency: 'EUR', version: 'original-version' };
    function render(data = household) {
        cursor = 0;
        return HouseholdEditor({ household: data, userId: 'u', onSaved() { saved++; }, onCancel() {} });
    }
    function nodes(tree, type) {
        const result = [];
        function visit(value) {
            if (!value || typeof value !== 'object') return;
            if (Array.isArray(value)) return value.forEach(visit);
            if (value.type === type) result.push(value);
            visit(value.props?.children);
        }
        visit(tree);
        return result;
    }
    let tree = render();
    nodes(tree, 'input')[0].props.onChange({ target: { value: '  My draft  ' } });
    tree = render({ ...household, name: 'Server change', version: 'new-server-version' });
    tree.props.onSubmit({ preventDefault() {} });
    tree.props.onSubmit({ preventDefault() {} });
    assert.equal(calls, 1, 'A second synchronous submit must not enqueue a duplicate write');
    await options.mutationFn();
    assert.equal(request.updateHouseholdRequest.name, 'My draft');
    assert.equal(request.updateHouseholdRequest.expectedVersion, 'original-version');
    options.onSettled();
    error = new ApiError(409);
    tree = render();
    assert.equal(nodes(tree, 'input')[0].props.value, '  My draft  ', 'Conflict preserves user input');
    assert.equal(nodes(tree, 'button').find(n => n.props.type === 'submit').props.disabled, true);
    tree.props.onSubmit({ preventDefault() {} });
    assert.equal(calls, 1, 'Conflicted draft cannot be blindly retried');
    await options.onSuccess();
    assert.deepEqual(invalidations.map(k => k[0]).sort(), ['auth', 'dashboard', 'household-settings', 'onboarding']);
    assert.equal(saved, 1);
});

test('household page blocks stale drafts until explicit reload succeeds, including failed reload retries', async () => {
    const state = [];
    let cursor = 0;
    let finishReload;
    let reloadPromise;
    const original = { id: 'h', name: 'Original', countryCode: 'HU', preferredCurrency: 'EUR', role: 'owner', version: 'v1', members: [] };
    const query = {
        data: original, isPending: false, isError: false, isFetching: false,
        refetch() {
            query.isFetching = true;
            reloadPromise = new Promise(resolve => { finishReload = resolve; });
            return reloadPromise;
        },
    };
    const { HouseholdPage } = load('../src/features/household/HouseholdPage.tsx', {
        react: { useState: initial => { const i = cursor++; if (!(i in state)) state[i] = initial; return [state[i], value => { state[i] = value; }]; } },
        'react/jsx-runtime': jsxRuntime,
        'react-i18next': { useTranslation: () => ({ t: key => key, i18n: { resolvedLanguage: 'en' } }) },
        '@tanstack/react-query': { useQuery: () => query },
        '../../shared/components/ui/AverioCard': { AverioCard: 'section' },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/concept/ConceptIcon': { ConceptIcon: 'icon' },
        './householdQueries': { householdQueries: { detail: () => ({}) } },
        './HouseholdEditor': { HouseholdEditor: 'editor' },
    });
    function render() { cursor = 0; return HouseholdPage({ userId: 'u', householdId: 'h' }); }
    function find(tree, type, text) {
        if (!tree || typeof tree !== 'object') return undefined;
        if (Array.isArray(tree)) return tree.map(child => find(child, type, text)).find(Boolean);
        if (tree.type === type && (text === undefined || tree.props.children === text)) return tree;
        return find(tree.props?.children, type, text);
    }
    const edit = () => find(render(), 'button', 'householdSettings.edit');
    async function completeReload(success, data = query.data) {
        query.isFetching = false;
        query.isError = !success;
        query.data = data;
        finishReload({ isSuccess: success, data });
        await reloadPromise;
        await Promise.resolve();
    }

    edit().props.onClick();
    assert.equal(find(render(), 'editor').props.household.version, 'v1');
    find(render(), 'editor').props.onCancel();
    assert.equal(edit().props.disabled, true, 'Cannot reopen during delayed reload');
    edit().props.onClick();
    assert.equal(find(render(), 'editor'), undefined, 'Reopen handler also rejects stale data');
    await completeReload(true, { ...original, name: 'Latest', version: 'v2' });
    assert.equal(edit().props.disabled, false);
    edit().props.onClick();
    assert.equal(find(render(), 'editor').props.household.version, 'v2');

    find(render(), 'editor').props.onCancel();
    await completeReload(false);
    assert.equal(edit().props.disabled, true, 'Cached data must remain uneditable after failed reload');
    find(render(), 'button', 'common.actions.retry').props.onClick();
    assert.equal(edit().props.disabled, true);
    await completeReload(true, { ...original, version: 'v3' });
    edit().props.onClick();
    assert.equal(find(render(), 'editor').props.household.version, 'v3', 'Retry releases editor only with refreshed version');
});
