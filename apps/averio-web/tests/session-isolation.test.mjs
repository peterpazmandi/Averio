import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';
import { QueryClient, QueryObserver } from '@tanstack/react-query';

function load(path, imports, globals = {}) {
    const code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, ...globals, require: name => {
        if (name in imports) return imports[name];
        throw new Error(`Unexpected import: ${name}`);
    } };
    vm.runInNewContext(code, context);
    return context.exports;
}
const deferred = () => {
    let resolve, reject;
    const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
    return { promise, resolve, reject };
};

test('logout clears all financial queries and mutation payloads while retaining the live auth observer', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const authKey = ['authentication', 'current-user'];
    let identity = { userId: 'owner', household: 'old' };
    const observer = new QueryObserver(client, { queryKey: authKey, queryFn: async () => identity, enabled: false });
    const unsubscribe = observer.subscribe(() => {});
    client.setQueryData(authKey, identity);
    client.setQueryData(['authentication', 'other-sensitive-result'], { privateNote: 'SECRET auth result' });
    const attached = client.getQueryCache().find({ queryKey: authKey });
    for (const root of ['transactions', 'transfers', 'activities', 'categories', 'labels', 'statistics', 'history', 'future-feature']) {
        client.setQueryData([root, 'old-household'], { privateNote: 'SECRET old identity' });
    }
    await client.getMutationCache().build(client, {
        mutationFn: async () => ({ privateNote: 'SECRET response' }),
    }).execute({ privateNote: 'SECRET request' });
    const late = deferred(); let requestSignal;
    const request = client.fetchQuery({ queryKey: ['private-in-flight'], queryFn: ({ signal }) => {
        requestSignal = signal;
        return late.promise;
    } }).catch(error => error);
    let csrfCleared = 0;
    const { clearCachedAuthState } = load('../src/features/authentication/api/authCache.ts', {
        './authQueryKeys': { authQueryKeys: { all: ['authentication'], currentUser: () => authKey } },
        '../../../shared/api/csrf': { clearCsrfToken: () => { csrfCleared++; } },
    });
    await clearCachedAuthState(client);
    assert.equal(requestSignal.aborted, true);
    assert.equal(csrfCleared, 1);
    assert.equal(client.getQueryCache().getAll().length, 1);
    assert.equal(client.getQueryCache().find({ queryKey: authKey }), attached);
    assert.equal(observer.getCurrentResult().data, undefined);
    assert.equal(client.getMutationCache().getAll().length, 0);
    late.resolve({ privateNote: 'SECRET delayed response' }); await request;
    assert.equal(client.getQueryData(['private-in-flight']), undefined);
    identity = { userId: 'partner', household: 'new' };
    await client.fetchQuery({ queryKey: authKey, queryFn: async () => identity, staleTime: 0 });
    assert.equal(observer.getCurrentResult().data.userId, 'partner');
    assert.equal(JSON.stringify(client.getQueryCache().getAll().map(q => q.state.data)).includes('SECRET'), false);
    unsubscribe(); client.clear();
});

for (const knownIdentity of [false, true]) {
    test(`unauthorized ${knownIdentity ? 'expired session cancels' : 'initial anonymous check preserves'} pending public CSRF actions`, async () => {
        const client = new QueryClient();
        const authKey = ['authentication', 'current-user'];
        const identity = knownIdentity ? { userId: 'previous-member' } : undefined;
        if (identity) client.setQueryData(authKey, identity);
        client.setQueryData(['private-finances'], { note: 'SECRET' });
        const request = deferred();
        const csrf = load('../src/shared/api/csrf.ts', {
            './apiConfiguration': { API_BASE_URL: 'http://api.test' },
        }, { DOMException, fetch: () => request.promise });
        const authCache = load('../src/features/authentication/api/authCache.ts', {
            './authQueryKeys': { authQueryKeys: { currentUser: () => authKey } },
            '../../../shared/api/csrf': csrf,
        });
        const scheduled = [];
        class ApiError extends Error { status = 401; }
        const { AuthProvider } = load('../src/features/authentication/context/AuthProvider.tsx', {
            react: { createContext: () => ({ Provider: 'provider' }), useCallback: fn => fn,
                useEffect: fn => fn(), useState: () => [false, () => {}] },
            'react/jsx-runtime': jsxRuntime,
            '@tanstack/react-query': { useQueryClient: () => client,
                useQuery: () => ({ data: identity, error: new ApiError(), isPending: false }) },
            '../../../shared/api/ApiError': { ApiError },
            '../api/authQueries': { authQueries: { currentUser: () => ({ queryKey: authKey }) } },
            '../api/authCache': authCache,
        }, { window: { setTimeout: fn => { scheduled.push(fn); return 1; }, clearTimeout() {} } });
        AuthProvider({ children: null });
        const pending = csrf.getCsrfToken().catch(error => error);
        scheduled.forEach(fn => fn());
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(client.getQueryData(['private-finances']), undefined, 'Unauthorized cache cleanup always runs');
        request.resolve({ ok: true, json: async () => ({ token: 'anonymous-token' }) });
        const result = await pending;
        if (knownIdentity) assert.equal(result.name, 'AbortError');
        else assert.equal(result, 'anonymous-token', 'The initial anonymous check cannot cancel email verification');
        client.clear();
    });
}

for (const order of ['old-first', 'new-first', 'old-error']) {
    test(`CSRF identity switch cancels old actions and shares the replacement (${order})`, async () => {
        const requests = [];
        const { getCsrfToken, clearCsrfToken } = load('../src/shared/api/csrf.ts', {
            './apiConfiguration': { API_BASE_URL: 'http://api.test' },
        }, { DOMException, fetch: () => { const request = deferred(); requests.push(request); return request.promise; } });
        const old = getCsrfToken().catch(error => error);
        const oldWaiter = getCsrfToken().catch(error => error);
        assert.equal(requests.length, 1);
        clearCsrfToken();
        const current = getCsrfToken();
        assert.equal(requests.length, 2);
        const reply = token => ({ ok: true, json: async () => ({ token }) });
        if (order === 'new-first') requests[1].resolve(reply('new-identity'));
        if (order === 'old-error') requests[0].reject(new Error('Old session failed'));
        else requests[0].resolve(reply('old-identity'));
        // Let the obsolete request's finally run while the new request is pending.
        await Promise.resolve(); await Promise.resolve(); await Promise.resolve();
        const currentWaiter = getCsrfToken();
        assert.equal(requests.length, 2, 'The old finally cannot evict the new in-flight request');
        requests[1].resolve(reply('new-identity'));
        assert.deepEqual(await Promise.all([current, currentWaiter]), Array(2).fill('new-identity'));
        for (const result of await Promise.all([old, oldWaiter])) {
            assert.equal(result.name, 'AbortError', 'An old action must not resume under the new identity');
        }
        assert.equal(await getCsrfToken(), 'new-identity');
        assert.equal(requests.length, 2);
        clearCsrfToken();
        const next = getCsrfToken(); requests[2].resolve(reply('third-identity'));
        assert.equal(await next, 'third-identity');
    });
}
