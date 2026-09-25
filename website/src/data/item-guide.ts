// Player explanations accompany fields projected from the published archives.
type Ability={title:string;text:string};
export const statName=(key:string)=>({quickness:'Speed',focusCapacity:'Maximum Focus',startinggold:'Starting gold'}[key]??key[0].toUpperCase()+key.slice(1));
export const statValue=(key:string,value:unknown)=>{const n=Number(value)*(['vitality','speed','strength','intelligence','awareness','talent'].includes(key)?100:1);return `${n>0?'+':''}${Math.round(n*100)/100}`;};
export function itemAbilities(e:any,entries:any[]):Ability[]{
 const result:Ability[]=[];
 for(const id of e.proficiencies??[]){
  const p=entries.find(p=>p.id===id);if(!p)throw Error(`Missing published proficiency ${id}`);
  if(id.startsWith('paladin_censure'))result.push({title:'Grants Censure',text:`Strike at 75% weapon damage. A successful debuff application randomly lowers Armor or Resistance by ${Math.abs(p.fields.m_CustomValue)}, with equal odds. Resistance reduced by Censure enables the bonus from Smite. Normal weapon actions remain available.`});
  else if(id==='paladin_smite')result.push({title:'Grants Smite',text:'A magic attack using your current weapon’s rolls and 25% of its damage. Against a target with active Resistance reduction from Censure, this rises to 150% weapon damage. Armor reduction and unrelated Resistance debuffs do not qualify. The effect is not consumed and perfect rolls are not required. Available while this trinket is equipped.'});
  else result.push({title:`Grants ${p.displayName}`,text:p.description+(id.includes('feint')||id.includes('draw_out')?' Prepared lets a Thief’s next eligible basic precision attack treat the target as Open. It is spent on the attempt and expires after your next turn or on weapon change. Non-Thieves get the reduced damage only.':' Partial rolls still face full armor. This special action cannot Sneak Attack.')});
 }
 const perks:Record<string,(n:any)=>Ability>={
 guardHealPercent:n=>({title:'Guard healing',text:`Using Guard heals the ally for ${n}% of their maximum HP, at least 1 and capped by missing health.`}),
 focusHealBonusPercent:n=>({title:'Focused-hit healing',text:`Adds ${n}% of the designated ally’s maximum HP to the usual 8% healing from a landed focused attack. Healing is capped by missing health.`}),
 wardDebuffs:()=>({title:'Guard ailment ward',text:'Wards Poison, Stun, Daze, and Curse applied by guarded direct attacks.'}),
 retaliationDamage:n=>({title:'Guard retaliation',text:`Retaliates for ${n} damage once per damaging direct enemy attack against the guarded ally.`}),
 guardFocusRestore:n=>({title:'Vigil',text:`The first enemy hit actually reduced by each Guard restores ${n} Focus to the ally, capped at maximum Focus. Keep this weapon equipped until the trigger. Full Focus still spends the opportunity.`}),
 guardReckoning:()=>({title:'Reckoning',text:'The first enemy hit reduced by Guard readies +50% damage on your next eligible single-target blunt attack with Kingsfall. Spent on the attempt, even a miss. Expires at the end of your next turn; changing weapons loses it.'}),
 guardCleanse:()=>({title:'Guard cleanse',text:'Guard removes one existing condition in this order: Stun, Daze, an active removable Curse, then Poison. Permanent campaign curses remain and a lost turn is not restored.'})
 };
 for(const [key,value]of Object.entries(e.guardianBonuses??{})){
  if(!perks[key])throw Error(`Unexplained Guardian perk ${key}`);
  const a=perks[key](value);a.text+=' Requires the Paladin’s Guardian capability. Matching equipped perks use the strongest value, not a sum.';result.push(a);
 }
 if(e.precisionWeapon){result.push({title:'Precision weapon',text:'A Thief’s perfect basic attack can add 20% current weapon damage against an Open target or while Prepared, once per own turn. Open means the enemy has not acted yet, or a different ally damaged it directly since its last turn. The entitlement is spent on the qualifying attempt; partial results get ordinary damage. Specials do not Sneak Attack.'});if(e.precisionWeapon==='paired')result.push({title:'Twin Feint · Thief only',text:'A basic Strike that misses exactly one check and causes HP damage grants Prepared for your next eligible attack. Once per own turn. Two blades resolve as one hit; the preparation does not enhance that same attack.'});}
 const artifacts:Record<string,Ability>={
 borrowedFortune:{title:'Borrowed Fortune · Thief only',text:'A damaging Sneak Attack refunds one Focus actually spent on that attack, capped at capacity. No Focus spent means no refund. Keep the weapon equipped through resolution. Partial rolls, fully absorbed hits, and special actions return nothing.'},
 lastLight:{title:'Last Light · Thief only',text:'Once per combat, an eligible basic Strike against a full-health enemy replaces the usual +20% Sneak Attack bonus with +75% on perfect rolls. Requires an opening or Prepared. Spent on the qualifying attempt, including partial or fully absorbed hits. Weapon swaps and revival do not refresh it.'},
 looseAndLeave:{title:'Loose and Leave · Thief only',text:'A damaging Sneak Attack grants +8 Evasion points until your next turn. It cannot stack and ends on weapon change, incapacity, or combat exit. Partial rolls, absorbed hits, and special actions grant nothing. This is not a guaranteed dodge.'}
 };
 if(e.thiefArtifact){if(!artifacts[e.thiefArtifact])throw Error('Unexplained artifact');result.push(artifacts[e.thiefArtifact]);}
 return result;
}
