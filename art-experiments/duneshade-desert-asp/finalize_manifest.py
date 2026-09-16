#!/usr/bin/env python3
"""Freeze the offline Duneshade authoring bundle with deterministic pins."""
import hashlib
import json
from pathlib import Path


OUT = Path(__file__).resolve().parent
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()

required = [
    "README.md",
    "build_geometry.py",
    "build_blender.py",
    "verify_original_geometry.py",
    "verify_target_binding.py",
    "finalize_manifest.py",
    "stage_runtime_profile.py",
    "runtime-profile.json",
    "studio-review.json",
    "hero.png",
    "side.png",
    "portrait.png",
    "duneshade-desert-asp.blend",
    "duneshade-desert-asp-studio.blend",
    "duneshade-desert-asp.glb",
    "duneshade-desert-asp.source.json",
    "duneshade-desert-asp.pieces.json",
    "duneshade-desert-asp.validation.json",
    "duneshade-desert-asp_basecolor.png",
    "duneshade-desert-asp-reopened.glb",
    "duneshade-desert-asp-reopened.png",
    "duneshade-desert-asp-reopened.source.json",
    "duneshade-desert-asp-reopened.validation.json",
    "reopened-validation.json",
    "original-geometry-proof.json",
    "target-binding-proof.json",
]
for name in required:
    assert (OUT / name).is_file(), name
runtime = json.loads((OUT / "runtime-profile.json").read_text())
assert len(runtime["profiles"]) == 1
profile = runtime["profiles"][0]
assets = {name: sha(OUT / name) for name in ("duneshade-desert-asp.glb", "duneshade-desert-asp_basecolor.png")}
live_evidence = {}
for evidence in sorted(OUT.glob("live-validation-*/validation.json")):
    if evidence.is_file() and not evidence.is_symlink():
        live_evidence[str(evidence.relative_to(OUT))] = sha(evidence)
manifest = {
    "status": (
        "FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE"
        if live_evidence
        else "FROZEN_OFFLINE_ORIGINAL_CANDIDATE_LIVE_VALIDATION_PENDING"
    ),
    "name": "Duneshade Asp",
    "nativeEnemy": "snakeDesertA",
    "referenceRendererId": 121552,
    "rendererPath": "enDesertSnakeA",
    "boneCount": 46,
    "nativeSurfaceCopied": False,
    "authoringInputs": "bone_names and inverse bind matrices only; proof restricts the generator to those keys.",
    "profile": profile,
    "originalAssets": assets,
    "files": {name: sha(OUT / name) for name in required},
    "liveEvidence": live_evidence,
    "limits": [
        "Static studio render and binary checks do not prove native animation, portrait framing, camera fit, culling, material lifetime, ragdoll, or gameplay; named live evidence has its own bounded claims.",
        "The exact Desert A native root scale is 0.65; public visualScale remains 1.0 so the authored mesh uses the chassis scale without a second authored-scale multiplier.",
        "Desert A has ordinary skeleton ragdoll death and no source-proven FallOffLimb hand-off or external native fragment mesh set. live-validation-v2-canonical records the exact custom body through an explicit KillSingle fixture, the animator and rigidbody hand-off, settling, one native Collect and strict Ready; it does not establish ordinary lethal damage, general corpse lifetime or finished corpse presentation.",
        "This profile is only snakeDesertA/enDesertSnakeA. It does not establish other 46-bone snake renderers, 44-bone Jungle C, or visual interchangeability between families.",
    ],
}
(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps({"manifest": str(OUT / "manifest.json"), "assets": assets, "liveEvidence": live_evidence}, indent=2))
