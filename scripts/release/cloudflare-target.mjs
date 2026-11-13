export function validateCloudflareTarget(body, { project, branch, frontendUrl }) {
  const url = new URL(frontendUrl);
  if (url.protocol !== 'https:' || url.username || url.password || url.port ||
      url.pathname !== '/' || url.search || url.hash) {
    throw new Error('FRONTEND_URL must be an HTTPS origin');
  }
  if (!body.success || body.result?.name !== project || body.result?.production_branch !== branch) {
    throw new Error('Cloudflare project/production branch does not match deployment configuration');
  }
  if (!Array.isArray(body.result.domains) ||
      !body.result.domains.some(domain => typeof domain === 'string' && domain.toLowerCase() === url.hostname)) {
    throw new Error('Cloudflare project does not serve the configured frontend hostname');
  }
}
