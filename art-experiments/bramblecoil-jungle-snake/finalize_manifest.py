#!/usr/bin/env python3
"""Freeze the offline Bramblecoil authoring bundle with deterministic pins."""
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
    "run_death_fixture_v1.py",
    "run_deathlight_playback_v1.py",
    "run_deathlight_trigger_v1.py",
    "run_falloff_fixture_v2.py",
    "verify_falloff_fixture_v2.py",
    "run_falloff_cleanup_v1.py",
    "falloff-policy-comparison-v2.json",
    "death-handoff-analysis-v1.json",
    "runtime-profile.json",
    "studio-review.json",
    "hero.png",
    "side.png",
    "portrait.png",
    "bramblecoil-jungle-snake.blend",
    "bramblecoil-jungle-snake-studio.blend",
    "bramblecoil-jungle-snake.glb",
    "bramblecoil-jungle-snake.source.json",
    "bramblecoil-jungle-snake.pieces.json",
    "bramblecoil-jungle-snake.validation.json",
    "bramblecoil-jungle-snake_basecolor.png",
    "bramblecoil-jungle-snake-reopened.glb",
    "bramblecoil-jungle-snake-reopened.png",
    "bramblecoil-jungle-snake-reopened.source.json",
    "bramblecoil-jungle-snake-reopened.validation.json",
    "reopened-validation.json",
    "original-geometry-proof.json",
    "target-binding-proof.json",
]
for name in required:
    assert (OUT / name).is_file(), name
runtime = json.loads((OUT / "runtime-profile.json").read_text())
assert len(runtime["profiles"]) == 1
profile = runtime["profiles"][0]
assets = {name: sha(OUT / name) for name in ("bramblecoil-jungle-snake.glb", "bramblecoil-jungle-snake_basecolor.png")}
live_evidence = {}
for version in ("v1", "v2", "v3"):
    evidence = OUT / f"live-validation-{version}/validation.json"
    if evidence.is_file():
        live_evidence[f"live-validation-{version}/validation.json"] = sha(evidence)
manifest = {
    "status": (
        "FROZEN_ORIGINAL_CANDIDATE_WITH_SCOPED_LIVE_EVIDENCE"
        if live_evidence
        else "FROZEN_OFFLINE_ORIGINAL_CANDIDATE_LIVE_VALIDATION_PENDING"
    ),
    "name": "Bramblecoil Viper",
    "nativeEnemy": "snakeJungleC",
    "referenceRendererId": 121424,
    "rendererPath": "enJungleSnakeC",
    "boneCount": 44,
    "nativeSurfaceCopied": False,
    "authoringInputs": "bone_names and inverse bind matrices only; proof restricts the generator to those keys.",
    "profile": profile,
    "originalAssets": assets,
    "files": {name: sha(OUT / name) for name in required},
    "liveEvidence": live_evidence,
    "limits": [
        "Static studio render and binary checks do not prove native animation, portrait framing, camera fit, culling, material lifetime, ragdoll, or gameplay; named live evidence has its own bounded claims.",
        "The exact Jungle C native root scale is 1.0; public visualScale stays 1.0 until the spawned custom clone confirms it.",
        "Normal Jungle C Death can hide enJungleSnakeC and activate 13 native quetzalPiece fall-off fragments. live-validation-v1 records the opted exact-lease policy retaining the custom body through a fixture death, a same-binary omitted-policy control, and native Collect to strict Ready; this does not establish ordinary lethal damage or corpse presentation quality.",
        "This profile is only snakeJungleC/enJungleSnakeC. It does not establish the incompatible 46-bone Desert snake family or other snake renderers.",
    ],
}
(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps({"manifest": str(OUT / "manifest.json"), "assets": assets, "liveEvidence": live_evidence}, indent=2))
