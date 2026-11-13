import test from 'node:test';
import assert from 'node:assert/strict';
import { affectedComponents, nextVersions } from './version.mjs';
const previous = { frontend: { version: '1.3.4' }, api: { version: '1.3.2' }, worker: { version: '1.3.0' } };
test('feature aligns divergent component patches', () => {
  assert.deepEqual(nextVersions(previous, 'feature', []), { frontend: '1.4.0', api: '1.4.0', worker: '1.4.0' });
});
test('frontend hotfix preserves API and worker', () => {
  assert.deepEqual(nextVersions(previous, 'hotfix', affectedComponents(['apps/averio-web/src/main.tsx'])),
    { frontend: '1.3.5', api: '1.3.2', worker: '1.3.0' });
});
test('shared backend and migration changes bump both backend consumers', () => {
  assert.deepEqual(affectedComponents(['src/Averio.Infrastructure/Persistence/Migrations/new.cs']), ['api', 'worker']);
});
test('unknown build inputs conservatively affect all components', () => {
  assert.deepEqual(affectedComponents(['Directory.Packages.props']), ['frontend', 'api', 'worker']);
});
test('initial release and empty hotfix guards', () => {
  assert.deepEqual(nextVersions(undefined, 'feature', []), { frontend: '0.1.0', api: '0.1.0', worker: '0.1.0' });
  assert.throws(() => nextVersions(undefined, 'hotfix', ['api']));
  assert.throws(() => nextVersions(previous, 'hotfix', []));
});
