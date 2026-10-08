import assert from 'node:assert/strict';import fs from 'node:fs';
import {itemAbilities,itemTooltipLines} from '../src/data/item-guide.ts';
import {thiefWeaponTooltip} from '../src/data/thief-weapon-tooltip.ts';
const current=JSON.parse(fs.readFileSync('../marketplace/packages/thief/content.json')).entries;
const pistols=current.filter(e=>e.proficiencies?.includes('thief_pistol_fire'));assert.equal(pistols.length,8);
for(const e of pistols){const lines=itemTooltipLines(e,current).map(l=>l.text);assert(lines.includes('Fire: basic precision shot.'));assert(lines.includes('Can Sneak Attack; does not grant Prepared.'));assert(!lines.includes('Thief: damaging hit grants Prepared.')||e.proficiencies.some(id=>id!=='thief_pistol_fire'&&current.find(p=>p.id===id)?.precisionAction==='prepare'));const fire=itemAbilities(e,current).find(a=>a.title==='Grants Fire');assert(fire.text.includes('does not grant Prepared'));assert(fire.text.includes('Loose and Leave'));}
// Published1.0 has no Fire: its existing per-proficiency copy remains unchanged.
const published=JSON.parse(fs.readFileSync('src/data/thief.json')).entries;assert(!published.some(e=>e.id==='thief_pistol_fire'));
for(const e of published.filter(e=>e.kind==='weapon')){const lines=itemTooltipLines(e,published);assert(!lines.some(l=>l.text.startsWith('Fire:')));assert(lines.some(l=>l.text==='Special attacks cannot Sneak Attack.'));}
console.log('PASS8 currentFire weapons and published1.0 baseline');
for(const entries of [published,current]){
 for(const weapon of entries.filter(e=>e.kind==='weapon')){
  const card=thiefWeaponTooltip(weapon,entries);
  const actions=card.lines.filter(line=>line.tone==='ability').map(line=>line.text);
  const named=weapon.proficiencies.map(id=>entries.find(e=>e.id===id).displayName);
  assert.deepEqual(actions,weapon.fields.m_NoRegularAttack?named:[weapon.precisionWeapon==='bow'?'Shoot':'Strike',...named]);
  assert(!card.lines.some(line=>line.text.includes('Thief Skill:')));
  assert(!card.lines.some(line=>line.text.includes('damage per level')));
  assert(card.contextLines.some(line=>line.text==='Thief only:'));
  if(weapon.thiefArmament)assert(card.contextLines.some(line=>line.text==='Matching weapon optional; no extra bonus.'));
 }
}
console.log('PASS compact Thief action ownership for published and candidate weapons');
