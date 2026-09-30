import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';

// Exercise the real hook callbacks and editor render/event handlers without a browser.
// Native dialog layout/focus behavior remains a separate browser check.
class ApiError extends Error {
    constructor(status) { super('Account changed'); this.status = status; }
}
function load(path, mocks) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, require: name => {
        if (name in mocks) return mocks[name];
        throw new Error(`Unexpected dependency: ${name}`);
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}

test('creation, update and void refresh account choices after conflicts or lost access', async () => {
    const invalidations = [];
    const { useTransferMutations } = load('../src/features/transfers/transferQueries.ts', {
        '../../shared/api/ApiError': { ApiError },
        '@tanstack/react-query': {
            useMutation: config => config,
            useQueryClient: () => ({ invalidateQueries: async args => invalidations.push(args.queryKey) }),
        },
        '../../shared/api/generated': { TransfersApi: class {} },
        '../../shared/api/generatedApiConfiguration': { generatedApiConfiguration: {} },
        '../accounts/api/accountsQueryKeys': { accountsQueryKeys: { household: () => ['accounts'] } },
        '../dashboard/api/dashboardQueryKeys': { dashboardQueryKeys: { household: () => ['dashboard'] } },
    });
    const mutations = useTransferMutations('user', 'household');
    for (const operation of ['create', 'update', 'void']) {
        for (const status of [403, 404, 409]) {
            invalidations.length = 0;
            await mutations[operation].onError(new ApiError(status));
            assert.equal(invalidations.length, 3, `${operation}: HTTP ${status}`);
            assert.ok(invalidations.some(key => key[0] === 'accounts'));
        }
    }
    invalidations.length = 0;
    await mutations.create.onError(new ApiError(400));
    assert.equal(invalidations.length, 0, 'Validation errors must not reset account choices');
});

test('creation draft survives account removal and insufficient remaining accounts', async () => {
    const state = [];
    let cursor = 0;
    const { TransferEditor } = load('../src/features/transfers/TransferEditor.tsx', {
        '../../shared/api/ApiError': { ApiError },
        'react': { useId: () => 'editor', useState: initial => {
            const index = cursor++;
            if (!(index in state)) state[index] = initial;
            return [state[index], value => { state[index] = typeof value === 'function' ? value(state[index]) : value; }];
        } },
        'react/jsx-runtime': jsxRuntime,
        'date-fns': { format: () => '2026-09-28' },
        'react-i18next': { useTranslation: () => ({ t: key => key }) },
        '../../shared/components/ui/AverioDialog': { AverioDialog: 'dialog' },
        '../../shared/components/ui/AverioCard': { AverioCard: 'card' },
        '../../shared/components/concept/ConceptIcon': { ConceptIcon: 'icon' },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioFormControls': {
            AverioField: 'label', AverioInput: 'input', AverioSelect: 'select', AverioTextarea: 'textarea',
        },
    });
    let accounts = [
        { id: 'source', currency: 'EUR', status: 'Active' },
        { id: 'destination', currency: 'USD', status: 'Active' },
    ];
    let saveCalls = 0;
    let reloadCalls = 0;
    const props = { transfer: null, busy: false, onClose() {}, onVoid() {},
        onReload: async () => { reloadCalls++; },
        onSave: async () => { saveCalls++; throw new ApiError(409); },
    };
    function render() { cursor = 0; return TransferEditor({ ...props, accounts }); }
    function nodes(tree, type) {
        const result = [];
        function walk(value) {
            if (!value || typeof value !== 'object') return;
            if (Array.isArray(value)) return value.forEach(walk);
            if (value.type === type) result.push(value);
            walk(value.props?.children);
            walk(value.props?.actions);
        }
        walk(tree);
        return result;
    }
    let tree = render();
    nodes(tree, 'select')[1].props.onChange({ target: { value: 'destination' } });
    tree = render();
    nodes(tree, 'input')[0].props.onChange({ target: { value: '42' } });
    nodes(tree, 'input')[1].props.onChange({ target: { value: '48' } });
    nodes(tree, 'input')[2].props.onChange({ target: { value: '2026-09-25' } });
    nodes(tree, 'textarea')[0].props.onChange({ target: { value: 'Keep this note' } });
    tree = render();
    nodes(tree, 'form')[0].props.onSubmit({ preventDefault() {} });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(saveCalls, 1);
    accounts = [accounts[1]]; // refreshed list hides the unavailable source
    tree = render();
    assert.equal(nodes(tree, 'select')[0].props.value, '');
    assert.equal(nodes(tree, 'select')[1].props.value, 'destination');
    assert.deepEqual(nodes(tree, 'input').map(node => node.props.value), ['42', '48', '2026-09-25']);
    assert.equal(nodes(tree, 'textarea')[0].props.value, 'Keep this note');
    assert.ok(nodes(tree, 'p').some(node => node.props.children === 'transfers.needAccounts'));
    assert.equal(nodes(tree, 'button').find(node => node.props.type === 'submit').props.disabled, true);
    nodes(tree, 'form')[0].props.onSubmit({ preventDefault() {} });
    assert.equal(saveCalls, 1, 'Cannot retry with an unavailable account');
    nodes(tree, 'button').find(node => node.props.children === 'transfers.refreshAccounts').props.onClick();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(reloadCalls, 1);
    assert.equal(nodes(render(), 'textarea')[0].props.value, 'Keep this note');
});
