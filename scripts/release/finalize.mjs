import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
const manifest = JSON.parse(readFileSync('release/manifest.json', 'utf8'));
for (const component of ['api', 'worker']) {
  if (manifest.components[component].rebuild) {
    const digest = process.env[`${component.toUpperCase()}_DIGEST`];
    if (!/^sha256:[a-f0-9]{64}$/.test(digest ?? '')) throw new Error(`Invalid ${component} digest`);
    manifest.components[component].image = `${process.env.ECR_REGISTRY}/averio-${component}@${digest}`;
    manifest.components[component].commit = manifest.commit;
  }
}
if (!/^sha256:[a-f0-9]{64}$/.test(process.env.MIGRATION_DIGEST ?? '')) throw new Error('Invalid migrator digest');
manifest.migrator = `${process.env.ECR_REGISTRY}/averio-migrator@${process.env.MIGRATION_DIGEST}`;
manifest.components.frontend.sha256 = createHash('sha256').update(readFileSync('release/frontend.tar.gz')).digest('hex');
if (manifest.components.frontend.rebuild) manifest.components.frontend.commit = manifest.commit;
writeFileSync('release/manifest.json', JSON.stringify(manifest, null, 2) + '\n');
writeFileSync('release/notes.md', `Release ${manifest.tag}\n\nCommit: ${manifest.commit}\n\n` +
  Object.entries(manifest.components).map(([name, value]) => `- ${name}: ${value.version}`).join('\n') +
  '\n\nDeployment uses the attached manifest and frontend archive. Database migrations run explicitly before each backend deployment.\n');
