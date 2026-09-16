#!/usr/bin/env python3
"""Record the exact Desert Snake A palette and inverse binds used by Duneshade."""
import hashlib
import json
from pathlib import Path

import numpy as np


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
reference_path = ROOT / "scratch/skeleton-audit/121552/reference.npz"
reference = np.load(reference_path, allow_pickle=False)
source = OUT / "duneshade-desert-asp.source.json"
data = json.loads(source.read_text())
assert data["bone_names"] == reference["bone_names"].tolist()
binds = reference["bindposes"]
centers = np.linalg.inv(binds)[:, :3, 3]
index = {name: data["bone_names"].index(name) for name in data["bone_names"]}
assert len(data["bone_names"]) == 46
assert all(name in index for name in ("Root_M", "Head", "Jaw", "Tongue_08", "Tongue_End"))
used = sorted({joint for joints, weights in zip(data["joints"], data["weights"]) for joint, weight in zip(joints, weights) if weight > 0})
assert used == list(range(len(data["bone_names"])))
result = {
    "status": "PASS",
    "nativeChassis": "snakeDesertA",
    "targetRendererId": 121552,
    "rendererPath": "enDesertSnakeA",
    "boneCount": len(data["bone_names"]),
    "weightedBoneCount": len(used),
    "allPaletteBonesWeighted": used == list(range(len(data["bone_names"]))),
    "boneNamesSha256": hashlib.sha256("\n".join(data["bone_names"]).encode()).hexdigest(),
    "inverseBindMatricesSha256": hashlib.sha256(binds.astype("<f8").tobytes()).hexdigest(),
    "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
    "reference": {
        "path": str(reference_path.relative_to(ROOT)),
        "sha256": hashlib.sha256(reference_path.read_bytes()).hexdigest(),
    },
    "bindingLandmarks": {
        name: centers[index[name]].tolist()
        for name in (
            "BackRib_End", "BackRib_20", "Root_M", "FrontRib_01", "FrontRib_11",
            "Head", "joint13", "Jaw", "Jaw_End", "Tongue_01", "Tongue_08", "Tongue_End",
        )
    },
    "authoringBoundary": "The original surface follows only the exact tail-to-head BackRib/Root_M/FrontRib chain and adds independently head-, jaw-, tongue-, and joint13-bound anatomy. It retains every palette entry without copying native surface data.",
    "scope": "Exact palette and bind identity for snakeDesertA/enDesertSnakeA. It does not establish source-surface similarity, animation safety, portrait framing, material behavior, culling, ragdoll, or live art acceptance.",
}
(OUT / "target-binding-proof.json").write_text(json.dumps(result, indent=2) + "\n")
print(result["status"])
