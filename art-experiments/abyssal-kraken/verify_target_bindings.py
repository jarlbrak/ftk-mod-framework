#!/usr/bin/env python3
"""Pin each exact modern Kraken palette used by the original examples."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np

OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect(asset: str, renderer: int, chassis: str, renderer_path: str) -> dict:
    reference_path = ROOT / f"scratch/skeleton-audit/{renderer}/reference.npz"
    reference = np.load(reference_path, allow_pickle=False)
    source = json.loads((OUT / f"{asset}.source.json").read_text())
    names = reference["bone_names"].tolist()
    assert source["bone_names"] == names
    used = sorted({joint for joints, weights in zip(source["joints"], source["weights"]) for joint, weight in zip(joints, weights) if weight > 0})
    assert used == list(range(len(names))), asset
    binds = reference["bindposes"]
    return {
        "asset": asset,
        "targetRendererId": renderer,
        "nativeChassis": chassis,
        "rendererPath": renderer_path,
        "boneCount": len(names),
        "weightedBoneCount": len(used),
        "allPaletteBonesWeighted": used == list(range(len(names))),
        "boneNamesSha256": hashlib.sha256("\n".join(names).encode()).hexdigest(),
        "inverseBindMatricesSha256": hashlib.sha256(binds.astype("<f8").tobytes()).hexdigest(),
        "sourceSha256": sha(OUT / f"{asset}.source.json"),
        "reference": {"path": str(reference_path.relative_to(ROOT)), "sha256": sha(reference_path)},
    }


records = [
    inspect("abyssal-crown-kraken-head-v2", 121035, "krakenHead", "kraken2"),
    inspect("sargassum-kraken-tentacle-v2", 121595, "krakenTentacle", "krakenTentacle"),
    inspect("royal-sargassum-seaking-tentacle-v3", 121315, "seaKingTentacleA/B", "KrakenGodTentacle"),
]
assert records[1]["inverseBindMatricesSha256"] != records[2]["inverseBindMatricesSha256"], "Distinct 20-bone renderer bind profiles must remain distinct assets."
result = {
    "status": "PASS_V3",
    "assets": records,
    "authoringBoundary": "Each source weights every member of its exact palette. The two 20-bone assets share authored intent but retain distinct bind-profile exports.",
    "scope": "Exact palette and bind identity only. It does not establish live renderer replacement, animation safety, portrait framing, material behavior, culling, death or gameplay.",
}
(OUT / "target-bindings-proof.json").write_text(json.dumps(result, indent=2) + "\n")
print(result["status"])
