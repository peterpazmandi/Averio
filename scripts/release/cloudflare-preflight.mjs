// Fail before migration if the token/project/branch cannot update the intended site.
import { validateCloudflareTarget } from './cloudflare-target.mjs';
const { CLOUDFLARE_ACCOUNT_ID: account, CLOUDFLARE_API_TOKEN: token,
  PAGES_PROJECT: project, PAGES_BRANCH: branch, FRONTEND_URL: frontendUrl } = process.env;
const response = await fetch(`https://api.cloudflare.com/client/v4/accounts/${encodeURIComponent(account)}/pages/projects/${encodeURIComponent(project)}`, {
  headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(15000),
});
if (!response.ok) throw new Error(`Cloudflare project preflight failed (HTTP ${response.status})`);
const body = await response.json();
validateCloudflareTarget(body, { project, branch, frontendUrl });
console.log('Cloudflare project, production branch and frontend hostname verified');
