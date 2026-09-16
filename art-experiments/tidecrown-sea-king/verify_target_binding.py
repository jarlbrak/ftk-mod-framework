#!/usr/bin/env python3
"""Record the exact Sea King palette and inverse binds used by Tidecrown."""
import hashlib
import json
from pathlib import Path

import numpy as np


OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
reference_path = ROOT / 'scratch/skeleton-audit/121357/reference.npz'
reference = np.load(reference_path, allow_pickle=False)
source = OUT / 'tidecrown-sea-king.source.json'
data = json.loads(source.read_text())
assert data['bone_names'] == reference['bone_names'].tolist()
binds = reference['bindposes']
centers = np.linalg.inv(binds)[:, :3, 3]
index = {name: data['bone_names'].index(name) for name in data['bone_names']}
assert len(data['bone_names']) == 60
assert data['bone_names'][index['Hair_M']] == 'Hair_M'
result = {
    'status': 'PASS',
    'nativeChassis': 'seaKing',
    'targetRendererId': 121357,
    'rendererPath': 'enSeaKing',
    'boneCount': len(data['bone_names']),
    'boneNamesSha256': hashlib.sha256('\n'.join(data['bone_names']).encode()).hexdigest(),
    'inverseBindMatricesSha256': hashlib.sha256(binds.astype('<f8').tobytes()).hexdigest(),
    'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'reference': {
        'path': str(reference_path.relative_to(ROOT)),
        'sha256': hashlib.sha256(reference_path.read_bytes()).hexdigest(),
    },
    'bindingLandmarks': {name: centers[index[name]].tolist() for name in ('Root_M', 'Chest_M', 'Neck_M', 'Head_M', 'Hair_M', 'Wrist_R', 'Wrist_L', 'Hip_R', 'Hip_L')},
    'authoringBoundary': 'The tall Hair_M bind landmark is retained in the palette but deliberately carries no Tidecrown surface. The visible crown is head-bound because Hair_M is a zero-native-surface-weight palette entry above the skull.',
    'scope': 'Exact palette and bind identity for seaKing/enSeaKing. It does not establish source-surface similarity, animation safety, portrait framing, material behavior, or live art acceptance.',
}
(OUT / 'target-binding-proof.json').write_text(json.dumps(result, indent=2) + '\n')
print(result['status'])
