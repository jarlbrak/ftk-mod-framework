// Run deliberately when updating published media; never part of a normal build.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
const catalog=JSON.parse(await fs.readFile('src/data/catalog.json','utf8'));
const evidence=JSON.parse(await fs.readFile('../docs/evidence/paladin-1.3.0/verification.json','utf8'));
const libraryArt=JSON.parse(await fs.readFile('src/data/library-art.json','utf8'));
const records=[];
async function convert(bytes,name,source,kind,expected){
 const hash=crypto.createHash('sha256').update(bytes).digest('hex');
 if(expected && hash!==expected) throw Error(`Hash mismatch: ${source}`);
 const output=`public/media/${name}.webp`;
 await sharp(bytes).resize({width:1280,withoutEnlargement:true}).webp({quality:82}).toFile(output);
 records.push({file:`media/${name}.webp`,source,sourceSha256:hash,kind,version:kind==='native game capture'?'Paladin 1.3.0 / framework 1.2.0':undefined,transform:'Resize to at most 1280px wide; WebP quality 82; no crop',sha256:crypto.createHash('sha256').update(await fs.readFile(output)).digest('hex')});
}
for(const p of catalog.packages){const art=libraryArt[p.packageId];if(!art)throw Error(`Missing library banner: ${p.packageId}`);let bytes;if(art.localSource){bytes=await fs.readFile(art.localSource);}else{const r=await fetch(art.url);if(!r.ok)throw Error(r.status);bytes=Buffer.from(await r.arrayBuffer());}await convert(bytes,p.packageId.replace(/^ftkmf\./,''),art.localSource??art.url,art.kind,art.sha256);}
for(const [name,hash] of Object.entries(evidence.files)) await convert(await fs.readFile(`../docs/evidence/paladin-1.3.0/${name}`),name.replace('.png',''),`docs/evidence/paladin-1.3.0/${name}`,'native game capture',hash);
await fs.copyFile('../marketplace/packages/paladin/ASSET-LICENSE.md','public/media/ASSET-LICENSE.md');
await fs.writeFile('src/data/media-provenance.json',JSON.stringify({date:'2026-09-25',captureScope:'Native macOS game captures, expanded review inventory; not natural acquisition evidence. See original evidence README.',records},null,2)+'\n');
