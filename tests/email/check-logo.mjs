import assert from 'node:assert/strict';

// Deployment preflight only: no recipients, invitation tokens or email delivery.
const url = new URL(process.argv[2] || 'https://averioapp.com/branding/averio-email-logo.png');
assert.equal(url.protocol, 'https:', 'Email logo must be hosted over HTTPS');
const response = await fetch(url, { signal: AbortSignal.timeout(10000), redirect: 'follow' });
assert.equal(new URL(response.url).protocol, 'https:', 'Logo redirects must retain HTTPS');
assert.ok(response.ok, 'Logo request failed with HTTP ' + response.status);
assert.match(response.headers.get('content-type') || '', /^image\/png(?:;|$)/i,
    'Logo must return image/png, not the SPA HTML fallback or a login/challenge page');
const bytes = Buffer.from(await response.arrayBuffer());
assert.ok(bytes.length >= 24 && bytes.length < 1024 * 1024, 'Logo payload must be a small PNG');
assert.deepEqual(bytes.subarray(0, 8), Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), 'Logo has a valid PNG signature');
assert.equal(bytes.toString('ascii', 12, 16), 'IHDR', 'Logo starts with its PNG image header');
assert.ok(bytes.readUInt32BE(16) > 0 && bytes.readUInt32BE(20) > 0, 'Logo has nonzero image dimensions');
console.log('PASS public email logo returns a PNG over HTTPS');
