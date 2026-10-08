// Both published snapshots and local candidates retain the same player-facing rules.
const fields=new Set(['kind','id','displayName','fields','modifiers','guardianBonuses',
 'proficiencies','icon','description','precisionWeapon','precisionAction','thiefArtifact',
 'thiefArmor','thiefArmament','guardianEquipmentSets','guardianProfile','guardianSmiteAction',
 'weaponProficiencies','townExchange']);

export function projectModContent(content){
 return content.entries.map(entry=>Object.fromEntries(
  Object.entries(entry).filter(([key])=>fields.has(key))));
}
