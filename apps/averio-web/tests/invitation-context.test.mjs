import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import ts from 'typescript';

test('invitation context survives the auth journey, expires, and tolerates disabled storage', () => {
    const source = fs.readFileSync(new URL('../src/features/household/invitationContext.ts', import.meta.url), 'utf8');
    const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
    let stored = null;
    const context = { exports: {}, sessionStorage: { getItem: () => stored, setItem: (_, value) => { stored = value; }, removeItem: () => { stored = null; } } };
    vm.runInNewContext(code, context);
    const { rememberInvitation, pendingInvitation, forgetInvitation } = context.exports;
    assert.equal(pendingInvitation(), null);
    rememberInvitation('A'.repeat(64));
    assert.equal(pendingInvitation(), 'A'.repeat(64));
    rememberInvitation('https://untrusted.test');
    assert.equal(pendingInvitation(), 'A'.repeat(64), 'Invalid input cannot replace the invitation context');
    stored = JSON.stringify({ token: 'A'.repeat(64), expires: Date.now() - 1 });
    assert.equal(pendingInvitation(), null);
    rememberInvitation('B'.repeat(64));
    forgetInvitation();
    assert.equal(pendingInvitation(), null);
    context.sessionStorage.getItem = () => { throw new Error('Storage blocked'); };
    context.sessionStorage.setItem = context.sessionStorage.getItem;
    assert.equal(pendingInvitation(), null);
    assert.doesNotThrow(() => rememberInvitation('A'.repeat(64)));
});
