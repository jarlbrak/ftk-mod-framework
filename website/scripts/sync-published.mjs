// Explicit maintenance command. Normal builds use the reviewed snapshot offline.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
const url='https://raw.githubusercontent.com/jarlbrak/ftk-mod-framework/master/marketplace/catalog.json';
const response=await fetch(url);if(!response.ok)throw Error(response.status);
const catalog=await response.json();
const temp=await fs.mkdtemp(path.join(os.tmpdir(),'ftk-published-'));
let paladin;
for(const p of catalog.packages){
 const response=await fetch(p.packageUrl);if(!response.ok)throw Error(`${p.name}: ${response.status}`);
 const bytes=Buffer.from(await response.arrayBuffer());
 if(crypto.createHash('sha256').update(bytes).digest('hex')!==p.sha256)throw Error(`${p.name}: hash mismatch`);
 const archive=path.join(temp,p.packageId+'.zip');await fs.writeFile(archive,bytes);
 const read=name=>JSON.parse(execFileSync('unzip',['-p',archive,name],{maxBuffer:8*1024*1024}).toString());
 const manifest=read('manifest.json');
 if(manifest.version!==p.version||manifest.modGuid!==p.modGuid||manifest.frameworkVersion!==p.frameworkVersion)throw Error(`${p.name}: manifest mismatch`);
 if(p.packageId==='ftkmf.paladin'){
  if(p.version!=='1.3.0')throw Error('Review and update the version-specific Paladin guide before syncing a new release.');
  paladin={version:p.version,sha256:p.sha256,entries:read('content.json').entries.map(e=>Object.fromEntries(Object.entries(e).filter(([k])=>['kind','id','displayName','fields','modifiers','guardianBonuses','proficiencies'].includes(k))))};
 }
 console.log(`Verified ${p.name} ${p.version}`);
}
if(!paladin)throw Error('Paladin absent from published catalog');
await fs.writeFile('src/data/catalog.json',JSON.stringify(catalog,null,2)+'\n');
await fs.writeFile('src/data/paladin.json',JSON.stringify(paladin,null,2)+'\n');
