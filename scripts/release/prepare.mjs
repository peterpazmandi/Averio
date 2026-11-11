import { execFileSync } from 'node:child_process';
import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { affectedComponents, components, nextVersions } from './version.mjs';

const run = (command, args) => execFileSync(command, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] }).trim();
const api = path => JSON.parse(run('gh', ['api', path]));
const repo = process.env.GITHUB_REPOSITORY;
const sha = run('git', ['rev-parse', 'HEAD']);
const kind = process.env.RELEASE_KIND;
if (process.env.GITHUB_REF !== 'refs/heads/develop') throw new Error('Release only from develop');
const checks = api(`repos/${repo}/actions/workflows/verify.yml/runs?head_sha=${sha}&event=push&per_page=100`).workflow_runs;
if (!checks.some(check => check.head_branch === 'develop' && check.conclusion === 'success')) {
  throw new Error('The exact develop commit must pass Verify before release');
}
mkdirSync('release', { recursive: true });
// Published releases only: an incomplete draft must never become the version baseline.
const releases = JSON.parse(run('gh', ['api', '--paginate', '--slurp', `repos/${repo}/releases?per_page=100`])).flat();
const prior = releases.find(release => !release.draft && !release.prerelease && /^release-\d+-\d+$/.test(release.tag_name));
let previous;
let changed = components;
if (prior) {
  run('gh', ['release', 'download', prior.tag_name, '--repo', repo, '--pattern', 'manifest.json', '--dir', 'previous']);
  previous = JSON.parse(readFileSync('previous/manifest.json', 'utf8'));
  if (previous.schema !== 1 || !/^[a-f0-9]{40}$/.test(previous.commit)) throw new Error('Invalid previous release');
  run('git', ['merge-base', '--is-ancestor', previous.commit, sha]);
  const paths = run('git', ['diff', '--name-only', previous.commit, sha]).split('\n').filter(Boolean);
  changed = affectedComponents(paths);
  if (kind === 'hotfix') {
    const commits = run('git', ['rev-list', '--first-parent', `${previous.commit}..${sha}`]).split('\n').filter(Boolean);
    if (!commits.length) throw new Error('No changes since previous release');
    for (const commit of commits) {
      const prs = api(`repos/${repo}/commits/${commit}/pulls`);
      if (!prs.some(pr => pr.merged_at && pr.base.ref === 'develop' && pr.head.ref.startsWith('hotfix/'))) {
        throw new Error(`Hotfix release includes ${commit} without a merged hotfix/ PR; create a feature release`);
      }
    }
  }
}
const versions = nextVersions(previous?.components, kind, changed);
const tag = `release-${process.env.GITHUB_RUN_NUMBER}-${process.env.GITHUB_RUN_ATTEMPT}`;
const manifest = { schema: 1, tag, commit: sha, kind, previous: prior?.tag_name ?? null,
  components: Object.fromEntries(components.map(component => [component, {
    ...(previous?.components[component] ?? {}), version: versions[component],
    rebuild: kind === 'feature' || changed.includes(component),
  }])) };
writeFileSync('release/manifest.json', JSON.stringify(manifest, null, 2) + '\n');
for (const [key, value] of Object.entries({ tag, sha, ...Object.fromEntries(components.flatMap(component => [
  [`${component}_version`, versions[component]], [`${component}_build`, manifest.components[component].rebuild],
])) })) appendFileSync(process.env.GITHUB_OUTPUT, `${key}=${value}\n`);
if (!manifest.components.frontend.rebuild) {
  run('gh', ['release', 'download', prior.tag_name, '--repo', repo, '--pattern', 'frontend.tar.gz', '--dir', 'release']);
}
