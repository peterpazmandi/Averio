import { chromium } from '../../apps/averio-web/node_modules/playwright/index.mjs';
import { verifyBrowser } from './browser-checks.mjs';
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await verifyBrowser({ page, frontend: process.env.FRONTEND_URL.replace(/\/$/, ''),
    api: process.env.API_URL.replace(/\/$/, ''), environment: process.env.DEPLOY_ENVIRONMENT,
    email: process.env.SMOKE_EMAIL, password: process.env.SMOKE_PASSWORD });
  console.log('Deployed application rendering and API routing verified' +
    (process.env.DEPLOY_ENVIRONMENT === 'integration' ? '; real login/logout UI passed' : ' (anonymous production check)'));
} finally { await browser.close(); }
