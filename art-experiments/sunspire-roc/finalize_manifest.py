"""Freeze the offline Sunspire Roc authoring bundle with deterministic pins."""
import hashlib
import json
from pathlib import Path

OUT = Path(__file__).resolve().parent
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()

required = [
    'README.md',
    'build_geometry.py',
    'build_blender.py',
    'verify_original_geometry.py',
    'verify_target_binding.py',
    'finalize_manifest.py',
    'stage_runtime_profile.py',
    'write_live_review_v1.py',
    'write_live_review_v2.py',
    'write_live_review_v3.py',
    'live-validation-v1/archive.py',
    'live-validation-v2/archive.py',
    'live-validation-v3/archive.py',
    'live-validation-v4/archive.py',
    'runtime-profile.json',
    'studio-review.json',
    'hero.png',
    'side.png',
    'sunspire-roc.blend',
    'sunspire-roc-studio.blend',
    'sunspire-roc.glb',
    'sunspire-roc.source.json',
    'sunspire-roc.pieces.json',
    'sunspire-roc.validation.json',
    'sunspire-roc_basecolor.png',
    'sunspire-roc-reopened.glb',
    'sunspire-roc-reopened.png',
    'sunspire-roc-reopened.source.json',
    'sunspire-roc-reopened.validation.json',
    'reopened-validation.json',
    'original-geometry-proof.json',
    'target-binding-proof.json',
]
for name in required:
    assert (OUT / name).is_file(), name
runtime = json.loads((OUT / 'runtime-profile.json').read_text())
assert len(runtime['profiles']) == 1
profile = runtime['profiles'][0]
assets = {name: sha(OUT / name) for name in ('sunspire-roc.glb', 'sunspire-roc_basecolor.png')}
live_evidence = {}
for version in ('v1', 'v2', 'v3', 'v4'):
    evidence = OUT / f'live-validation-{version}/validation.json'
    if evidence.is_file():
        live_evidence[f'live-validation-{version}/validation.json'] = sha(evidence)
manifest = {
    'status': ('FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE'
               if live_evidence else 'FROZEN_OFFLINE_ORIGINAL_CANDIDATE_LIVE_VALIDATION_PENDING'),
    'name': 'Sunspire Roc',
    'nativeEnemy': 'rocA',
    'referenceRendererId': 121238,
    'rendererPath': 'enRoc01',
    'boneCount': 36,
    'nativeSurfaceCopied': False,
    'authoringInputs': 'bone_names and inverse bind matrices only; proof restricts the generator to those keys.',
    'profile': profile,
    'originalAssets': assets,
    'files': {name: sha(OUT / name) for name in required},
    'liveEvidence': live_evidence,
    'limits': [
        'Static studio render and binary checks do not prove native animation, portrait framing, camera fit, culling, material lifetime, or gameplay; named live evidence has its own bounded claims.',
        'The root native visual scale must be observed in the exact live clone before changing public visualScale; the archived rocA run preserves that observation at 0.9.',
        'This profile is only rocA/enRoc01. It does not establish rocB, jungle Roc, or other bird-rig compatibility.',
    ],
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps({'manifest': str(OUT / 'manifest.json'), 'assets': assets, 'liveEvidence': live_evidence}, indent=2))
