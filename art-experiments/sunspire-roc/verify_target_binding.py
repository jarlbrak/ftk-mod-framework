"""Record the exact rocA target binding used by the authored Roc source."""
import hashlib
import json
from pathlib import Path

import numpy as np

OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
reference_path = ROOT / 'scratch/skeleton-audit/121238/reference.npz'
reference = np.load(reference_path, allow_pickle=False)
source = OUT / 'sunspire-roc.source.json'
data = json.loads(source.read_text())
assert data['bone_names'] == reference['bone_names'].tolist()
binds = reference['bindposes']
head_index = data['bone_names'].index('Head_M')
jaw_index = data['bone_names'].index('JawEnd_M')
result = {
    'status': 'PASS',
    'nativeChassis': 'rocA',
    'targetRendererId': 121238,
    'rendererPath': 'enRoc01',
    'boneCount': len(data['bone_names']),
    'boneNamesSha256': hashlib.sha256('\n'.join(data['bone_names']).encode()).hexdigest(),
    'inverseBindMatricesSha256': hashlib.sha256(binds.astype('<f8').tobytes()).hexdigest(),
    'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'reference': {
        'path': str(reference_path.relative_to(ROOT)),
        'sha256': hashlib.sha256(reference_path.read_bytes()).hexdigest(),
    },
    'headBindCenter': np.linalg.inv(binds)[head_index, :3, 3].tolist(),
    'jawTipBindCenter': np.linalg.inv(binds)[jaw_index, :3, 3].tolist(),
    'scope': 'Exact palette/bind identity for the target renderer. It does not establish native surface similarity, pose safety, camera fit, or live art acceptance.',
}
(OUT / 'target-binding-proof.json').write_text(json.dumps(result, indent=2) + '\n')
print(result['status'])
