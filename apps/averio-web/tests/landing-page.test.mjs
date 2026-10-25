import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';

function loadHome(readAuth, readHash = () => '') {
    function LandingPage() {}
    function Navigate() {}
    const imports = {
        '@tanstack/react-router': { createFileRoute: () => options => options, Navigate, useLocation: ({ select }) => select({ hash: readHash() }) },
        '../features/authentication/context/AuthProvider': { useAuth: readAuth },
        '../features/landing/LandingPage': { LandingPage },
        'react/jsx-runtime': jsxRuntime,
    };
    const code = ts.transpileModule(fs.readFileSync(new URL('../src/routes/index.tsx', import.meta.url), 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    const context = { exports: {}, require: name => {
        if (name in imports) return imports[name];
        throw new Error(`Unexpected import: ${name}`);
    } };
    vm.runInNewContext(code, context);
    return { Route: context.exports.Route, LandingPage, Navigate };
}

test('public homepage renders while authentication is pending and reacts when it succeeds', () => {
    let auth = { status: 'loading' };
    const { Route, LandingPage, Navigate } = loadHome(() => auth);
    // A blocking route guard would stop React from mounting while /auth/me hangs.
    assert.equal(Route.beforeLoad, undefined);
    assert.equal(Route.component().type, LandingPage);

    auth = { status: 'authenticated', user: { onboardingRequired: false } };
    const result = Route.component();
    assert.equal(result.type, Navigate);
    assert.equal(result.props.to, '/app');
    assert.equal(result.props.replace, true, 'Back must not return to a redirecting homepage');
    assert.equal(result.props.search.redirect, undefined);
});

test('authenticated new members go to onboarding', () => {
    const { Route, Navigate } = loadHome(() => ({
        status: 'authenticated', user: { onboardingRequired: true },
    }));
    const result = Route.component();
    assert.equal(result.type, Navigate);
    assert.equal(result.props.to, '/onboarding');
    assert.equal(result.props.replace, true);
});

for (const status of ['loading', 'anonymous', 'error']) {
    test(`public homepage remains available for ${status} auth, even with stale user data`, () => {
        const { Route, LandingPage } = loadHome(() => ({
            status, user: { onboardingRequired: false }, error: new Error('API unavailable'),
        }));
        assert.equal(Route.component().type, LandingPage);
    });
}

for (const onboardingRequired of [false, true]) {
    test('explicit public sections survive authenticated state and hash changes: onboarding=' + onboardingRequired, () => {
        let hash = 'faq';
        const { Route, LandingPage, Navigate } = loadHome(() => ({
            status: 'authenticated', user: { onboardingRequired },
        }), () => hash);
        for (hash of ['main-content', 'top', 'why-averio', 'features', 'how-it-works', 'faq']) {
            assert.equal(Route.component().type, LandingPage);
        }
        for (hash of ['', 'unknown']) {
            const result = Route.component();
            assert.equal(result.type, Navigate);
            assert.equal(result.props.to, onboardingRequired ? '/onboarding' : '/app');
        }
    });
}
