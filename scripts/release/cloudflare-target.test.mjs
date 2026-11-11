import test from 'node:test';
import assert from 'node:assert/strict';
import { validateCloudflareTarget } from './cloudflare-target.mjs';
const config = { project: 'averio-integration', branch: 'develop', frontendUrl: 'https://integration.averioapp.com' };
const body = { success: true, result: { name: config.project, production_branch: config.branch,
  domains: ['averio-integration.pages.dev', 'integration.averioapp.com'] } };
test('accepts the project serving the configured custom domain', () => {
  assert.doesNotThrow(() => validateCloudflareTarget(body, config));
});
test('rejects a production project mistakenly configured in integration even with the same branch', () => {
  assert.throws(() => validateCloudflareTarget({ success: true, result: {
    name: 'averio', production_branch: 'develop', domains: ['averioapp.com'],
  } }, { ...config, project: 'averio' }), /hostname/);
});
test('rejects mismatched branch, project, missing domains and non-origin URLs', () => {
  assert.throws(() => validateCloudflareTarget(body, { ...config, branch: 'main' }));
  assert.throws(() => validateCloudflareTarget(body, { ...config, project: 'other' }));
  assert.throws(() => validateCloudflareTarget({ ...body, result: { ...body.result, domains: null } }, config));
  for (const frontendUrl of ['http://integration.averioapp.com', 'https://integration.averioapp.com/login',
    'https://user:pass@integration.averioapp.com', 'https://integration.averioapp.com?x=1']) {
    assert.throws(() => validateCloudflareTarget(body, { ...config, frontendUrl }));
  }
});
