#!/usr/bin/env python3
"""Prepare original calibration/artwork assets and production-spawn test profiles.

Never copies native meshes or starts/deploys a game. Python standard library only.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def read(path):
    return json.loads(path.read_text())


def fingerprint(value):
    if not isinstance(value, str) or not re.fullmatch('[a-f0-9]{64}', value):
        raise ValueError('Invalid SHA-256 fingerprint')
    return value


def renderer_path(row, renderer):
    names = renderer['ancestor_names']
    # The verified inventory stores renderer-to-root ancestry, including the root.
    # Do not trim at the first matching name: the gladiator root and body share a name.
    if not names or names[-1] != row['prefab_name'] or names[0] != renderer['renderer_name']:
        raise ValueError(f"Unresolved prefab ancestry for {row['enemy_id']}")
    cels = renderer['character_event_listeners']
    if not any(c['component_path_id'] == row['cel_path_id'] and c['game_object'] == row['prefab_name'] for c in cels):
        raise ValueError(f"CEL root is not verified for {row['enemy_id']}")
    parts = list(reversed(names[:-1]))
    if any(not x or x in ('.', '..') or '/' in x or '\\' in x for x in parts):
        raise ValueError('Unrepresentable renderer path')
    return '/'.join(parts) or '.'


def combined(renderers):
    ordered = sorted((r['rendererPath'], r['combatProfile']) for r in renderers)
    if len(ordered) == 1:
        return ordered[0][1]
    # Domain prefix + canonical JSON of sorted [CEL path, combat fingerprint] pairs.
    return hashlib.sha256(('ftk-multipart-combat-v1\n' + json.dumps(ordered, separators=(',', ':'))).encode()).hexdigest()


def prepare(args):
    for target in (args.models_dir, args.output, args.manifest):
        if not any(parent.name == 'scratch' for parent in target.absolute().parents):
            raise ValueError('Output staging paths must be under an explicit scratch directory')
        if any(parent.is_symlink() for parent in (target.absolute(), *target.absolute().parents)):
            raise ValueError('Symlink staging paths are refused')
    for output in (args.output, args.manifest):
        if output.exists() or output.is_symlink():
            raise ValueError(f'Refusing existing output: {output}')
    if args.output.resolve() == args.manifest.resolve():
        raise ValueError('Profile and manifest output must differ')
    mapping, probes = read(args.mapping), read(args.probes)
    source_hash = fingerprint(mapping['source_sha256'])
    if probes['source_sha256'] != source_hash:
        raise ValueError('Mapping and probes refer to different game assets')
    source = Path(mapping['source_file'])
    if sha(source) != source_hash:
        raise ValueError('Mapped source game asset hash has changed')
    by_rig = {}
    for probe in probes['results']:
        rig = fingerprint(probe['rig_profile_fingerprint'])
        if probe['status'] != 'calibration_export_pass':
            raise ValueError('Probe generation contains failures')
        if rig in by_rig:
            raise ValueError('Ambiguous duplicate probe rig fingerprint')
        by_rig[rig] = probe
    profiles, provenance, assets = [], [], {}

    def asset(path, basename, expected=None):
        if not re.fullmatch(r'[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}\.(glb|png)', basename):
            raise ValueError('Invalid asset basename: ' + basename)
        digest = sha(path)
        if expected and digest != expected:
            raise ValueError('Asset hash mismatch: ' + str(path))
        if basename in assets and assets[basename]['sha256'] != digest:
            raise ValueError('Conflicting output asset basename: ' + basename)
        assets[basename] = {'source': str(path.resolve()), 'sha256': digest, 'bytes': path.stat().st_size}
        return basename

    seen_enemies = set()
    for row in mapping['enemies']:
        enemy = row['enemy_id']
        if enemy in seen_enemies or not re.fullmatch('[A-Za-z0-9_]+', enemy):
            raise ValueError('Invalid or duplicated native enemy ID')
        seen_enemies.add(enemy)
        assignments, renderer_evidence = [], []
        for renderer in row['renderers']:
            rig = fingerprint(renderer['rig_profile_fingerprint'])
            if rig not in by_rig:
                raise ValueError('No exact rig probe for ' + enemy + ': ' + rig)
            probe = by_rig[rig]
            rep = probe['renderer_path_id']
            folder = args.probe_root / str(rep)
            path = renderer_path(row, renderer)
            assignments.append({'rendererPath': path,
                'glbFile': asset(folder / 'rig-probe.glb', f'probe_{rep}.glb', probe['glb_sha256']),
                'textureFile': asset(folder / 'probe-palette.png', 'probe_palette.png')})
            renderer_evidence.append({'rendererPath': path, 'rendererPathId': renderer['renderer_path_id'],
                'representativeRendererPathId': rep, 'rigProfile': rig,
                'combatProfile': fingerprint(renderer['combat_profile_fingerprint'])})
        if not 1 <= len(assignments) <= 16 or len({a['rendererPath'] for a in assignments}) != len(assignments):
            raise ValueError('Missing or ambiguous renderer assignments: ' + enemy)
        key = 'ftkmf_modeltest_probe_' + enemy.lower()
        profile = {'key': key, 'baseEnemy': enemy, 'displayName': 'Rig probe: ' + enemy,
            'combatProfile': combined(renderer_evidence), 'renderers': assignments}
        profiles.append(profile)
        provenance.append({'key': key, 'nativeEnemyId': enemy, 'classification': 'original_calibration_geometry',
            'validation': 'production_spawn_pending', 'prefabPathId': row['prefab_game_object_path_id'],
            'celPathId': row['cel_path_id'], 'weaponPathId': row['weapon_path_id'],
            'controllers': row['weapon_animation_controller_candidates'], 'renderers': renderer_evidence})

    if args.examples:
        example_doc = read(args.examples)
        if example_doc['version'] != 1:
            raise ValueError('Unknown example schema version')
        native = {r['enemy_id']: r for r in mapping['enemies']}
        for profile in example_doc['profiles']:
            row = native[profile['baseEnemy']]
            available = {renderer_path(row, r): r for r in row['renderers']}
            evidence = []
            for renderer in profile['renderers']:
                target = available[renderer['rendererPath']]
                evidence.append({'rendererPath': renderer['rendererPath'],
                    'rendererPathId': target['renderer_path_id'], 'rigProfile': target['rig_profile_fingerprint'],
                    'combatProfile': target['combat_profile_fingerprint']})
                for field in ('glbFile', 'textureFile'):
                    basename = renderer.get(field)
                    if basename is None:
                        continue
                    found = [directory / basename for directory in args.example_assets if (directory / basename).is_file()]
                    if len(found) != 1:
                        raise ValueError('Expected exactly one original example source for ' + basename)
                    asset(found[0], basename)
            if profile['combatProfile'] != combined(evidence):
                raise ValueError('Example combat profile differs from verified mapping')
            profiles.append(profile)
            provenance.append({'key': profile['key'], 'nativeEnemyId': row['enemy_id'],
                'classification': 'original_creature_artwork', 'validation': 'production_spawn_pending',
                'prefabPathId': row['prefab_game_object_path_id'], 'celPathId': row['cel_path_id'],
                'weaponPathId': row['weapon_path_id'], 'controllers': row['weapon_animation_controller_candidates'],
                'renderers': evidence})
    keys = [p['key'] for p in profiles]
    if len(set(keys)) != len(keys) or not 1 <= len(keys) <= 512:
        raise ValueError('Duplicate keys or profile count outside 1..512')
    for key in keys:
        if not re.fullmatch('ftkmf_modeltest_[a-z0-9_]{1,64}', key):
            raise ValueError('Invalid profile key')
    # Finish all validations before any writes. Existing assets are accepted only byte-identical.
    for name, item in assets.items():
        destination = args.models_dir / name
        if destination.is_symlink() or (destination.exists() and sha(destination) != item['sha256']):
            raise ValueError('Refusing conflicting existing asset: ' + str(destination))
    args.models_dir.mkdir(parents=True, exist_ok=True)
    for name, item in sorted(assets.items()):
        destination = args.models_dir / name
        if not destination.exists():
            with destination.open('xb') as stream:
                stream.write(Path(item['source']).read_bytes())
        if sha(destination) != item['sha256']:
            raise ValueError('Copied asset verification failed')
    document = {'version': 1, 'profiles': profiles}
    encoded = (json.dumps(document, indent=2) + '\n').encode()
    if len(encoded) > 1024 * 1024:
        raise ValueError('Profile JSON exceeds runtime 1 MiB limit')
    manifest = {'version': 1, 'sourceSha256': source_hash, 'assemblySha256': mapping['assembly_sha256'],
        'mapping': {'path': str(args.mapping.resolve()), 'sha256': sha(args.mapping)},
        'probes': {'path': str(args.probes.resolve()), 'sha256': sha(args.probes)},
        'profilesSha256': hashlib.sha256(encoded).hexdigest(), 'assets': assets,
        'cases': provenance, 'summary': {'nativeEnemyCases': len(seen_enemies),
            'artworkCases': len(profiles)-len(seen_enemies), 'totalCases': len(profiles),
            'uniqueNativeRigProfiles': len({r['rigProfile'] for p in provenance for r in p['renderers']}),
            'uniqueRendererCombatProfiles': len({r['combatProfile'] for p in provenance for r in p['renderers']}),
            'assetFiles': len(assets)}, 'limitation': 'Prepared assets and metadata only; no live support claims.'}
    for output in (args.output, args.manifest):
        output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as stream:
        stream.write(encoded)
    with args.manifest.open('x') as stream:
        json.dump(manifest, stream, indent=2)
        stream.write('\n')
    return manifest['summary']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('mapping', 'probes', 'probe-root', 'models-dir', 'output', 'manifest'):
        parser.add_argument('--' + flag, type=Path, required=True)
    parser.add_argument('--examples', type=Path)
    parser.add_argument('--example-assets', type=Path, action='append', default=[])
    args = parser.parse_args()
    try:
        print(json.dumps(prepare(args), indent=2))
    except (ValueError, KeyError, OSError, TypeError) as error:
        parser.exit(1, f'Profile preparation refused: {error}\n')


if __name__ == '__main__':
    main()
