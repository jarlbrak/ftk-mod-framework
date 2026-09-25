// Explicit maintenance command. Normal builds use the reviewed snapshot offline.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
const url='https://raw.githubusercontent.com/jarlbrak/ftk-mod-framework/master/marketplace/catalog.json';
const catalogPath=process.argv[2];
const catalog=catalogPath?JSON.parse(await fs.readFile(catalogPath,'utf8')):await (async()=>{
 const response=await fetch(url);if(!response.ok)throw Error(response.status);
 return response.json();
})();
const temp=await fs.mkdtemp(path.join(os.tmpdir(),'ftk-published-'));
const snapshots={};
for(const p of catalog.packages){
 const response=await fetch(p.packageUrl);if(!response.ok)throw Error(`${p.name}: ${response.status}`);
 const bytes=Buffer.from(await response.arrayBuffer());
 if(crypto.createHash('sha256').update(bytes).digest('hex')!==p.sha256)throw Error(`${p.name}: hash mismatch`);
 const archive=path.join(temp,p.packageId+'.zip');await fs.writeFile(archive,bytes);
 const read=name=>JSON.parse(execFileSync('unzip',['-p',archive,name],{maxBuffer:8*1024*1024}).toString());
 const manifest=read('manifest.json');
 if(manifest.version!==p.version||manifest.modGuid!==p.modGuid||manifest.frameworkVersion!==p.frameworkVersion)throw Error(`${p.name}: manifest mismatch`);
 if(['Paladin','Thief'].includes(p.name)){
  if(p.version!==({Paladin:'1.4.0',Thief:'1.0.0'})[p.name])throw Error('Review and update the version-specific mod guide before syncing a new release.');
  snapshots[p.name.toLowerCase()]={version:p.version,sha256:p.sha256,entries:read('content.json').entries.map(e=>Object.fromEntries(Object.entries(e).filter(([k])=>['kind','id','displayName','fields','modifiers','guardianBonuses','proficiencies','icon','description','precisionWeapon','thiefArtifact'].includes(k))))};
 }
 console.log(`Verified ${p.name} ${p.version}`);
}
if(!snapshots.paladin||!snapshots.thief)throw Error('Required class absent from published catalog');
await fs.writeFile('src/data/catalog.json',JSON.stringify(catalog,null,2)+'\n');
for(const [name,data] of Object.entries(snapshots))await fs.writeFile(`src/data/${name}.json`,JSON.stringify(data,null,2)+'\n');
