import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import { renderToStaticMarkup } from 'react-dom/server';
import { createElement } from 'react';

const require = createRequire(import.meta.url);
function compile(path, customRequire = require) {
    const module = { exports: {} };
    const code = ts.transpileModule(readFileSync(path, 'utf8'), {
        compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX },
    }).outputText;
    vm.runInNewContext(code, { module, exports: module.exports, require: customRequire });
    return module.exports;
}
const copies = compile('src/i18n/locales/receipts.ts');
const stages = ['Photo', 'Uploading', 'Queued', 'Processing', 'Ready', 'Saving', 'Posted'];
const outcomes = ['Failed', 'Discarded', 'Expired'];
for (const language of ['EN', 'DE', 'HU']) {
    const copy = copies['receipts' + language];
    const t = (key, options = {}) => {
        if (key === 'transactionManagement.saving') return 'Saving';
        const value = key.split('.').slice(1).reduce((node, part) => node?.[part], copy) ?? options.defaultValue;
        assert.equal(typeof value, 'string', `Missing ${language} translation: ${key}`);
        return value.replace('{{status}}', options.status ?? '');
    };
    const { ReceiptProgress } = compile('src/features/receipts/ReceiptProgress.tsx', name =>
        name === 'react-i18next' ? { useTranslation: () => ({ t }) } : require(name));
    for (const status of [...stages, ...outcomes, 'UnknownFutureState']) {
        test(`${language} progress highlights only ${status} and keeps all outcomes visible`, () => {
            const html = renderToStaticMarkup(createElement(ReceiptProgress, { status }));
            const matches = [...html.matchAll(/<(li|span)\b[^>]*aria-current="step"[^>]*>([\s\S]*?)<\/\1>/g)];
            assert.equal(matches.length, status === 'UnknownFutureState' ? 0 : 1);
            if (matches.length) {
                const label = status === 'Photo' ? copy.photoStep : status === 'Saving' ? 'Saving' : copy.status[status];
                assert.ok(matches[0][2].includes(label));
            }
            assert.equal((html.match(/<li\b/g) ?? []).length, stages.length);
            for (const outcome of outcomes) assert.ok(html.includes(copy.status[outcome]));
            // No completed milestones can be inferred for a terminal branch or unknown state.
            assert.equal((html.match(/✓/g) ?? []).length, Math.max(0, stages.indexOf(status)));
            assert.ok(!html.includes('aria-valuenow'), 'Do not invent a percent complete for asynchronous extraction.');
        });
    }
}
