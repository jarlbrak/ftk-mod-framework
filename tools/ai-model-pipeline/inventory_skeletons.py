#!/usr/bin/env python3
"""Inventory serialized rig candidates. Metadata only; does not prove runtime support."""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def file_hash(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()


def inventory(assets):
    assets = assets.resolve()
    env = UnityPy.load(str(assets))
    source = next(f for f in env.files.values() if Path(f.name).name == assets.name)
    cache = {}
    touched = {assets.name}
    def identity(obj):
        return (obj.assets_file.name, obj.path_id)
    def tree(obj):
        if obj is None: return {}
        key = identity(obj)
        if key not in cache: cache[key] = obj.read_typetree(check_read=False)
        return cache[key]
    def resolve(owner, pointer):
        if not pointer or not pointer.get('m_PathID'): return None
        # UnityPy deref follows the owner's actual externals table, not a fixed fileID.
        result = PPtr(m_FileID=pointer['m_FileID'], m_PathID=pointer['m_PathID'], assetsfile=owner.assets_file).deref()
        touched.add(Path(result.assets_file.name).name)
        return result
    def ref(owner, field): return resolve(owner, tree(owner).get(field))
    def name(obj): return tree(obj).get('m_Name') if obj else None
    def components(go):
        return [resolve(go, c.get('component', c)) for c in tree(go).get('m_Component', [])]
    def ancestors(go):
        result = []; seen = set()
        while go and identity(go) not in seen:
            seen.add(identity(go)); result.append(go)
            transform = next((c for c in components(go) if c and c.type.name in ('Transform','RectTransform')), None)
            parent = ref(transform, 'm_Father') if transform else None
            go = ref(parent, 'm_GameObject') if parent else None
        return result
    def bone_name(bone): return name(ref(bone, 'm_GameObject')) if bone else None
    rows = []
    for obj in list(source.objects.values()):
        if obj.type.name != 'SkinnedMeshRenderer': continue
        row = dict(renderer_path_id=obj.path_id, validation='discovery_only', errors=[])
        try:
            t = tree(obj); go = ref(obj,'m_GameObject'); chain = ancestors(go)
            mesh = ref(obj,'m_Mesh'); mt = tree(mesh)
            bones = [resolve(obj,p) for p in t.get('m_Bones',[])]
            names = [bone_name(b) for b in bones]
            keys = {identity(b) for b in bones if b}
            parents = []
            for b in bones:
                parent = ref(b,'m_Father') if b else None; seen = set()
                while parent and identity(parent) not in keys and identity(parent) not in seen:
                    seen.add(identity(parent)); parent = ref(parent,'m_Father')
                parents.append(bone_name(parent) if parent and identity(parent) in keys else None)
            controllers = []; listeners = []
            for ancestor in chain:
                for c in components(ancestor):
                    if not c: continue
                    if c.type.name == 'Animator':
                        controller = ref(c,'m_Controller')
                        controllers.append(dict(animator_path_id=c.path_id,game_object=name(ancestor),
                            controller_path_id=controller.path_id if controller else None,
                            controller_asset_file=Path(controller.assets_file.name).name if controller else None,
                            controller_name=name(controller)))
                    elif c.type.name == 'MonoBehaviour':
                        try:
                            script = ref(c,'m_Script')
                            if tree(script).get('m_ClassName') == 'CharacterEventListener':
                                listeners.append(dict(component_path_id=c.path_id,game_object=name(ancestor)))
                        except Exception as exc:
                            row['errors'].append(f'MonoScript resolution {c.path_id}: {type(exc).__name__}: {exc}')
            bind = mt.get('m_BindPose',[])
            topology = sorted(zip(names,parents),key=str)
            canonical_bind = sorted(zip(names,bind),key=str)
            valid = bool(names) and len(names)==len(bind) and None not in names and len(set(names))==len(names)
            row.update(mesh_path_id=mesh.path_id if mesh else None,
                       mesh_asset_file=Path(mesh.assets_file.name).name if mesh else None,
                       mesh_name=mt.get('m_Name'),renderer_name=name(go),ancestor_names=[name(g) for g in chain],
                       joint_count=len(names),bone_names=names,nearest_skinned_bone_parent_names=parents,
                       duplicate_bone_names=sorted(n for n,c in collections.Counter(names).items() if c>1 and n),
                       unresolved_bone_count=sum(b is None for b in bones),bindpose_count=len(bind),
                       topology_fingerprint=digest(topology),bindpose_fingerprint=digest(canonical_bind),
                       rig_profile_fingerprint=digest([topology,canonical_bind]) if valid else None,
                       animators=controllers,character_event_listeners=listeners,
                       classification='character_renderer_candidate' if listeners else 'unclassified_skinned_renderer')
        except Exception as exc:
            row['errors'].append(f'{type(exc).__name__}: {exc}')
        rows.append(row)
    groups = collections.defaultdict(list)
    for row in rows:
        if row.get('character_event_listeners') and row.get('rig_profile_fingerprint'):
            groups[row['rig_profile_fingerprint']].append(row)
    profiles = []
    for key, members in sorted(groups.items()):
        profiles.append(dict(rig_profile_fingerprint=key, topology_fingerprint=members[0]['topology_fingerprint'],
            bindpose_fingerprint=members[0]['bindpose_fingerprint'],joint_count=members[0]['joint_count'],
            renderer_path_ids=sorted(m['renderer_path_id'] for m in members),
            representative_renderer_path_id=min(m['renderer_path_id'] for m in members),
            mesh_names=sorted({m['mesh_name'] or '' for m in members}),
            controller_names=sorted({a['controller_name'] or '' for m in members for a in m['animators']}),
            validation='discovery_only'))
    hashes = {n:file_hash(assets.parent/n) for n in sorted(touched) if (assets.parent/n).is_file()}
    return dict(source_file=str(assets),source_sha256=file_hash(assets),resolved_asset_sha256=hashes,
        scope='SkinnedMeshRenderers serialized directly in the selected assets file; external pointers resolved for metadata.',
        grouping='Exact per-name bind matrices plus nearest included ancestor topology; fingerprints are order-independent, not animation compatibility proof.',
        limitations=['Discovery only; includes players, NPCs, portraits and accessories, not an enemy database mapping.',
                     'External renderers, bundles, rigid MeshRenderers and separately mounted weapons are not enumerated.',
                     'Same bind/topology fingerprint does not prove matching animation controllers or runtime renderer selection.',
                     'No spawn, mesh replacement or animation was tested by this inventory.',
                     'Path IDs and fingerprints are build-specific. Missing references appear in per-renderer errors.'],
        summary=dict(skinned_renderer_count=len(rows),character_renderer_candidate_count=sum(bool(r.get('character_event_listeners')) for r in rows),
                     candidate_rig_profile_count=len(profiles),renderers_with_errors=sum(bool(r['errors']) for r in rows)),
        candidate_profiles=profiles,renderers=rows)

if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--assets',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args();out=inventory(a.assets)
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(out,indent=2)+'\n')
    print(json.dumps(out['summary'],indent=2))
