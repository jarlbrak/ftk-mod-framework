#!/usr/bin/env python3
"""Freeze the offline Tidecrown authoring bundle with deterministic pins."""
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
    'verify_live_validation_v1.py',
    'live-validation-v1/archive.py',
    'runtime-profile.json',
    'studio-review.json',
    'hero.png',
    'side.png',
    'portrait.png',
    'tidecrown-sea-king.blend',
    'tidecrown-sea-king-studio.blend',
    'tidecrown-sea-king.glb',
    'tidecrown-sea-king.source.json',
    'tidecrown-sea-king.pieces.json',
    'tidecrown-sea-king.validation.json',
    'tidecrown-sea-king_basecolor.png',
    'tidecrown-sea-king-reopened.glb',
    'tidecrown-sea-king-reopened.png',
    'tidecrown-sea-king-reopened.source.json',
    'tidecrown-sea-king-reopened.validation.json',
    'reopened-validation.json',
    'original-geometry-proof.json',
    'target-binding-proof.json',
]
for name in required:
    assert (OUT / name).is_file(), name
runtime = json.loads((OUT / 'runtime-profile.json').read_text())
assert len(runtime['profiles']) == 1
profile = runtime['profiles'][0]
assets = {name: sha(OUT / name) for name in ('tidecrown-sea-king.glb', 'tidecrown-sea-king_basecolor.png')}
live_evidence = {}
for version in ('v1', 'v2', 'v3'):
    evidence = OUT / f'live-validation-{version}/validation.json'
    if evidence.is_file():
        live_evidence[f'live-validation-{version}/validation.json'] = sha(evidence)
manifest = {
    'status': ('FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE'
               if live_evidence else 'FROZEN_OFFLINE_ORIGINAL_CANDIDATE_LIVE_VALIDATION_PENDING'),
    'name': 'Tidecrown Sovereign',
    'nativeEnemy': 'seaKing',
    'referenceRendererId': 121357,
    'rendererPath': 'enSeaKing',
    'boneCount': 60,
    'nativeSurfaceCopied': False,
    'authoringInputs': 'bone_names and inverse bind matrices only; proof restricts the generator to those keys.',
    'profile': profile,
    'originalAssets': assets,
    'files': {name: sha(OUT / name) for name in required},
    'liveEvidence': live_evidence,
    'limits': [
        'Static studio render and binary checks do not prove native animation, portrait framing, camera fit, culling, material lifetime, ragdoll, or gameplay; named live evidence has its own bounded claims.',
        'The exact Sea King native root scale is 1.0; public visualScale stays 1.0 until the spawned custom clone confirms it.',
        'This profile is only seaKing/enSeaKing. It does not establish Sea King tentacles, other 60-bone humanoids, or external weapon/breakable compatibility.',
    ],
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps({'manifest': str(OUT / 'manifest.json'), 'assets': assets, 'liveEvidence': live_evidence}, indent=2))
