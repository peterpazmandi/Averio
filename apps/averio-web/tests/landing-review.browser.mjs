import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import fs from 'node:fs';
import { spawn } from 'node:child_process';
const url = process.env.LANDING_URL ?? 'http://127.0.0.1:5278';
const server = process.env.LANDING_URL ? null : spawn(process.execPath,
 ['node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', '5278', '--strictPort'],
 { windowsHide: true, stdio: 'ignore' });
for (let i=0; i<80; i++) {
 try { if ((await fetch(url)).ok) break; } catch { /* Wait for preview startup. */ }
 await new Promise(resolve=>setTimeout(resolve,100));
}
const artifacts = '../../test-results/landing-review';
fs.mkdirSync(artifacts, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', headless: true });
let cases = 0;
try {
 for (const width of [320, 390, 768, 1024, 1440, 1920]) for (const language of ['en','de','hu']) for (const theme of ['light','dark']) {
  const page = await browser.newPage({ viewport: { width, height: 945 } });
  const errors = [];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(({language,theme})=>{if(!localStorage.getItem('review.initialized')) {localStorage.setItem('i18nextLng',language);localStorage.setItem('averio.theme',theme);localStorage.setItem('review.initialized','yes');}},{language,theme});
  await page.route('**/api/v1/**', r=>r.fulfill({status:401,contentType:'application/json',body:'{}'}));
  for (const path of ['/','/contact']) {
   await page.goto(url+path);await page.locator('h1').waitFor();
   assert.equal(await page.locator('header select').inputValue(),language);
   assert.equal(await page.locator('html').evaluate(e=>e.classList.contains('dark')),theme==='dark');
   const overflow = await page.evaluate(()=> [...document.querySelectorAll('main *, header *')].filter(e=>{const r=e.getBoundingClientRect();const st=getComputedStyle(e);return r.width>0 && st.position!=='absolute' && !e.closest('[aria-hidden="true"]') && (r.right>innerWidth+1||r.left < -1);}).map(e=>e.tagName+':'+e.className));
   assert.deepEqual(overflow,[],`${path} ${width} ${language} ${theme}`);
   if(path==='/') {
    assert.ok(await page.locator('h1').evaluate(e=>parseFloat(getComputedStyle(e).fontSize))<=56);
    if(width>=640) assert.equal(await page.locator('aside > div').evaluateAll(es=>es.slice(1).every(e=>e.scrollWidth<=e.clientWidth)),true);
    await page.locator('main button[aria-pressed]').nth(1).click();await page.getByText('€312.00',{exact:true}).waitFor();
    await page.locator('main button[aria-pressed]').first().click();
    assert.equal(await page.locator('#faq a[href="/contact"]').count(),1);
   } else {
    assert.equal(await page.locator('a[href="mailto:support@averioapp.com"]').count(),1);
    assert.equal(await page.locator('form').count(),0);
   }
   if([320,1440,1920].includes(width)) await page.screenshot({path:`${artifacts}/${path==='/'?'landing':'contact'}-${width}-${language}-${theme}.png`,fullPage:true});
  }
  await page.locator('header select').selectOption(language==='en'?'hu':'en');
  await page.locator('header button').first().click();await page.reload();await page.locator('h1').waitFor();
  assert.equal(await page.locator('header select').inputValue(),language==='en'?'hu':'en');
  assert.equal(await page.locator('html').evaluate(e=>e.classList.contains('dark')),theme!=='dark');
  await page.goto(url+'/');await page.locator('h1').waitFor();
  if(width<1280) {await page.locator('[aria-controls="landing-mobile-nav"]').click();await page.locator('#landing-mobile-nav a[href="/contact"]').click();} else await page.locator('header a[href="/contact"]').click();
  await page.waitForURL('**/contact');await page.locator('h1').waitFor();
  assert.deepEqual(errors,[]);await page.close();cases++;
  if(cases%12===0) console.log(`PASS width ${width}: 12 landing/contact renders, navigation and preferences`);
 }
 const page = await browser.newPage({viewport:{width:320,height:900}});
 await page.route('**/api/v1/**', r=>r.fulfill({status:503,contentType:'application/json',body:'{}'}));
 await page.goto(url+'/'); await page.locator('h1').waitFor();
 await page.locator('[aria-controls="landing-mobile-nav"]').click();
 await page.locator('#landing-mobile-nav a[href="/#features"]').click();
 await page.waitForFunction(()=>location.hash==='#features');
 await page.locator('#faq summary').first().click();
 assert.equal(await page.locator('#faq details').first().evaluate(e=>e.open),true);
 await page.locator('#faq a[href="/contact"]').click();await page.waitForURL('**/contact');
 await page.locator('main a[href="/#faq"]').click();await page.waitForURL('**/#faq');
 await page.locator('footer a[href="/contact"]').click();await page.waitForURL('**/contact');
 await page.goto(url+'/login');await page.locator('select').waitFor();
 const loginSize=await page.locator('select').evaluate(e=>({height:e.offsetHeight,width:e.offsetWidth}));
 await page.locator('select').selectOption('de');await page.reload();
 assert.equal(await page.locator('select').inputValue(),'de');
 await page.goto(url+'/contact');await page.locator('h1').waitFor();
 assert.deepEqual(await page.locator('header select').evaluate(e=>({height:e.offsetHeight,width:e.offsetWidth})),loginSize);
 assert.equal(await page.locator('header select').inputValue(),'de');
 await page.goto(url+'/register');await page.locator('select').waitFor();
 assert.equal(await page.locator('select').inputValue(),'de');
 assert.equal(new URL(page.url()).pathname,'/register');
 await page.close();
 console.log(`PASS ${cases} viewport/language/theme combinations across landing/contact, overflow, preview, persisted preferences, navigation, email, screenshots.`);
} finally {await browser.close();server?.kill();}
