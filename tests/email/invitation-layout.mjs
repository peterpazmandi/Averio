import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { promisify } from 'node:util';

const runChrome = promisify(execFile);

const directory = path.resolve(process.argv[2]);
const chrome = process.env.CHROME_BINARY || 'google-chrome';
const logo = fs.readFileSync('apps/averio-web/public/branding/averio-email-logo.png');
const fixtures = new Map();
const server = http.createServer((request, response) => {
    if (fixtures.has(request.url)) {
        response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        response.end(fixtures.get(request.url));
        return;
    }
    if (request.url !== '/branding/averio-email-logo.png') {
        response.writeHead(404).end();
        return;
    }
    response.writeHead(200, { 'Content-Type': 'image/png', 'Cache-Control': 'no-store' });
    response.end(logo);
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const logoFixtureUrl = 'http://127.0.0.1:' + server.address().port + '/branding/averio-email-logo.png';
const expectedUrl = 'http://localhost:5173/invitation#PREVIEW_ONLY_' + 'A'.repeat(43);
const measurement = function measureInvitationLayout() {
 const selectors={brand:'.av-email-brand',heading:'h1',body:'.av-email-body',action:'.av-email-action',privacy:'.av-email-privacy',expiry:'.av-email-expiry',fallback:'.av-email-fallback',footer:'.av-email-footer'};
 const select=selector=>document.querySelector(selector)??document.querySelector(selector.replaceAll('.av-email-', '.x_av-email-'));
 const rgb=value=>(value.match(/[\d.]+/g)??[]).slice(0,3).map(Number);
 const luminance=value=>{const c=rgb(value).map(n=>{n/=255;return n<=0.04045?n/12.92:((n+0.055)/1.055)**2.4;});return 0.2126*c[0]+0.7152*c[1]+0.0722*c[2];};
 const background=element=>{for(let node=element;node;node=node.parentElement){const value=getComputedStyle(node).backgroundColor;if(value!=='rgba(0, 0, 0, 0)'&&value!=='transparent')return value;}return 'rgb(255, 255, 255)';};
 const result={width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,blocks:{},links:[],visibleUrl:'',buttonHeight:0};
 for(const [name,selector]of Object.entries(selectors)){
   const element=select(selector);
   if(!element){result.blocks[name]={missing:true};continue;}
   const bounds=element.getBoundingClientRect();
   const style=getComputedStyle(element);
   const fg=luminance(style.color), bg=luminance(background(element));
   result.blocks[name]={left:bounds.left,right:bounds.right,top:bounds.top,bottom:bounds.bottom,width:bounds.width,height:bounds.height,contrast:(Math.max(fg,bg)+0.05)/(Math.min(fg,bg)+0.05),textLength:element.textContent.trim().length,color:style.color,background:background(element)};
 }
 const card=select('td.av-email-card');
 const bounds=card.getBoundingClientRect();
 result.card={left:bounds.left,right:bounds.right,top:bounds.top,bottom:bounds.bottom,background:getComputedStyle(card).backgroundColor};
 const canvas=select('td.av-email-page');
 result.canvasBackground=getComputedStyle(canvas).backgroundColor;
 result.rowBackgrounds=[...select('.av-email-content').querySelectorAll('td')].filter(cell=>![...cell.classList].some(name=>/av-email-(privacy|button)$/.test(name))).map(cell=>getComputedStyle(cell).backgroundColor);
 const image=select('.av-email-logo');
 result.logo={complete:image.complete,width:image.naturalWidth,height:image.naturalHeight};
 const button=select('td.av-email-button');
 result.buttonHeight=button?.getBoundingClientRect().height??0;
 result.links=[...document.querySelectorAll('a')].map(link=>link.getAttribute('href'));
 result.visibleUrl=select('.av-email-fallback')?.textContent.trim()??'';
 const output=document.createElement('pre');
 output.id='averio-email-layout-result';
 output.style.display='none';
 output.textContent=JSON.stringify(result);
 document.body.append(output);
};
const modes = ['light', 'native-dark', 'outlook-ogsc', 'outlook-ogsb',
    'outlook-light-dark-os', 'outlook-prefixed-ogsc', 'outlook-prefixed-ogsb',
    'outlook-prefixed-ogac', 'outlook-prefixed-ogab',
    'head-styles-stripped', 'anchor-box-stripped', 'images-blocked'];
let count = 0;

try {
    for (const culture of ['en-US', 'de-DE', 'hu-HU']) {
        const source = fs.readFileSync(path.join(directory, culture + '.html'), 'utf8');
        // Reproduce the reported local navigation origin without letting it host images.
        assert.match(source, /src="https:\/\/averioapp\.com\/branding\/averio-email-logo\.png"/,
            culture + ': production logo origin is independent of localhost navigation');
        for (const mode of modes) {
            for (const width of [960, 320]) {
                const name = culture + '-' + mode + '-' + width;
                let html = source.replace(/src="https:\/\/averioapp\.com\/branding\/averio-email-logo\.png"/,
                    'src="' + logoFixtureUrl + '"');
                const prefixed = mode === 'outlook-light-dark-os' || mode.startsWith('outlook-prefixed-');
                const dark = mode === 'native-dark' || (mode.startsWith('outlook-') && mode !== 'outlook-light-dark-os');
                if (prefixed) {
                    // Model Outlook's documented class rewriting in markup and CSS.
                    html = html.replaceAll('.av-email-', '.x_av-email-').replace(/class="([^"]+)"/g,
                        (match, classes) => 'class="' + classes.split(/\s+/).map(name => 'x_' + name).join(' ') + '"');
                }
                if (mode === 'native-dark' || prefixed) {
                    // Force just this media branch active, modeling OS dark mode.
                    html = html.replace('@media (prefers-color-scheme:dark)', '@media all');
                }
                if (dark && mode !== 'native-dark') {
                    const marker = mode.split('-').at(-1);
                    const root = prefixed ? 'x_av-email-theme-root' : 'av-email-theme-root';
                    // Mark an actual table element, not an artificial HTML ancestor.
                    html = html.replace('class="' + root + '"', 'class="' + root + '" data-' + marker + '=""');
                }
                if (mode === 'head-styles-stripped') html = html.replace(/<style>[\s\S]*?<\/style>/gi, '');
                if (mode === 'anchor-box-stripped') {
                    html = html.replace(/(<a\b[^>]*style=")([^"]*)"/g, (match, start, styles) =>
                        start + styles.replace(/(?:display|padding|background(?:-color)?|line-height)\s*:[^;"]*;?/gi, '') + '"');
                }
                if (mode === 'images-blocked') html = html.replace('src="' + logoFixtureUrl + '"', 'src="data:image/png;base64,blocked"');
                html = html.replace('</body>', '<script>window.addEventListener("load", ' + measurement.toString() + ');</script></body>');
                const file = path.join(directory, name + '.html');
                fs.writeFileSync(file, html);
                fixtures.set('/' + name + '.html', html);
                const { stdout: output } = await runChrome(chrome, [
                    '--headless', '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage',
                    '--disable-background-networking', '--no-first-run', '--no-default-browser-check',
                    '--user-data-dir=' + path.join(directory, 'chrome-profile'),
                    '--dump-dom', '--virtual-time-budget=1000', '--window-size=' + width + ',1600',
                    new URL('/' + name + '.html', logoFixtureUrl).href,
                ], { encoding: 'utf8', timeout: 30000, maxBuffer: 4 * 1024 * 1024 });
                const match = output.match(/<pre\b[^>]*id="averio-email-layout-result"[^>]*>([\s\S]*?)<\/pre>/);
                assert.ok(match, name + ': browser must execute the measurement');
                const result = JSON.parse(match[1].replaceAll('&amp;', '&').replaceAll('&lt;', '<').replaceAll('&gt;', '>'));
                assert.ok(result.logo.complete, name + ': logo request has completed');
                if (mode === 'images-blocked') assert.equal(result.logo.width, 0, name + ': blocked image keeps the readable wordmark');
                else assert.ok(result.logo.width > 0 && result.logo.height > 0, name + ': real PNG loads over HTTP');
                assert.equal(result.card.background, dark ? 'rgb(13, 23, 38)' : 'rgb(255, 255, 255)',
                    name + ': card follows the message theme, independently of OS dark mode');
                assert.equal(result.canvasBackground, dark ? 'rgb(7, 16, 31)' : 'rgb(243, 248, 246)',
                    name + ': canvas follows the same message theme');
                assert.ok(result.rowBackgrounds.every(color => color === 'rgba(0, 0, 0, 0)'),
                    name + ': text and spacer rows inherit one continuous card surface');
                assert.ok(result.scrollWidth <= result.width + 1, name + ': no horizontal overflow');
                assert.ok(result.card.right - result.card.left <= 560.5, name + ': card fits the reading pane');
                assert.ok(result.buttonHeight >= 48, name + ': button remains visible without anchor box styles');
                assert.deepEqual(result.links, [expectedUrl, expectedUrl], name + ': button and fallback preserve the same credential');
                assert.equal(result.visibleUrl, expectedUrl, name + ': copied URL has no invisible breaks');
                let previousBottom = -1;
                for (const [blockName, block] of Object.entries(result.blocks)) {
                    assert.equal(block.missing, undefined, name + ': missing ' + blockName);
                    assert.ok(block.textLength > 0 && block.height > 0, name + ': visible ' + blockName);
                    assert.ok(block.contrast >= 4.5, name + ': readable ' + blockName + ' (' + block.color + ' on ' + block.background + ')');
                    assert.ok(block.top >= previousBottom - 1, name + ': no overlap or escaped row for ' + blockName);
                    if (blockName !== 'footer' && blockName !== 'brand') {
                        assert.ok(block.left >= result.card.left && block.right <= result.card.right + 1,
                            name + ': ' + blockName + ' stays inside the card');
                    }
                    previousBottom = block.bottom;
                }
                assert.ok(result.blocks.privacy.top - result.blocks.action.bottom >= 20, name + ': privacy text is separated from the action');
                if (mode !== 'anchor-box-stripped') assert.ok(result.blocks.action.height >= 48, name + ': full-height clickable review action');
                console.log('PASS invitation email browser layout ' + name);
                count++;
            }
        }
    }
    console.log('PASS ' + count + ' invitation email layout scenarios with real PNG loads (browser checks, not Outlook emulation)');
} finally {
    await new Promise(resolve => server.close(resolve));
}
