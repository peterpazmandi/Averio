import { createHash } from 'node:crypto';

export function verifyFrontendArchiveHash(archive, expectedHash) {
  if (typeof expectedHash !== 'string' || !/^[a-f0-9]{64}$/.test(expectedHash)) {
    throw new Error('Previous release has no valid frontend archive SHA-256');
  }

  const actualHash = createHash('sha256').update(archive).digest('hex');
  if (actualHash !== expectedHash) {
    throw new Error('Previous frontend archive SHA-256 mismatch');
  }
}