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
