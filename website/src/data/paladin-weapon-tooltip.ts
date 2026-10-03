import { statName, statValue } from './item-guide';

export type TooltipLine = { text: string; tone: string; bold?: boolean };

// Native uiWeaponDetail.ShowWeapon puts actions before modifiers and skill rows.
// Published package fields remain the authority for values and eligible weapons.
export function paladinWeaponTooltip(e: any, entries: any[]) {
 const lines: TooltipLine[] = [];
 if (!e.fields.m_NoRegularAttack) lines.push({text:e.fields.m_AttackDisplay || 'Strike',tone:'ability'});
 for (const id of e.proficiencies ?? []) {
  const action = entries.find(entry => entry.id === id);
  if (!action) throw Error(`Missing published proficiency ${id}`);
  lines.push({text:action.displayName,tone:'ability'});
 }
 for (const [key,value] of Object.entries(e.modifiers ?? {})) {
  if (Number(value) !== 0) lines.push({text:`${statValue(key,value)} ${statName(key)}`,tone:'normal'});
 }
 const paladin = entries.find(entry => entry.kind === 'class' && entry.id === 'paladin');
 const skills = new Set<string>();
 for (const group of paladin?.weaponProficiencies ?? []) {
  if (!group.weapons.includes(e.id)) continue;
  for (const id of group.proficiencies) {
   const action = entries.find(entry => entry.id === id);
   if (!action) throw Error(`Missing published proficiency ${id}`);
   skills.add(`${paladin.displayName} Skill: ${action.displayName}`);
  }
 }
 for (const text of skills) lines.push({text,tone:'skill'});
 return {lines,guardianLines:guardianWeaponLines(e,paladin,entries)};
}

// Match GuardianEquipmentDescription and GuardianSetDescription with no wearer:
// no matching armor or armament is equipped, so the card shows the next threshold.
function guardianWeaponLines(e:any,paladin:any,entries:any[]):TooltipLine[] {
 const lines:TooltipLine[]=[];
 const add=(text:string,bold=false)=>lines.push({text,tone:'guardian',bold});
 const perks:Record<string,(value:any)=>string[]>={
  guardHealPercent:v=>[`Guard heals ${v}% ally max HP (min 1).`],
  focusHealBonusPercent:v=>[`Focused-hit healing: +${v}% ally max HP.`],
  wardDebuffs:()=>['Guard blocks direct-attack','Poison, Stun, Daze and Curse.'],
  retaliationDamage:v=>[`Guard retaliates for ${v} damage`,'once per damaging direct attack','from an enemy.'],
  guardFocusRestore:()=>['First reduced hit per Guard:','restore 1 Focus to guarded ally.'],
  guardReckoning:()=>['Guarded hit readies Reckoning:','+50% next single-target hammer hit.','Spent on attempt; expires next turn end.'],
  guardCleanse:()=>['Guard removes one ally condition:','Stun, Daze, Curse, then Poison.']
 };
 if (Object.keys(e.guardianBonuses ?? {}).length) {
  add(`${paladin.displayName} only:`);
  for (const [key,value] of Object.entries(e.guardianBonuses)) {
   if (!perks[key]) throw Error(`Unexplained Guardian perk ${key}`);
   if (value) perks[key](value).forEach(text=>add(text));
  }
 }
 for (const set of paladin?.guardianEquipmentSets ?? []) {
  if (![set.oneHand,set.twoHand].includes(e.id)) continue;
  const family=set.id[0].toUpperCase()+set.id.slice(1);
  const neutral=paladin.guardianProfile;
  const minor=set.minor;
  add(`${family} (${paladin.displayName}) 0/3 armor`,true);
  add('Next: 2 armor',true);
  if (minor.physicalPercent!==neutral.physicalPercent) add(`Physical hits deal ${minor.physicalPercent}% damage`);
  const offense:string[]=[];
  const change=(n:number)=>`${n>=100?'+':''}${n-100}%`;
  if (minor.smitePercent!==neutral.smitePercent) offense.push(`Smite ${change(minor.smitePercent)}`);
  if (minor.healingPercent!==neutral.healingPercent) offense.push(`healing ${change(minor.healingPercent)}`);
  if (offense.length) add(offense.join('; '));
  if (minor.guardReductionPercent!==neutral.guardReductionPercent) add(`Guard blocks ${minor.guardReductionPercent}%`);
  if (minor.bonuses?.retaliationDamage) add(`Guard retaliates for ${minor.bonuses.retaliationDamage} damage`);
  if (lines.at(-1)?.text==='Next: 2 armor') add('Core role unlocked');
  add('Full set: locked',true);
  add('Requires 3 matching armor pieces');
  const shortName=(id:string)=>{
   const name=entries.find(entry=>entry.id===id)?.displayName;
   if (!name) throw Error(`Missing published armament ${id}`);
   return name.startsWith(family+' ')?name.slice(family.length+1):name;
  };
  add(`Equip ${shortName(set.oneHand)} + ${shortName(set.shield)} or ${shortName(set.twoHand)} (empty offhand)`);
  if (set.completion.focusHealBonusPercent) {
   const percent=(8+Math.max(set.core.bonuses?.focusHealBonusPercent??0,set.completion.focusHealBonusPercent))*set.core.healingPercent/100;
   add(`${set.core.guardSmiteHealing?'Guard bond: next focused Smite':'Focused hits'} heal${set.core.guardSmiteHealing?'s':''} ally ${percent}% max HP`);
  }
  if (set.completion.wardDebuffs&&!set.core.bonuses?.wardDebuffs) add('Guard wards direct-hit Poison, Stun, Daze, Curse');
  if (set.armorDamageBonus?.multiplier>1) add(`Direct physical hits +${Math.round((set.armorDamageBonus.multiplier-1)*100)}% vs set Armor debuff`);
 }
 return lines;
}
