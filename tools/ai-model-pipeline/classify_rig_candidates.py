#!/usr/bin/env python3
"""Positively map remaining rig candidates through vanilla skinset avatar fields.

Metadata only. Unknown roles stay unknown; absence from a DB is not proof unused.
"""
import argparse
import collections
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import UnityPy
from UnityPy.classes import PPtr
from map_enemy_rigs import require, sha


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--resources', type=Path, required=True)
    p.add_argument('--inventory', type=Path, required=True)
    p.add_argument('--enemy-mapping', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--ilspy', default=shutil.which('ilspycmd') or str(Path.home()/'.dotnet/tools/ilspycmd'))
    args = p.parse_args(); source = args.resources.resolve()
    inv = json.loads(args.inventory.read_text()); enemies = json.loads(args.enemy_mapping.read_text())
    require(inv['source_sha256'] == enemies['source_sha256'] == sha(source), 'Source hash mismatch')
    env = UnityPy.load(str(source)); cache = {}
    def identity(obj): return (Path(obj.assets_file.name).name, obj.path_id)
    def tree(obj):
        if obj is None: return {}
        key = identity(obj)
        if key not in cache: cache[key] = obj.read_typetree(check_read=False)
        return cache[key]
    def resolve(owner, pointer):
        if not pointer or not pointer.get('m_PathID'): return None
        return PPtr(m_FileID=pointer['m_FileID'], m_PathID=pointer['m_PathID'], assetsfile=owner.assets_file).deref()
    def ref(owner, field): return resolve(owner, tree(owner).get(field))
    def classname(obj): return tree(ref(obj, 'm_Script')).get('m_ClassName')
    skinset_database = source.parent/'sharedassets1.assets'
    db_env = UnityPy.load(str(skinset_database))
    dbs = []
    for obj in db_env.objects:
        if obj.type.name != 'MonoBehaviour': continue
        try: found = classname(obj) == 'FTK_skinsetDB'
        except FileNotFoundError: continue
        if found: dbs.append(obj)
    require(len(dbs) == 1, 'Expected exactly one FTK_skinsetDB')
    db = dbs[0]; raw = db.get_raw_data()
    code = subprocess.check_output([args.ilspy, str(source.parent/'Managed/Assembly-CSharp.dll'), '-t', 'GridEditor.FTK_skinset'], text=True)
    m = re.search(r'public enum ID\s*\{(.*?)\n\t\}', code, re.S)
    require(m is not None, 'Skinset enum missing')
    names = [x.strip().split('=')[0].strip() for x in m.group(1).split(',')]
    names.remove('None'); rows = []
    for name in names:
        needle = struct.pack('<i', len(name))+name.encode()
        offsets = [i for i in range(0, len(raw)-len(needle), 4) if raw.startswith(needle, i)]
        require(len(offsets) == 1, 'Missing or ambiguous skinset row '+name)
        pos = offsets[0]; end = (pos+len(needle)+3)//4*4
        fid, pid = struct.unpack_from('<iq', raw, end)
        cel = resolve(db, {'m_FileID': fid, 'm_PathID': pid})
        require(cel is not None and classname(cel) == 'CharacterEventListener', 'Invalid skinset avatar '+name)
        fid, pid = struct.unpack_from('<iq', raw, end+12)
        armor = resolve(db, {'m_FileID': fid, 'm_PathID': pid})
        require(armor is None or classname(armor) == 'Armor', 'Invalid skinset armor '+name)
        rows.append(dict(skinset_id=name, db_byte_offset=pos, avatar_asset_file=identity(cel)[0], avatar_cel_path_id=cel.path_id, avatar_prefab_name=tree(ref(cel, 'm_GameObject')).get('m_Name'), armor_asset_file=identity(armor)[0] if armor else None, armor_component_path_id=armor.path_id if armor else None))
    count = struct.unpack_from('<i', raw, min(r['db_byte_offset'] for r in rows)-4)[0]
    require(count == len(rows), 'Skinset enum/header count mismatch')
    by_cel = collections.defaultdict(list)
    for row in rows: by_cel[(row['avatar_asset_file'], row['avatar_cel_path_id'])].append(row['skinset_id'])
    mapped_enemies = {rr['renderer_path_id'] for row in enemies['enemies'] for rr in row['renderers']}
    enemy_profiles = {rr['rig_profile_fingerprint'] for row in enemies['enemies'] for rr in row['renderers']}
    unmapped_evidence = {r['renderer_path_id']: r.get('ancestor_component_classes', []) for r in enemies.get('unmapped_renderers', [])}
    records = []
    for renderer in inv['renderers']:
        if not renderer.get('character_event_listeners'): continue
        record = dict(renderer_path_id=renderer['renderer_path_id'], renderer_name=renderer['renderer_name'], ancestor_names=renderer['ancestor_names'], rig_profile_fingerprint=renderer['rig_profile_fingerprint'], joint_count=renderer['joint_count'])
        skinsets = sorted({key for cel in renderer['character_event_listeners'] for key in by_cel.get((source.name, cel['component_path_id']), [])})
        record.update(skinset_avatar_references=skinsets, enemy_db_referenced=renderer['renderer_path_id'] in mapped_enemies, rig_profile_used_by_enemy=renderer['rig_profile_fingerprint'] in enemy_profiles)
        if record['enemy_db_referenced']: role = 'enemy_db_prefab'
        elif skinsets: role = 'player_skinset_avatar'
        elif not renderer.get('mesh_path_id') or not renderer.get('rig_profile_fingerprint'): role = 'empty_renderer_no_mesh'
        elif any(c['component_class'] in ('Diorama', 'DioramaDungeon', 'DioramaBoat') for c in unmapped_evidence.get(renderer['renderer_path_id'], [])): role = 'diorama_embedded_character'
        else: role = 'unresolved_character_prefab'
        record['ancestor_component_evidence'] = unmapped_evidence.get(renderer['renderer_path_id'], [])
        record['positive_role'] = role
        record['integration_path'] = {'enemy_db_prefab':'enemy body mesh API, including multipart selection where required', 'player_skinset_avatar':'player skinset/avatar integration; enemy body API does not target these player spawns', 'unresolved_character_prefab':'resolve live ownership/spawn mechanism before choosing integration API', 'empty_renderer_no_mesh':'no source mesh to replace; inspect owner and intended populated renderer', 'diorama_embedded_character':'diorama scene-object integration; distinguish embedded staging objects from runtime enemy clones'}[role]
        records.append(record)
    groups = collections.defaultdict(list)
    for row in records:
        if row['rig_profile_fingerprint']: groups[row['rig_profile_fingerprint']].append(row)
    profile_rows = [dict(rig_profile_fingerprint=fp, joint_count=rr[0]['joint_count'], renderer_path_ids=sorted(r['renderer_path_id'] for r in rr), positive_roles=sorted({r['positive_role'] for r in rr}), enemy_profile=fp in enemy_profiles, skinset_avatar_referenced=any(r['skinset_avatar_references'] for r in rr), validation='metadata_classification_only') for fp, rr in sorted(groups.items())]
    result = dict(source_file=str(source), source_sha256=sha(source), skinset_database_file=str(skinset_database), skinset_database_sha256=sha(skinset_database), skinset_database_path_id=db.path_id, skinset_rows=rows, summary=dict(skinset_row_count=len(rows), all_character_renderer_candidates=len(records), character_rig_profiles=len(groups), character_renderers_without_rig_profile=sum(not r['rig_profile_fingerprint'] for r in records), enemy_rig_profiles=len(enemy_profiles), nonenemy_rig_profiles=len(set(groups)-enemy_profiles), role_counts=dict(collections.Counter(r['positive_role'] for r in records)), nonenemy_profile_role_sets=dict(collections.Counter('|'.join(r['positive_roles']) for r in profile_rows if not r['enemy_profile']))), renderers=records, profiles=profile_rows, limitations=['A positive role comes only from an explicit enemy DB prefab, skinset m_Avatar pointer, diorama ancestor component, or empty mesh reference. Names are not used to declare NPC, portrait, test, or unused assets.', 'Unresolved prefabs remain in the denominator. They may be indirect assets or unused templates; no unused claim is made.', 'Equivalent skinset profiles do not establish player model support. Player body/armor/hair equipment composition needs its own integration.', 'No original asset or live animation validation is supplied by this metadata classification.'])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result['summary'], indent=2))


if __name__ == '__main__': main()
