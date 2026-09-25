// Publish only original icons or studio renders from hash-verified public packages.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import os from 'node:os';
import path from 'node:path';
import sharp from 'sharp';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const catalog=JSON.parse(await fs.readFile('src/data/catalog.json'));
const temp=await fs.mkdtemp(path.join(os.tmpdir(),'ftk-item-art-'));
const records=[];
await fs.mkdir('public/media/items',{recursive:true});
for(const p of catalog.packages.filter(p=>['Paladin','Thief'].includes(p.name))){
 const response=await fetch(p.packageUrl);if(!response.ok)throw Error(response.status);
 const bytes=Buffer.from(await response.arrayBuffer());if(hash(bytes)!==p.sha256)throw Error('Package hash mismatch');
 const archive=path.join(temp,p.name+'.zip');await fs.writeFile(archive,bytes);
 const read=n=>execFileSync('unzip',['-p',archive,n],{maxBuffer:32*1024*1024});
 const entries=JSON.parse(read('content.json')).entries;
 for(const e of entries.filter(e=>['weapon','item'].includes(e.kind))){
  const source=e.icon?read(e.icon):await fs.readFile(`artwork/items/${e.id}.png`);
  const output=`public/media/items/${e.id}.webp`;
  const result=await sharp(source).resize(384,384,{fit:'inside',withoutEnlargement:true}).webp({quality:85}).toBuffer();await fs.writeFile(output,result);
  records.push({id:e.id,package:p.name,version:p.version,packageSha256:p.sha256,kind:e.icon?'original package icon':'studio render of original published model',source:e.icon||`artwork/items/${e.id}.png`,sourceSha256:hash(source),...(e.icon?{}:{renderer:'scripts/render-turntable.py --still',model:e.itemModels[0].model,modelSha256:hash(read(e.itemModels[0].model)),texture:e.itemModels[0].texture,textureSha256:hash(read(e.itemModels[0].texture))}),output,outputSha256:hash(result)});
 }
}
await fs.writeFile('src/data/item-art-provenance.json',JSON.stringify(records,null,2)+'\n');
console.log(`Prepared ${records.length} original item images`);
