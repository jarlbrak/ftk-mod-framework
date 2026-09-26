import published from './catalog.json';

// Page and media paths derive from the stable package identity, because display names
// may contain spaces. The ftkmf.* suffix matches the existing Paladin, Thief, and Possum paths.
export const slugFor=(packageId:string)=>packageId.replace(/^ftkmf\./,'');

// Website editorial copy: the author labels Thief 1.0.0 as a release.
// Preserve the published snapshot, package identity, and compatibility facts.
export default {
 ...published,
 packages: published.packages.map(p=>({...(p.packageId==='ftkmf.thief'&&p.version==='1.0.0'?{
  ...p,
  description:p.description.replace(' Version 1.0.0 playtest.',''),
  changelog:p.changelog.replace('1.0.0 playtest candidate. ','1.0.0 release. '),
  requirements:p.requirements.map(r=>r.replace('Single-player playtest. ','Single-player release. ')),
 }:p),slug:slugFor(p.packageId)})),
};
