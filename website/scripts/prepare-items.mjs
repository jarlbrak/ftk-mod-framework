// Publish only original icons or studio renders from hash-verified packages.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import sharp from 'sharp';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const args=process.argv.slice(2);
const options=new Map();
for(let i=0;i<args.length;i+=2){
 const key=args[i],value=args[i+1];
 if(!['--package','--archive','--archive-sha256','--version'].includes(key)||
    !value||value.startsWith('--')||options.has(key))
  throw Error('Use --package NAME [--archive ZIP --archive-sha256 SHA256 --version VERSION]');
 options.set(key,value);
}
const selected=options.get('--package')||'Paladin';
const localArchive=options.get('--archive');
const localHash=options.get('--archive-sha256');
const localVersion=options.get('--version');
if(Boolean(localArchive)!==Boolean(localHash)||Boolean(localArchive)!==Boolean(localVersion))
 throw Error('Local archive requires --archive, --archive-sha256 and --version together');
const catalog=JSON.parse(await fs.readFile('src/data/catalog.json'));
const matching=catalog.packages.filter(p=>p.name===selected);
if(matching.length!==1)throw Error(`Expected one catalog package named ${selected}; found ${matching.length}`);
const oldRecords=JSON.parse(await fs.readFile('src/data/item-art-provenance.json'));
const temp=await fs.mkdtemp(path.join(os.tmpdir(),'ftk-item-art-'));
const records=[];
const images=[];
const digest=/^[0-9a-f]{64}$/;
for(const p of matching){
 const packageSha256=localArchive?localHash:p.sha256;
 const version=localArchive?localVersion:p.version;
 const bytes=localArchive?await fs.readFile(localArchive):Buffer.from(await (async()=>{const response=await fetch(p.packageUrl);if(!response.ok)throw Error(response.status);return response.arrayBuffer();})());
 if(!digest.test(packageSha256)||hash(bytes)!==packageSha256)throw Error(`${p.name} package hash mismatch`);
 const archive=path.join(temp,p.name+'.zip');await fs.writeFile(archive,bytes);
 const read=n=>execFileSync('unzip',['-p',archive,n],{maxBuffer:32*1024*1024});
 const manifest=JSON.parse(read('manifest.json'));
 if(manifest.name!==p.name||manifest.version!==version)throw Error(`${p.name} manifest/version mismatch`);
 const entries=JSON.parse(read('content.json')).entries;
 for(const e of entries.filter(e=>['weapon','item'].includes(e.kind))){
  const model=e.itemModels?.find(m=>m.path==='.')||e.itemModels?.[0];
  let source,extra={};
  if(e.icon) source=read(e.icon);
  else{
   if(!model?.model||!model.texture||!model.metallicGlossTexture)throw Error(`${e.id}: complete equipped root itemModels required`);
   const receiptPath=`artwork/items/${e.id}.receipt.json`;
   const receipt=JSON.parse(await fs.readFile(receiptPath));
   const still=`artwork/items/${e.id}.png`;
   source=await fs.readFile(still);
   const inputs=[['model','modelSha256'],['texture','textureSha256'],['metallicGlossTexture','metallicGlossTextureSha256']];
   if(receipt.id!==e.id||receipt.output!==still||!digest.test(receipt.outputSha256)||receipt.outputSha256!==hash(source)||
      typeof receipt.renderer!=='string'||!receipt.renderer.trim())throw Error(`${e.id}: studio receipt/output mismatch`);
   for(const [field,sha] of inputs)
    if(receipt[field]!==model[field]||!digest.test(receipt[sha])||receipt[sha]!==hash(read(model[field])))
     throw Error(`${e.id}: studio receipt ${field} mismatch`);
   extra={renderer:receipt.renderer,receipt:receiptPath,receiptSha256:hash(await fs.readFile(receiptPath)),
    model:model.model,modelSha256:receipt.modelSha256,texture:model.texture,textureSha256:receipt.textureSha256,
    metallicGlossTexture:model.metallicGlossTexture,metallicGlossTextureSha256:receipt.metallicGlossTextureSha256};
  }
  const output=`public/media/items/${e.id}.webp`;
  const result=await sharp(source).resize(384,384,{fit:'inside',withoutEnlargement:true}).webp({quality:85}).toBuffer();
  images.push([output,result]);
  records.push({id:e.id,package:p.name,version,packageSha256,kind:e.icon?'original package icon':'studio render of original package model',source:e.icon||`artwork/items/${e.id}.png`,sourceSha256:hash(source),...extra,output,outputSha256:hash(result)});
 }
}
// Replace only the selected package's records; unrelated provenance and media stay intact.
const first=oldRecords.findIndex(r=>r.package===selected);
const insertAt=first<0?oldRecords.length:first;
const merged=[...oldRecords.slice(0,insertAt),...records,...oldRecords.slice(insertAt).filter(r=>r.package!==selected)];
await fs.mkdir('public/media/items',{recursive:true});
for(const [output,result] of images)await fs.writeFile(output,result);
await fs.writeFile('src/data/item-art-provenance.json',JSON.stringify(merged,null,2)+'\n');
console.log(`Prepared ${records.length} original item images`);
