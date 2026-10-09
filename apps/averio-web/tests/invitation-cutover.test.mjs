import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';
import { QueryClient, QueryObserver } from '@tanstack/react-query';

function load(path, imports) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, window: { location: { hash: '', pathname: '/invitation' }, history: { replaceState() {} } },
        require: name => { if (name in imports) return imports[name]; throw new Error(`Unexpected import: ${name}`); } };
    vm.runInNewContext(code, context);
    return context.exports;
}
function nodes(tree, type) {
    if (!tree || typeof tree !== 'object') return [];
    if (Array.isArray(tree)) return tree.flatMap(child => nodes(child, type));
    return [...(tree.type === type ? [tree] : []), ...nodes(tree.props?.children, type)];
}

test('invitation acceptance requires fresh account choices, successor and confirmation; preserves exact review values', async () => {
    const state = []; let cursor = 0; let mutationIndex = 0; let request;
    const mutations = []; const transition = [];
    const client = { household: 'source' };
    const auth = { status: 'authenticated', user: { userId: 'new-user', displayName: 'Person' }, refresh: async () => { transition.push('refresh'); } };
    const review = { householdName: 'Destination', canJoin: true, alreadyJoined: false,
        departure: { householdName: 'Source', version: 'source-version', willArchive: false, requiresSuccessor: true,
            accountsStaying: 1, successors: [{ id: 'successor', displayName: 'Partner' }],
            accounts: [{ id: 'account', name: 'Savings', balance: '899999999999999.9999', currency: 'HUF', version: 'exact-version' }] } };
    const { InvitationPage } = load('../src/features/household/InvitationPage.tsx', {
        react: { useState: initial => { const i = cursor++; if (!(i in state)) state[i] = typeof initial === 'function' ? initial() : initial;
            return [state[i], value => { state[i] = typeof value === 'function' ? value(state[i]) : value; }]; }, useEffect() {} },
        'react/jsx-runtime': jsxRuntime,
        '@tanstack/react-router': { Link: 'a', useNavigate: () => async destination => { assert.equal(destination.to, '/app/household'); transition.push('navigate'); } },
        '@tanstack/react-query': { useQueryClient: () => client,
            useQuery: () => ({ data: [{ id: 'invite', householdName: 'Destination' }], isSuccess: true }),
            useMutation: opts => { const i = mutationIndex++; mutations[i] = opts; return { data: i === 0 ? review : undefined, isPending: false, reset() {}, mutate() {} }; } },
        'react-i18next': { useTranslation: () => ({ t: key => key, i18n: { language: 'en' } }) },
        '../authentication/context/AuthProvider': { useAuth: () => auth },
        '../authentication/api/authApi': { logoutUser: async () => {} },
        '../onboarding/api/onboardingApi': { saveOnboardingProfile: async () => {} },
        '../../shared/money/decimalAmount': { formatDecimalAmount: amount => amount },
        '../../shared/api/ApiError': { ApiError: class extends Error {} },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioDialog': { AverioDialog: 'dialog' },
        '../../shared/components/ui/AverioCard': { AverioCard: 'section' },
        '../../shared/components/ui/AverioFormControls': { AverioField: 'label', AverioInput: 'input', AverioSelect: 'select' },
        './invitationQueries': { invitationKeys: { pending: user => ['invitations', user, 'pending'] }, invitationApi: { apiV1InvitationsAcceptancesPost: async value => { request = value; } } },
        './invitationContext': { pendingInvitation: () => null, rememberInvitation() {}, forgetInvitation() { transition.push('forget'); } },
        './householdTransition': { refreshHouseholdContext: async (current, refresh) => {
            assert.equal(current, client); transition.push('transition'); return await refresh();
        } },
    });
    const outer = InvitationPage(); state.length = 0;
    function render() { cursor = 0; mutationIndex = 0; return outer.type(outer.props); }
    const accept = tree => nodes(tree, 'button').find(node => node.props.children === 'invitations.accept');
    let tree = render();
    assert.equal(accept(tree).props.disabled, true);
    nodes(tree, 'input').find(node => node.props.type === 'checkbox').props.onChange({ target: { checked: true } });
    tree = render(); assert.equal(accept(tree).props.disabled, true, 'Confirmation alone cannot accept');
    nodes(tree, 'select')[0].props.onChange({ target: { value: 'private' } });
    tree = render(); assert.equal(accept(tree).props.disabled, true, 'An owner needs a successor');
    nodes(tree, 'select')[1].props.onChange({ target: { value: 'successor' } });
    tree = render(); assert.equal(accept(tree).props.disabled, false);
    await mutations[1].mutationFn();
    assert.equal(request.acceptInvitationRequest.invitationId, 'invite', 'Verified discovery works without tab token');
    assert.equal(request.acceptInvitationRequest.expectedSourceVersion, 'source-version');
    assert.equal(request.acceptInvitationRequest.accounts[0].openingBalance, '899999999999999.9999');
    assert.equal(request.acceptInvitationRequest.accounts[0].expectedVersion, 'exact-version');
    assert.equal(request.acceptInvitationRequest.accounts[0].handling, 'private');
    mutations[0].onSuccess(review);
    tree = render();
    assert.equal(accept(tree).props.disabled, true, 'Another preview resets prior consent and handling');
    assert.equal(nodes(tree, 'select')[0].props.value, '');
    assert.equal(nodes(tree, 'input').find(node => node.props.type === 'checkbox').props.checked, false);
    await mutations[1].onSuccess();
    assert.deepEqual(transition, ['transition', 'refresh', 'navigate', 'forget'], 'Refresh finishes before navigation and token cleanup');
});

test('verified invitation onboarding never shows household creation while pending, loading or failed', () => {
    let pending = { isPending: true };
    const { OnboardingPage } = load('../src/features/onboarding/components/OnboardingPage.tsx', {
        'react/jsx-runtime': jsxRuntime,
        '@tanstack/react-router': { useNavigate: () => () => {} },
        'react-i18next': { useTranslation: () => ({ t: key => key }) },
        '@tanstack/react-query': { useQuery: () => pending },
        '../../household/invitationQueries': { invitationApi: {}, invitationKeys: { pending: user => ['invitations', user, 'pending'] } },
        '../../household/InvitationPage': { InvitationPage: 'invitation' },
        '../../../shared/components/PrimaryButton': { PrimaryButton: 'button' },
        '../../authentication/context/AuthProvider': { useAuth: () => ({ status: 'authenticated', user: { userId: 'new-user' } }) },
        './OnboardingForUser': { OnboardingForUser: 'household-create' },
    });
    assert.equal(nodes(OnboardingPage(), 'household-create').length, 0);
    pending = { isError: true };
    assert.equal(nodes(OnboardingPage(), 'household-create').length, 0);
    pending = { data: [{ id: 'invite' }] };
    assert.equal(OnboardingPage().type, 'invitation');
    pending = { data: [] };
    assert.equal(OnboardingPage().type, 'household-create', 'Declining all invitations permits solo setup');
});

test('acceptance updates an already mounted auth observer and discards source household requests and finance data', async () => {
    const { authQueryKeys } = load('../src/features/authentication/api/authQueryKeys.ts', {});
    const { refreshHouseholdContext } = load('../src/features/household/householdTransition.ts', {
        '../authentication/api/authQueryKeys': { authQueryKeys },
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
    const key = authQueryKeys.currentUser();
    const source = { userId: 'member', household: { householdId: 'source' } };
    const destination = { ...source, household: { householdId: 'destination' } };
    client.setQueryData(key, source);
    client.setQueryData(['accounts', 'member', 'source'], [{ id: 'private-old-account' }]);
    client.setQueryData(['household-history', 'member'], [{ id: 'old-snapshot' }]);
    const observer = new QueryObserver(client, { queryKey: key, queryFn: async () => source, staleTime: Infinity });
    const seen = [];
    const unsubscribe = observer.subscribe(result => seen.push(result.data?.household.householdId));
    const authQuery = client.getQueryCache().find({ queryKey: key });
    let cancelled = false;
    const financeRequest = client.fetchQuery({
        queryKey: ['statistics', 'member', 'source'],
        queryFn: ({ signal }) => new Promise(resolve => {
            signal.addEventListener('abort', () => { cancelled = true; resolve([]); }, { once: true });
        }),
    }).catch(() => undefined);
    await client.getMutationCache().build(client, {
        mutationFn: async () => ({ privateAccount: 'old-account' }),
    }).execute();
    try {
        const result = await refreshHouseholdContext(client, () => client.fetchQuery({
            queryKey: key, queryFn: async () => destination, staleTime: 0,
        }));
        await financeRequest;
        assert.equal(result.household.householdId, 'destination');
        assert.equal(observer.getCurrentResult().data.household.householdId, 'destination',
            'The AuthProvider observer must receive the new membership without remounting');
        assert.ok(seen.includes('destination'));
        assert.equal(client.getQueryCache().find({ queryKey: key }), authQuery, 'Keep the query attached to its live observer');
        assert.equal(cancelled, true, 'Cancel source requests before discarding cached financial context');
        assert.equal(client.getQueryData(['accounts', 'member', 'source']), undefined);
        assert.equal(client.getQueryData(['household-history', 'member']), undefined);
        assert.equal(client.getQueryData(['statistics', 'member', 'source']), undefined);
        assert.equal(client.getMutationCache().getAll().length, 0);
    } finally {
        unsubscribe();
        client.clear();
    }
});

test('previous household section is omitted only for a successful empty history', () => {
    let history = { isSuccess: true, data: [] };
    let queryIndex = 0;
    const { PreviousHouseholds } = load('../src/features/household/PreviousHouseholds.tsx', {
        react: { useState: initial => [initial, () => {}] },
        'react/jsx-runtime': jsxRuntime,
        '@tanstack/react-query': { useQuery: () => queryIndex++ % 2 === 0 ? history : { data: [], isSuccess: true } },
        'react-i18next': { useTranslation: () => ({ t: key => key, i18n: { language: 'en' } }) },
        '../../shared/components/ui/AverioCard': { AverioCard: 'section' },
        '../../shared/components/ui/AverioButton': { AverioButton: 'button' },
        '../../shared/components/ui/AverioFormControls': { AverioField: 'label', AverioInput: 'input' },
        '../../shared/money/decimalAmount': { formatDecimalAmount: amount => amount, subtractDecimalAmounts() {} },
        './invitationQueries': { invitationApi: {}, invitationKeys: { history: () => [], former: () => [] } },
    });
    const render = () => PreviousHouseholds({ userId: 'member', householdId: 'destination' });
    assert.equal(nodes(render(), 'section').length, 0, 'Users without history have no previous-household card');
    history = { isPending: true };
    assert.equal(nodes(render(), 'p').some(node => node.props.role === 'status'), true);
    history = { isError: true, refetch() {} };
    assert.equal(nodes(render(), 'p').some(node => node.props.role === 'alert'), true, 'Failures are never interpreted as empty history');
    assert.equal(nodes(render(), 'button').some(node => node.props.children === 'common.actions.retry'), true);
    history = { isSuccess: true, data: [{ id: 'history', householdName: 'Previous home', leftAtUtc: '2026-10-09T12:00:00Z' }] };
    assert.equal(nodes(render(), 'section').length, 1);
    assert.equal(nodes(render(), 'button').some(node => node.props.children === 'invitations.history.open'), true);
});
