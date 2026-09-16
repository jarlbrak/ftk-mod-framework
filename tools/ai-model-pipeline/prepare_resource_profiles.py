#!/usr/bin/env python3
"""Stage original probes for native resource-prefab test clones; never deploy or run FTK."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from prepare_runtime_profiles import read, sha, fingerprint


def resource_combat_profile(row):
    """Return the exact resource-prefab controller/rig route fingerprint."""
    rig = fingerprint(row['rig_profile_fingerprint'])
    controller = row.get('weapon_controller')
    renderer_path = row.get('renderer_path')
    if not isinstance(controller, dict) or not isinstance(renderer_path, str) or not renderer_path:
        raise ValueError('Invalid resource-prefab controller or renderer route')
    payload = {'rig': rig, 'controller': controller, 'rendererPath': renderer_path}
    return hashlib.sha256(
        ('ftk-resource-rig-controller-v1\n' + json.dumps(
            payload, sort_keys=True, separators=(',', ':'))).encode()
    ).hexdigest()


def prepare(args):
    targets = [args.output, args.manifest, args.models_dir]
    for target in targets:
        if not any(p.name == 'scratch' for p in target.absolute().parents):
            raise ValueError('Outputs must be staged under scratch')
        if any(p.is_symlink() for p in [target.absolute(), *target.absolute().parents]):
            raise ValueError('Symlink output path refused')
    if args.output.resolve() == args.manifest.resolve():
        raise ValueError('Profile and manifest paths must differ')
    for target in targets[:2]:
        if target.exists():
            raise ValueError('Refusing existing output: ' + str(target))
    preflight, resources, inventory, probes = map(read, [args.preflight, args.resources, args.inventory, args.probes])
    if inventory['source_sha256'] != probes['source_sha256'] or sha(Path(inventory['source_file'])) != inventory['source_sha256']:
        raise ValueError('Native inventory/probe source hash mismatch')
    reference = {r['resource_load_path']: r for r in resources['references']}
    renderers = {r['renderer_path_id']: r for r in inventory['renderers']}
    by_rig = {}
    for p in probes['results']:
        rig = fingerprint(p['rig_profile_fingerprint'])
        if rig in by_rig or p['status'] != 'calibration_export_pass':
            raise ValueError('Ambiguous or failed original probe')
        by_rig[rig] = p
    selected, excluded, assets, profiles, cases = {}, [], {}, [], []
    for row in sorted(preflight['rows'], key=lambda r: (r['rig_profile_fingerprint'], r['resource_load_path'])):
        path = row['resource_load_path']
        if not re.fullmatch(r'[A-Za-z0-9_-]{1,120}', path):
            raise ValueError('Invalid exact resource path')
        if row['base_enemy_id'] == 'krakenHead' or row['new_missing_vs_base']:
            excluded.append({'resourcePrefab': path, 'reason': 'Pending controller path compatibility; excluded from test batch', 'preflight': row})
            continue
        if not row['has_Root_M'] or not row['weapon_holder_present'] or len(row['cel_ids']) != 1 or not {'Animator','CharacterEventListener'}.issubset(row['root_components']):
            raise ValueError('Missing root/CEL/holder preflight: ' + path)
        resource = reference[path]
        if resource['object_type'] != 'GameObject' or resource['object_id'] != row['game_object_id']:
            raise ValueError('Resource mapping identity mismatch')
        renderer = renderers[row['renderer_id']]
        rig = fingerprint(row['rig_profile_fingerprint'])
        if renderer['rig_profile_fingerprint'] != rig or renderer['enemy_db_referenced'] or renderer['rig_profile_used_by_enemy']:
            raise ValueError('Expected exact orphan renderer rig')
        names = renderer['ancestor_names']
        if names[-1] != resource['target']['name'] or '/'.join(reversed(names[:-1])) != row['renderer_path']:
            raise ValueError('CEL-relative ancestry mismatch')
        # One representative case per exact rig group, deterministic path order.
        # Record equivalent alternatives without claiming controller equivalence.
        if rig in selected:
            selected[rig]['alternatives'].append(row)
            continue
        selected[rig] = {'row': row, 'alternatives': []}
    if len(selected) != 9:
        raise ValueError('Expected exactly nine preflight candidate rig groups; got ' + str(len(selected)))
    for rig, choice in sorted(selected.items()):
        row = choice['row']; probe = by_rig[rig]; rep = probe['renderer_path_id']; folder = args.probe_root / str(rep)
        for source, name, expected in [(folder/'rig-probe.glb', f'resource_probe_{rep}.glb', probe['glb_sha256']), (folder/'probe-palette.png', 'resource_probe_palette.png', None)]:
            digest = sha(source)
            if expected and digest != expected:
                raise ValueError('Original GLB hash mismatch')
            if name in assets and assets[name]['sha256'] != digest:
                raise ValueError('Conflicting asset basename')
            assets[name] = {'source': str(source.resolve()), 'sha256': digest, 'bytes': source.stat().st_size}
        # Domain-separated fixture metadata, not a promise of controller compatibility.
        combo = resource_combat_profile(row)
        key = 'ftkmf_modeltest_resource_' + row['resource_load_path'].replace('/','_').lower()
        profile = {'key':key, 'baseEnemy':row['base_enemy_id'], 'resourcePrefab':row['resource_load_path'], 'displayName':'Resource probe: '+row['resource_load_path'], 'combatProfile':combo, 'renderers':[{'rendererPath':row['renderer_path'],'glbFile':f'resource_probe_{rep}.glb','textureFile':'resource_probe_palette.png'}]}
        profiles.append(profile);cases.append({'key':key,'classification':'original_calibration_geometry','validation':'offline_preflight_only_runtime_decode_and_production_spawn_pending','preflight':row,'rigProfile':rig,'representativeProbeRendererId':rep,'unselectedSameRigAlternatives':choice['alternatives']})
    if len({p['key'] for p in profiles}) != len(profiles):
        raise ValueError('Duplicate fixture key')
    for name,a in assets.items():
        dest=args.models_dir/name
        if dest.is_symlink() or (dest.exists() and sha(dest)!=a['sha256']):
            raise ValueError('Conflicting existing asset: '+str(dest))
    encoded=(json.dumps({'version':1,'profiles':profiles},indent=2)+'\n').encode()
    manifest={'version':1,'inputs':[{'path':str(x.resolve()),'sha256':sha(x)} for x in [args.preflight,args.resources,args.inventory,args.probes]],'sourceSha256':inventory['source_sha256'],'profilesSha256':hashlib.sha256(encoded).hexdigest(),'assets':assets,'cases':cases,'excluded':excluded,'summary':{'candidateCases':len(cases),'assetFiles':len(assets)},'limitations':['Root Animator presence does not prove controller compatibility.','Only original probe geometry copied; native assets referenced by exact Resources path.','No game action, deployment, runtime decode, production spawn or visual acceptance.']}
    args.models_dir.mkdir(parents=True,exist_ok=True)
    for name,a in assets.items():
        dest=args.models_dir/name
        if not dest.exists():
            with dest.open('xb') as stream:stream.write(Path(a['source']).read_bytes())
        if sha(dest)!=a['sha256']:raise ValueError('Copied asset hash mismatch')
    for target,data in [(args.output,encoded),(args.manifest,(json.dumps(manifest,indent=2)+'\n').encode())]:
        target.parent.mkdir(parents=True,exist_ok=True)
        with target.open('xb') as stream:stream.write(data)
    return manifest['summary']


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['preflight','resources','inventory','probes','probe-root','models-dir','output','manifest']:
        p.add_argument('--'+name,type=Path,required=True)
    args=p.parse_args()
    try: print(json.dumps(prepare(args),indent=2))
    except (OSError,ValueError,KeyError,TypeError) as e:p.exit(1,str(e)+'\n')


if __name__=='__main__':main()
