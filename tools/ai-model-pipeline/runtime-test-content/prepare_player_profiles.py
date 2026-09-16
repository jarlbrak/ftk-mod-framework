#!/usr/bin/env python3
"""Stage original player rig probes; does not deploy or launch a game."""
import argparse
import hashlib
import json
import re
from pathlib import Path


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    for name in ('classification', 'probes', 'probe-root', 'output-dir', 'base-class', 'default-skin-type'):
        ap.add_argument('--' + name, required=True, type=str if name in ('base-class', 'default-skin-type') else Path)
    choice = ap.add_mutually_exclusive_group(required=True)
    choice.add_argument('--skinset', action='append')
    choice.add_argument('--all-skinsets', action='store_true')
    ap.add_argument('--starting-armor', help='Optional exact native armor item key; plugin verifies equippability/type before class registration')
    a = ap.parse_args()
    try:
        if a.default_skin_type not in ('Female','Male','Undead','Cat','Demon','Fish','Goblin'):
            raise ValueError('Unknown default skin type')
        if not re.fullmatch('[a-zA-Z0-9_]+', a.base_class):
            raise ValueError('Exact native class enum name required')
        if a.starting_armor is not None and not re.fullmatch('[a-zA-Z][a-zA-Z0-9_]{0,119}', a.starting_armor):
            raise ValueError('Exact bounded native starting armor key required')
        if not any(p.name == 'scratch' for p in a.output_dir.absolute().parents):
            raise ValueError('Output staging directory must be under scratch')
        if a.output_dir.exists() or a.output_dir.is_symlink():
            raise ValueError('Refusing existing output directory')
        if any(p.is_symlink() for p in a.output_dir.absolute().parents):
            raise ValueError('Symlink output ancestors refused')
        c = json.loads(a.classification.read_text()); probes = json.loads(a.probes.read_text())
        if c['source_sha256'] != probes['source_sha256'] or sha(Path(c['source_file'])) != c['source_sha256']:
            raise ValueError('Source asset/probe fingerprint mismatch')
        by_skin = {s['skinset_id']: s for s in c['skinset_rows']}
        selected = sorted(by_skin) if a.all_skinsets else a.skinset
        if len(set(selected)) != len(selected) or not 1 <= len(selected) <= 128:
            raise ValueError('Repeated skinset or invalid profile count')
        by_rig = {}
        for p in probes['results']:
            rig=p['rig_profile_fingerprint']
            if rig in by_rig or p['status'] != 'calibration_export_pass':
                raise ValueError('Ambiguous or failed probe')
            by_rig[rig] = p
        profiles=[]; cases=[]; assets={}
        for name in selected:
            skin=by_skin[name]; assignments=[]; evidence=[]
            renderers=[r for r in c['renderers'] if name in r['skinset_avatar_references']]
            for r in renderers:
                ancestors=r['ancestor_names']
                if ancestors[-1] != skin['avatar_prefab_name'] or not any(
                    e['component_class']=='CharacterEventListener' and e['component_path_id']==skin['avatar_cel_path_id']
                    for e in r['ancestor_component_evidence']):
                    raise ValueError('Unverified player CEL ancestry')
                parts=list(reversed(ancestors[:-1])); path='/'.join(parts) or '.'
                if any(not x or x in ('.','..') or '/' in x or '\\' in x for x in parts):
                    raise ValueError('Unrepresentable renderer path')
                probe=by_rig[r['rig_profile_fingerprint']]; rep=probe['renderer_path_id']
                files={'glbFile':(a.probe_root/str(rep)/'rig-probe.glb',f'probe_{rep}.glb'),
                    'textureFile':(a.probe_root/str(rep)/'probe-palette.png','probe_palette.png')}
                assignment={'rendererPath':path}
                for field,(source,dest) in files.items():
                    digest=sha(source)
                    if field=='glbFile' and digest != probe['glb_sha256']:
                        raise ValueError('Probe hash mismatch')
                    if dest in assets and assets[dest]['sha256']!=digest:
                        raise ValueError('Conflicting asset basename')
                    assets[dest]={'source':str(source.resolve()),'sha256':digest}
                    assignment[field]=dest
                assignments.append(assignment)
                evidence.append({'rendererPath':path,'rendererPathId':r['renderer_path_id'],
                    'rigProfile':r['rig_profile_fingerprint'],'representativeRendererPathId':rep})
            if not 1<=len(assignments)<=16 or len({x['rendererPath'] for x in assignments})!=len(assignments):
                raise ValueError('Missing or ambiguous player renderer set: '+name)
            key='ftkmf_modeltest_player_'+name.lower()
            profiles.append({'key':key,'baseClass':a.base_class,'displayName':'Player rig probe: '+name,
                'skinset':name,'defaultSkinType':a.default_skin_type,'renderers':assignments})
            if a.starting_armor is not None:profiles[-1]['startingArmor']=a.starting_armor
            cases.append({'key':key,'skinset':name,'classification':'original_calibration_geometry',
                'fixture':'all_seven_skin_type_slots_same_skinset','nativeClass':a.base_class,
                'avatarCelPathId':skin['avatar_cel_path_id'],'armorComponentPathId':skin['armor_component_path_id'],
                'renderers':evidence,'validation':'assembled_avatar_and_combat_pending'})
            if a.starting_armor is not None:
                cases[-1]['startingArmor']={'key':a.starting_armor,'count':1,'mode':'appended_to_custom_class_start_items',
                    'validation':'runtime_native_item_type_preflight_pending'}
        encoded=(json.dumps({'version':1,'profiles':profiles},indent=2)+'\n').encode()
        manifest={'version':1,'sourceSha256':c['source_sha256'],'classificationSha256':sha(a.classification),
            'probesSha256':sha(a.probes),'profilesSha256':hashlib.sha256(encoded).hexdigest(),'assets':assets,'cases':cases}
        a.output_dir.mkdir(parents=True); (a.output_dir/'assets').mkdir()
        for dest,item in assets.items():
            target=a.output_dir/'assets'/dest;target.write_bytes(Path(item['source']).read_bytes())
            if sha(target)!=item['sha256']:raise ValueError('Copied asset hash mismatch')
        (a.output_dir/'model-test-player-profiles.json').write_bytes(encoded)
        (a.output_dir/'provenance.json').write_text(json.dumps(manifest,indent=2)+'\n')
        print(json.dumps({'profiles':len(profiles),'assets':len(assets),'profilesSha256':manifest['profilesSha256']}))
    except (ValueError, KeyError, OSError, TypeError) as e:
        ap.exit(1,'Player profile preparation refused: '+str(e)+'\n')


if __name__=='__main__':main()
