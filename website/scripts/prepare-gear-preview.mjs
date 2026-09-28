// Project unpublished package data and hash-verified studio artwork into preview pages.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import sharp from 'sharp';
const source=process.argv[2];
if(!source)throw Error('Usage: node scripts/prepare-gear-preview.mjs STUDIO_DIRECTORY');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const manifest=JSON.parse(await fs.readFile(path.join(source,'manifest.json'),'utf8'));
const provenance=[];
const fields=['kind','id','displayName','fields','modifiers','guardianBonuses','proficiencies','description','precisionWeapon','thiefArtifact'];
await fs.mkdir('public/media/gear-preview',{recursive:true});
for(const [mod,count] of [['paladin',51],['thief',45]]){
 const packageBytes=await fs.readFile(`../marketplace/packages/${mod}/content.json`);
 const entries=JSON.parse(packageBytes).entries;
 const gear=entries.filter(e=>['weapon','item'].includes(e.kind));
 if(gear.length!==count)throw Error(`${mod}: unexpected item count`);
 for(const entry of gear){
  const matches=manifest.items.filter(item=>item.id===entry.id);
  if(matches.length!==1)throw Error(`Expected exactly one render: ${entry.id}`);
  const item=matches[0];
  if(item.content.sha256!==hash(packageBytes))throw Error(`Stale package selection: ${entry.id}`);
  const canonicalSources=[];
  for(const asset of item.sources){
   if(path.isAbsolute(asset.path)||asset.path.split('/').includes('..'))throw Error('Source path must be repository relative');
   if(hash(await fs.readFile(path.join('..',asset.path)))!==asset.sha256)throw Error(`Stale source: ${asset.path}`);
   // Frozen game trials can supply artwork only after exact canonical adoption.
   const stagedPrefix=`${manifest.inputRoot}/${mod}/assets/`;
   const canonicalPrefix=`marketplace/packages/${mod}/assets/`;
   let canonicalPath=manifest.inputRoot&&asset.path.startsWith(stagedPrefix)
    ?canonicalPrefix+asset.path.slice(stagedPrefix.length):asset.path;
   // Retained portraits can cite an older trial; require exact adopted bytes below.
   const retainedTrial=asset.path.match(/^scratch\/blacksmith-gear-game\/gear-fit-[a-z0-9-]+\/(paladin|thief)\/assets\/([^/]+)$/);
   if(retainedTrial&&retainedTrial[1]===mod)canonicalPath=canonicalPrefix+retainedTrial[2];
   const routeMetadata=mod==='thief'&&canonicalPath==='art-experiments/thief/native-route.json';
   if(!canonicalPath.startsWith(canonicalPrefix)&&!routeMetadata)throw Error(`Unexpected package source: ${asset.path}`);
   if(hash(await fs.readFile(path.join('..',canonicalPath)))!==asset.sha256)throw Error(`Unadopted source: ${canonicalPath}`);
   canonicalSources.push({path:canonicalPath,sha256:asset.sha256});
  }
  const input=await fs.readFile(path.join(source,item.image));
  if(hash(input)!==item.sha256)throw Error(`Stale render: ${entry.id}`);
  const output=await sharp(input).resize(512,512,{fit:'inside',withoutEnlargement:true}).webp({quality:88}).toBuffer();
  await fs.writeFile(`public/media/gear-preview/${entry.id}.webp`,output);
  provenance.push({id:entry.id,sources:canonicalSources,sourceSha256:item.sha256,webpSha256:hash(output)});
 }
 const projected=entries.map(e=>Object.fromEntries(Object.entries(e).filter(([key])=>fields.includes(key))));
 await fs.writeFile(`src/data/${mod}-preview.json`,JSON.stringify({status:'Coming Soon',sourceSha256:hash(packageBytes),entries:projected},null,2)+'\n');
}
await fs.writeFile('src/data/gear-preview-provenance.json',JSON.stringify({scope:'Studio renders of exact candidate geometry and textures. Offline art review only; native fit and motion remain separate gates.',items:provenance},null,2)+'\n');
console.log(`Prepared ${provenance.length} verified candidate portraits`);
