export const components = ['frontend', 'api', 'worker'];

export function affectedComponents(paths) {
  const affected = new Set();
  for (const path of paths) {
    if (/^(docs\/|\.github\/|scripts\/|tests\/|AGENTS\.md$|README\.md$)/.test(path)) continue;
    if (path.startsWith('apps/averio-web/')) affected.add('frontend');
    else if (path.startsWith('apps/Averio.Api/') || path === 'Dockerfile.api') affected.add('api');
    else if (path.startsWith('apps/Averio.Worker/') || path === 'Dockerfile.worker') affected.add('worker');
    else if (path.startsWith('src/')) { affected.add('api'); affected.add('worker'); }
    else components.forEach(component => affected.add(component));
  }
  return components.filter(component => affected.has(component));
}

export function nextVersions(previous, kind, affected) {
  if (!['feature', 'hotfix'].includes(kind)) throw new Error('Unknown release kind');
  const versions = Object.fromEntries(components.map(component => {
    const value = previous?.[component]?.version ?? '0.0.0';
    if (!/^\d+\.\d+\.\d+$/.test(value)) throw new Error('Invalid component version');
    return [component, value.split('.').map(Number)];
  }));
  if (!previous && kind !== 'feature') throw new Error('First release must be a feature release');
  if (kind === 'feature') {
    const highest = Object.values(versions).sort((a, b) => b[0] - a[0] || b[1] - a[1] || b[2] - a[2])[0];
    return Object.fromEntries(components.map(component => [component, `${highest[0]}.${highest[1] + 1}.0`]));
  }
  if (!affected.length) throw new Error('No deployable component changed; use a feature release for pipeline changes');
  return Object.fromEntries(components.map(component => {
    const [major, minor, patch] = versions[component];
    return [component, `${major}.${minor}.${patch + (affected.includes(component) ? 1 : 0)}`];
  }));
}
