import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const directory = path.resolve(process.argv[2]);
const chrome = process.env.CHROME_BINARY || 'google-chrome';
const logo = fs.readFileSync('apps/averio-web/public/branding/averio-email-logo.png').toString('base64');
const expectedUrl = 'https://preview.averio.example.test/invitation#PREVIEW_ONLY_' + 'A'.repeat(43);
const measurement = function measureInvitationLayout() {
 const selectors={brand:'.av-email-brand',heading:'h1',body:'.av-email-body',action:'.av-email-action',privacy:'.av-email-privacy',expiry:'.av-email-expiry',fallback:'.av-email-fallback',footer:'.av-email-footer'};
 const rgb=value=>(value.match(/[\d.]+/g)??[]).slice(0,3).map(Number);
 const luminance=value=>{const c=rgb(value).map(n=>{n/=255;return n<=0.04045?n/12.92:((n+0.055)/1.055)**2.4;});return 0.2126*c[0]+0.7152*c[1]+0.0722*c[2];};
 const background=element=>{for(let node=element;node;node=node.parentElement){const value=getComputedStyle(node).backgroundColor;if(value!=='rgba(0, 0, 0, 0)'&&value!=='transparent')return value;}return 'rgb(255, 255, 255)';};
 const result={width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,blocks:{},links:[],visibleUrl:'',buttonHeight:0};
 for(const [name,selector]of Object.entries(selectors)){
   const element=document.querySelector(selector);
   if(!element){result.blocks[name]={missing:true};continue;}
   const bounds=element.getBoundingClientRect();
   const style=getComputedStyle(element);
   const fg=luminance(style.color), bg=luminance(background(element));
   result.blocks[name]={left:bounds.left,right:bounds.right,top:bounds.top,bottom:bounds.bottom,width:bounds.width,height:bounds.height,contrast:(Math.max(fg,bg)+0.05)/(Math.min(fg,bg)+0.05),textLength:element.textContent.trim().length,color:style.color,background:background(element)};
 }
 const card=document.querySelector('td.av-email-card');
 const bounds=card.getBoundingClientRect();
 result.card={left:bounds.left,right:bounds.right,top:bounds.top,bottom:bounds.bottom};
 const button=document.querySelector('td.av-email-button');
 result.buttonHeight=button?.getBoundingClientRect().height??0;
 result.links=[...document.querySelectorAll('a')].map(link=>link.getAttribute('href'));
 result.visibleUrl=document.querySelector('.av-email-fallback')?.textContent.trim()??'';
 const output=document.createElement('pre');
 output.id='averio-email-layout-result';
 output.style.display='none';
 output.textContent=JSON.stringify(result);
 document.body.append(output);
};
const modes = ['light', 'outlook-ogsc', 'outlook-ogsb', 'head-styles-stripped', 'anchor-box-stripped', 'images-blocked'];
let count = 0;

for (const culture of ['en-US', 'de-DE', 'hu-HU']) {
    const source = fs.readFileSync(path.join(directory, culture + '.html'), 'utf8');
    for (const mode of modes) {
        for (const width of [960, 320]) {
            const name = culture + '-' + mode + '-' + width;
            let html = source.replace(/src="[^"]*averio-email-logo\.png"/,
                'src="data:image/png;base64,' + logo + '"');
            if (mode === 'outlook-ogsc' || mode === 'outlook-ogsb') {
                html = html.replace('<html ', '<html data-' + mode.slice('outlook-'.length) + '="" ');
            }
            if (mode === 'head-styles-stripped') html = html.replace(/<style>[\s\S]*?<\/style>/gi, '');
            if (mode === 'anchor-box-stripped') {
                html = html.replace(/(<a\b[^>]*style=")([^"]*)"/g, (match, start, styles) =>
                    start + styles.replace(/(?:display|padding|background(?:-color)?|line-height)\s*:[^;"]*;?/gi, '') + '"');
            }
            if (mode === 'images-blocked') html = html.replace(/src="data:image\/png;base64,[^"]*"/, 'src="data:image/png;base64,blocked"');
            html = html.replace('</body>', '<script>(' + measurement.toString() + ')();</script></body>');
            const file = path.join(directory, name + '.html');
            fs.writeFileSync(file, html);
            const output = execFileSync(chrome, [
                '--headless', '--no-sandbox', '--disable-gpu', '--disable-dev-shm-usage',
                '--disable-background-networking', '--no-first-run', '--no-default-browser-check',
                '--user-data-dir=' + path.join(directory, 'chrome-profile'),
                '--dump-dom', '--window-size=' + width + ',1600',
                pathToFileURL(file).href,
            ], { encoding: 'utf8', timeout: 30000, maxBuffer: 4 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
            const match = output.match(/<pre\b[^>]*id="averio-email-layout-result"[^>]*>([\s\S]*?)<\/pre>/);
            assert.ok(match, name + ': browser must execute the measurement');
            const result = JSON.parse(match[1].replaceAll('&amp;', '&').replaceAll('&lt;', '<').replaceAll('&gt;', '>'));
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
console.log('PASS ' + count + ' invitation email layout scenarios (browser checks, not Outlook emulation)');
