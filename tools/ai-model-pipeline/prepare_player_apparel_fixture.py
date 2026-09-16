#!/usr/bin/env python3
"""Prepare an isolated Blacksmith apparel calibration fixture; no game actions."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    for name in ('catalog', 'existing-models', 'probes', 'probe-root', 'models-dir', 'output', 'manifest'):
        ap.add_argument('--' + name, required=True, type=Path)
    ap.add_argument('--starting-armor', choices=['armorCloth1'], help='Explicit native item59 starter fixture; omitted by default.')
    args = ap.parse_args()
    scratch = Path(__file__).resolve().parents[2] / 'scratch'
    targets = [args.output, args.manifest, args.models_dir]
    resolved = []
    for target in targets:
        absolute = target.absolute()
        for component in [absolute] + list(absolute.parents):
            if component.is_symlink():
                ap.error('Refusing symlink output component: ' + str(component))
        final = absolute.resolve()
        if scratch not in final.parents:
            ap.error('Outputs must be strictly below this repository scratch directory.')
        resolved.append(final)
    if len(set(resolved)) != len(resolved):
        ap.error('Catalog, manifest and models directory must be distinct paths.')
    if resolved[2] in resolved[0].parents or resolved[2] in resolved[1].parents:
        ap.error('Catalog and manifest must be outside the models directory.')
    if args.output.exists() or args.manifest.exists():
        ap.error('Refusing existing output or manifest.')
    catalog = json.loads(args.catalog.read_text())
    candidates = [p for p in catalog['profiles'] if p['key'] == 'ftkmf_modeltest_player_blacksmith_female']
    if len(candidates) != 1:
        ap.error('Expected exactly one existing Blacksmith Female profile.')
    profile = copy.deepcopy(candidates[0])
    if profile['skinset'] != 'blacksmith_Female' or profile['baseClass'] != 'blacksmith' or len(profile['renderers']) != 3:
        ap.error('Expected verified Blacksmith Female three-renderer baseline.')
    profile['key'] = 'ftkmf_modeltest_player_blacksmith_female_apparel'
    profile['displayName'] = 'Blacksmith Female apparel calibration'
    profile.pop('startingArmor', None)
    if args.starting_armor:
        profile['startingArmor'] = args.starting_armor
    if any(p['key'] == profile['key'] for p in catalog['profiles']):
        ap.error('Apparel fixture key already exists.')
    inventory = json.loads(args.probes.read_text())
    assignments = [(121248, 'armorBlacksmithF(Clone)', 'armorBlacksmith'),
                   (121113, 'bootsBlacksmith(Clone)', 'bootsBlacksmith'),
                   (121211, 'armorGambesonF(Clone)', 'armorGambesonF')]
    assets = {}
    provenance = []
    for p in catalog['profiles']:
        for m in p['renderers'] + p.get('apparel', []):
            for field in ('glbFile', 'textureFile'):
                if m.get(field):
                    assets[m[field]] = args.existing_models / m[field]
    profile['apparel'] = []
    for rid, path, native_name in assignments:
        rows = [r for r in inventory['results'] if r['renderer_path_id'] == rid]
        if len(rows) != 1 or rows[0]['status'] != 'calibration_export_pass':
            ap.error('Missing exact original probe: ' + str(rid))
        row = rows[0]
        if row['joint_scope'] != 'weighted' or row['connections'] != 'none' or row['radius_scale'] != 6:
            ap.error('Expected weighted markers-only radius6 recipe.')
        glb = args.probe_root / str(rid) / 'rig-probe.glb'
        if sha(glb) != row['glb_sha256']:
            ap.error('Probe hash does not match inventory.')
        name = 'apparel_probe_' + str(rid) + '.glb'
        texture = 'apparel_palette_' + str(rid) + '.png'
        assets[name] = glb
        assets[texture] = args.probe_root / str(rid) / 'probe-palette.png'
        profile['apparel'].append(dict(rendererPath=path, expectedNativeMeshName=native_name, glbFile=name, textureFile=texture))
        provenance.append(dict(rendererPath=path, expectedNativeMeshName=native_name, sourceRendererId=rid,
                               rigProfileFingerprint=row['rig_profile_fingerprint'], glbSha256=sha(glb)))
    # Validate every source and destination before writing anything.
    for name, src in assets.items():
        if Path(name).name != name or not src.is_file():
            ap.error('Invalid or missing asset: ' + name)
        dst = args.models_dir / name
        if dst.exists() and sha(dst) != sha(src):
            ap.error('Existing asset differs: ' + str(dst))
    original = copy.deepcopy(catalog['profiles'])
    catalog['profiles'].append(profile)
    assert catalog['profiles'][:-1] == original
    args.models_dir.mkdir(parents=True, exist_ok=True)
    for name, src in assets.items():
        dst = args.models_dir / name
        if not dst.exists():
            shutil.copy2(src, dst)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x') as stream:
        stream.write(json.dumps(catalog, indent=2) + '\n')
    report = dict(classification='original calibration; runtime apparel validation pending',
                  inputCatalog=dict(path=str(args.catalog), sha256=sha(args.catalog)),
                  probeInventory=dict(path=str(args.probes), sha256=sha(args.probes)),
                  outputCatalog=dict(path=str(args.output), sha256=sha(args.output)),
                  unchangedProfiles=len(original), profiles=len(catalog['profiles']), fixture=profile,
                  apparelProvenance=provenance,
                  assets={name: sha(args.models_dir / name) for name in sorted(assets)},
                  equipmentFixture=('Explicit native armorCloth1 item59 appended to fresh custom-class starting items. Native auto-equips on start; unequip exercises default apparel, re-equip exercises Gambeson.' if args.starting_armor else 'No explicit startingArmor. Native inherited starting items remain; Gambeson assignment is conditional until item59 is owned and equipped naturally.'),
                  limits='No native geometry copied. No game launch, registration, apparel spawn, equipment rebuild or visual acceptance.')
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    with args.manifest.open('x') as stream:
        stream.write(json.dumps(report, indent=2) + '\n')
    print(json.dumps(dict(profiles=len(catalog['profiles']), assets=len(assets), sha256=sha(args.output))))


if __name__ == '__main__':
    main()
