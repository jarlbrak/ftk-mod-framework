import { statName, statValue } from './item-guide.ts';
import type { TooltipLine } from './paladin-weapon-tooltip.ts';

// Action names and checks come from the selected published or preview projection.
// Class effects are explanatory context, not weapon-granted proficiency labels.
export function thiefWeaponTooltip(e:any, entries:any[]) {
 const lines:TooltipLine[]=[];
 if(!e.fields.m_NoRegularAttack)lines.push({text:e.fields.m_AttackDisplay || (e.precisionWeapon==='bow'?'Shoot':'Strike'),tone:'ability'});
 for(const id of e.proficiencies??[]){
  const action=entries.find(entry=>entry.id===id);
  if(!action)throw Error(`Missing Thief proficiency ${id}`);
  lines.push({text:action.displayName,tone:'ability'});
 }
 for(const [key,value] of Object.entries(e.modifiers??{})){
  if(Number(value)!==0)lines.push({text:`${statValue(key,value)} ${statName(key)}`,tone:'normal'});
 }
 const contextLines:TooltipLine[]=[];
 const add=(text:string,bold=false)=>contextLines.push({text,tone:'context',bold});
 if(e.precisionWeapon){
  add('Thief only:',true);
  add('Sneak Attack: perfect basic hit vs Open or while Prepared.');
  add('Normal bonus: +20% current weapon damage.');
  add('Once per turn; qualifying attempt spends it.');
  if(e.precisionWeapon==='paired'){
   add('Twin Feint: one failed check + HP damage');
   add('prepares the next eligible basic attack.');
  }
 }
 if(e.thiefArtifact==='borrowedFortune'){
  add('Borrowed Fortune',true);
  add('Damaging Sneak Attack: return 1 Focus spent on that attack.');
 }else if(e.thiefArtifact==='lastLight'){
  add('Last Light',true);
  add('Full-health target: +75% replaces the normal Sneak bonus.');
  add('Once per combat; qualifying attempt spends it.');
 }else if(e.thiefArtifact==='looseAndLeave'){
  add('Loose and Leave',true);
  add('Damaging Sneak Attack: +8 Evasion until next turn.');
  add('Cannot stack; lost on weapon change.');
 }else if(e.thiefArtifact)throw Error('Unexplained Thief artifact');
 if(e.thiefArmament){
  const family=e.thiefArmament;
  const previews:Record<string,string>={
   locksmith:'Prepared Sneak +5 points; other Sneak -5 points.',
   nightblade:'Full-health or unacted opener +5 points; other Sneak -5 points.',
   wayfarer:'Eligible direct hit: +2 Evasion to next turn; Sneak -5 points.'
  };
  if(!previews[family])throw Error('Unexplained Thief armament family');
  add(`${family[0].toUpperCase()+family.slice(1)} (Thief) 0/3 armor`,true);
  add('Next: 2 matching Head, Body, Foot pieces',true);
  add(previews[family]);
  add('Core: locked until 3 matching armor pieces.');
  add('Matching weapon optional; no extra bonus.');
 }
 return {lines,contextLines};
}
