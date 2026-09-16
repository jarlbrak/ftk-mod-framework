#!/usr/bin/env python3
"""Join discovery, enemy mapping and offline audit without inferring live success."""
import argparse,json
from pathlib import Path

def build(inventory,mapping,audit):
 hashes={x['source_sha256'] for x in (inventory,mapping,audit)}
 if len(hashes)!=1:raise ValueError('Input source hashes differ; regenerate against one game build')
 audited={r['rig_profile_fingerprint']:r for r in audit['results']}
 profiles=[]
 for p in inventory['candidate_profiles']:
  key=p['rig_profile_fingerprint'];result=audited.get(key)
  profiles.append({'rig_profile_fingerprint':key,'renderer_path_ids':p['renderer_path_ids'],
   'joint_count':p['joint_count'],'offline_reference_roundtrip':result['status'] if result else 'pending',
   'original_model':'pending','production_binding':'pending','appearance':'pending','idle':'pending',
   'attack':'pending','hit':'pending','death':'pending','gameplay':'pending'})
 combos=[]
 for p in mapping['combat_profiles']:
  combos.append({**p,'validation':'pending','evidence':[]})
 return {'schema_version':1,'source_sha256':next(iter(hashes)),
  'scope':'Candidate character rigs from the inventory, plus all mapped enemy combat-controller combinations. Native roundtrips never imply custom-model or live success.',
  'discovery_summary':inventory['summary'],'enemy_mapping_summary':mapping['summary'],
  'offline_audit_summary':audit['summary'],'rig_profiles':profiles,'combat_profiles':combos,
  'null_enemy_asset_rows':mapping['null_enemy_asset_rows'],
  'unmapped_renderers':[{'renderer_path_id':r['renderer_path_id'],'mesh_name':r['mesh_name'],
   'rig_profile_fingerprint':r['rig_profile_fingerprint'],'has_character_listener':bool(r['character_event_listeners']),
   'role':r.get('validated_role','unknown'),'component_evidence':r.get('ancestor_component_classes',[])} for r in mapping['unmapped_renderers']],
  'limitations':['This generated baseline deliberately resets custom-model/live checks to pending. Keep evidence records separately and do not overwrite a manually annotated coverage file.',
   'Exact rig and controller fingerprints still require live renderer, transform, multipart and material verification.',
   'Unmapped character candidates are not silently excluded. Resolve their role and supported integration path before claiming complete candidate coverage.']}

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for n in ['inventory','mapping','audit','output']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args()
 if a.output.exists():p.error('--output already exists; choose a fresh baseline path to preserve evidence')
 x=build(*[json.loads(v.read_text()) for v in (a.inventory,a.mapping,a.audit)])
 a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(x,indent=2)+'\n')
 print(json.dumps({k:x[k] for k in ['discovery_summary','enemy_mapping_summary','offline_audit_summary']},indent=2))
if __name__=='__main__':main()
