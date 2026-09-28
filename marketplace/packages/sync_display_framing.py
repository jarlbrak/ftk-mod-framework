#!/usr/bin/env python3
"""Preflight or adopt the exact original display-only family; never generate geometry."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
LEDGER = HERE/'display-framing.provenance.json'
MANIFEST_SHA = '4ab959f42b6e6430cd0c22f61c6fd9b1416cd8719efea4b09121c10e66223895'
PROFILES = ROOT/'art-experiments/gear-display-framing/profiles.json'
PALADIN_TIERS = ('novice','oathkeeper','highward','mercy','censure','verdict')
THIEF_TIERS = ('street','burglar','guild','masterwork','locksmith','nightblade','wayfarer')
ALLOWED = {'paladin': {f'paladin_{part}_{tier}' for part in ('helmet','boots') for tier in PALADIN_TIERS},
           'thief': {f'thief_{part}_{tier}' for part in ('hood','boots') for tier in THIEF_TIERS}}


def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def logical(path):
    path = Path(path)
    assert not path.is_absolute() and '..' not in path.parts
    return path.relative_to('scratch').as_posix() if path.parts[0] == 'scratch' else path.as_posix()


def pin(path, sha):
    assert digest(ROOT/path) == sha, path
    return {'sourceId': logical(path), 'sha256': sha}


def validate_display_framing(package, entries):
    """Existing candidates remain valid before adoption; adopted families require exact public pins."""
    if not LEDGER.exists(): return []
    ledger = json.loads(LEDGER.read_text())
    assert ledger['deliveryManifest']['sha256'] == MANIFEST_SHA
    profiles = {p['itemId']:p for p in json.loads(PROFILES.read_text())['profiles']}
    for ref in ledger['publicSources']:
        assert digest(ROOT/ref['path']) == ref['sha256'], ref['path']
    rows = [r for r in ledger['items'] if r['package'] == package]
    assert len(rows) == len(ALLOWED[package]) and {r['itemId'] for r in rows} == ALLOWED[package]
    by_id = {e['id']: e for e in entries}
    for row in rows:
        entry = by_id[row['itemId']]
        profile = profiles[row['itemId']]
        if row.get('censureVisualRevision'):
            from censure_visual_revision import validate_display_row
            validate_display_row(ledger,row,entry)
            continue
        if row.get('revisionManifestSha256'):
            validate_helmet_display_row(ledger,row)
        else:
            assert row['output']['sha256'] == profile['outputSha256']
            assert row['source']['sha256'] == profile['sourceSha256']
            assert row['transform'] == {'positiveUniformScale':profile['scale'],'translation':profile['translation']}
        assert row['displayAssignment'] == profile['displayAssignment']
        assert row['texture']['sha256'] == profile['textureSha256']
        assert entry['displayModels'] == [row['displayAssignment']]
        assert digest(ROOT/row['output']['path']) == row['output']['sha256']
        for ref in row['equippedAssets'] + [row['texture']]:
            assert digest(ROOT/ref['path']) == ref['sha256'], ref['path']
    return rows


def generate(manifest_path):
    assert digest(manifest_path) == MANIFEST_SHA, 'Unexpected frozen display manifest'
    manifest = json.loads(Path(manifest_path).read_text())
    assert len(manifest['items']) == 26
    profiles = {p['itemId']: p for p in json.loads(PROFILES.read_text())['profiles']}
    contents = {p: json.loads((HERE/p/'content.json').read_text()) for p in ALLOWED}
    rows, copies, seen = [], [], set()
    for item in manifest['items']:
        package, key = item['package'], item['itemId']
        assert package in ALLOWED and key in ALLOWED[package] and key not in seen
        seen.add(key)
        profile = profiles[key]
        assert profile['package'] == package
        assert profile['outputSha256'] == item['outputSha256']
        assert profile['sourceSha256'] == item['sourceSha256']
        assert profile['displayAssignment'] == item['displayAssignmentAfter']
        assert profile['scale'] == item['bakedUniformScale'] > 0
        assert profile['translation'] == item['bakedLocalTranslation']
        assert item['normalsUvsIndicesByteIdentical'] and item['unchangedBinaryOutsidePositions']
        assert item['equippedSourceBytesUnchanged'] and item['newInvertedTriangles'] == 0
        before, after = item['displayAssignmentBefore'], item['displayAssignmentAfter']
        assert before['path'] == after['path'] and before['texture'] == after['texture']
        entry = next(e for e in contents[package]['entries'] if e['id'] == key)
        assert entry['kind'] == 'item'
        assert entry['displayModels'] in ([before], [after])
        for name in (profile['sourceFile'], profile['outputFile']):
            assert Path(name).name == name and name.endswith('.glb')
        assert after['model'] == 'assets/'+profile['outputFile']
        source = ROOT/item['outputPath']
        assert digest(source) == item['outputSha256']
        pin(item['sourceModel'], item['sourceSha256'])
        original = {'sourceId':'retained-original-display/'+package+'/'+profile['sourceFile'], 'sha256':item['sourceSha256']}
        destination = HERE/package/after['model']
        if destination.exists():
            assert digest(destination) in (item['sourceSha256'], item['outputSha256']), destination
        equipped = []
        for ref in item['equippedAssetPins']:
            pin(ref['path'], ref['sha256'])
            target = HERE/package/'assets'/Path(ref['path']).name
            assert digest(target) == ref['sha256'] and target != destination
            equipped.append({'path':str(target.relative_to(ROOT)), 'sha256':ref['sha256']})
        texture = item['unchangedTexture']
        pin(texture['path'], texture['sha256'])
        texture_target = HERE/package/after['texture']
        assert digest(texture_target) == texture['sha256'] == profile['textureSha256']
        pin(item['sourceReceipt'],item['sourceReceiptSha256'])
        rows.append({'itemId':key, 'package':package, 'source':original,
                     'output':{'path':str(destination.relative_to(ROOT)), 'sha256':item['outputSha256'], 'bytes':source.stat().st_size},
                     'displayAssignmentBefore':before, 'displayAssignment':after,
                     'equippedAssets':equipped,
                     'texture':{'path':str(texture_target.relative_to(ROOT)), 'sha256':texture['sha256']},
                     'transform':{'positiveUniformScale':profile['scale'], 'translation':profile['translation']},
                     'sourceObservation':{'sourceId':'native-display-baseline/'+key+'/snapshot', 'sha256':item['sourceReceiptSha256']},
                     'preservation':{'normalsUvsIndicesByteIdentical':True,'unchangedBinaryOutsidePositions':True,'newInvertedTriangles':0}})
        copies.append((source, destination))
        entry['displayModels'] = [copy.deepcopy(after)]
    assert seen == set.union(*ALLOWED.values())
    public_sources, retained_sources = [], []
    for frozen_ref in manifest['authoringSources']:
        ref = dict(frozen_ref)
        if ref['path'] == 'art-experiments/gear-display-framing/README.md':
            ref['path'] = 'art-experiments/gear-display-framing/history/README-v1.md'
        retained = pin(ref['path'],ref['sha256'])
        if ref['path'].startswith('art-experiments/'):
            public_sources.append({'path':ref['path'],'sha256':ref['sha256']})
        else: retained_sources.append(retained)
    ledger = {'schema':'ftkmf.original-display-framing.v1',
              'scope':'Original display-only derivatives. Equipped assets and textures unchanged; native acceptance is recorded separately in package validation docs.',
              'deliveryManifest':{'sourceId':logical(Path(manifest_path).resolve().relative_to(ROOT)), 'sha256':MANIFEST_SHA},
              'publicSources':public_sources, 'retainedSources':retained_sources, 'items':rows}
    return ledger, copies, contents


def paladin_provenance(ledger, content):
    path = HERE/'paladin-assets.provenance.json'
    receipt = json.loads(path.read_text())
    for row in ledger['items']:
        if row['package'] != 'paladin': continue
        key = str(Path(row['output']['path']).relative_to('marketplace/packages/paladin'))
        current = receipt['files'].get(key)
        if current and isinstance(current.get('derivation'),dict) and current['derivation'].get('displayFramingItem') == row['itemId']:
            continue
        source_key = row['displayAssignmentBefore']['model']
        previous = copy.deepcopy(receipt['files'][source_key])
        assert previous['sha256'] == row['source']['sha256']
        receipt['files'][key] = {'source':'art-experiments/gear-display-framing/fit_displays.py',
            'sha256':row['output']['sha256'], 'bytes':row['output']['bytes'],
            'baseline':copy.deepcopy(previous.get('baseline',previous)), 'previous':previous,
            'derivation':{'displayFramingItem':row['itemId'], 'ledger':str(LEDGER.relative_to(ROOT)),
                          'inputSha256':row['source']['sha256'], 'transform':row['transform']}}
    receipt['rendererRoutes'] = {e['id']:{k:e[k] for k in ('itemModels','displayModels','apparelModels') if k in e}
                                for e in content['entries'] if any(k in e for k in ('itemModels','displayModels','apparelModels'))}
    receipt['displayFramingRevision'] = {'ledger':str(LEDGER.relative_to(ROOT)),
        'sha256':hashlib.sha256((json.dumps(ledger,indent=2)+'\n').encode()).hexdigest(),
        'scope':'Twelve display-only derivatives. Prior paired-boot and equipped helmet provenance retained in previous records.'}
    return receipt


HELMET_REVISION_SHA = 'a60cdbf4e55330ef0f62f70e80a9c474cb3364ba2ccb5f217dce560996a729c3'
HELMET_IDS = {'paladin_helmet_oathkeeper', 'paladin_helmet_censure'}
HELMET_BASELINE_LEDGER_SHA = '14d52fea1dabb786590d66cfc40abafd00efdab17f05da5ead163b11cf1256b8'
HELMET_ICON_SHA = '64ecdf97e629813c5b8d2e5256b421ec81fe41ada3b77e62f350033a9e4ac08f'
HELMET_ICON_OUTPUTS = {
    'paladin_helmet_oathkeeper':'813c4856ff7a704ffec82d91b40d53c2a432dcd03ae98074a83649de0d641237',
    'paladin_helmet_censure':'0876d164690ad5ee57ec7cb2b30cf51a3e78936e4e36b3285ba1497112808d8d',
}
HELMET_PUBLIC_SOURCES = {
    'art-experiments/paladin-overhaul/helmet_clearance.py': 'ac3344d3fe9bb3c603fa84082ae415ad25650f9856716c2d0f221ca18c75e0c4',
    'art-experiments/paladin-overhaul/helmet-shape-profiles.json': '9975be49596032ec145ddc95b26b0ff1c83409f113a649b61572532a2cafff35',
}
HELMET_HISTORIES = {
    'display-framing.provenance.json': ('display-framing.pre-helmet-shape.provenance.json', HELMET_BASELINE_LEDGER_SHA),
    'nonweapon-icons.provenance.json': ('nonweapon-icons.studio-v3.provenance.json', '5b0660e390ffc8d0e022035de30cc8624228d4b09065b7975a19e4cae54288e5'),
    'paladin-assets.provenance.json': ('paladin-assets.pre-helmet-shape.provenance.json', '05c71b7cc21e17c3299fe9274040d2724247cd8799db40212e1fb9a47d64a353'),
}


def helmet_history(name):
    filename,sha = HELMET_HISTORIES[name]
    path = HERE/filename
    assert digest(path) == sha, filename
    return json.loads(path.read_text())


def serialized_sha(value):
    return hashlib.sha256((json.dumps(value,indent=2)+'\n').encode()).hexdigest()


def validate_helmet_display_row(ledger,row):
    assert row['itemId'] in HELMET_IDS and row['revisionManifestSha256'] == HELMET_REVISION_SHA
    revision = ledger['helmetRevision']
    assert revision['manifest']['sha256'] == HELMET_REVISION_SHA
    assert revision['publicRecipe']['profile'] == 'original-shape-v6'
    assert {r['path']:r['sha256'] for r in revision['publicRecipe']['sources']} == HELMET_PUBLIC_SOURCES
    for path,sha in HELMET_PUBLIC_SOURCES.items(): pin(path,sha)
    previous = helmet_history('display-framing.provenance.json')
    old = next(r for r in previous['items'] if r['itemId'] == row['itemId'])
    assert row['previous'] == old
    profile = json.loads((ROOT/'art-experiments/paladin-overhaul/helmet-shape-profiles.json').read_text())['profiles']['original-shape-v6']['items'][row['itemId'].rsplit('_',1)[1]]
    pin(profile['input']['path'],profile['input']['sha256'])
    assert row['output']['sha256'] == profile['display']['sha256']
    assert row['source']['sha256'] == profile['equipped']['sha256']
    assert row['transform'] == {'positiveUniformScale':profile['display']['scale'],'translation':profile['display']['translation']}
    assert row['texture'] == old['texture'] and row['displayAssignment'] == old['displayAssignment']
    assert row['equippedAssets'] == [{'path':old['equippedAssets'][0]['path'],'sha256':profile['equipped']['sha256']}]
    for prior,current in zip(previous['items'],ledger['items']):
        assert prior['itemId'] == current['itemId']
        if prior['itemId'] not in HELMET_IDS: assert current == prior


def validate_helmet_icons(ledger,package,entries):
    from nonweapon_icon_revisions import exact_items, CURRENT
    from sync_nonweapon_icons import check_png
    previous = helmet_history('nonweapon-icons.provenance.json')
    old = exact_items(previous,CURRENT)
    current = exact_items(ledger,CURRENT)
    assert ledger['helmetRevision']['revision'] == 'studio-v4'
    assert ledger['helmetRevision']['previousLedgerSha256'] == serialized_sha(previous)
    for revision,data in previous['revisions'].items(): assert ledger['revisions'][revision] == data
    assert set(ledger['revisions']) == set(previous['revisions']) | {'studio-v4'}
    assert ledger['revisions']['studio-v4']['deliveryManifest']['sha256'] == HELMET_ICON_SHA
    assert ledger['revisions']['studio-v4']['studioManifest']['sha256'] == '82dcbebdfc88d40b068d7019ca606321822f5e2ec83ba63d7f8571e8d2d371e5'
    for key,row in current.items():
        if key in HELMET_IDS:
            assert row['previous'] == old[key] and row['priorSha256'] == old[key]['sha256']
            assert row['sourceRevision'] == 'studio-v4' and row['path'] == old[key]['path']
            assert row['sha256'] == row['renderSource']['sha256'] == HELMET_ICON_OUTPUTS[key]
        else: assert row == old[key]
    by_id = {e['id']:e for e in entries}
    rows = [r for r in ledger['items'] if r['package'] == package]
    for row in rows:
        entry = by_id[row['itemId']]
        assert entry['kind'] == 'item' and 'precisionWeapon' not in entry
        path = HERE/package/entry['icon']
        assert row['path'] == str(path.relative_to(ROOT)) and digest(path) == row['sha256']
        check_png(path)
        expected = {str((HERE/package/m[k]).relative_to(ROOT)) for m in entry['displayModels'] for k in ('model','texture')}
        assert {r['path'] for r in row['inputs']} == expected
        for ref in row['inputs']: pin(ref['path'],ref['sha256'])
    return rows


def helmet_paladin_provenance(previous,displays,icons,copies,native_evidence):
    """Replace six records and retain their complete predecessors without reauthoring history."""
    result = copy.deepcopy(previous)
    display_rows = {r['itemId']:r for r in displays['items']}
    icon_rows = {r['itemId']:r for r in icons['items']}
    for source,destination in copies:
        name = str(destination.relative_to(HERE/'paladin'))
        prior = previous['files'][name]
        tier = next(t for t in ('oathkeeper','censure') if '/paladin-'+t+'-helmet' in name)
        key = 'paladin_helmet_'+tier
        derivation = {'helmetShapeRevision':'original-shape-v6','tier':tier,
                      'ledger':str(LEDGER.relative_to(ROOT))}
        if name.endswith('-icon.png'):
            row = icon_rows[key]
            derivation.update(iconRevision='studio-v4',inputs=copy.deepcopy(row['inputs']))
            origin = row['renderSource']['sourceId']
        else:
            origin = 'art-experiments/paladin-overhaul/helmet_clearance.py'
            derivation.update(publicRecipe=copy.deepcopy(displays['helmetRevision']['publicRecipe']),
                              variant='display' if name.endswith('-display.glb') else 'equipped')
            if derivation['variant'] == 'display':
                derivation.update(displayFramingItem=key,transform=display_rows[key]['transform'])
        result['files'][name] = {'source':origin,'sha256':digest(source),'bytes':source.stat().st_size,
            'baseline':copy.deepcopy(prior.get('baseline',prior)),'previous':copy.deepcopy(prior),'derivation':derivation}
    result['helmetShapeRevision'] = {'profile':'original-shape-v6','manifestSha256':HELMET_REVISION_SHA,
        'previousLedger':{'path':str((HERE/HELMET_HISTORIES['paladin-assets.provenance.json'][0]).relative_to(ROOT)),
                          'sha256':HELMET_HISTORIES['paladin-assets.provenance.json'][1]},
        'assets':[str(b.relative_to(HERE/'paladin')) for _,b in copies],
        'nativeEvidence':copy.deepcopy(native_evidence),
        'scope':'Selected original-shape helmets; static native fit and display evidence only. Motion, lifecycle and prior RPC failures remain separate.'}
    for field,name,ledger in [('displayFramingRevision','display-framing.provenance.json',displays),('nonweaponIconRevision','nonweapon-icons.provenance.json',icons)]:
        result[field] = {'ledger':str((HERE/name).relative_to(ROOT)),'sha256':serialized_sha(ledger),
                         'previous':copy.deepcopy(previous[field]),'scope':'Two selected helmet revisions; all other records and full history retained.'}
    return result


def validate_helmet_paladin(receipt,display_rows,icon_rows):
    previous = helmet_history('paladin-assets.provenance.json')
    revision = receipt['helmetShapeRevision']
    assert revision['profile'] == 'original-shape-v6' and revision['manifestSha256'] == HELMET_REVISION_SHA
    assert revision['previousLedger']['sha256'] == serialized_sha(previous)
    assert revision['nativeEvidence']['sha256'] and revision['nativeEvidence']['sourceId']
    names = {'assets/paladin-'+tier+'-helmet'+suffix for tier in ('oathkeeper','censure') for suffix in ('.glb','-display.glb','-icon.png')}
    assert set(revision['assets']) == names and set(receipt['files']) == set(previous['files'])
    for name,record in receipt['files'].items():
        if name not in names: assert record == previous['files'][name]
        else:
            assert record['previous'] == previous['files'][name]
            assert record['baseline'] == previous['files'][name].get('baseline',previous['files'][name])
            assert record['derivation']['helmetShapeRevision'] == 'original-shape-v6'
            assert digest(HERE/'paladin'/name) == record['sha256']
            if name.endswith('.glb'):
                assert record['derivation']['publicRecipe']['profile'] == 'original-shape-v6'
                assert {r['path']:r['sha256'] for r in record['derivation']['publicRecipe']['sources']} == HELMET_PUBLIC_SOURCES
    for key,value in previous.items():
        if key not in ('files','displayFramingRevision','nonweaponIconRevision'): assert receipt[key] == value
    for field,name in [('displayFramingRevision','display-framing.provenance.json'),('nonweaponIconRevision','nonweapon-icons.provenance.json')]:
        assert receipt[field]['sha256'] == digest(HERE/name)
        assert receipt[field]['previous'] == previous[field]
    for row in display_rows:
        if row['itemId'] in HELMET_IDS:
            assert receipt['files'][row['displayAssignment']['model']]['sha256'] == row['output']['sha256']
            assert receipt['files'][str(Path(row['equippedAssets'][0]['path']).relative_to('marketplace/packages/paladin'))]['sha256'] == row['source']['sha256']
    for row in icon_rows:
        if row['itemId'] in HELMET_IDS:
            record = receipt['files'][str(Path(row['path']).relative_to('marketplace/packages/paladin'))]
            assert record['sha256'] == row['sha256'] and record['derivation']['inputs'] == row['inputs']


def prepare_oathkeeper_successor(manifest_path, icon_manifest_path, review_refs):
    """Select the accepted Oathkeeper only; never revive the rejected Censure row."""
    from censure_visual_revision import pinned, frozen, logical_metadata, input_pins
    assert digest(manifest_path) == HELMET_REVISION_SHA
    manifest = json.loads(Path(manifest_path).read_text())
    key = 'paladin_helmet_oathkeeper'
    item = next(row for row in manifest['items'] if row['itemId'] == key)
    expected = {'equipped':'0f1c364ce86b726e960b8c2d253b430928b6d9790bbdbead7e568fdb8d7bca95',
                'display':'ca2bf74937dbc540185d1ab0851f8ea699f6359ef6fd70c4c5972f24f73cf5fa'}
    assert item['output']['sha256'] == expected['equipped']
    assert item['display']['output']['sha256'] == expected['display']
    for ref in (item['original'],item['current'],item['output'],item['display']['output'],item['texture']): pinned(ROOT,ref)
    entry = next(e for e in json.loads((HERE/'paladin/content.json').read_text())['entries'] if e['id'] == key)
    assert entry['itemModels'][0]['model'] == item['canonicalModelPath']
    assert entry['displayModels'][0]['model'] == item['display']['canonicalModelPath']
    assert digest(HERE/'paladin'/item['canonicalModelPath']) == item['current']['sha256']
    for path,sha in HELMET_PUBLIC_SOURCES.items(): pin(path,sha)
    assert digest(icon_manifest_path) == HELMET_ICON_SHA
    icons = json.loads(Path(icon_manifest_path).read_text())
    for name in ('sourceStudioManifest','sourceStudioFrozenReceipt','generator'): pinned(ROOT,icons[name])
    studio_path = ROOT/icons['sourceStudioManifest']['path']
    _,studio,_ = frozen(ROOT,[studio_path,icons['sourceStudioManifest']['sha256'],
                              ROOT/icons['sourceStudioFrozenReceipt']['path'],icons['sourceStudioFrozenReceipt']['sha256']])
    row = next(r for r in icons['items'] if r['itemId'] == key)
    portrait = next(r for r in studio['items'] if r['id'] == key)
    assert row['outputSha256'] == row['sourceSha256'] == portrait['sha256'] == HELMET_ICON_OUTPUTS[key]
    assert row['exactCopy'] is True and row['sourceRenderCamera'] == portrait['camera']
    source = pinned(ROOT,{'path':row['outputPath'],'sha256':row['outputSha256']})
    pinned(ROOT,{'path':row['sourceRenderPath'],'sha256':row['sourceSha256']})
    from sync_nonweapon_icons import check_png
    check_png(source)
    prefix='marketplace/packages/paladin/'
    assert row['canonicalIconPath'] == prefix+entry['icon']
    assert digest(HERE/'paladin'/entry['icon']) == row['canonicalBeforeSha256']
    texture = prefix+entry['displayModels'][0]['texture']
    assert digest(ROOT/texture) == item['texture']['sha256']
    hashes={prefix+item['display']['canonicalModelPath']:expected['display'],texture:item['texture']['sha256']}
    inputs=input_pins(ROOT,row['sourceModelAndTexturePins'],set(hashes),hashes)
    wearers=[];native=[];display_pass=False
    for ref in review_refs:
        path=pinned(ROOT,ref);review=json.loads(path.read_text())
        seal=json.loads((path.parent/'frozen.json').read_text())
        assert seal['review.json'] == ref['sha256']
        selected=[]
        for record in review.get('jobs',[]):
            job=record['job']
            if job['tier'] != 'oathkeeper':continue
            assert record['verdict']=='pass-bounded-visible-fit'
            pinned(ROOT,record['complete'])
            wearer=job['classKey']+':'+job['skinName']
            assert wearer not in wearers;wearers.append(wearer)
            selected.append({'wearer':wearer,'verdict':record['verdict']})
        for record in review.get('items',[]):
            if record['tier'] != 'oathkeeper':continue
            assert record['verdict']=='pass-native-display-shape-and-framing' and record['postCloseVerified']
            assert record['renderers'][0]['model']['sha256']==expected['display']
            for name in ('show','complete','image'):pinned(ROOT,record[name])
            display_pass=True;selected.append({'nativeDisplay':True,'verdict':record['verdict']})
        assert selected
        native.append({'receipt':logical_metadata(ref),'selected':selected})
    assert len(wearers)==15 and len({w.split(':')[0] for w in wearers})==15 and display_pass
    files=[]
    for ref,destination in [(item['output'],item['canonicalModelPath']),
                            (item['display']['output'],item['display']['canonicalModelPath']),
                            ({'path':row['outputPath'],'sha256':row['outputSha256']},entry['icon'])]:
        files.append({'source':ref['path'],'sha256':ref['sha256'],'path':prefix+destination,
                      'previousSha256':digest(HERE/'paladin'/destination)})
    return {'schema':'ftkmf.oathkeeper-visual-successor.v1','itemId':key,'canonicalWrites':False,
        'files':files,'contentEntry':entry,'texture':{'path':texture,'sha256':item['texture']['sha256']},
        'sourceManifest':{'sourceId':logical(Path(manifest_path).resolve().relative_to(ROOT)),'sha256':HELMET_REVISION_SHA},
        'iconManifest':{'sourceId':logical(Path(icon_manifest_path).resolve().relative_to(ROOT)),'sha256':HELMET_ICON_SHA},
        'publicRecipe':{'profile':'original-shape-v6','sources':[{'path':p,'sha256':s} for p,s in HELMET_PUBLIC_SOURCES.items()]},
        'original':logical_metadata(item['original']),'fit':item['fit'],'nativeEvidence':native,'wearers':sorted(wearers),
        'portrait':copy.deepcopy(portrait),'iconRow':{'itemId':key,'package':'paladin','path':row['canonicalIconPath'],
            'sha256':row['outputSha256'],'bytes':source.stat().st_size,'priorSha256':row['canonicalBeforeSha256'],
            'renderSource':{'sourceId':logical(row['sourceRenderPath']),'sha256':row['sourceSha256']},
            'inputs':inputs,'camera':row['sourceRenderCamera'],'native64Bounds':row['native64Bounds'],
            'native64AlphaThreshold':row['native64AlphaThreshold']},
        'limits':'Fifteen native class defaults and one native display; minor ear/rim contacts accepted. No motion or lifecycle claim. Explicit combined adoption approval still required.'}


def prepare_helmet_revision(manifest_path):
    """Preflight four artist-authored files without changing any canonical asset or record."""
    assert digest(manifest_path) == HELMET_REVISION_SHA
    manifest = json.loads(Path(manifest_path).read_text())
    assert len(manifest['items']) == 2 and {r['itemId'] for r in manifest['items']} == HELMET_IDS
    assert digest(LEDGER) == HELMET_BASELINE_LEDGER_SHA, 'Unexpected display predecessor ledger'
    previous = json.loads(LEDGER.read_text())
    old_rows = {r['itemId']:r for r in previous['items']}
    entries = {e['id']:e for e in json.loads((HERE/'paladin/content.json').read_text())['entries']}
    files, replacements, originals = [], [], []
    recipe = pin(manifest['generator']['path'],manifest['generator']['sha256'])
    reader = pin(manifest['strictReader']['path'],manifest['strictReader']['sha256'])
    for item in manifest['items']:
        key, tier = item['itemId'], item['tier']
        assert key == 'paladin_helmet_'+tier and tier in ('oathkeeper','censure')
        entry, old = entries[key], old_rows[key]
        model = 'assets/paladin-'+tier+'-helmet.glb'
        display = 'assets/paladin-'+tier+'-helmet-display.glb'
        assert item['canonicalModelPath'] == model and item['display']['canonicalModelPath'] == display
        assert len(entry['itemModels']) == 1 and entry['itemModels'][0]['model'] == model
        assert entry['displayModels'] == [old['displayAssignment']]
        assert old['displayAssignment']['model'] == display
        assert entry['helmetHairVisibility'] == {'top':False,'bottom':False}
        for name in ('original','current','output','texture'):
            pin(item[name]['path'],item[name]['sha256'])
        pin(item['display']['output']['path'],item['display']['output']['sha256'])
        assert digest(HERE/'paladin'/model) == item['current']['sha256']
        assert digest(HERE/'paladin'/display) == old['output']['sha256']
        assert digest(HERE/'paladin'/entry['itemModels'][0]['texture']) == item['texture']['sha256'] == old['texture']['sha256']
        # Original input identity is distinct from the rejected local-clearance predecessor.
        assert item['original']['sha256'] != item['current']['sha256']
        assert item['checks']['positiveUniformScale'] == item['fit']['scale'] == 1.16
        for checks in (item['checks'],item['display']['checks']):
            assert checks['unchangedBinaryOutsidePosition'] and checks['normalsUvIndicesExact']
            assert checks['newlyInvertedFaces'] == 0 and checks['positiveUniformScale'] > 0
        assert item['display']['checks']['positiveUniformScale'] == 1.0
        row = copy.deepcopy(old)
        row['previous'] = copy.deepcopy(old)
        row['revisionManifestSha256'] = HELMET_REVISION_SHA
        row['source'] = {'sourceId':'selected-original-helmet/'+tier+'/equipped','sha256':item['output']['sha256']}
        row['output'] = {'path':old['output']['path'],'sha256':item['display']['output']['sha256'],
                         'bytes':(ROOT/item['display']['output']['path']).stat().st_size}
        row['equippedAssets'] = [{'path':str((HERE/'paladin'/model).relative_to(ROOT)),'sha256':item['output']['sha256']}]
        row['transform'] = {'positiveUniformScale':item['display']['checks']['positiveUniformScale'],
                            'translation':item['display']['checks']['translation']}
        row['sourceObservation'] = {'sourceId':'native-display-baseline/'+key+'/snapshot',
                                    'sha256':item['display']['cameraReceipt']['sha256']}
        pin(item['display']['cameraReceipt']['path'],item['display']['cameraReceipt']['sha256'])
        replacements.append(row)
        originals.append({'itemId':key,'original':{'sourceId':'true-original-helmet/'+tier,'sha256':item['original']['sha256']},
                          'previousEquippedSha256':item['current']['sha256'],'outputSha256':item['output']['sha256'],
                          'fit':copy.deepcopy(item['fit'])})
        files.extend([(ROOT/item['output']['path'],HERE/'paladin'/model),
                      (ROOT/item['display']['output']['path'],HERE/'paladin'/display)])
    revision = {'manifest':{'sourceId':logical(Path(manifest_path).resolve().relative_to(ROOT)),'sha256':HELMET_REVISION_SHA},
                'previousLedgerSha256':HELMET_BASELINE_LEDGER_SHA,'originals':originals,
                'retainedRecipe':recipe,'retainedReader':reader,
                'scope':'True-original uniform-scale/placement revision. Native approval and public reproduction recipe must be supplied before adoption.'}
    return merge_helmet_display_revision(previous,replacements,revision), files


def merge_helmet_display_revision(previous,replacements,revision):
    assert len(replacements) == 2 and {r['itemId'] for r in replacements} == HELMET_IDS
    assert len(previous['items']) == 26
    assert revision['manifest']['sha256'] == HELMET_REVISION_SHA
    result = copy.deepcopy(previous)
    by_id = {r['itemId']:r for r in previous['items']}
    assert len(by_id) == 26
    updates = {r['itemId']:r for r in replacements}
    for key,row in updates.items():
        old = by_id[key]
        assert row['previous'] == old and row['package'] == 'paladin'
        assert row['output']['path'] == old['output']['path']
        assert row['texture'] == old['texture'] and row['displayAssignment'] == old['displayAssignment']
        assert row['revisionManifestSha256'] == revision['manifest']['sha256']
    result['items'] = [copy.deepcopy(updates.get(r['itemId'],r)) for r in previous['items']]
    result['helmetRevision'] = copy.deepcopy(revision)
    return result


def merge_helmet_icon_revision(previous,refresh,revision_id):
    """Replace exactly two pinned portraits after source/render preflight by the existing adopter."""
    from nonweapon_icon_revisions import exact_items, CURRENT
    old=exact_items(previous,CURRENT)
    updates=refresh['items']
    assert len(updates)==2 and {r['itemId'] for r in updates}==HELMET_IDS
    assert revision_id and revision_id not in previous['revisions']
    result=copy.deepcopy(previous)
    new={}
    for row in updates:
        prior=old[row['itemId']]
        assert row['package']=='paladin' and row['path']==prior['path']
        assert row['priorSha256']==prior['sha256']
        item=copy.deepcopy(row)
        item['sourceRevision']=revision_id
        item['previous']=copy.deepcopy(prior)
        new[row['itemId']]=item
    result['items']=[copy.deepcopy(new.get(row['itemId'],row)) for row in previous['items']]
    result['revisions'][revision_id]={k:copy.deepcopy(v) for k,v in refresh.items() if k!='items'}
    result['helmetRevision']={'revision':revision_id,'items':sorted(HELMET_IDS),
        'previousLedgerSha256':hashlib.sha256((json.dumps(previous,indent=2)+'\n').encode()).hexdigest()}
    return result


def prepare_helmet_delivery(manifest_path, icon_manifest_path):
    """Join frozen artist geometry and portraits prospectively, without canonical writes."""
    from sync_nonweapon_icons import check_png, retained
    displays, copies = prepare_helmet_revision(manifest_path)
    manifest = json.loads(Path(manifest_path).read_text())
    for path,sha in HELMET_PUBLIC_SOURCES.items(): pin(path,sha)
    profiles = json.loads((ROOT/'art-experiments/paladin-overhaul/helmet-shape-profiles.json').read_text())['profiles']['original-shape-v6']['items']
    for item in manifest['items']:
        profile = profiles[item['tier']]
        assert profile['input']['sha256'] == item['original']['sha256']
        pin(profile['input']['path'],profile['input']['sha256'])
        assert profile['textureSha256'] == item['texture']['sha256']
        for variant, data in (('equipped',item),('display',item['display'])):
            assert profile[variant]['sha256'] == data['output']['sha256']
            assert profile[variant]['scale'] == data['checks']['positiveUniformScale']
            assert profile[variant]['translation'] == data['checks']['translation']
    displays['helmetRevision']['publicRecipe'] = {'profile':'original-shape-v6',
        'sources':[{'path':p,'sha256':s} for p,s in HELMET_PUBLIC_SOURCES.items()],
        'inputRule':'Both variants reproduce from the pinned true original; display includes the equipped float32 bake.'}
    displays['helmetRevision']['scope'] = 'True-original uniform-scale/placement revision; native approval remains a separate required gate.'
    assert digest(icon_manifest_path) == HELMET_ICON_SHA
    delivery = json.loads(Path(icon_manifest_path).read_text())
    assert len(delivery['items']) == 2 and {r['itemId'] for r in delivery['items']} == HELMET_IDS
    previous = json.loads((HERE/'nonweapon-icons.provenance.json').read_text())
    assert delivery['supersedesForTheseItemsOnly']['sha256'] == previous['deliveryManifest']['sha256']
    prospective = {str(b.relative_to(ROOT)):digest(a) for a,b in copies}
    entries = {e['id']:e for e in json.loads((HERE/'paladin/content.json').read_text())['entries']}
    rows = []
    for row in delivery['items']:
        assert row['package'] == 'paladin'
        entry = entries[row['itemId']]
        assert entry['kind'] == 'item' and 'precisionWeapon' not in entry
        destination = HERE/'paladin'/entry['icon']
        assert row['canonicalIconPath'] == str(destination.relative_to(ROOT))
        assert digest(destination) == row['canonicalBeforeSha256']
        source = ROOT/row['outputPath']; check_png(source)
        assert row['exactCopy'] and digest(source) == row['outputSha256'] == row['sourceSha256'] == digest(ROOT/row['sourceRenderPath'])
        inputs = []
        for ref in row['sourceModelAndTexturePins']:
            pin(ref['path'],ref['sha256'])
            canonical = HERE/'paladin/assets'/Path(ref['path']).name
            path = str(canonical.relative_to(ROOT))
            assert prospective.get(path,digest(canonical)) == ref['sha256']
            inputs.append({'path':path,'sha256':ref['sha256']})
        expected = {str((HERE/'paladin'/m[k]).relative_to(ROOT)) for m in entry['displayModels'] for k in ('model','texture')}
        assert {r['path'] for r in inputs} == expected
        rows.append({'itemId':row['itemId'],'package':'paladin','path':str(destination.relative_to(ROOT)),
            'sha256':row['outputSha256'],'bytes':source.stat().st_size,'priorSha256':row['canonicalBeforeSha256'],
            'renderSource':{'sourceId':logical(row['sourceRenderPath']),'sha256':row['sourceSha256']},
            'inputs':inputs,'camera':row['sourceRenderCamera'],
            'native64AlphaThreshold':row['native64AlphaThreshold'],'native64Bounds':row['native64Bounds']})
        copies.append((source,destination))
    refresh = {'deliveryManifest':{'sourceId':logical(Path(icon_manifest_path).resolve().relative_to(ROOT)),'sha256':HELMET_ICON_SHA},
        'studioManifest':retained(delivery['sourceStudioManifest']),
        'studioFrozenReceipt':retained(delivery['sourceStudioFrozenReceipt']),
        'renderGenerator':retained(delivery['generator']),'recipe':delivery['recipe'],
        'outputFormat':delivery['outputFormat'],'items':rows}
    icons = merge_helmet_icon_revision(previous,refresh,'studio-v4')
    icons['scope'] = '40 nonweapon inventory icons with exactly two selected helmet portraits refreshed from studio-v4;38 prior records and all history preserved.'
    assert len(copies) == 6 and len({b for _,b in copies}) == 6
    return displays,icons,copies


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,required=True)
    parser.add_argument('--adopt',action='store_true',help='Write canonical display assets/routes/ledgers only after native acceptance; default is preflight only')
    parser.add_argument('--helmet-icons',type=Path,help='Select the bounded four-GLB/two-icon helmet revision, using this frozen icon manifest')
    parser.add_argument('--native-evidence',type=Path,help='Parent-approved native acceptance receipt; required for helmet adoption')
    parser.add_argument('--native-evidence-sha256')
    parser.add_argument('--baseline',type=Path,help='Pinned package file map captured before helmet work')
    parser.add_argument('--baseline-sha256')
    args=parser.parse_args()
    if args.helmet_icons:
        if args.adopt:
            parser.error('The earlier original-shape-v6 Censure adoption is held after banner-fidelity rejection. Use the separately reviewed Censure delivery workflow.')
        displays,icons,copies = prepare_helmet_delivery(args.manifest,args.helmet_icons)
        for name,(_,sha) in HELMET_HISTORIES.items(): assert digest(HERE/name) == sha
        evidence = None
        if args.native_evidence:
            assert args.native_evidence_sha256 and digest(args.native_evidence) == args.native_evidence_sha256
            evidence = {'sourceId':logical(args.native_evidence.resolve().relative_to(ROOT)),
                        'sha256':args.native_evidence_sha256}
        if args.adopt:
            assert evidence and args.baseline and args.baseline_sha256, 'Explicit native acceptance and canonical baseline pins required'
            assert digest(args.baseline) == args.baseline_sha256
            baseline = json.loads(args.baseline.read_text())
            assert baseline and all(digest(ROOT/path) == sha for path,sha in baseline.items())
            assert all(str(destination.relative_to(ROOT)) in baseline for _,destination in copies)
            previous = json.loads((HERE/'paladin-assets.provenance.json').read_text())
            receipt = helmet_paladin_provenance(previous,displays,icons,copies,evidence)
            # Archive full predecessor ledgers before writing any current asset.
            for name,(history,sha) in HELMET_HISTORIES.items():
                path = HERE/history
                if path.exists(): assert digest(path) == sha
                else: path.write_bytes((HERE/name).read_bytes())
            for source,destination in copies: destination.write_bytes(source.read_bytes())
            for name,data in [('display-framing.provenance.json',displays),('nonweapon-icons.provenance.json',icons),('paladin-assets.provenance.json',receipt)]:
                (HERE/name).write_text(json.dumps(data,indent=2)+'\n')
            changed = {path for path,sha in baseline.items() if digest(ROOT/path) != sha}
            assert changed == {str(destination.relative_to(ROOT)) for _,destination in copies}
        print(('ADOPTED' if args.adopt else 'PREFLIGHT ONLY')+': exact4 helmet GLBs and2 icons;24 display rows and38 icon rows retained. No content/gameplay writes.')
        return
    ledger,copies,contents=generate(args.manifest)
    receipt=paladin_provenance(ledger,contents['paladin'])
    if args.adopt:
        for source,destination in copies: destination.write_bytes(source.read_bytes())
        for package,content in contents.items(): (HERE/package/'content.json').write_text(json.dumps(content,indent=2)+'\n')
        LEDGER.write_text(json.dumps(ledger,indent=2)+'\n')
        (HERE/'paladin-assets.provenance.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print(('ADOPTED' if args.adopt else 'PREFLIGHT ONLY')+': exact26 display derivatives; equipped/texture pins preserved. Icon provenance refresh and package validation are separate gates.')


if __name__ == '__main__': main()
