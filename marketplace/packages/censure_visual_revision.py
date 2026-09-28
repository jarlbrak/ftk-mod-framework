"""Bounded Censure visual revisions; no model generation or runtime operations."""
import copy
import hashlib
import json
import os
import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = 'marketplace/packages/paladin/'
META = 'marketplace/packages/'
REVISION = META+'censure-visual.provenance.json'
LEDGERS = ('paladin-assets.provenance.json', 'display-framing.provenance.json',
           'nonweapon-icons.provenance.json')
ITEMS = {'paladin_'+part+'_censure' for part in
         ('armor', 'helmet', 'boots', 'hammer_1h', 'hammer_2h', 'shield')}
ICONS = {'paladin_'+part+'_censure' for part in ('armor', 'helmet', 'boots', 'shield')}
OATH = 'paladin_helmet_oathkeeper'
ROUTES = {'itemModels', 'displayModels', 'apparelModels', 'offHandModels'}


def encode(value): return (json.dumps(value, indent=2)+'\n').encode()
def sha(data): return hashlib.sha256(data).hexdigest()


def safe(root, relative):
    name = Path(relative)
    assert not name.is_absolute() and '..' not in name.parts
    path = root/name
    path.resolve().relative_to(root.resolve())
    assert not path.is_symlink()
    return path


def pinned(root, ref):
    path = safe(root, ref['path'])
    assert sha(path.read_bytes()) == ref['sha256'], ref['path']
    return path


def frozen(root, args):
    manifest, manifest_sha, freeze, freeze_sha = args
    path = Path(manifest).resolve(); path.relative_to(root/'scratch')
    seal = Path(freeze).resolve(); seal.relative_to(path.parent)
    assert sha(path.read_bytes()) == manifest_sha
    assert sha(seal.read_bytes()) == freeze_sha
    receipt = json.loads(seal.read_text())
    assert receipt['manifestSha256'] == manifest_sha
    assert receipt['files'][path.name] == manifest_sha
    for name, digest in receipt['files'].items():
        assert sha(safe(path.parent, name).read_bytes()) == digest, name
    return path, json.loads(path.read_text()), receipt


def visual_only(before, after):
    assert type(before) is type(after)
    if isinstance(before, dict):
        assert set(before) == set(after)
        for key in before:
            if key in ('model', 'texture'):
                path = Path(after[key])
                assert len(path.parts) == 2 and path.parts[0] == 'assets'
                assert path.suffix == ('.glb' if key == 'model' else '.png')
            else: visual_only(before[key], after[key])
    elif isinstance(before, list):
        assert len(before) == len(after)
        for a, b in zip(before, after): visual_only(a, b)
    else: assert before == after, 'Nonvisual field or native route changed'


def refs(value):
    if isinstance(value, list): return set().union(*(refs(v) for v in value))
    if not isinstance(value, dict): return set()
    found = set()
    for key, value in value.items():
        if key in ('model', 'texture', 'icon') and isinstance(value, str) and value.startswith('assets/'):
            found.add(value)
        else: found.update(refs(value))
    return found


def content_scope(before, after):
    assert set(before) == set(after)
    cleaned = copy.deepcopy(after)
    assert len(before['entries']) == len(after['entries'])
    for old, new, restored in zip(before['entries'], after['entries'], cleaned['entries']):
        assert old['id'] == new['id']
        if old['id'] in ITEMS:
            for route in ROUTES:
                assert (route in old) == (route in new)
                if route in old:
                    visual_only(old[route], new[route]); restored[route] = old[route]
    assert cleaned == before, 'Gameplay, icon, identity or unrelated item changed'


def exact_rows(rows, key):
    result = {row[key]: row for row in rows}
    assert len(result) == len(rows), 'Duplicate item identity'
    return result


def approval(root, ref, candidate):
    document = json.loads(pinned(root, ref).read_text())
    assert document['schema'] == 'ftkmf.censure-native-acceptance.v1'
    assert document['approved'] is True and document['authority'] == 'parent-native-review'
    assert document['baselineSha256'] == candidate['baselineSha256']
    assert document['deliveryManifestSha256s'] == sorted(d['manifestSha256'] for d in candidate['deliveries'])
    assert document['assetSha256s'] == {r['path']: r['sha256'] for r in candidate['files']}
    assert document['contentSha256'] == candidate['content']['prospectiveSha256']
    rows = exact_rows(document['items'], 'itemId'); assert set(rows) == ITEMS
    # Coverage is explicit parent-reviewed evidence, not inferred from a successful build.
    for key, row in rows.items():
        assert row['nativeEquipped'] is True and row['nativeDisplay'] is True
        assert row['wearers'] and len(set(row['wearers'])) == len(row['wearers'])
        assert row['evidence']
        for evidence in row['evidence']: pinned(root, evidence)
    assert rows['paladin_armor_censure']['nativeCombatMotion'] is True
    assert isinstance(document['limitations'], list) and document['limitations']
    return document


def provenance_pins(root, value):
    if isinstance(value, dict):
        if 'path' in value and 'sha256' in value: pinned(root, value)
        for child in value.values(): provenance_pins(root, child)
    elif isinstance(value, list):
        for child in value: provenance_pins(root, child)


def logical_metadata(value):
    """Retained author inputs have logical IDs; public recipes keep repository paths."""
    if isinstance(value, list): return [logical_metadata(v) for v in value]
    if not isinstance(value, dict): return value
    result = {k: logical_metadata(v) for k, v in value.items()}
    if isinstance(result.get('path'), str) and result['path'].startswith('scratch/'):
        result['sourceId'] = result.pop('path')[8:]
    return result


def input_pins(root, pins, expected, asset_hashes):
    actual = {}
    for ref in pins:
        path = pinned(root, ref)
        destination = PACKAGE+'assets/'+path.name
        assert destination not in actual
        actual[destination] = ref['sha256']
    assert actual == {name: asset_hashes[name] for name in expected}, 'Stale or ambiguous display inputs'
    return [{'path': name, 'sha256': actual[name]} for name in sorted(actual)]


def prepare(root, candidate, generated_content, baseline, studio_args, icon_args, acceptance_ref, oath_ref=None):
    """Return fully checked writes. Caller must explicitly choose adoption after dry-run."""
    root = root.resolve()
    assert set(candidate['content']['visualOverrides']) == ITEMS, 'Complete six-item Censure delivery required'
    before = json.loads(safe(root, PACKAGE+'content.json').read_text())
    after = json.loads(generated_content); content_scope(before, after)
    assert sha(generated_content) == candidate['content']['prospectiveSha256']
    assert sha(safe(root, PACKAGE+'content.json').read_bytes()) == candidate['content']['previousSha256']
    for path, digest in baseline.items(): assert sha(safe(root, path).read_bytes()) == digest, path
    accepted = approval(root, acceptance_ref, candidate)
    oath = None
    if oath_ref:
        assert accepted['oathkeeperSuccessorSha256'] == oath_ref['sha256']
        oath = json.loads(pinned(root, oath_ref).read_text())
        assert oath['schema'] == 'ftkmf.oathkeeper-visual-successor.v1' and oath['itemId'] == OATH
        from sync_display_framing import prepare_oathkeeper_successor
        rebuilt = prepare_oathkeeper_successor(root/'scratch'/oath['sourceManifest']['sourceId'],
            root/'scratch'/oath['iconManifest']['sourceId'],
            [{'path':'scratch/'+r['receipt']['sourceId'],'sha256':r['receipt']['sha256']} for r in oath['nativeEvidence']])
        assert rebuilt == oath, 'Oathkeeper successor no longer reproduces its exact pins'
    else:
        assert not accepted.get('oathkeeperSuccessorSha256'), 'Explicit Oathkeeper successor input required'
    changed_items = ITEMS | ({OATH} if oath else set())
    for delivery in candidate['deliveries']: provenance_pins(root, delivery['sourceProvenance'])
    studio_path, studio, studio_freeze = frozen(root, studio_args)
    icon_path, icons, icon_freeze = frozen(root, icon_args)
    assert studio['complete'] is True and studio['resolution'] == [512, 512] and studio['mode'] == 'RGBA'
    assert icons['sourceStudioManifest'] == {'path': studio_path.relative_to(root).as_posix(), 'sha256': studio_args[1]}
    assert icons['sourceStudioFrozenReceipt'] == {'path': Path(studio_args[2]).resolve().relative_to(root).as_posix(), 'sha256': studio_args[3]}
    for name in ('generator', 'renderer', 'decoder'): pinned(root, studio[name])
    pinned(root, icons['generator'])
    assert icons['recipe'] == studio['recipe']
    assert icons['outputFormat']['width'] == icons['outputFormat']['height'] == 512
    assert icons['outputFormat']['mode'] == 'RGBA'
    old_ledgers = {name: json.loads(safe(root, META+name).read_text()) for name in LEDGERS}
    assert sha(safe(root, META+LEDGERS[0]).read_bytes()) == candidate['previousPaladinLedgerSha256']
    previous_revision = None
    if old_ledgers[LEDGERS[0]].get('censureVisualRevision'):
        previous_revision = json.loads(pinned(root, old_ledgers[LEDGERS[0]]['censureVisualRevision']).read_text())
    writes = {}; expected_before = {}
    asset_hashes = {PACKAGE+key: row['sha256'] for key, row in old_ledgers[LEDGERS[0]]['files'].items()}
    for row in candidate['files']:
        assert row['path'].startswith(PACKAGE+'assets/')
        assert row['path'] not in writes, 'Duplicate output asset'
        assert row['previous'] == old_ledgers[LEDGERS[0]]['files'].get(row['path'].removeprefix(PACKAGE))
        source = pinned(root, {'path': 'scratch/'+row['sourceId'], 'sha256': row['sha256']})
        writes[row['path']] = source.read_bytes(); asset_hashes[row['path']] = row['sha256']
    entries = {e['id']: e for e in after['entries']}
    assert set(writes) <= {PACKAGE+r for key in ITEMS for r in refs({k:v for k,v in entries[key].items() if k in ROUTES})}
    for entry in before['entries']:
        if entry['id'] not in ITEMS: assert not set(writes)&{PACKAGE+r for r in refs(entry)}
    if oath:
        assert entries[OATH] == oath['contentEntry']
        for ref in oath['files']:
            assert ref['path'] not in writes
            assert sha(safe(root, ref['path']).read_bytes()) == ref['previousSha256']
            source = pinned(root, {'path':ref['source'],'sha256':ref['sha256']})
            writes[ref['path']] = source.read_bytes(); asset_hashes[ref['path']] = ref['sha256']
    portraits = exact_rows(studio['items'], 'id')
    assert ITEMS <= set(portraits)
    old_studio_ref = old_ledgers['nonweapon-icons.provenance.json']['studioManifest']
    old_studio = json.loads(pinned(root, {'path': 'scratch/'+old_studio_ref['sourceId'], 'sha256': old_studio_ref['sha256']}).read_text())
    old_portraits = exact_rows(old_studio['items'], 'id')
    assert set(portraits) == ITEMS or set(portraits) == set(old_portraits)
    # Art may deliver six rows or a complete gallery; all unrelated rows must name exact reused pixels.
    for key, row in portraits.items():
        image = safe(studio_path.parent, row['image'])
        assert sha(image.read_bytes()) == row['sha256'] == studio_freeze['files'][row['image']]
        if key not in ITEMS:
            prior = row['renderProvenance']['previousImage']; pinned(root, prior)
            expected_portrait = oath['portrait'] if oath and key == OATH else old_portraits[key]
            assert prior['sha256'] == row['sha256'] == expected_portrait['sha256'], 'Unrelated portrait changed'
            if oath and key == OATH:
                input_pins(root,row['sources'],{PACKAGE+r for r in refs(entries[OATH]['displayModels'])},asset_hashes)
                assert row['camera'] == oath['portrait']['camera']
            continue
        assert row['package'] == 'paladin'
        content_file = pinned(root, row['content'])
        assert content_file.read_bytes() == generated_content
        # Existing studio portraits render the intact weapon root. Native fracture
        # children remain pinned by the full content and delivery, not painted into cards.
        visible = entries[key]['displayModels'][:1]
        assert len(entries[key]['displayModels']) == 1 or key in {'paladin_hammer_1h_censure','paladin_hammer_2h_censure'}
        expected = {PACKAGE+ref for ref in refs(visible)}
        input_pins(root, row['sources'], expected, asset_hashes)
        assert [a['rendererPath'] for a in row['assignments']] == [m['path'] for m in visible]
    icon_rows = exact_rows(icons['items'], 'itemId'); assert set(icon_rows) == ICONS
    new_icons = [copy.deepcopy(oath['iconRow'])] if oath else []
    from sync_nonweapon_icons import check_png
    for key, row in icon_rows.items():
        entry = entries[key]; portrait = portraits[key]
        assert row['package'] == 'paladin' and entry['kind'] == 'item' and 'precisionWeapon' not in entry
        destination = PACKAGE+entry['icon']
        assert row['canonicalIconPath'] == destination
        assert row['canonicalBeforeSha256'] == sha(safe(root, destination).read_bytes())
        output = pinned(root, {'path': row['outputPath'], 'sha256': row['outputSha256']}); check_png(output)
        assert output.resolve().relative_to(icon_path.parent).as_posix() in icon_freeze['files']
        assert row['exactCopy'] is True
        assert row['outputSha256'] == row['sourceSha256'] == portrait['sha256']
        assert pinned(root, {'path': row['sourceRenderPath'], 'sha256': row['sourceSha256']}) == safe(studio_path.parent, portrait['image'])
        assert row['sourceRenderCamera'] == portrait['camera']
        inputs = input_pins(root, row['sourceModelAndTexturePins'], {PACKAGE+r for r in refs(entry['displayModels'])}, asset_hashes)
        new_icons.append({'itemId': key, 'package': 'paladin', 'path': destination,
            'sha256': row['outputSha256'], 'bytes': output.stat().st_size, 'priorSha256': row['canonicalBeforeSha256'],
            'renderSource': {'sourceId': row['sourceRenderPath'].removeprefix('scratch/'), 'sha256': row['sourceSha256']},
            'inputs': inputs, 'camera': row['sourceRenderCamera'], 'native64Bounds': row['native64Bounds'],
            'native64AlphaThreshold': row['native64AlphaThreshold']})
        writes[destination] = output.read_bytes()
    history_root = META+'history/censure/'+candidate['previousPaladinLedgerSha256']+'/'
    histories = {}
    for name in LEDGERS:
        data = safe(root, META+name).read_bytes(); target = history_root+name
        histories[name] = {'path': target, 'sha256': sha(data)}; writes[target] = data
    writes[history_root+'content.json'] = safe(root, PACKAGE+'content.json').read_bytes()
    thief_provenance = 'art-experiments/thief-advanced/provenance.json'
    writes[history_root+'thief-advanced.provenance.json'] = safe(root, thief_provenance).read_bytes()
    if previous_revision is not None:
        writes[history_root+'censure-visual.provenance.json'] = safe(root, REVISION).read_bytes()
    revision = {'schema': 'ftkmf.censure-visual-revision.v1', 'previousLedgers': histories,
        'previousContent': {'path': history_root+'content.json', 'sha256': sha(writes[history_root+'content.json'])},
        'previousThiefProvenance': {'path': history_root+'thief-advanced.provenance.json',
            'sha256': sha(writes[history_root+'thief-advanced.provenance.json'])},
        'baselineSha256': candidate['baselineSha256'], 'deliveries': [
            {'manifest': {'sourceId': d['manifest'].removeprefix('scratch/'), 'sha256': d['manifestSha256']},
             'freeze': {'sourceId': d['freeze'].removeprefix('scratch/'), 'sha256': d['freezeSha256']},
             'sourceProvenance': logical_metadata(d['sourceProvenance'])} for d in candidate['deliveries']],
        'nativeAcceptance': copy.deepcopy(accepted), 'nativeAcceptanceReceipt': copy.deepcopy(acceptance_ref),
        'studioManifest': {'sourceId': studio_path.relative_to(root/'scratch').as_posix(), 'sha256': studio_args[1]},
        'iconManifest': {'sourceId': icon_path.relative_to(root/'scratch').as_posix(), 'sha256': icon_args[1]},
        'studioFreeze': {'sourceId': Path(studio_args[2]).resolve().relative_to(root/'scratch').as_posix(), 'sha256': studio_args[3]},
        'iconFreeze': {'sourceId': Path(icon_args[2]).resolve().relative_to(root/'scratch').as_posix(), 'sha256': icon_args[3]},
        'renderRecipe': copy.deepcopy(studio['recipe']), 'renderSources': logical_metadata({k: studio[k] for k in ('generator','renderer','decoder')}),
        'priorStudioManifest': copy.deepcopy(old_studio_ref),
        'changedAssets': {r['path']: {'sha256': r['sha256'], 'previousSha256': (r['previous'] or {}).get('sha256')} for r in candidate['files']},
        'changedIcons': {r['path']: {'sha256': r['sha256'], 'previousSha256': r['priorSha256']} for r in new_icons},
        'contentSha256': sha(generated_content), 'retainedHistoricalTextures': sorted(
            set(candidate['retainHistoricalUnreferencedTextures']) | set((previous_revision or {}).get('retainedHistoricalTextures', [])))}
    if previous_revision is not None:
        revision['previousRevision'] = {'path': history_root+'censure-visual.provenance.json',
            'sha256': sha(writes[history_root+'censure-visual.provenance.json'])}
    if oath:
        revision['oathkeeperSuccessor'] = {'sourceId':oath_ref['path'].removeprefix('scratch/'),
            'sha256':oath_ref['sha256'],'details':logical_metadata(oath)}
        revision['changedAssets'].update({r['path']:{'sha256':r['sha256'],'previousSha256':r['previousSha256']}
            for r in oath['files'] if r['path'].endswith('.glb')})
    # Store logical retained input IDs in public metadata, never host paths or native logs.
    revision['nativeAcceptance'] = {k: v for k, v in accepted.items() if k != 'items'} | {'items': [
        {k: v for k, v in row.items() if k != 'evidence'} | {'evidence': [
            {'sourceId': ref['path'].removeprefix('scratch/'), 'sha256': ref['sha256']} for ref in row['evidence']]}
        for row in accepted['items']]}
    revision['nativeAcceptanceReceipt'] = {'sourceId': acceptance_ref['path'].removeprefix('scratch/'), 'sha256': acceptance_ref['sha256']}
    revision_ref = {'path': REVISION, 'sha256': sha(encode(revision))}
    new_ledgers = copy.deepcopy(old_ledgers)
    icon_ledger = new_ledgers['nonweapon-icons.provenance.json']
    old_icons = exact_rows(icon_ledger['items'], 'itemId')
    assert (len(old_icons),len(set(old_icons)&ICONS)) in ((40,2),(42,4))
    assert {'paladin_helmet_censure','paladin_boots_censure'} <= set(old_icons)
    for row in new_icons:
        if row['itemId'] in old_icons: row['previous'] = copy.deepcopy(old_icons[row['itemId']])
        old_icons[row['itemId']] = row
    icon_ledger['items'] = sorted(old_icons.values(), key=lambda r: r['itemId'])
    icon_ledger['censureVisualRevision'] = revision_ref
    icon_ledger['scope'] = ('42 nonweapon inventory icons; four Censure copies and one separately accepted Oathkeeper copy, 37 unchanged prior rows.' if oath else
        '42 nonweapon inventory icons; four accepted Censure studio copies, 38 unchanged prior records.')+' Native icon UI states are a separate gate.'
    display_ledger = new_ledgers['display-framing.provenance.json']
    display_ledger['scope'] = ('Twenty-three retained display records, two Censure replacements and one separately accepted Oathkeeper successor.' if oath else
        'Twenty-four retained display-framing records and two accepted Censure replacements.')+' Full prior ledger retained; equipped changes have separate pinned lineage.'
    for row in display_ledger['items']:
        if row['itemId'] not in changed_items: continue
        old = copy.deepcopy(row); entry = entries[row['itemId']]
        assert len(entry['displayModels']) == 1
        assignment = entry['displayModels'][0]
        row.clear()
        row.update({'itemId': old['itemId'], 'package': 'paladin', 'previous': old, 'censureVisualRevision': revision_ref,
            'displayAssignment': copy.deepcopy(assignment),
            'output': {'path': PACKAGE+assignment['model'], 'sha256': asset_hashes[PACKAGE+assignment['model']], 'bytes': len(writes[PACKAGE+assignment['model']])},
            'texture': {'path': PACKAGE+assignment['texture'], 'sha256': asset_hashes[PACKAGE+assignment['texture']]},
            'equippedAssets': [{'path': PACKAGE+r, 'sha256': asset_hashes[PACKAGE+r]} for r in sorted(refs({k:v for k,v in entry.items() if k in ROUTES-{'displayModels'}})) if r.endswith('.glb')]})
    paladin = new_ledgers['paladin-assets.provenance.json']
    for path, data in writes.items():
        if not path.startswith(PACKAGE+'assets/'): continue
        key = path.removeprefix(PACKAGE); old = copy.deepcopy(paladin['files'].get(key))
        paladin['files'][key] = {'sha256': sha(data), 'bytes': len(data), 'previous': old,
            'derivation': {'censureVisualRevision': revision_ref}}
        if old: paladin['files'][key]['baseline'] = copy.deepcopy(old.get('baseline', old))
    paladin['rendererRoutes'] = {e['id']: {k: e[k] for k in ('itemModels','displayModels','apparelModels') if k in e}
        for e in after['entries'] if any(k in e for k in ('itemModels','displayModels','apparelModels'))}
    paladin['censureVisualRevision'] = revision_ref
    for name, field in [('display-framing.provenance.json','displayFramingRevision'), ('nonweapon-icons.provenance.json','nonweaponIconRevision')]:
        paladin[field] = {'ledger': META+name, 'sha256': sha(encode(new_ledgers[name])), 'scope': 'Bounded accepted Censure visual revision; previous complete ledger retained.'}
    writes[REVISION] = encode(revision)
    writes[PACKAGE+'content.json'] = generated_content
    for name, ledger in new_ledgers.items(): writes[META+name] = encode(ledger)
    campaign = safe(root, 'art-experiments/thief-advanced/provenance.py')
    spec = importlib.util.spec_from_file_location('censure_thief_provenance', campaign)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    writes['art-experiments/thief-advanced/provenance.json'] = encode(module.build_document({
        META+name: writes[META+name] for name in ('display-framing.provenance.json','nonweapon-icons.provenance.json')}))
    allowed = set(revision['changedAssets']) | set(revision['changedIcons']) | {PACKAGE+'content.json'}
    assert {p for p in writes if p.startswith(PACKAGE)} == allowed
    for name in writes:
        path = safe(root, name)
        if name.startswith(history_root) and path.exists(): assert path.read_bytes() == writes[name]
        expected_before[name] = sha(path.read_bytes()) if path.exists() else None
    return revision, writes, expected_before


def revision_state(root=ROOT):
    revision = json.loads(safe(root, REVISION).read_text())
    assert revision['schema'] == 'ftkmf.censure-visual-revision.v1'
    previous = {name: json.loads(pinned(root, ref).read_text()) for name, ref in revision['previousLedgers'].items()}
    assert set(previous) == set(LEDGERS)
    before = json.loads(pinned(root, revision['previousContent']).read_text())
    pinned(root, revision['previousThiefProvenance'])
    after_bytes = safe(root, PACKAGE+'content.json').read_bytes()
    assert sha(after_bytes) == revision['contentSha256']
    after = json.loads(after_bytes); content_scope(before, after)
    for section in ('changedAssets', 'changedIcons'):
        for path, ref in revision[section].items():
            assert sha(safe(root, path).read_bytes()) == ref['sha256']
            old = previous[LEDGERS[0]]['files'].get(path.removeprefix(PACKAGE))
            assert (old['sha256'] if old else None) == ref['previousSha256']
    changed_icons = ICONS | ({OATH} if revision.get('oathkeeperSuccessor') else set())
    assert set(revision['changedIcons']) == {PACKAGE+e['icon'] for e in after['entries'] if e['id'] in changed_icons}
    inherited = json.loads(pinned(root, revision['previousRevision']).read_text()) if revision.get('previousRevision') else {}
    retired = (refs(before)-refs(after)) | set(inherited.get('retainedHistoricalTextures', []))
    assert retired == set(revision['retainedHistoricalTextures'])
    assert all(Path(p).name.startswith('atlas-') and p.endswith('.png') for p in retired)
    revision_ref = {'path': REVISION, 'sha256': sha(safe(root, REVISION).read_bytes())}
    return revision, previous, after, revision_ref


def validate_icons(ledger, package, entries, root=ROOT):
    revision, previous, _, revision_ref = revision_state(root)
    assert ledger['censureVisualRevision'] == revision_ref
    old = exact_rows(previous['nonweapon-icons.provenance.json']['items'], 'itemId')
    current = exact_rows(ledger['items'], 'itemId')
    assert set(current) == set(old)|ICONS
    changed_icons = ICONS | ({OATH} if revision.get('oathkeeperSuccessor') else set())
    for key, row in current.items():
        if key not in changed_icons: assert row == old[key], 'Unrelated icon lineage changed'
        else:
            assert row['package'] == 'paladin'
            assert row['sha256'] == revision['changedIcons'][row['path']]['sha256']
            assert row.get('previous') == old.get(key)
    result = [r for r in ledger['items'] if r['package'] == package]
    by_id = {e['id']: e for e in entries}
    from sync_nonweapon_icons import check_png
    for row in result:
        entry = by_id[row['itemId']]
        assert entry['kind'] == 'item' and 'precisionWeapon' not in entry
        path = META+package+'/'+entry['icon']
        assert row['path'] == path and sha(safe(root, path).read_bytes()) == row['sha256']
        check_png(safe(root, path))
        assert {r['path'] for r in row['inputs']} == {META+package+'/'+r for r in refs(entry['displayModels'])}
        for ref in row['inputs']: pinned(root, ref)
    return result


def validate_display_row(ledger, row, entry, root=ROOT):
    revision, previous, _, revision_ref = revision_state(root)
    old = exact_rows(previous['display-framing.provenance.json']['items'], 'itemId')
    current = exact_rows(ledger['items'], 'itemId')
    assert set(old) == set(current)
    changed_items = ITEMS | ({OATH} if revision.get('oathkeeperSuccessor') else set())
    for key in old:
        if key not in changed_items: assert current[key] == old[key]
    assert row['itemId'] in {'paladin_helmet_censure', 'paladin_boots_censure'} | ({OATH} if revision.get('oathkeeperSuccessor') else set())
    assert row['previous'] == old[row['itemId']] and row['censureVisualRevision'] == revision_ref
    assert entry['displayModels'] == [row['displayAssignment']]
    assert row['output']['path'] == PACKAGE+entry['displayModels'][0]['model']
    assert row['texture']['path'] == PACKAGE+entry['displayModels'][0]['texture']
    for ref in [row['output'], row['texture']]+row['equippedAssets']: pinned(root, ref)


def validate_paladin(receipt, root=ROOT):
    revision, previous, _, revision_ref = revision_state(root)
    assert receipt['censureVisualRevision'] == revision_ref
    old = previous['paladin-assets.provenance.json']
    changed = {p.removeprefix(PACKAGE) for group in ('changedAssets','changedIcons') for p in revision[group]}
    assert set(receipt['files']) == set(old['files'])|changed
    for name, row in receipt['files'].items():
        if name not in changed: assert row == old['files'][name]
        else:
            assert row['previous'] == old['files'].get(name)
            assert row['derivation'] == {'censureVisualRevision': revision_ref}
            assert sha(safe(root, PACKAGE+name).read_bytes()) == row['sha256']
    for key in old:
        if key not in {'files','rendererRoutes','displayFramingRevision','nonweaponIconRevision','censureVisualRevision'}:
            assert receipt[key] == old[key]
    for name, field in [('display-framing.provenance.json','displayFramingRevision'), ('nonweapon-icons.provenance.json','nonweaponIconRevision')]:
        assert receipt[field]['sha256'] == sha(safe(root, META+name).read_bytes())
    return set(revision['retainedHistoricalTextures'])


def apply(root, writes, expected_before, verify=None):
    """Recheck every destination, then replace; restore exact preflight bytes on failure."""
    assert set(writes) == set(expected_before)
    old = {}
    for name, expected in expected_before.items():
        path = safe(root, name); old[name] = path.read_bytes() if path.exists() else None
        assert (sha(old[name]) if old[name] is not None else None) == expected, 'Destination changed after preflight: '+name
    changed = []
    try:
        for name, data in writes.items():
            path = safe(root, name); path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_name(path.name+'.censure-tmp')
            assert not temporary.exists(), 'Pending adoption temporary file requires inspection'
            try:
                with temporary.open('xb') as stream:
                    stream.write(data); stream.flush(); os.fsync(stream.fileno())
                os.replace(temporary, path)
            finally:
                if temporary.exists(): temporary.unlink()
            changed.append(name)
        if verify: verify()
    except BaseException:
        for name in reversed(changed):
            path = safe(root, name)
            if old[name] is None: path.unlink()
            else: path.write_bytes(old[name])
        raise
