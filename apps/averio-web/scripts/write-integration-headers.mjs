import { writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const headersFile = resolve(projectRoot, 'dist', '_headers');

await writeFile(headersFile, '/*\n  X-Robots-Tag: noindex\n', 'utf8');
