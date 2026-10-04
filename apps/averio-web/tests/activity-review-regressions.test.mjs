import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';
import { QueryClient, queryOptions } from '@tanstack/react-query';

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

const activities = load('../src/features/activities/activityQueries.ts', {
    '@tanstack/react-query': { queryOptions },
    '../../shared/api/generated': { ActivitiesApi: class {} },
    '../../shared/api/generatedApiConfiguration': { generatedApiConfiguration: {} },
});

test('quick-create activity submit is isolated from its ancestor transaction form, including while saving', () => {
    let saves = 0;
    let pending = false;
    const { ActivityEditor } = load('../src/features/activities/ActivityEditor.tsx', {
        react: { useId: () => 'activity-form', useState: initial => [initial, () => {}] },
        'react/jsx-runtime': jsxRuntime,
        'react-i18next': { useTranslation: () => ({ t: key => key }) },
        '@tanstack/react-query': {
            useQueryClient: () => ({}),
            useMutation: () => ({ isPending: pending, mutate: () => { saves++; } }),
        },
        '../../shared/components/ui/AverioDialog': { AverioDialog: 'dialog' },
        '../../shared/components/ui/AverioCard': { AverioCard: 'card' },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioFormControls': { AverioField: 'label', AverioInput: 'input' },
        './activityQueries': activities,
        '../transactions/api/transactionQueries': {},
        '../dashboard/api/dashboardQueryKeys': {},
    });
    for (pending of [false, true]) {
        const tree = ActivityEditor({ userId: 'u', householdId: 'h', onClose() {} });
        const form = tree.props.children;
        assert.equal(form.type, 'form');
        let defaultPrevented = false, propagationStopped = false;
        form.props.onSubmit({
            preventDefault() { defaultPrevented = true; },
            stopPropagation() { propagationStopped = true; },
        });
        assert.equal(defaultPrevented, true);
        assert.equal(propagationStopped, true, 'A portal submit must not reach the ancestor transaction handler');
    }
    assert.equal(saves, 1, 'A second submit during an in-flight save must not create another activity');
});

test('account changes invalidate personal activity totals and timeline descendants without touching other members', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: false } } });
    const { useAccountMutations } = load('../src/features/accounts/api/useAccountMutations.ts', {
        '@tanstack/react-query': { useQueryClient: () => client, useMutation: options => options },
        '../../activities/activityQueries': activities,
        '../../transfers/transferQueries': { transferKeys: { all: () => ['transfers'] } },
        '../../../shared/api/ApiError': { ApiError: class extends Error {} },
        './accountsQueryKeys': { accountsQueryKeys: { household: () => ['accounts'] } },
        '../../dashboard/api/dashboardQueryKeys': { dashboardQueryKeys: { household: () => ['dashboard'] } },
        './accountsApi': {},
    });
    const totals = activities.activityQueries.list('u', 'h').queryKey;
    const timeline = activities.activityQueries.timeline('u', 'h', 'a').queryKey;
    const other = activities.activityQueries.list('other', 'h').queryKey;
    try {
        const mutations = useAccountMutations('u', 'h');
        for (const operation of ['create', 'update', 'archive', 'restore']) {
            for (const key of [totals, timeline, other]) client.setQueryData(key, ['previously visible']);
            await mutations[operation].onSuccess();
            assert.equal(client.getQueryState(totals).isInvalidated, true, operation);
            assert.equal(client.getQueryState(timeline).isInvalidated, true, operation);
            assert.equal(client.getQueryState(other).isInvalidated, false, operation);
            let requests = 0;
            for (const key of [totals, timeline]) {
                const data = await client.fetchQuery({ queryKey: key, queryFn: async () => { requests++; return []; } });
                assert.deepEqual(data, [], 'Returning to the view must read current visibility from the API');
            }
            assert.equal(requests, 2);
        }
    } finally { client.clear(); }
});
