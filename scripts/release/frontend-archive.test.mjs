import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { verifyFrontendArchiveHash } from './frontend-archive.mjs';

const archive = Buffer.from('verified frontend archive');
const archiveHash = createHash('sha256').update(archive).digest('hex');

test('accepts a frontend archive matching the previous release manifest', () => {
  assert.doesNotThrow(() => verifyFrontendArchiveHash(archive, archiveHash));
});

test('rejects a corrupted frontend archive', () => {
  assert.throws(
    () => verifyFrontendArchiveHash(Buffer.from('altered archive'), archiveHash),
    /Previous frontend archive SHA-256 mismatch/,
  );
});

test('rejects a missing or malformed previous archive hash', () => {
  for (const hash of [undefined, 'not-a-hash', 'a'.repeat(63), 'A'.repeat(64)]) {
    assert.throws(
      () => verifyFrontendArchiveHash(archive, hash),
      /Previous release has no valid frontend archive SHA-256/,
    );
  }
});