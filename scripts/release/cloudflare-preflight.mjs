// Fail before migration if the token/project/branch cannot update the intended site.
const { CLOUDFLARE_ACCOUNT_ID: account, CLOUDFLARE_API_TOKEN: token,
  PAGES_PROJECT: project, PAGES_BRANCH: branch } = process.env;
const response = await fetch(`https://api.cloudflare.com/client/v4/accounts/${encodeURIComponent(account)}/pages/projects/${encodeURIComponent(project)}`, {
  headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(15000),
});
if (!response.ok) throw new Error(`Cloudflare project preflight failed (HTTP ${response.status})`);
const body = await response.json();
if (!body.success || body.result?.production_branch !== branch) {
  throw new Error('Cloudflare project/production branch does not match deployment configuration');
}
console.log('Cloudflare project access and production branch verified');
