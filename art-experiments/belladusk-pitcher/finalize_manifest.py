#!/usr/bin/env python3
"""Freeze the Belladusk authoring bundle and its scoped live evidence."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence(path: Path) -> dict:
    return {
        "path": str(path.relative_to(ROOT)),
        "sha256": sha(path),
        "bytes": path.stat().st_size,
    }


findings_path = ROOT / "scratch" / "plant-e-native-topology-analysis" / "findings.json"
findings = json.loads(findings_path.read_text())
reference = np.load(ROOT / "scratch" / "plant-e-native-topology-analysis" / "reference-121530" / "reference.npz")
source = json.loads((OUT / "belladusk.source.json").read_text())
weights = np.array(source["weights"])
joints = np.array(source["joints"])
used = {int(index) for index in joints[weights > 0]}
native_used = {int(index) for index in reference["joints"][reference["weights"] > 0]}
assert used == native_used and len(used) == 29 and len(source["bone_names"]) == 37

for name in ["belladusk.validation.json", "reopened-validation.json"]:
    validation = json.loads((OUT / name).read_text())
    assert validation["bones"] == 37
    assert validation["max_bind_rest_error"] < 1e-5
    assert validation["source_normal_agreement"]["positive_fraction"] == 1
assert json.loads((OUT / "original-geometry-proof.json").read_text())["status"] == "PASS"
for label in ["pass", "hit", "death", "hit-mouth"]:
    audit = json.loads((OUT / f"native-{label}-pose-audit.json").read_text())
    assert audit["frames"] == 120 and audit["glbSha256"] == sha(OUT / "belladusk.glb")

proof = {
    "paletteBones": 37,
    "weightedBones": 29,
    "softVertices": int(((weights > 0).sum(1) > 1).sum()),
    "positiveVerticesByBone": {
        name: int(((joints == index) & (weights > 0)).any(1).sum())
        for index, name in enumerate(source["bone_names"])
    },
    "nativeUnusedRetained": [
        name for index, name in enumerate(source["bone_names"]) if index not in used
    ],
}
(OUT / "weight-proof.json").write_text(json.dumps(proof, indent=2) + "\n")

profile_document = {
    "version": 1,
    "profiles": [{
        "key": "ftkmf_modeltest_belladusk",
        "baseEnemy": "plantE",
        "displayName": "Belladusk Pitcher",
        "combatProfile": findings["profile"]["combatProfile"],
        "renderers": [{
            "rendererPath": "enJungleNibbler_A",
            "glbFile": "belladusk.glb",
            "textureFile": "belladusk_basecolor.png",
            "disableNativeEmission": True,
        }],
        "minimumBaseHealth": 64,
        "visualScale": 1.0,
    }],
}
(OUT / "runtime-profile.json").write_text(json.dumps(profile_document, indent=2) + "\n")

offline_review = {
    "reviewer": "Parent /root",
    "reviewedImage": evidence(OUT / "native-hit-mouth-pose-study.png"),
    "reviewedFrames": [0, 27, 28, 29, 30, 31],
    "verdict": "At hit27-30 continuous purple tube connects rim to hood; no detached ring, native opening retained. Approved for final offline package and future live trial.",
    "scope": "Selected original offline views only; actual game rendering, portraits, culling and native effects still need review.",
}
(OUT / "root-offline-review.json").write_text(json.dumps(offline_review, indent=2) + "\n")

live_evidence = {}
for relative in [
    "live-validation-v1.json",
    "live-validation-v2/validation.json",
    "live-validation-v3/validation.json",
]:
    path = OUT / relative
    if path.is_file():
        live_evidence[relative] = sha(path)

files = {
    str(path.relative_to(OUT)): sha(path)
    for path in sorted(OUT.rglob("*"))
    if path.is_file() and path.name != "manifest.json"
}
manifest = {
    "model": "Belladusk Pitcher",
    "status": "FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE",
    "nativeEnemy": "plantE",
    "rendererId": 121530,
    "rendererPath": "enJungleNibbler_A",
    "boneCount": 37,
    "nativeCelScale": findings["nativeScale"],
    "visualScaleFactor": 1.0,
    "sourceFindings": evidence(findings_path),
    "profile": profile_document["profiles"][0],
    "assets": [
        evidence(OUT / "belladusk.glb"),
        evidence(OUT / "belladusk_basecolor.png"),
    ],
    "liveEvidence": live_evidence,
    "files": files,
    "limits": [
        "Both historical mouth failures are retained; neither is the final asset.",
        "The open inner funnel and outer sleeve have scoped surface-orientation checks, not a global watertight-mesh claim.",
        "Offline pass and hit studies normalize Root_M; the death study retains renderer-local travel; head closeups use per-frame framing.",
        "The canonical live V3 archive covers only plantE / enJungleNibbler_A renderer 121530 and keeps ordinary damage separate from explicit-fixture death.",
        "V3 records animator-driven death with m_DoRagdoll false and no rigidbodies. It does not establish collision, physics sleeping, precise corpse lifetime, portraits, full culling, final resource disposal or finished art direction.",
    ],
}
(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps({
    "manifest": str(OUT / "manifest.json"),
    "sha256": sha(OUT / "manifest.json"),
    "files": len(files),
    "liveEvidence": live_evidence,
}, indent=2))
