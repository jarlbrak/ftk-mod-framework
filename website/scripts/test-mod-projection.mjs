import assert from 'node:assert/strict';
import fs from 'node:fs';
import {projectModContent} from './project-mod-content.mjs';

const candidate=JSON.parse(fs.readFileSync('../marketplace/packages/thief/content.json'));
const projected=projectModContent(candidate);
for(const key of ['precisionAction','thiefArmor','thiefArmament']){
 const source=candidate.entries.filter(entry=>entry[key]);
 assert(source.length>0,`Candidate must exercise ${key}`);
 assert.equal(projected.filter(entry=>entry[key]).length,source.length);
 for(const entry of source)assert.deepEqual(projected.find(e=>e.id===entry.id)[key],entry[key]);
}
assert(!projected.some(entry=>'itemModels' in entry||'offHandModels' in entry||'displayModels' in entry));
for(const name of ['paladin','thief']){
 const published=JSON.parse(fs.readFileSync(`src/data/${name}.json`));
 assert.deepEqual(projectModContent(published),published.entries);
}
console.log('PASS candidate action/armor/armament projection and unchanged published snapshots');
