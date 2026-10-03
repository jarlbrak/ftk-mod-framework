import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const root=process.env.SITE_URL || 'http://127.0.0.1:4321/ftk-mod-framework/';
const paths=['','installation/','tweaks/','compatibility/','troubleshooting/','mods/paladin/','mods/paladin-equipment/','mods/thief/','mods/blacksmith/','mods/possum/','mods/lore-store-unlocked/','gallery/','releases/','credits/'];
const browser=await chromium.launch({headless:true});
const page=await browser.newPage();
const errors=[];page.on('pageerror',e=>errors.push(e.message));
const resources=new Set();const links=new Set();
for(const size of [{width:1440,height:1000},{width:390,height:844}]){
 await page.setViewportSize(size);
 for(const path of paths){
  const response=await page.goto(root+path);assert.equal(response.status(),200,path);
  await page.waitForLoadState('networkidle');
  for(const img of await page.locator('img:visible').all()){await img.scrollIntoViewIfNeeded();await img.evaluate(async i=>{try{await i.decode()}catch(error){throw new Error(`Image failed to decode: ${i.src}: ${error.message}`)}});}
  await page.evaluate(()=>window.scrollTo(0,0));
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,`Overflow ${path} at ${size.width}`);
  const info=await page.evaluate(()=>({images:[...document.images].filter(i=>i.getClientRects().length).map(i=>({src:i.src,alt:i.alt,loaded:i.complete&&i.naturalWidth>0})),links:[...document.querySelectorAll('a[href]')].map(a=>a.href),media:[...document.querySelectorAll('source')].map(s=>s.src),videos:[...document.querySelectorAll('video')].map(v=>({controls:v.controls,autoplay:v.autoplay,poster:v.poster,loop:v.loop}))}));
  for(const i of info.images){assert(i.alt,`Missing alt ${i.src}`);assert(i.loaded,`Broken image ${i.src}`);resources.add(i.src)}
  info.links.forEach(l=>links.add(l));info.media.forEach(l=>resources.add(l));
  for(const v of info.videos){assert(v.controls&&v.loop&&!v.autoplay);resources.add(v.poster)}
  if(['','mods/paladin/','gallery/','mods/paladin-equipment/','mods/blacksmith/'].includes(path))await page.screenshot({path:`/tmp/ftk-site-${path.replaceAll('/','-')||'home'}-${size.width}.png`,fullPage:true});
 }
}
await page.goto(root+'mods/blacksmith/');
assert.equal(await page.locator('.forge-card').count(),32);
assert.match(await page.locator('.preview-status').textContent(),/Coming Soon/);
assert.equal(await page.locator('.forge-card .affinity').count(),43);
await page.getByLabel('Tier',{exact:true}).selectOption('kilnward');
assert.equal(await page.locator('.forge-card:visible').count(),8);
await page.getByLabel('Slot',{exact:true}).selectOption('armor');
assert.equal(await page.locator('.forge-card:visible').count(),1);
await page.getByRole('searchbox',{name:'Search equipment'}).fill('no-such-item');
assert.equal(await page.locator('.forge-card:visible').count(),0);
assert(await page.locator('.preview-empty').isVisible());
await page.getByLabel('Tier',{exact:true}).selectOption('all');
await page.getByLabel('Slot',{exact:true}).selectOption('all');
await page.getByRole('searchbox',{name:'Search equipment'}).fill('Temper');
assert.equal(await page.locator('.forge-card:visible').count(),3);
// Exercise every published HTML card and its visible explanations at both widths.
for(const size of [{width:1440,height:1000},{width:390,height:844}]){
 await page.setViewportSize(size);
 for(const [mod,count] of [['paladin',51],['thief',45]]){
  await page.goto(root+'mods/'+mod+'/');
  const items=page.locator('.forge-card');assert.equal(await items.count(),count);
  for(const item of await items.all()){
   const name=await item.locator('h2').textContent();
   await item.scrollIntoViewIfNeeded();
   await item.locator('.forge-art img').evaluate(i=>i.decode());
   assert.equal(await item.evaluate(d=>d.scrollWidth<=d.clientWidth),true,'Card overflow '+name);
   assert.equal(await item.locator('details, summary').count(),0);
   if(name==='Tin Oath Token'){assert.doesNotMatch(await item.innerText(),/Smite|Censure|set bonus/i);assert.match(await item.innerText(),/Vitality/);}
   if(name==='Mercy Helm'){assert.match(await item.innerText(),/Guard heals 6%/);assert.match(await item.innerText(),/one focused class Smite heal/);assert.match(await item.innerText(),/15% instead of 12%/);}
   if(name==='Censure Helm'){assert.match(await item.innerText(),/100% physical damage/);assert.match(await item.innerText(),/Guard-reduced hit readies \+50%/);assert.doesNotMatch(await item.innerText(),/125% physical damage/);}
   const slot=await item.getAttribute('data-slot');
   if(mod==='paladin'&&slot.startsWith('hammer_')){
    const rows=await item.locator('.forge-properties > p').allTextContents();
    assert.match(rows[0],/^\d+ Physical Damage$/);
    assert.equal(rows[1],'Strike');
    assert.deepEqual(rows.slice(-2),['Paladin Skill: Censure','Paladin Skill: Smite']);
    assert.doesNotMatch(await item.innerText(),/eligible hammer|Other classes retain|damage per level|% weapon damage|Completion:/);
    assert.equal(await item.locator('.forge-rolls').getAttribute('aria-label'),`${slot==='hammer_1h'?4:5} Vitality checks`);
    if(name.startsWith('Mercy ')){
     assert.match(await item.innerText(),/Mercy \(Paladin\) 0\/3 armor/);
     assert.match(await item.innerText(),/Next: 2 armor\s+healing \+12%/);
     assert.match(await item.innerText(),/Guard bond: next focused Smite heals ally 15% max HP/);
     assert.doesNotMatch(await item.innerText(),/Guard heals 6%|15% instead of 12%/);
    }
    if(name==='The Last Vigil')assert.match(await item.innerText(),/Paladin only:\s+First reduced hit per Guard:\s+restore 1 Focus to guarded ally\./);
    if(name==='Kingsfall')assert.match(await item.innerText(),/Paladin only:\s+Guarded hit readies Reckoning:\s+\+50% next single-target hammer hit\./);
   }
   assert(await item.locator('.forge-properties').innerText());
  }
  await page.getByRole('searchbox',{name:'Search equipment'}).fill('no-such-item');assert.equal(await page.locator('.forge-card:visible').count(),0);
  await page.getByRole('searchbox',{name:'Search equipment'}).fill(mod==='paladin'?'Paladin Skill: Censure':'borrowed fortune');
  assert.equal(await page.locator('.forge-card:visible').count(),mod==='paladin'?14:1);
  for(const card of await page.locator('.forge-card:visible').all())assert(await card.evaluate(c=>c.getBoundingClientRect().width<=310.5),'Tooltip width exceeds 310px');
  await page.getByRole('searchbox',{name:'Search equipment'}).fill('');
  await page.getByLabel('Tier',{exact:true}).selectOption('artifacts');assert.equal(await page.locator('.forge-card:visible').count(),3);
  await page.getByLabel('Slot',{exact:true}).selectOption(mod==='paladin'?'shield':'bow');assert.equal(await page.locator('.forge-card:visible').count(),1);
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
// Pagefind renders result rows before their text and ranking settle; wait for the expected hit
// rather than reading the list as soon as the first row appears.
await page.locator('.pagefind-ui__result',{hasText:'Paladin'}).first().waitFor();
assert.match(await page.locator('.pagefind-ui__results').innerText(),/Paladin/);
await page.keyboard.press('Escape');
await page.setViewportSize({width:390,height:844});await page.goto(root+'mods/paladin/');
await page.getByRole('button',{name:'Menu',exact:true}).click();
await page.locator('#starlight__sidebar').waitFor({state:'visible'});
await page.locator('#starlight__sidebar').getByRole('link',{name:'Installation',exact:true}).click();
await page.waitForURL(root+'installation/');
assert.deepEqual(errors,[]);
await fs.writeFile('/tmp/ftk-site-external-links.json',JSON.stringify([...links].filter(l=>!l.startsWith(root)),null,2));
console.log(`PASS: ${paths.length} pages at desktop/mobile, local links, fragments, images, search, tier/slot filters, inline ability text, and mobile menu.`);
await browser.close();
