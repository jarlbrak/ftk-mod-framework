// Project the approved local preview into public-safe fields and compressed artwork.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import sharp from 'sharp';
const source=process.argv[2];
if(!source)throw new Error('Usage: node scripts/prepare-blacksmith-preview.mjs GALLERY_DIRECTORY');
const html=await fs.readFile(path.join(source,'index.html'),'utf8');
const data=JSON.parse(html.match(/const data=(.*);\nconst \$/)[1]);
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const items=[];
const provenance=[];
await fs.mkdir('public/media/blacksmith',{recursive:true});
for(const item of data.items){
 const input=await fs.readFile(path.join(source,item.render));
 const output=await sharp(input).resize(512,512,{fit:'inside',withoutEnlargement:true}).webp({quality:88}).toBuffer();
 await fs.writeFile(`public/media/blacksmith/${item.id}.webp`,output);
 items.push({id:item.id,name:item.name,tier:item.tier,kind:item.kind,rarity:item.tooltip.rarity,damage:item.tooltip.damage,damageType:item.tooltip.damageType,lines:item.tooltip.text.split('\n').map(line=>({text:line.replace(/<[^>]*>/g,'').trim(),tone:line.includes('#FFDF9A')?'ability':line.includes('#C4B6A6')?'skill':line.replace(/<[^>]*>/g,'').startsWith('Blacksmith')?'affinity':'normal'})).filter(line=>line.text)});
 provenance.push({id:item.id,sourceSha256:hash(input),webpSha256:hash(output)});
}
const bannerInput=await fs.readFile(path.join(source,data.banner));
const bannerOutput=await sharp(bannerInput).resize(1280,720,{fit:'inside',withoutEnlargement:true}).webp({quality:88}).toBuffer();
await fs.writeFile('public/media/blacksmith/banner.webp',bannerOutput);
provenance.push({id:'blacksmith-banner',sourceSha256:hash(bannerInput),webpSha256:hash(bannerOutput)});
if(items.length!==32)throw new Error('Expected 32 approved items');
await fs.writeFile('src/data/blacksmith-preview.json',JSON.stringify({status:'Coming Soon',artwork:'Studio renders of original Blacksmith equipment. Preview stats may change.',items},null,2)+'\n');
await fs.writeFile('src/data/blacksmith-art-provenance.json',JSON.stringify({scope:'Original studio artwork, not native game captures. Source renders retained in the Blacksmith art campaign.',items:provenance},null,2)+'\n');
