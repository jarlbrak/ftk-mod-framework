import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {validateZipInventory,validateMarketing,validateMarketingIdentity,validateOutput} from './prepare-thief-candidate.mjs';

const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const record={name:'assets/original.png',size:8,type:0o100000,encrypted:false};
assert.equal(validateZipInventory([record]).size,1);
for(const name of ['../escape.png','/absolute.png','a/../escape.png','a//b.png','a\\b.png','a/*.png','directory/'])
 assert.throws(()=>validateZipInventory([{...record,name}]),/Unsafe/);
assert.throws(()=>validateZipInventory([record,record]),/duplicate/);
assert.throws(()=>validateZipInventory([{...record,type:0o120000}]),/Unsafe/);
assert.throws(()=>validateZipInventory([{...record,encrypted:true}]),/Unsafe/);
assert.throws(()=>validateZipInventory([{...record,size:17*1024*1024}]),/Unsafe/);

const identity={gameplayArchiveSha256:'2418e48b482577f0a9f04092c1f32a584dfb0ba0f5cf43f731beacbadc7dd2bf',marketingArchiveSha256:'ea4c52b1db32b7d24c45ddc00b76ceef046d18a5d546f5103f64a883cb15c420'};
const model=Buffer.from('original model'),texture=Buffer.from('original texture'),image=Buffer.from('original studio image');
const content={entries:Array.from({length:17},(_,index)=>({kind:'weapon',id:`thief_fixture_${index}`,displayModels:[{model:'assets/model.glb',texture:'assets/texture.png'}]}))};
const media={schemaVersion:1,status:'UNRELEASED_MARKETING_MEDIA',nativeCombatGlyphsChanged:false,gameplayArchiveSha256:'288f8432fe081d9e9f8b04c1149e9e5cff22607d69e7462dcd89e8169a5b2b12',sourceFreezeSha256:'9dcc56f262a6b46e2424977183915e785bc20d03536db3a29a56c4944b5fae97',version:'1.1.0',frameworkVersion:'1.6.3',bannerSha256:hash(image),weapons:content.entries.map(e=>({id:e.id,image:`items/${e.id}.png`,label:'Faithful studio render; no native gameplay claim',sha256:hash(image),originalSources:[{model:{path:'marketplace/packages/thief/assets/model.glb',sha256:hash(model),bytes:model.length},texture:{path:'marketplace/packages/thief/assets/texture.png',sha256:hash(texture),bytes:texture.length}}]}))};
const readGame=name=>name.endsWith('.glb')?model:texture,readMedia=()=>image;
assert.equal(validateMarketing(content,media,readGame,readMedia,identity).images.length,17);
const fails=mutate=>{const changed=structuredClone(media);mutate(changed);assert.throws(()=>validateMarketing(content,changed,readGame,readMedia,identity));};
fails(m=>m.weapons.pop());
fails(m=>m.weapons[1]=m.weapons[0]);
fails(m=>m.weapons[0].id='unknown');
fails(m=>m.weapons[0].image='../escape.png');
fails(m=>m.weapons[0].sha256='c'.repeat(64));
fails(m=>m.weapons[0].originalSources[0].model.sha256='c'.repeat(64));
fails(m=>m.weapons[0].originalSources[0].model.bytes++);
fails(m=>m.weapons[0].originalSources[0].model.path='marketplace/packages/thief/assets/other.glb');
fails(m=>m.weapons[0].originalSources.push(m.weapons[0].originalSources[0]));
fails(m=>m.nativeCombatGlyphsChanged=true);
fails(m=>m.bannerSha256='c'.repeat(64));
assert.throws(()=>validateMarketing(content,media,()=>Buffer.from('altered source'),readMedia,identity),/bytes mismatch/);
assert.throws(()=>validateMarketing(content,media,readGame,()=>{throw Error('missing image');},identity),/missing image/);
assert.equal(media.frameworkVersion,'1.6.3');
assert.equal(media.gameplayArchiveSha256,'288f8432fe081d9e9f8b04c1149e9e5cff22607d69e7462dcd89e8169a5b2b12');
const successor=structuredClone(media);
successor.status='UNRELEASED_MARKETING_METADATA_SUCCESSOR_ORIGINAL_RENDER_BYTES';
successor.historicalRenderProvenance={gameplayArchiveSha256:media.gameplayArchiveSha256,frameworkVersion:media.frameworkVersion,sourceFreezeSha256:media.sourceFreezeSha256,originalMarketingArchiveSha256:identity.marketingArchiveSha256,originalManifestSha256:'866bab904794f764799614b8fdd8eb68b02e96b10f4dae66ec22f2ae7db16d4d'};
successor.gameplayArchiveSha256=identity.gameplayArchiveSha256;
successor.frameworkVersion='1.9.0';successor.frameworkRange='>=1.9.0 <2.0.0';successor.platforms=['macos'];
const successorIdentity={...identity,marketingArchiveSha256:'8ddf27c2f31cfe450c2367d5fdfd848eb08bdcc2b3fd24350505c53293b916b7'};
const accepted=validateMarketing(content,successor,readGame,readMedia,successorIdentity);
assert.equal(accepted.images.length,17);
assert.deepEqual(accepted.historicalMarketingIdentity,validateMarketingIdentity(media,identity));
for(const mutate of [m=>m.status='RELEASED',m=>delete m.historicalRenderProvenance,
 m=>m.historicalRenderProvenance.gameplayArchiveSha256=identity.gameplayArchiveSha256,
 m=>m.historicalRenderProvenance.frameworkVersion='1.9.0',m=>m.historicalRenderProvenance.sourceFreezeSha256='c'.repeat(64),
 m=>m.historicalRenderProvenance.originalMarketingArchiveSha256='c'.repeat(64),m=>m.historicalRenderProvenance.originalManifestSha256='c'.repeat(64),
 m=>m.gameplayArchiveSha256='c'.repeat(64),m=>m.frameworkVersion='1.6.3',m=>m.frameworkRange='>=1.0.0',m=>m.platforms.push('windows'),
 m=>m.schemaVersion=2,m=>m.version='1.0.0',m=>m.weapons[0].sha256='c'.repeat(64),m=>m.weapons[0].originalSources[0].texture.sha256='c'.repeat(64)]){
 const changed=structuredClone(successor);mutate(changed);
 assert.throws(()=>validateMarketing(content,changed,readGame,readMedia,successorIdentity));
}
for(const changed of [undefined,{...identity,gameplayArchiveSha256:'c'.repeat(64)},identity,
 {...successorIdentity,marketingArchiveSha256:'c'.repeat(64)}])
 assert.throws(()=>validateMarketing(content,successor,readGame,readMedia,changed));
assert.throws(()=>validateMarketing(content,media,readGame,readMedia,successorIdentity));

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
await assert.rejects(validateOutput(path.join(root,'website/public/candidate')),/scratch/);
const testRoot=process.env.THIEF_CANDIDATE_TEST_ROOT??path.join(root,'scratch');
assert(path.resolve(testRoot).startsWith(path.join(root,'scratch')+path.sep)||path.resolve(testRoot)===path.join(root,'scratch'));
const temporary=await fs.mkdtemp(path.join(testRoot,'thief-candidate-test-'));
await assert.rejects(validateOutput(temporary),/exists/);
const destination=path.join(temporary,'new');
assert.equal(await validateOutput(destination),destination);
await fs.symlink(path.join(root,'website'),path.join(temporary,'linked'));
await assert.rejects(validateOutput(path.join(temporary,'linked/escaped')),/symlink/);
console.log('PASS candidate ZIP/media refusals, both reviewed sidecars, exact successor pairing/history, source joins and scratch-only output');

// An optional owned preview server exercises the generated page, not the published site.
if(process.env.THIEF_CANDIDATE_URL){
 const {chromium}=await import('@playwright/test');
 const browser=await chromium.launch({headless:true});
 try{
  const page=await browser.newPage();
  for(const width of [1440,390]){
   await page.setViewportSize({width,height:900});
   assert.equal((await page.goto(process.env.THIEF_CANDIDATE_URL)).status(),200);
   assert.match(await page.locator('.notice').innerText(),/Unreleased local review/);
   assert.match(await page.locator('.notice').innerText(),/V169.*unresolved/);
   assert.equal(await page.locator('a,button').count(),0);
   assert.equal(await page.locator('.forge-card').count(),45);
   assert.equal(await page.locator('option[value=bow]').innerText(),'Pistol');
   assert.equal(await page.locator('option[value=hood]').innerText(),'Bandana');
   await page.screenshot({path:path.join(temporary,`overview-${width}.png`)});
   await page.locator('.forge-card').first().screenshot({path:path.join(temporary,`card-${width}.png`)});
   for(const card of await page.locator('.forge-card').all()){
    await card.scrollIntoViewIfNeeded();
    await card.locator('.forge-art > img').evaluate(image=>image.decode());
    assert.equal(await card.evaluate(element=>element.scrollWidth<=element.clientWidth),true);
    assert(await card.locator('.forge-art > img').getAttribute('alt'));
   }
   for(const group of await page.locator('.forge-rolls').all()){
    const label=await group.getAttribute('aria-label'),match=label.match(/^(\d+) (Speed|Talent) checks$/);
    assert(match,label);assert.equal(await group.locator('img').count(),Number(match[1]));
    for(const icon of await group.locator('img').all()){
     assert((await icon.getAttribute('src')).endsWith(`/stat-icons/${match[2].toLowerCase()}.png`));
     await icon.evaluate(image=>image.decode());
    }
   }
   assert.equal(await page.locator('[data-slot=bow] .forge-rolls[aria-label="4 Talent checks"]').count(),8);
   assert.equal(await page.locator('[data-slot=twins] .forge-rolls').count(),9);
   assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
   await page.getByLabel('Slot',{exact:true}).selectOption('bow');
   assert.equal(await page.locator('.forge-card:visible').count(),8);
   await page.getByLabel('Tier',{exact:true}).selectOption('artifacts');
   assert.equal(await page.locator('.forge-card:visible').count(),1);
   await page.getByLabel('Tier',{exact:true}).selectOption('all');
   await page.getByLabel('Slot',{exact:true}).selectOption('hood');
   assert.equal(await page.locator('.forge-card:visible').count(),7);
   await page.getByLabel('Slot',{exact:true}).selectOption('all');
   await page.getByRole('searchbox',{name:'Search equipment'}).fill('Farstep');
   assert.equal(await page.locator('.forge-card:visible').count(),1);
   await page.getByRole('searchbox',{name:'Search equipment'}).fill('no-such-item');
   assert.equal(await page.locator('.forge-card:visible').count(),0);
   await page.getByRole('searchbox',{name:'Search equipment'}).fill('');
   await page.screenshot({path:path.join(temporary,`preview-${width}.png`),fullPage:true});
  }
 }finally{await browser.close();}
 console.log(`PASS all45 candidate cards, official checks, filters and desktop/mobile layout; screenshots ${temporary}`);
}
