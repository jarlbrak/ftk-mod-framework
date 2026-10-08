// Prepare a local review artifact. Published catalog, snapshots and media stay separate.
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import sharp from 'sharp';
import {projectModContent} from './project-mod-content.mjs';
import {thiefWeaponTooltip} from '../src/data/thief-weapon-tooltip.ts';
import {itemTooltipLines,statName,statValue} from '../src/data/item-guide.ts';
import {officialStatIcon} from '../src/data/stat-icons.ts';

const website=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const root=path.dirname(website);
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const digest=/^[0-9a-f]{64}$/;
const escape=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const families=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer','artifacts'];
const slots={twins:'Paired daggers',bow:'Pistol',coat:'Armor',hood:'Bandana',boots:'Boots',charm:'Charm'};

export function validateZipInventory(records){
 const names=new Set();let expanded=0;
 for(const record of records){
  const name=record.name;
  if(typeof name!=='string'||!name||! /^[A-Za-z0-9_./-]+$/.test(name)||
     name.startsWith('/')||name.split('/').some(part=>!part||part==='.'||part==='..')||
     names.has(name)||record.encrypted||![0,0o100000].includes(record.type)||
     !Number.isSafeInteger(record.size)||record.size<0||record.size>16*1024*1024)
   throw Error('Unsafe or duplicate archive entry');
  names.add(name);expanded+=record.size;
 }
 if(expanded>256*1024*1024)throw Error('Archive expanded size exceeds budget');
 return names;
}

async function openArchive(filename,expected){
 if(!digest.test(expected??''))throw Error('An explicit SHA-256 is required');
 if(!(await fs.lstat(filename)).isFile())throw Error('Archive must be a regular file');
 if(hash(await fs.readFile(filename))!==expected)throw Error('Archive hash mismatch');
 // Inspect metadata before reading any member; never extract or execute archive contents.
 const source='import json,stat,sys,zipfile\nwith zipfile.ZipFile(sys.argv[1]) as z:\n print(json.dumps([dict(name=i.filename,size=i.file_size,type=stat.S_IFMT(i.external_attr>>16),encrypted=bool(i.flag_bits&1)) for i in z.infolist()]))';
 const names=validateZipInventory(JSON.parse(execFileSync('python3',['-c',source,filename],{maxBuffer:1024*1024})));
 const read=name=>{
  if(!names.has(name))throw Error('Missing archive member');
  return execFileSync('unzip',['-p',filename,name],{maxBuffer:16*1024*1024});
 };
 return {names,read,json:name=>JSON.parse(read(name))};
}

const reviewedMedia={
 candidate:'2418e48b482577f0a9f04092c1f32a584dfb0ba0f5cf43f731beacbadc7dd2bf',
 original:'ea4c52b1db32b7d24c45ddc00b76ceef046d18a5d546f5103f64a883cb15c420',
 successor:'8ddf27c2f31cfe450c2367d5fdfd848eb08bdcc2b3fd24350505c53293b916b7',
 renderedGameplay:'288f8432fe081d9e9f8b04c1149e9e5cff22607d69e7462dcd89e8169a5b2b12',
 sourceFreeze:'9dcc56f262a6b46e2424977183915e785bc20d03536db3a29a56c4944b5fae97',
 originalManifest:'866bab904794f764799614b8fdd8eb68b02e96b10f4dae66ec22f2ae7db16d4d'
};

export function validateMarketingIdentity(media,identity){
 if(identity?.gameplayArchiveSha256!==reviewedMedia.candidate)throw Error('Unreviewed candidate pairing');
 const successor=media.status==='UNRELEASED_MARKETING_METADATA_SUCCESSOR_ORIGINAL_RENDER_BYTES';
 const original=media.status==='UNRELEASED_MARKETING_MEDIA';
 if((!successor&&!original)||identity.marketingArchiveSha256!==reviewedMedia[successor?'successor':'original'])
  throw Error('Unreviewed marketing archive/status');
 const history=successor?media.historicalRenderProvenance:media;
 if(!history||history.gameplayArchiveSha256!==reviewedMedia.renderedGameplay||
    history.frameworkVersion!=='1.6.3'||history.sourceFreezeSha256!==reviewedMedia.sourceFreeze||
    media.sourceFreezeSha256!==reviewedMedia.sourceFreeze)
  throw Error('Historical render identity mismatch');
 if(successor&&(media.gameplayArchiveSha256!==identity.gameplayArchiveSha256||
    media.frameworkVersion!=='1.9.0'||media.frameworkRange!=='>=1.9.0 <2.0.0'||
    !Array.isArray(media.platforms)||media.platforms.length!==1||media.platforms[0]!=='macos'||
    history.originalMarketingArchiveSha256!==reviewedMedia.original||
    history.originalManifestSha256!==reviewedMedia.originalManifest))
  throw Error('Successor pairing or historical archive mismatch');
 return {gameplayArchiveSha256:history.gameplayArchiveSha256,frameworkVersion:history.frameworkVersion,
  version:media.version,sourceFreezeSha256:history.sourceFreezeSha256,
  originalMarketingArchiveSha256:reviewedMedia.original,originalManifestSha256:reviewedMedia.originalManifest};
}

export function validateMarketing(content,media,readGame,readMedia,identity){
 const historicalMarketingIdentity=validateMarketingIdentity(media,identity);
 const weapons=content.entries.filter(e=>e.kind==='weapon');
 if(media.schemaVersion!==1||
    media.nativeCombatGlyphsChanged!==false||!digest.test(media.gameplayArchiveSha256)||
    !digest.test(media.sourceFreezeSha256)||media.version!=='1.1.0'||
    !Array.isArray(media.weapons)||media.weapons.length!==17||weapons.length!==17)
  throw Error('Unexpected marketing manifest');
 const seen=new Set();const images=[];
 for(const record of media.weapons){
  const item=weapons.find(e=>e.id===record.id);
  if(!item||seen.has(record.id)||record.image!==`items/${record.id}.png`||
     !digest.test(record.sha256)||record.label!=='Faithful studio render; no native gameplay claim'||
     !Array.isArray(record.originalSources)||!Array.isArray(item.displayModels)||
     record.originalSources.length!==item.displayModels.length)
   throw Error('Missing, duplicate or unrelated marketing weapon');
  seen.add(record.id);
  const sources=[];
  for(let index=0;index<record.originalSources.length;index++){
   const source=record.originalSources[index],declared=item.displayModels[index];
   const portable={};
   for(const key of ['model','texture']){
    const pin=source[key];
    const prefix='marketplace/packages/thief/';
    if(!pin||typeof pin.path!=='string'||!pin.path.startsWith(prefix)||
       pin.path.slice(prefix.length)!==declared[key]||!digest.test(pin.sha256))
     throw Error('Marketing source does not match declared display model');
    const bytes=readGame(declared[key]);
    if(bytes.length!==pin.bytes||hash(bytes)!==pin.sha256)throw Error('Marketing source bytes mismatch');
    portable[key]={path:declared[key],sha256:pin.sha256,bytes:pin.bytes};
   }
   sources.push(portable);
  }
  const bytes=readMedia(record.image);
  if(hash(bytes)!==record.sha256)throw Error('Marketing image hash mismatch');
  images.push({id:item.id,bytes,sourceSha256:record.sha256,kind:'Faithful studio render',sources});
 }
 const banner=readMedia('thief-studio-banner.png');
 if(!digest.test(media.bannerSha256)||hash(banner)!==media.bannerSha256)throw Error('Marketing banner hash mismatch');
 return {images,banner,historicalMarketingIdentity};
}

export async function validateOutput(output){
 const resolved=path.resolve(output),scratch=path.join(root,'scratch');
 if(!resolved.startsWith(scratch+path.sep))throw Error('Output must be inside this checkout scratch directory');
 let ancestor=resolved;
 for(;;){
  try{await fs.lstat(ancestor);break;}catch(error){if(error.code!=='ENOENT')throw error;ancestor=path.dirname(ancestor);}
 }
 if((await fs.realpath(ancestor))!==ancestor)throw Error('Output ancestor must not be a symlink');
 try{await fs.lstat(resolved);throw Error('Output already exists');}catch(error){if(error.code!=='ENOENT')throw error;}
 execFileSync('git',['check-ignore','--quiet',resolved],{cwd:root});
 return resolved;
}

function renderPreview(entries,version,framework,css){
 const gear=entries.filter(e=>['weapon','item'].includes(e.kind));
 const cards=gear.map(e=>{
  const kind=Object.keys(slots).find(slot=>e.id.startsWith('thief_'+slot+'_'));
  const tier=e.fields.rarity==='artifact'?'artifacts':families.find(f=>e.id.endsWith('_'+f));
  if(!kind||!tier)throw Error('Unrecognized equipment family or slot');
  const tooltip=e.kind==='weapon'?thiefWeaponTooltip(e,entries):{
   lines:[...Object.entries(e.modifiers??{}).map(([key,value])=>({text:`${statValue(key,value)} ${statName(key)}`,tone:'normal'})),...itemTooltipLines(e,entries)],contextLines:[]};
  const text=[...tooltip.lines,...tooltip.contextLines].map(line=>line.text).join(' ');
  const checks=e.kind==='weapon'?`<div class="forge-rolls" role="img" aria-label="${e.fields.slots} ${escape(statName(e.fields.skill))} checks">${Array.from({length:e.fields.slots},()=>`<img class="forge-roll-icon" src="${officialStatIcon(e.fields.skill)}" alt="" aria-hidden="true" width="31" height="31">`).join('')}</div>`:'';
  const acquisition=['locksmith','nightblade','wayfarer'].includes(tier)?'Back Alley exchange only; one Guild Token.':tier==='artifacts'?'Native drops, Night Market and dungeon merchants; outside the exchange.':'Native drops and eligible town, Night Market and dungeon merchant stock.';
  return `<article class="forge-card ${escape(e.fields.rarity)} ${e.kind==='weapon'?'native-weapon':''}" id="${escape(e.id)}" data-tier="${tier}" data-slot="${kind}" data-search="${escape((e.displayName+' '+text+' '+acquisition).toLowerCase())}" aria-label="${escape(e.displayName)}"><div class="forge-art"><img src="media/items/${e.id}.webp" alt="${e.kind==='weapon'?'Studio render':'Original package icon'} of ${escape(e.displayName)}" width="384" height="384" loading="lazy">${checks}</div><header class="forge-identity"><h2>${escape(e.displayName)}</h2><p class="forge-rarity">${escape(e.fields.rarity.toUpperCase())}</p></header><div class="forge-properties">${e.kind==='weapon'?`<p class="forge-damage"><strong>${e.fields.damage}</strong> <span>Physical Damage</span></p>`:''}${tooltip.lines.map(line=>`<p class="${escape(line.tone)}">${escape(line.text)}</p>`).join('')}<div class="forge-guardian">${tooltip.contextLines.map(line=>`<p>${line.bold?'<strong>':''}${escape(line.text)}${line.bold?'</strong>':''}</p>`).join('')}</div><p class="acquisition">${escape(acquisition)}</p></div></article>`;
 }).join('');
 return `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Thief ${escape(version)} unreleased candidate</title><style>${css}\nbody{margin:0;background:#141019;color:#eee;font:16px/1.6 Arial,sans-serif}main{max-width:1200px;margin:auto;padding:24px}h1{line-height:1.2}.notice{border:2px solid #b69656;padding:16px}.banner{max-width:100%;height:auto}.forge-identity{padding-left:8px}.acquisition{margin-top:12px!important;border-top:1px solid var(--rim);padding-top:6px}.preview-filters select,.preview-filters input{background:#211c28;color:#eee}.blacksmith-gallery{justify-content:center}.preview-filters input{max-width:100%;min-width:0}table{border-collapse:collapse}td,th{padding:8px;border:1px solid #777;vertical-align:top}@media(max-width:600px){main{padding:12px}table,tbody,tr,td,th{display:block}thead{display:none}}</style><main><h1>Thief ${escape(version)} candidate</h1><p class="notice"><strong>Unreleased local review.</strong> Requires development framework ${escape(framework)} and its matching helper. The V169 native combat crash is unresolved and blocks release. Published Thief 1.0.0 remains available separately; this preview has no download or install action. Stats are provisional. Studio artwork does not prove native gameplay, campaign balance, co-op, other platforms or every animation.</p><figure><img class="banner" src="media/thief-studio-banner.webp" alt="Studio presentation of the original Nightblade equipment" width="1280" height="720"><figcaption>Original equipment in studio lighting; no native gameplay claim.</figcaption></figure><h2>Retained identities and pistol migration</h2><p>The eight former bow identities now describe two-handed pistols with four Talent checks and native ammunition. Native old-save bow-to-pistol migration remains unverified. Thief owners receive one bounded round at their turn start; other classes reload manually. Daggers keep two or three Speed checks. Fire is a basic precision shot; Bait Shot and Feint prepare a Thief after positive damage; Deadeye and Pierce bypass Armor on a perfect result and cannot Sneak Attack.</p><h2>Choose an armor role</h2><p>Only matching Head, Body and Foot pieces count. Two pieces grant the preview; three grant the core benefit and cost. Charms and artifacts never count. A matching weapon is optional and grants no additional power. Mixed outfits cannot activate two roles.</p><table><thead><tr><th>Family</th><th>Two pieces</th><th>Three-piece core and cost</th></tr></thead><tbody><tr><th>Locksmith</th><td>Prepared Sneak +5 points; other Sneak -5 points.</td><td>Perfect positive Prepared Sneak returns at most one spent Focus once per combat; all Sneak bonuses -10 points.</td></tr><tr><th>Nightblade</th><td>Full-health or unacted opener +5 points; other Sneak -5 points.</td><td>Qualifying opener +10 points; other Sneak -10 points.</td></tr><tr><th>Wayfarer</th><td>Positive eligible direct hit earns +2 Evasion until next turn; Sneak -5 points.</td><td>Earns +4 Evasion until next turn; Sneak -10 points. Unlost Road uses the greater value, not a sum.</td></tr></tbody></table><h2>Shared acquisition</h2><p>The 18 ordinary Locksmith, Nightblade and Wayfarer pieces cost one universal Guild Token each at the native Back Alley exchange. Three armor pieces cost three tokens. Artifacts remain outside the exchange. The token is shared with Paladin; Guild Insignia remains a wearable charm, not currency. Eligible displayed-level-8+ enemies have a provisional 10% opportunity, reviewed boss groups 50%, and the sixth consecutive eligible opportunity guarantees a token. Statistical supply and guaranteed pity remain unproved in native play. Online purchases are disabled. Failed delivery rolls back; completed purchases have no refund.</p><h2>All 45 pieces</h2><p>Weapon cards use the shared Paladin-style layout and unchanged official wiki check icons. Damage is the authored base before level growth and native modifiers.</p><div class="preview-filters"><label>Search equipment<input type="search"></label><label>Tier<select name="tier" aria-label="Tier"><option value="all">All tiers</option>${families.map(f=>`<option value="${f}">${escape(f)}</option>`).join('')}</select></label><label>Slot<select name="slot" aria-label="Slot"><option value="all">Every slot</option>${Object.entries(slots).map(([key,value])=>`<option value="${key}">${value}</option>`).join('')}</select></label></div><p role="status" aria-live="polite">45 items</p><div class="blacksmith-gallery">${cards}</div><p>Game symbols: unchanged originals from the Official For The King Wiki. Mod item artwork is original. All images retain source and output hashes in the local preview receipt.</p></main><script>const input=document.querySelector('input'),tier=document.querySelector('[name=tier]'),slot=document.querySelector('[name=slot]');function update(){let count=0;document.querySelectorAll('.forge-card').forEach(card=>{card.hidden=!(card.dataset.search.includes(input.value.trim().toLowerCase())&&(tier.value==='all'||card.dataset.tier===tier.value)&&(slot.value==='all'||card.dataset.slot===slot.value));if(!card.hidden)count++});document.querySelector('[role=status]').textContent=count+' of 45 items'}input.addEventListener('input',update);tier.addEventListener('change',update);slot.addEventListener('change',update)</script></html>`;
}

export async function prepareThiefCandidate(options){
 const output=await validateOutput(options.output);
 const game=await openArchive(path.resolve(options.archive),options.archiveSha256);
 const marketing=await openArchive(path.resolve(options.marketing),options.marketingSha256);
 const manifest=game.json('manifest.json'),content=game.json('content.json'),media=marketing.json('manifest.json');
 if(manifest.modGuid!=='com.ftkmf.thief'||manifest.name!=='Thief'||manifest.version!=='1.1.0'||manifest.frameworkVersion!=='1.9.0')throw Error('Expected development Thief 1.1.0 / framework 1.9.0');
 const ids=new Set();
 for(const entry of content.entries){
  if(typeof entry.id!=='string'||!/^thief(?:_[a-z0-9]+)*$/.test(entry.id)||ids.has(entry.id))throw Error('Unsafe or duplicate content identity');
  ids.add(entry.id);
 }
 if([...game.names].some(name=>!['manifest.json','content.json'].includes(name)&&!/^assets\/[A-Za-z0-9_.-]+\.(png|glb)$/.test(name)))throw Error('Unexpected gameplay archive member');
 const entries=projectModContent(content),gear=entries.filter(e=>['weapon','item'].includes(e.kind));
 if(gear.length!==45||entries.filter(e=>e.precisionAction).length!==9||entries.filter(e=>e.thiefArmor).length!==9||entries.filter(e=>e.thiefArmament).length!==6)throw Error('Candidate declarations incomplete');
 for(const weapon of gear.filter(e=>e.kind==='weapon')){
  if(!Number.isSafeInteger(weapon.fields.damage)||weapon.fields.damage<=0||
     !Number.isSafeInteger(weapon.fields.slots)||weapon.fields.slots<1||weapon.fields.slots>4)
   throw Error('Invalid candidate damage/check count');
 }
 const accepted=validateMarketing(content,media,game.read,marketing.read,{gameplayArchiveSha256:options.archiveSha256,marketingArchiveSha256:options.marketingSha256});
 const expectedMarketing=new Set(['manifest.json','thief-studio-banner.png',...media.weapons.map(e=>e.image)]);
 if(marketing.names.size!==expectedMarketing.size||[...marketing.names].some(name=>!expectedMarketing.has(name)))throw Error('Unexpected marketing archive member');
 const images=[],writes=[];
 for(const item of gear){
  const studio=accepted.images.find(image=>image.id===item.id);
  const bytes=studio?.bytes??game.read(item.icon);
  const target=`media/items/${item.id}.webp`;
  const derivative=await sharp(bytes).resize(384,384,{fit:'inside',withoutEnlargement:true}).webp({quality:85}).toBuffer();
  writes.push([target,derivative]);
  images.push({id:item.id,kind:studio?.kind??'Original package icon',sourceSha256:hash(bytes),sources:studio?.sources??[],output:target,outputSha256:hash(derivative)});
 }
 const banner=await sharp(accepted.banner).resize({width:1280,withoutEnlargement:true}).webp({quality:85}).toBuffer();
 writes.push(['media/thief-studio-banner.webp',banner]);
 const icons=JSON.parse(await fs.readFile(path.join(website,'src/data/wiki-stat-icons.json')));
 for(const record of icons.records){
  const bytes=await fs.readFile(path.join(website,'public',record.file));
  if(hash(bytes)!==record.sourceSha256||record.sha256!==record.sourceSha256)throw Error('Official stat icon hash mismatch');
  writes.push([record.file,bytes]);
 }
 const component=await fs.readFile(path.join(website,'src/components/TooltipGallery.astro'),'utf8');
 const css=[...component.matchAll(/<style>([\s\S]*?)<\/style>/g)].map(match=>match[1]).join('\n');
 const html=renderPreview(entries,manifest.version,manifest.frameworkVersion,css);
 const receipt={status:'UNRELEASED_LOCAL_PREVIEW',gameplayArchiveSha256:options.archiveSha256,
  marketingArchiveSha256:options.marketingSha256,marketingManifestSha256:hash(marketing.read('manifest.json')),
  historicalMarketingIdentity:accepted.historicalMarketingIdentity,
  association:'All 17 original studio source arrays and bytes match candidate archive displayModels; historical sidecar identity preserved.',
  version:manifest.version,frameworkVersion:manifest.frameworkVersion,images,
  banner:{sourceSha256:media.bannerSha256,outputSha256:hash(banner)},
  statIcons:icons.records.map(({stat,file,sha256,sourcePage})=>({stat,file,sha256,sourcePage})),
  nativeEvidence:'No live validation; V169 unresolved. Old-save migration, remaining action displays and balance gates open.'};
 // Complete every admission check before creating the destination.
 await fs.mkdir(output,{recursive:true});
 for(const [name,bytes] of writes){await fs.mkdir(path.dirname(path.join(output,name)),{recursive:true});await fs.writeFile(path.join(output,name),bytes);}
 await fs.writeFile(path.join(output,'index.html'),html);
 await fs.writeFile(path.join(output,'thief-candidate.json'),JSON.stringify({version:manifest.version,sha256:options.archiveSha256,entries},null,2)+'\n');
 await fs.writeFile(path.join(output,'preview-receipt.json'),JSON.stringify(receipt,null,2)+'\n');
 return {output,items:gear.length,images:images.length};
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const allowed=new Map([['--archive','archive'],['--archive-sha256','archiveSha256'],['--marketing','marketing'],['--marketing-sha256','marketingSha256'],['--output','output']]);
 const options={};
 for(let index=2;index<process.argv.length;index+=2){
  const key=allowed.get(process.argv[index]),value=process.argv[index+1];
  if(!key||!value||value.startsWith('--')||key in options)throw Error('Use explicit archive, marketing, SHA-256 values and scratch output');
  options[key]=value;
 }
 if(Object.keys(options).length!==allowed.size)throw Error('All five candidate arguments are required');
 console.log(JSON.stringify(await prepareThiefCandidate(options)));
}
