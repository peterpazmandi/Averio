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
const stages = ['Uploading', 'Queued', 'Processing'];
for (const language of ['EN', 'DE', 'HU']) {
    const copy = copies['receipts' + language];
    const t = (key, options = {}) => {
        const value = key.split('.').slice(1).reduce((node, part) => node?.[part], copy);
        assert.equal(typeof value, 'string', `Missing ${language} translation: ${key}`);
        return value.replace('{{status}}', options.status ?? '');
    };
    const { ReceiptProgress } = compile('src/features/receipts/ReceiptProgress.tsx', name =>
        name === 'react-i18next' ? { useTranslation: () => ({ t }) } : require(name));
    for (const status of [...stages, 'Photo', 'Ready', 'Saving', 'Posted', 'Failed', 'Discarded', 'Expired', 'UnknownFutureState']) {
        test(`${language} shows progress only during scanning: ${status}`, () => {
            const html = renderToStaticMarkup(createElement(ReceiptProgress, { status }));
            if (!stages.includes(status)) {
                assert.equal(html, '', 'No bar before scanning, during review, or after termination.');
                return;
            }
            assert.equal((html.match(/aria-current="step"/g) ?? []).length, 1);
            assert.equal((html.match(/<li\b/g) ?? []).length, 3);
            assert.ok(html.includes('role="progressbar"'));
            assert.ok(html.includes(`aria-valuetext="${copy.status[status]}"`));
            assert.ok(!html.includes('aria-valuenow'), 'Phases are not a measured percentage.');
        });
    }
}