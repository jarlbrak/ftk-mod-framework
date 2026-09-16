#!/usr/bin/env python3
"""Archive the fresh focused Sunspire Roc trial without game payloads."""
import gzip
import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path

ROOT = next(path for path in Path(__file__).resolve().parents if (path / 'FTKModFramework').is_dir())
ASSET = ROOT / 'art-experiments/sunspire-roc'
OUT = ASSET / 'live-validation-v1'
BASE = ROOT / 'scratch/mirewarden-game/model-test-output'
SESSION = 'c244446c2d9443759ce4a67cf7eaa022'
CASE = BASE / 'case-a331a3ee6dc7437284705dbb3d322025/case-result.json'
JOURNAL = CASE.with_name('journal.jsonl')
SESSION_RECORD = BASE / f'new-run-session-{SESSION}.json'
REVIEW = ROOT / 'scratch/sunspire-roc-root-visual-review-v1.json'
PROFILE = ROOT / 'scratch/mirewarden-game/model-test-profiles.json'
REGISTRATION = ROOT / 'scratch/mirewarden-game/model-test-registration.json'
MANIFEST = ASSET / 'manifest.json'
RUNTIME_PROFILE = ASSET / 'runtime-profile.json'
STAGE = ROOT / 'scratch/runtime-profile-412-sunspire-roc'
DEPLOYMENT = ROOT / 'scratch/mirewarden-game/deployment-backups/sunspire-roc-412-20260911-154046/deployment.json'
BATCH = ROOT / 'scratch/coverage-batch-20260911-154448.json'
RUNNER = ROOT / 'scratch/run-coverage-batch.py'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n')


def relative(path):
    return str(path.resolve().relative_to(ROOT))


assert not (OUT / 'validation.json').exists() or os.environ.get('FTK_ARCHIVE_REBUILD') == '1', 'Refusing to overwrite a completed archive.'
OUT.mkdir(parents=True, exist_ok=True)
case = read(CASE)
review = read(REVIEW)
session_record = read(SESSION_RECORD)
manifest = read(MANIFEST)
registration = read(REGISTRATION)
assert case['schema'] == 'ftkmf.exercise-case.v1'
assert case['session'] == SESSION and case['enemy'] == 'ftkmf_modeltest_sunspire_roc'
assert case['rendererPath'] == 'enRoc01' and case['focusedAttack'] is True
assert case['status'] == 'needs_visual_review' and len(case['actions']) == 3
assert review['session'] == SESSION and review['nativeChassis'] == 'rocA'
assert review['rendererPath'] == 'enRoc01' and len(review['frames']) == 9
assert session_record['session'] == SESSION
assert sha(PROFILE) == case['profileSha256']
assert case['finalReady']['strictReady']['ok'] is True
assert case['finalReady']['strictReady']['level'] == 0 and case['finalReady']['strictReady']['room'] == 2
for name, digest in manifest['files'].items():
    assert sha(ASSET / name) == digest, name
for name, digest in case['assetHashes'].items():
    assert sha(ASSET / name) == digest, name
registered = [row for row in registration['registered'] if row['key'] == case['enemy']]
assert registration['status'] == 'registered' and len(registered) == 1
assert registered[0]['portraitMarkerPath'] == 'Root_M/BackA_M/BackB_M/Chest_M/Neck_M/Head_M/PortraitCam'

actions = {action['action']: action for action in case['actions']}
assert set(actions) == {'pass', 'attack', 'kill-fixture'}
assert actions['attack']['focus'] is True
assert actions['attack']['actionResult']['result']['committed'] == 'Attack(focus)'
assert actions['attack']['hpOutcome']['status'] == 'nonlethal_hp_loss'
assert (actions['attack']['hpOutcome']['beforeHp'], actions['attack']['hpOutcome']['afterHp']) == (81, 71)
assert all(action['capture']['boundary']['completeCapture'] is True for action in actions.values())
assert actions['kill-fixture']['actionResult']['result']['committed'] == 'KillSingle'

sources = {CASE, JOURNAL, SESSION_RECORD, REVIEW, PROFILE, REGISTRATION, MANIFEST, RUNTIME_PROFILE,
           STAGE / 'receipt.json', STAGE / 'stage-script.py', DEPLOYMENT, BATCH, RUNNER,
           ASSET / 'sunspire-roc.glb', ASSET / 'sunspire-roc_basecolor.png', Path(__file__)}
setup_journal = Path(session_record['journal'])
sources.add(setup_journal)
setup_result = BASE / f"case-{session_record['case']}" / 'result.json'
if setup_result.is_file():
    sources.add(setup_result)

capture_inputs = []
for label, action in actions.items():
    raw_path = Path(action['capture']['rawCapture']['path'])
    raw = read(raw_path)
    assert raw_path.is_file() and raw['session'] == SESSION and len(raw['frames']) == 120 and raw['ok'] is True
    assert len(action['capture']['images']) == 120
    for image in action['capture']['images']:
        image_path = Path(image['path'])
        assert image_path.is_file() and sha(image_path) == image['sha256']
    action_journal = Path(action['journal']['path'])
    action_result = Path(action['rawResult']['path'])
    assert action_journal.is_file() and action_result.is_file()
    sources.update((raw_path, action_journal, action_result))
    capture_inputs.append((label, action, raw_path, raw))

# Preserve only metadata losslessly. PNGs are source-pinned and selected samples
# are copied below; game binaries and payload bundles never enter this archive.
pending_journals = {path for path in sources if path.suffix == '.jsonl'}
seen_journals = set()
excluded = {'.dll', '.assets', '.resources', '.bundle', '.exe', '.app', '.unity3d'}
while pending_journals:
    journal = pending_journals.pop()
    if journal in seen_journals or not journal.is_file():
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        candidate = Path(json.loads(line).get('data', {}).get('path', ''))
        if not candidate.is_file() or candidate.suffix.lower() in excluded:
            continue
        try:
            candidate.relative_to(ROOT)
        except ValueError:
            continue
        sources.add(candidate)
        if candidate.suffix == '.jsonl':
            pending_journals.add(candidate)
for source in sources:
    assert source.is_file() and not source.is_symlink() and source.suffix.lower() not in excluded, source

image_pins, mappings = {}, []
for source in sorted(sources):
    raw = source.read_bytes()
    source_relative = relative(source)
    if source.suffix.lower() == '.png':
        image_pins[source_relative] = hashlib.sha256(raw).hexdigest()
        continue
    destination = OUT / 'metadata' / (source_relative + '.gz')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    mappings.append({
        'source': source_relative,
        'sourceSha256': hashlib.sha256(raw).hexdigest(),
        'archive': str(destination.relative_to(OUT)),
        'archiveSha256': sha(destination),
        'encoding': 'gzip-lossless',
    })
write(OUT / 'source-image-pins.json', image_pins)
write(OUT / 'asset-pins.json', case['assetHashes'])

selected = []
for frame in review['frames']:
    source = ROOT / frame['path']
    assert source.is_file() and sha(source) == frame['sha256']
    destination = OUT / 'selected' / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(destination) == frame['sha256']
    selected.append({
        'source': frame['path'], 'archive': str(destination.relative_to(OUT)), 'sha256': frame['sha256'],
        'action': frame['action'], 'index': frame['index'], 'observation': frame['observation'],
    })

captures, videos, actions_summary = [], [], []
for label, action, raw_path, raw in capture_inputs:
    video = OUT / f'{label}.mp4'
    subprocess.run([
        'ffmpeg', '-y', '-hide_banner', '-loglevel', 'error', '-framerate', '12',
        '-i', str(raw_path.with_suffix('') / '%04d.png'), '-frames:v', '120',
        '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p', str(video),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-count_frames', '-select_streams', 'v:0',
        '-show_entries', 'stream=nb_read_frames,width,height', '-of', 'json', str(video),
    ]))['streams'][0]
    assert int(probe['nb_read_frames']) == 120
    clips = {}
    for index, frame in enumerate(raw['frames']):
        for layer in frame.get('animator', {}).get('layers', []):
            for clip in layer.get('playing', []):
                if clip.get('weight', 0) > 0:
                    clips.setdefault(clip['name'], []).append(index)
    captures.append({
        'label': label, 'captureId': raw_path.stem, 'frames': 120, 'complete': True,
        'rawCapture': {'source': relative(raw_path), 'sha256': sha(raw_path), 'ok': raw['ok'], 'error': raw['error']},
        'positiveClipRanges': {name: {'first': min(indices), 'last': max(indices), 'frames': len(indices)} for name, indices in clips.items()},
        'video': {'path': video.name, 'sha256': sha(video), 'width': int(probe['width']), 'height': int(probe['height']), 'playbackFps': 12, 'timing': 'Presentation derivative, not unperturbed timing'},
    })
    videos.append({'label': label, 'captureId': raw_path.stem, 'path': video.name, 'sha256': sha(video), 'frames': 120, 'width': int(probe['width']), 'height': int(probe['height']), 'playbackFps': 12, 'timing': 'Presentation derivative, not unperturbed timing'})
    outcome = action.get('hpOutcome') or {}
    actions_summary.append({
        'label': label, 'focus': action['focus'], 'classification': action['classification'],
        'accepted': action['actionAccepted'], 'result': action['actionResult']['result'],
        'enemyHpBefore': outcome.get('beforeHp', action['before']['combat']['enemies'][0]['hp']),
        'enemyHpAfter': outcome.get('afterHp', action['after']['combat']['enemies'][0]['hp']),
    })

renderer = case['initialRenderer']
validation = {
    'status': 'fresh_catalog_412_original_binding_focused_hit_motion_complete_fixture_death_and_progression',
    'session': SESSION,
    'enemy': case['enemy'],
    'displayName': 'Sunspire Roc',
    'nativeChassis': 'rocA',
    'rendererPath': 'enRoc01',
    'ownerInstanceId': renderer['ownerInstanceId'],
    'profileSha256': case['profileSha256'],
    'assetHashes': case['assetHashes'],
    'binaryPins': case['binaryPins'],
    'portraitMarkerRegistration': registered[0]['portraitMarkerPath'],
    'binding': review['binding'],
    'actions': actions_summary,
    'captures': captures,
    'focusedHit': 'OBSERVED: one fresh native Attack(focus) changed the same exact target from 81 to 71 HP.',
    'ordinaryNoFocusHit': 'NOT_CLAIMED in V1; a separate fresh ordinary-attack run is required.',
    'ordinaryLethal': 'NOT_TESTED',
    'explicitKillFixture': 'COMPLETE_120_FRAME_CAPTURE',
    'nativeCollects': len(case['collects']),
    'finalReady': case['finalReady']['strictReady'],
    'rootVisualReview': relative(REVIEW),
    'selectedPNGs': selected,
    'videos': videos,
    'sourceImageCount': len(image_pins),
    'sourceImagePins': 'source-image-pins.json',
    'assetPins': 'asset-pins.json',
    'losslessMappings': mappings,
    'limits': review['limits'],
}
write(OUT / 'validation.json', validation)
(OUT / 'README.md').write_text(f'''# Sunspire Roc fresh live trial V1

This archive records the fresh catalog-412 session `{SESSION}` against the exact `rocA` `enRoc01` renderer. The authored `sunspire-roc.glb` bound under one native owner with the expected 36-bone signature and preserved 0.9 native root scale. Its cloned Standard material uses the authored palette with emission disabled; the native head `PortraitCam` marker registered successfully, though this archive does not contain a separate portrait-pixel capture.

Pass, focused attack, and explicit `KillSingle` fixture captures each contain all 120 requested fixed-step frames. The focused `Attack(focus)` changed the same target from 81 to 71 HP. The selected review samples show the original bird remaining coherent through native wing, head, tail, leg, and death motion. Native combat UI/effects and victory/loot overlay restrict fine detail review. The fixture reached strict native Ready at level 0 room 2 after two guarded native Collect actions.

`validation.json` records binding, material, action, clip, and progression facts. `metadata/` stores lossless non-payload records, `selected/` contains nine reviewed original PNGs, and each MP4 is a 12 fps presentation derivative. The ordinary no-focus hit, ordinary lethal behavior, portrait pixels, full culling envelope, final material lifetime, and full art acceptance remain separate checks. `archive.py` refuses to overwrite a completed result.
''')
print(json.dumps({'validation': relative(OUT / 'validation.json'), 'validationSha256': sha(OUT / 'validation.json'), 'sourceImages': len(image_pins), 'losslessMappings': len(mappings), 'selected': len(selected), 'videos': len(videos)}, indent=2))
