#!/usr/bin/env python3
"""Reconcile serialized FTK enemy rows to rig/controller metadata, failing closed.

No mesh, texture, animation curve, or decompiled source is written. Requires the
inventory_skeletons.py output for the same resources.assets and local ilspycmd.
This recognizes verified Unity 2017 field prefixes, not arbitrary DB formats.
"""
import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import UnityPy
from UnityPy.classes import PPtr


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resources', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--ilspy', default=shutil.which('ilspycmd') or str(Path.home()/'.dotnet/tools/ilspycmd'))
    args = parser.parse_args()
    source = args.resources.resolve()
    inv = json.loads(args.inventory.read_text())
    require(inv['source_sha256'] == sha(source), 'Inventory hash does not match resources.assets')
    require(not any(r.get('errors') for r in inv['renderers']), 'Inventory has unresolved renderer errors')
    env = UnityPy.load(str(source))
    asset = next(f for f in env.files.values() if Path(f.name).name == source.name)
    cache = {}

    def identity(obj):
        return (Path(obj.assets_file.name).name, obj.path_id)

    def tree(obj):
        if obj is None:
            return {}
        key = identity(obj)
        if key not in cache:
            cache[key] = obj.read_typetree(check_read=False)
        return cache[key]

    def resolve(owner, pointer):
        if not pointer or not pointer.get('m_PathID'):
            return None
        return PPtr(m_FileID=pointer['m_FileID'], m_PathID=pointer['m_PathID'], assetsfile=owner.assets_file).deref()

    def ref(owner, field):
        return resolve(owner, tree(owner).get(field))

    def class_name(obj):
        return tree(ref(obj, 'm_Script')).get('m_ClassName')

    def object_name(obj):
        return tree(obj).get('m_Name') if obj else None

    def find_database(filename):
        e = UnityPy.load(str(source.parent/filename))
        matches = []
        for obj in e.objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            try:
                found = class_name(obj) == 'FTK_enemyCombatDB'
            except FileNotFoundError:
                continue  # Built-in scripts are not the game database.
            if found:
                matches.append(obj)
        require(len(matches) == 1, 'Expected one FTK_enemyCombatDB in '+filename)
        return e, matches[0]

    # Keep environments alive while their object readers are used.
    db_env, db = find_database('level1')
    copy_env, db_copy = find_database('sharedassets1.assets')
    assembly = source.parent/'Managed/Assembly-CSharp.dll'
    code = subprocess.check_output([args.ilspy, str(assembly), '-t', 'GridEditor.FTK_enemyCombat'], text=True)
    match = re.search(r'public enum ID\s*\{(.*?)\n\t\}', code, re.S)
    require(match is not None, 'Could not identify declared enemy enum')
    names = [x.strip().split('=')[0].strip() for x in match.group(1).split(',')]
    require(names.count('None') == 1, 'Expected exactly one None enum sentinel')
    names.remove('None')
    require(len(set(names)) == len(names), 'Duplicate enemy enum names')

    def decode_database(owner):
        raw = owner.get_raw_data()
        rows = []
        for name in names:
            needle = struct.pack('<i', len(name))+name.encode()
            positions = [p for p in range(0, len(raw)-len(needle), 4) if raw.startswith(needle, p)]
            require(len(positions) == 1, 'Missing or ambiguous serialized row '+name)
            start = positions[0]
            end = (start+len(needle)+3)//4*4
            level, fid, pid = struct.unpack_from('<iiq', raw, end)
            require(0 <= level <= 100, 'Unexpected enemy level '+name)
            pointer = {'m_FileID': fid, 'm_PathID': pid}
            cel = resolve(owner, pointer)
            require(cel is None or class_name(cel) == 'CharacterEventListener', 'Invalid enemy asset '+name)
            rows.append({'enemy_id': name, 'enemy_level': level, 'db_byte_offset': start, 'cel': cel, 'field_start': end})
        first = min(r['db_byte_offset'] for r in rows)
        count = struct.unpack_from('<i', raw, first-4)[0]
        require(count == len(names) == len(rows), 'Array header / enum / recognized row denominator mismatch')
        return raw, rows, count

    raw, decoded, count = decode_database(db)
    copy_raw, copy_rows, copy_count = decode_database(db_copy)
    signature = lambda rows: [(r['enemy_id'], identity(r['cel']) if r['cel'] else None) for r in rows]
    require(signature(decoded) == signature(copy_rows), 'Serialized database copies disagree')
    by_cel = collections.defaultdict(list)
    for renderer in inv['renderers']:
        for cel in renderer.get('character_event_listeners', []):
            by_cel[cel['component_path_id']].append(renderer)

    controllers = {}
    rows, nulls = [], []
    for entry in decoded:
        cel = entry['cel']
        if cel is None:
            nulls.append({k: entry[k] for k in ('enemy_id', 'enemy_level', 'db_byte_offset')})
            continue
        require(identity(cel)[0] == source.name, 'Enemy prefab outside inventoried resources: '+entry['enemy_id'])
        row = {k: entry[k] for k in ('enemy_id', 'enemy_level', 'db_byte_offset')}
        row.update(cel_path_id=cel.path_id, prefab_name=object_name(ref(cel, 'm_GameObject')),
                   prefab_game_object_path_id=ref(cel, 'm_GameObject').path_id,
                   renderers=json.loads(json.dumps(by_cel[cel.path_id])), validation='serialized_mapping_not_runtime_validated')
        p = entry['field_start']+4+12+12  # level, enemy asset, overworld controller
        # race array, group/archetype/decay, scourge/boss booleans (aligned)
        n = struct.unpack_from('<i', raw, p)[0]
        require(0 <= n <= 100, 'Unexpected race array length')
        p += 4+n*4+3*4+2*4
        n = struct.unpack_from('<i', raw, p)[0]
        require(0 <= n <= 1024, 'Unexpected rarity string length')
        p = (p+4+n+3)//4*4
        p += 5*4+4+4+4+4  # spawn booleans, camp, useCampName, boat, destroy
        for _ in range(2):
            n = struct.unpack_from('<i', raw, p)[0]
            require(0 <= n <= 1000, 'Unexpected realm array length')
            p += 4+n*4
        fid, pid = struct.unpack_from('<iq', raw, p)
        weapon = resolve(db, {'m_FileID': fid, 'm_PathID': pid})
        require(weapon is not None and class_name(weapon) == 'Weapon', 'Unresolved weapon '+entry['enemy_id'])
        row.update(weapon_path_id=weapon.path_id, weapon_asset_file=identity(weapon)[0], weapon_name=object_name(ref(weapon, 'm_GameObject')))
        # Weapon's declared m_WeaponHolderName:string immediately precedes
        # m_AnimationController:PPtr. Accept only a unique typed controller.
        wb = weapon.get_raw_data()
        candidates = []
        for q in range(0, len(wb)-16, 4):
            n = struct.unpack_from('<i', wb, q)[0]
            if not 0 <= n <= 64:
                continue
            label = wb[q+4:q+4+n]
            if not all(32 <= v < 127 for v in label):
                continue
            qq = (q+4+n+3)//4*4
            if qq+12 > len(wb):
                continue
            fid, pid = struct.unpack_from('<iq', wb, qq)
            if pid <= 0 or fid < 0 or fid > len(weapon.assets_file.externals):
                continue
            try:
                candidate = resolve(weapon, {'m_FileID': fid, 'm_PathID': pid})
            except (KeyError, ValueError, FileNotFoundError):
                continue
            if candidate is not None and candidate.type.name in ('AnimatorController', 'AnimatorOverrideController'):
                candidates.append((candidate, label.decode(), qq))
        require(len(candidates) == 1, 'Ambiguous or missing weapon animation controller '+entry['enemy_id'])
        controller, holder, offset = candidates[0]
        controller_key = identity(controller)
        controllers[controller_key] = controller
        descriptor = dict(controller_path_id=controller.path_id, controller_asset_file=controller_key[0], controller_name=object_name(controller), weapon_holder_name=holder, serialized_pointer_offset=offset)
        row['weapon_animation_controller_candidates'] = [descriptor]
        row['combat_controller_resolution'] = 'verified_field_prefix'
        for renderer in row['renderers']:
            renderer['combat_profile_fingerprint'] = digest({'rig_profile_fingerprint': renderer['rig_profile_fingerprint'], 'controller_identity': controller_key})
        row['renderer_count'] = len(row['renderers'])
        row['classification'] = 'skinned' if row['renderers'] else 'needs_rigid_or_other_inspection'
        rows.append(row)

    controller_rows = []
    for key, controller in sorted(controllers.items()):
        ct = tree(controller)
        tos = dict(ct.get('m_TOS', []))
        states = []
        for index, sm in enumerate(ct.get('m_Controller', {}).get('m_StateMachineArray', [])):
            for st in sm['data']['m_StateConstantArray']:
                item = st['data']; h = item['m_FullPathID']
                states.append(dict(state_machine_index=index, full_path=tos.get(h), short_name=tos.get(item['m_NameID']), full_path_hash=h if h < 2**31 else h-2**32, loop=item.get('m_Loop')))
        controller_rows.append(dict(asset_file=key[0], path_id=key[1], name=object_name(controller), type=controller.type.name, states=states, validation='serialized_metadata_not_animation_tested'))
    profile_groups = collections.defaultdict(list)
    for row in rows:
        for renderer in row['renderers']:
            profile_groups[renderer['combat_profile_fingerprint']].append((row, renderer))
    profiles = [dict(combat_profile_fingerprint=fp, rig_profile_fingerprint=group[0][1]['rig_profile_fingerprint'], controller=group[0][0]['weapon_animation_controller_candidates'][0]['controller_name'], joint_count=group[0][1]['joint_count'], enemy_ids=sorted({r['enemy_id'] for r, rr in group}), renderer_path_ids=sorted({rr['renderer_path_id'] for r, rr in group}), validation='requires_original_asset_and_live_checks') for fp, group in sorted(profile_groups.items())]
    mapped_ids = {rr['renderer_path_id'] for r in rows for rr in r['renderers']}
    unmapped = [r for r in inv['renderers'] if r['renderer_path_id'] not in mapped_ids]
    for renderer in unmapped:
        owner = asset.objects[renderer['renderer_path_id']]
        go = ref(owner, 'm_GameObject')
        seen = set()
        component_evidence = []
        while go and identity(go) not in seen:
            seen.add(identity(go))
            components = [resolve(go, c.get('component', c)) for c in tree(go).get('m_Component', [])]
            for component in components:
                if component is not None and component.type.name == 'MonoBehaviour':
                    try:
                        label = class_name(component)
                    except FileNotFoundError:
                        label = None
                    if label:
                        component_evidence.append(dict(component_class=label, ancestor_game_object=object_name(go), component_path_id=component.path_id))
            transform = next((c for c in components if c is not None and c.type.name in ('Transform', 'RectTransform')), None)
            parent = ref(transform, 'm_Father') if transform else None
            go = ref(parent, 'm_GameObject') if parent else None
        renderer['ancestor_component_classes'] = component_evidence
        renderer['enemy_db_mapping_status'] = 'not_referenced_by_any_nonnull_vanilla_enemy_row'
        renderer['validated_role'] = 'unknown'
        renderer['role_hints'] = ['player_controller_association'] if any(a['controller_name'] == 'playerOverworldController' for a in renderer['animators']) else []
    output = dict(source_file=str(source), source_sha256=sha(source), database_sources=[dict(file=str(source.parent/identity(o)[0]), path_id=o.path_id, sha256=sha(source.parent/identity(o)[0])) for o in (db, db_copy)], assembly_sha256=sha(assembly), method='Verified serialized field-prefix recognition; exact enum/header reconciliation and independent DB-copy crosscheck; unique weapon controller resolution. Not a full arbitrary-version deserializer.', summary=dict(serialized_array_count=count, recognized_enemy_rows=len(rows), null_enemy_asset_row_count=len(nulls), unresolved_enemy_rows=0, distinct_cel_prefabs=len({r['cel_path_id'] for r in rows}), distinct_skinned_renderer_ids=len(mapped_ids), distinct_rig_profiles=len({rr['rig_profile_fingerprint'] for r in rows for rr in r['renderers']}), distinct_rig_and_weapon_controller_profiles=len(profiles), distinct_combat_controllers=len(controllers), multipart_enemy_row_count=sum(len(r['renderers']) > 1 for r in rows), unmapped_renderers=len(unmapped), unmapped_character_renderer_candidates=sum(bool(r['character_event_listeners']) for r in unmapped)), enemies=rows, null_enemy_asset_rows=nulls, combat_controllers=controller_rows, combat_profiles=profiles, unmapped_renderers=unmapped, limitations=['Metadata coverage is not runtime support. Profiles still require original custom geometry and visual animation checks.', 'Equal exact bind poses/topology/controller do not check renderer transforms, runtime overrides, or multipart behavior.', 'Unmapped renderers remain accounted for; players, NPCs, equipment, previews and unused templates require positive role mapping. Names alone do not classify them.', 'Source IDs/fingerprints are specific to the hashed local installation. No proprietary geometry or animation curves exported.'])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2)+'\n')
    print(json.dumps(output['summary'], indent=2))


if __name__ == '__main__':
    main()
