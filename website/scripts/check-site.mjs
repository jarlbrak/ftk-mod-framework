import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const root=process.env.SITE_URL || 'http://127.0.0.1:4321/ftk-mod-framework/';
const paths=['','installation/','compatibility/','troubleshooting/','mods/paladin/','mods/paladin-equipment/','mods/thief/','mods/possum/','gallery/','releases/','credits/'];
const browser=await chromium.launch({headless:true});
const page=await browser.newPage();
const errors=[];page.on('pageerror',e=>errors.push(e.message));
const resources=new Set();const links=new Set();
for(const size of [{width:1440,height:1000},{width:390,height:844}]){
 await page.setViewportSize(size);
 for(const path of paths){
  const response=await page.goto(root+path);assert.equal(response.status(),200,path);
  await page.waitForLoadState('networkidle');
  for(const img of await page.locator('img:visible').all()){await img.scrollIntoViewIfNeeded();await img.evaluate(i=>i.decode());}
  await page.evaluate(()=>window.scrollTo(0,0));
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,`Overflow ${path} at ${size.width}`);
  const info=await page.evaluate(()=>({images:[...document.images].filter(i=>i.getClientRects().length).map(i=>({src:i.src,alt:i.alt,loaded:i.complete&&i.naturalWidth>0})),links:[...document.querySelectorAll('a[href]')].map(a=>a.href),media:[...document.querySelectorAll('source')].map(s=>s.src),videos:[...document.querySelectorAll('video')].map(v=>({controls:v.controls,autoplay:v.autoplay,poster:v.poster,loop:v.loop}))}));
  for(const i of info.images){assert(i.alt,`Missing alt ${i.src}`);assert(i.loaded,`Broken image ${i.src}`);resources.add(i.src)}
  info.links.forEach(l=>links.add(l));info.media.forEach(l=>resources.add(l));
  for(const v of info.videos){assert(v.controls&&v.loop&&!v.autoplay);resources.add(v.poster)}
  if(['','mods/paladin/','gallery/','mods/paladin-equipment/'].includes(path))await page.screenshot({path:`/tmp/ftk-site-${path.replaceAll('/','-')||'home'}-${size.width}.png`,fullPage:true});
 }
}
// Exercise every published item's dialog and its art, including mobile and keyboard dismissal.
for(const size of [{width:1440,height:1000},{width:390,height:844}]){
 await page.setViewportSize(size);
 for(const [mod,count] of [['paladin',51],['thief',45]]){
  await page.goto(root+'mods/'+mod+'/');
  const items=page.locator('.equipment-item');assert.equal(await items.count(),count);
  for(const item of await items.all()){
   const name=await item.locator('h4').textContent();
   await item.locator('summary').click();
   const dialog=page.getByRole('dialog',{name,exact:true});await dialog.waitFor();
   await dialog.locator('img').evaluate(i=>i.decode());
   assert(await dialog.locator('img').evaluate(i=>i.naturalWidth>0));
   assert.equal(await dialog.evaluate(d=>d.scrollWidth<=d.clientWidth),true,'Dialog overflow '+name);
   assert(await dialog.evaluate(d=>{const r=d.getBoundingClientRect();return Math.abs(r.left+r.width/2-innerWidth/2)<2;}),'Dialog centering '+name);
   assert(await dialog.locator('.item-acquisition').innerText());
   if(name==='Tin Oath Token'){
    assert.match(await dialog.innerText(),/Grants Smite/);assert.match(await dialog.innerText(),/150%/);
    await page.screenshot({path:`/tmp/ftk-item-smite-${size.width}.png`});
   }
   await page.keyboard.press('Escape');assert.equal(await dialog.count(),0);
   assert(await item.locator('summary').evaluate(s=>s===document.activeElement));
  }
  await page.getByRole('searchbox',{name:'Find equipment'}).fill('no-such-item');assert.equal(await page.locator('.equipment-item:visible').count(),0);
  await page.getByRole('searchbox',{name:'Find equipment'}).fill(mod==='paladin'?'smite':'borrowed fortune');
  assert.equal(await page.locator('.equipment-item:visible').count(),mod==='paladin'?20:1);
 }
}
for(const url of [...resources,...links]){
 if(!url.startsWith(root))continue;
 const u=new URL(url);const response=await page.request.get(u.href);assert.equal(response.status(),200,`Broken local URL ${url}`);
 if(u.hash && response.headers()['content-type']?.includes('text/html')){await page.goto(u.href);assert(await page.locator(`[id=${JSON.stringify(decodeURIComponent(u.hash.slice(1)))}]`).count(),`Broken fragment ${url}`);}
}
await page.setViewportSize({width:1440,height:1000});await page.goto(root);
await page.getByRole('button',{name:'Search',exact:false}).first().click();
await page.getByPlaceholder('Search',{exact:true}).fill('Smite');
await page.locator('.pagefind-ui__result').first().waitFor();
assert.match(await page.locator('.pagefind-ui__results').innerText(),/Paladin/);
await page.keyboard.press('Escape');
await page.emulateMedia({reducedMotion:'reduce'});await page.goto(root+'mods/paladin/');
assert(await page.locator('video').evaluateAll(vs=>vs.every(v=>v.paused&&!v.autoplay)));
for(const video of await page.locator('video').all()){
 await video.evaluate(async v=>{await v.play()});await page.waitForTimeout(300);assert(await video.evaluate(v=>v.currentTime>0&&!v.error));await video.evaluate(v=>v.pause());
}
await page.setViewportSize({width:390,height:844});await page.goto(root+'mods/paladin/');
await page.getByRole('button',{name:'Menu',exact:true}).click();
await page.locator('#starlight__sidebar').waitFor({state:'visible'});
await page.locator('#starlight__sidebar').getByRole('link',{name:'Installation',exact:true}).click();
await page.waitForURL(root+'installation/');
assert.deepEqual(errors,[]);
await fs.writeFile('/tmp/ftk-site-external-links.json',JSON.stringify([...links].filter(l=>!l.startsWith(root)),null,2));
console.log(`PASS: ${paths.length} pages at desktop/mobile, local links, fragments, images, search, mobile menu, reduced motion, and video playback.`);
await browser.close();
