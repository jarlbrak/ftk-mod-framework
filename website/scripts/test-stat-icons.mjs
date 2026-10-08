import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createHash} from 'node:crypto';
import {officialStatIcon} from '../src/data/stat-icons.ts';

const manifest=JSON.parse(fs.readFileSync('src/data/wiki-stat-icons.json'));
const expected=['Strength','Vitality','Intelligence','Awareness','Talent','Speed','Luck','Focus','Armor','Resistance','Evasion'];
assert.deepEqual(manifest.records.map(r=>r.stat).sort(),expected.toSorted());
for(const stat of expected){
 const record=manifest.records.find(r=>r.stat===stat);
 assert.equal(officialStatIcon(stat),`media/stat-icons/${stat.toLowerCase()}.png`);
 const bytes=fs.readFileSync('public/'+record.file);
 assert(bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10])));
 assert.equal(createHash('sha256').update(bytes).digest('hex'),record.sourceSha256);
 assert.equal(record.sha256,record.sourceSha256);
 assert.equal(new URL(record.source).hostname,'fortheking.wiki.gg');
 assert.equal(record.sourcePage,`https://fortheking.wiki.gg/wiki/File:StatIcon-${stat}.png`);
}
assert.equal(officialStatIcon('quickness'),officialStatIcon('Speed'));
assert.throws(()=>officialStatIcon('unverified-stat'),/Missing official wiki icon/);
for(const path of ['src/data/paladin.json','src/data/thief.json','../marketplace/packages/thief/content.json']){
 const data=JSON.parse(fs.readFileSync(path));
 for(const e of data.entries.filter(e=>e.kind==='weapon'))assert(officialStatIcon(e.fields.skill));
}
console.log('PASS all11 official stat icon hashes and all published/candidate weapon stats; no unknown fallback');
