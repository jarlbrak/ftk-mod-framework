#!/usr/bin/env python3
"""Archive the fresh corrected-palette Cairnfire Troll B trial.

The archive copies review derivatives and lossless metadata only. It never
copies a game binary, native game asset, or local extracted reference. Running
without FTK_ARCHIVE_REBUILD=1 refuses to overwrite completed evidence.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
ASSET = ROOT / "art-experiments" / "cairnfire-troll"
OUT = ASSET / "live-validation-v2-palette-corrected"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
CATALOG = GAME / "model-test-profiles.json"
CATALOG_SHA256 = "32351b3c32bcb0dc6d0e84b81a89de319537578abf7a613f8cabe8f2d99f7a37"
PROFILE_KEY = "ftkmf_modeltest_cairnfire_trollb"
SESSION = "e36135bb345f4697a89f917cc7033aca"
STAGE = "case-36fcddc247d249c393d04d124c8f13d2"
CASE = "case-08a1f70c16854700baf33c46fe5c683b"
COVERAGE = ROOT / "scratch" / "coverage-batch-20260911-221638.json"
INITIAL_STAGE = ROOT / "scratch" / "cairnfire-trollb-stage" / "receipt.json"
INITIAL_DEPLOYMENT = GAME / "deployment-backups" / "cairnfire-trollb-20260911-220430" / "deployment.json"
REVISION_STAGE = ROOT / "scratch" / "cairnfire-trollb-palette-v2-stage" / "receipt.json"
REVISION_DEPLOYMENT = GAME / "deployment-backups" / "cairnfire-trollb-palette-v2-20260911-221402" / "deployment.json"
BONE_SIGNATURE = "fb452d83882447420a1beecc09d88cbf4e12a775fda39d57944a486c9c3fe402"
ASSETS = {
    "cairnfire-trollb.glb": "719e7c836eaf9420ac59b9edcfaf5c1eeb6575b02827c5c4596e86bf1e21b03b",
    "cairnfire-trollb.png": "3a2918930420914da3c80b2a2424fd7e1792659a5963142e5f241743a3066c2a",
}
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
AUTHORING = [
    "README.md",
    "manifest.json",
    "runtime-profile.json",
    "build_geometry.py",
    "verify_original_geometry.py",
    "render_studio.py",
    "build-report.json",
    "original-geometry-proof.json",
    "cairnfire-trollb.source.json",
    "cairnfire-trollb.pieces.json",
    "cairnfire-trollb.validation.json",
    "cairnfire-trollb.glb",
    "cairnfire-trollb.png",
    "cairnfire-trollb-hero.png",
    "cairnfire-trollb-side.png",
]
EXTRA_SELECTED = [
    ("pass-native-attack", "3db7ac0fddbe4cdd8112750da2deae28", 60, "Native attack_troll pose inside the pass window."),
    ("attack-native-attack", "78cae2c2e3db474b836d03e00f4f6859", 80, "Native attack_troll pose during the ordinary-hit window."),
    ("kill-ragdoll", "12cdf776d5d34e92a387287f12448faa", 40, "Native deathHeavy_troll and ragdoll pose after the fixture death."),
    ("kill-settled", "12cdf776d5d34e92a387287f12448faa", 60, "Later sampled ragdoll pose after the fixture death."),
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def add_source(sources: set[Path], path: Path) -> None:
    value = path.resolve()
    assert value.is_file() and not value.is_symlink(), value
    assert value.is_relative_to(ROOT), value
    assert value.suffix.lower() not in FORBIDDEN_SUFFIXES, value
    sources.add(value)


def enemy_snapshot(snapshot: dict) -> dict:
    matches = [row for row in snapshot["combat"]["enemies"] if row.get("type") == PROFILE_KEY]
    assert len(matches) == 1, matches
    return matches[0]


def expected_profile() -> dict:
    return {
        "key": PROFILE_KEY,
        "baseEnemy": "trollB",
        "displayName": "Cairnfire Troll B",
        "combatProfile": "ab549dcfcad738d283caac671d43a8b7053e64a95ae0805babf218599efe09a2",
        "renderers": [{
            "rendererPath": "enTroll01",
            "glbFile": "cairnfire-trollb.glb",
            "textureFile": "cairnfire-trollb.png",
            "disableNativeEmission": True,
        }],
        "minimumBaseHealth": 64,
    }


def assert_renderer(renderer: dict) -> None:
    assert renderer["ownerRootName"] == "Enemy Dummy"
    assert renderer["celRootName"] == "enTroll02(Clone)"
    assert renderer["celRelativeRendererPath"] == "enTroll01"
    assert renderer["rendererKind"] == "SkinnedMeshRenderer"
    assert renderer["mesh"] == "ftkmf_glb_cairnfire-trollb.glb"
    assert renderer["boneSignature"] == BONE_SIGNATURE
    assert renderer["rootBone"] == "Root_M"
    assert renderer["isVisible"] is True
    assert renderer["animator"]["controller"] == "trollController"
    lease = renderer["resourceLease"]
    assert all(lease[key] is True for key in ("available", "present", "acquired", "applied"))
    assert len(renderer["materials"]) == 1
    material = renderer["materials"][0]
    assert material["emissionKeyword"] is False
    assert material["_EmissionMap"] is None
    assert material["_MainTex"]["name"] == "ftkmf_cairnfire-trollb.png"


def video_from_frames(frame_dir: Path, frame_count: int, label: str) -> dict:
    output = OUT / "video" / (label + ".mp4")
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", str(frame_count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == frame_count
    return {
        "label": label,
        "path": str(output.relative_to(OUT)),
        "sha256": sha(output),
        "frames": frame_count,
        "width": int(probe["width"]),
        "height": int(probe["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative. Raw capture metadata and PNG hashes preserve the evidence timing record.",
    }


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
assert sha(CATALOG) == CATALOG_SHA256
assert {name: sha(ASSET / name) for name in ASSETS} == ASSETS
GAME_MODELS = GAME / "BepInEx" / "plugins" / "FTKModFramework_content" / "models"
assert {name: sha(GAME_MODELS / name) for name in ASSETS} == ASSETS

catalog = read(CATALOG)
assert isinstance(catalog, dict) and catalog["version"] == 1
assert next(row for row in catalog["profiles"] if row["key"] == PROFILE_KEY) == expected_profile()

initial_stage = read(INITIAL_STAGE)
initial_deployment = read(INITIAL_DEPLOYMENT)
revision_stage = read(REVISION_STAGE)
revision_deployment = read(REVISION_DEPLOYMENT)
assert initial_stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
assert initial_deployment["status"] == "VERIFIED_COMPLETE"
assert revision_stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
assert revision_stage["replacedAssets"] == {
    "cairnfire-trollb.png": {
        "old": "d71084a625742a057d3d47703ceb620fd129aa70d5b0be826a54286a7626e962",
        "new": ASSETS["cairnfire-trollb.png"],
    }
}
assert revision_deployment["status"] == "VERIFIED_COMPLETE"

case_path = BASE / CASE / "case-result.json"
stage_path = BASE / STAGE / "result.json"
session_path = BASE / f"new-run-session-{SESSION}.json"
case = read(case_path)
stage = read(stage_path)
session = read(session_path)
coverage = read(COVERAGE)
assert isinstance(case, dict) and isinstance(stage, dict) and isinstance(session, dict) and isinstance(coverage, list)
assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["session"] == SESSION and case["enemy"] == PROFILE_KEY
assert case["profileSha256"] == CATALOG_SHA256
assert case["status"] == "needs_visual_review" and case["visualReview"] == "pending"
assert_renderer(case["initialRenderer"])
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert case["attackRetrySummary"] == [{
    "attempt": 1,
    "status": "nonlethal_hp_loss",
    "beforeHp": 72,
    "afterHp": 62,
    "boundary": "Observed same-target HP only; no inferred block, dodge, hit animation, or damage source.",
}]
assert session == {"session": SESSION, "case": STAGE.removeprefix("case-"), "journal": str(BASE / STAGE / "journal.jsonl")}
assert stage["status"] == "binding_metadata_observed"
assert stage["session"] == SESSION and stage["enemy"] == PROFILE_KEY
assert [(row["rendererPath"], row["glbFile"], row["boneSignature"]) for row in stage["matches"]] == [
    ("enTroll01", "cairnfire-trollb.glb", BONE_SIGNATURE),
]
assert len(coverage) == 1
assert coverage[0]["profile"] == PROFILE_KEY and coverage[0]["case"] == CASE and coverage[0]["session"] == SESSION
assert coverage[0]["error"] is None and coverage[0]["exerciseExitCode"] == 0

sources: set[Path] = set()
for path in [
    Path(__file__), CATALOG, COVERAGE, INITIAL_STAGE, INITIAL_DEPLOYMENT, REVISION_STAGE, REVISION_DEPLOYMENT,
    case_path, BASE / CASE / "journal.jsonl", stage_path, BASE / STAGE / "journal.jsonl", session_path, Path(session["journal"]),
    ROOT / "tools/ai-model-pipeline/stage_custom_model_profile.py", ROOT / "tools/ai-model-pipeline/deploy_custom_model_stage.py",
]:
    add_source(sources, path)
for name in AUTHORING:
    add_source(sources, ASSET / name)

captures = []
image_pins: dict[str, str] = {}
videos = []
for action, expected_before, expected_after in (("pass", 72, 72), ("attack", 72, 62), ("kill-fixture", 62, 0)):
    record = next(row for row in case["actions"] if row["action"] == action)
    capture = record["capture"]
    raw = capture["rawCapture"]
    raw_path = Path(raw["path"])
    raw_data = read(raw_path)
    assert isinstance(raw_data, dict)
    frame_dir = raw_path.with_suffix("")
    frames = sorted(frame_dir.glob("*.png"))
    boundary = capture["boundary"]
    assert sha(raw_path) == raw["sha256"]
    assert raw_data["ok"] is True
    assert boundary["completeCapture"] is True and boundary["rawCaptureOk"] is True
    assert boundary["retainedFrameCount"] == len(frames) == 120
    assert boundary["termination"] is None and boundary["limitation"] is None
    before, after = enemy_snapshot(record["before"]), enemy_snapshot(record["after"])
    assert (before["hp"], after["hp"]) == (expected_before, expected_after)
    assert before["alive"] is True and after["alive"] is (action != "kill-fixture")
    for path in [raw_path, Path(record["journal"]["path"]), Path(record["rawResult"]["path"])]:
        add_source(sources, path)
    for frame in frames:
        add_source(sources, frame)
        image_pins[relative(frame)] = sha(frame)
    captures.append({
        "label": action,
        "captureId": raw_path.stem,
        "rawCapture": {"source": relative(raw_path), "sha256": sha(raw_path)},
        "frames": len(frames),
        "complete": True,
        "dimensions": {"width": raw_data["width"], "height": raw_data["height"], "requestedFps": raw_data["requestedFps"]},
        "enemyHpBefore": before["hp"],
        "enemyHpAfter": after["hp"],
    })
    videos.append(video_from_frames(frame_dir, len(frames), action))

assert [entry["label"] for entry in captures] == ["pass", "attack", "kill-fixture"]
selected = []
for frame in case["selectedFrames"]:
    source = Path(frame["path"])
    assert sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"harness-{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(destination) == frame["sha256"]
    selected.append({
        "label": f"harness-{frame['action']}-{frame['index']:04d}",
        "source": relative(source),
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": "Harness-selected live frame. It establishes this frame only and grants no all-camera or final-art verdict.",
    })
assert len(case["selectedFrames"]) == 6
for label, capture_id, index, observation in EXTRA_SELECTED:
    source = BASE / capture_id / f"{index:04d}.png"
    assert source.is_file()
    destination = OUT / "selected" / (label + ".png")
    shutil.copy2(source, destination)
    assert sha(destination) == sha(source)
    selected.append({
        "label": label,
        "source": relative(source),
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": observation + " This is a sampled visual record only.",
    })

for name in AUTHORING:
    path = ASSET / name
    if path.suffix.lower() == ".png":
        image_pins[relative(path)] = sha(path)

metadata = []
for source in sorted(sources):
    if source.suffix.lower() == ".png":
        continue
    raw = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    metadata.append({
        "source": relative(source),
        "sourceSha256": sha(source),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", image_pins)
validation = {
    "status": "fresh_v2_palette_corrected_cairnfire_trollb_exact_binding_motion_gameplay_ready_observed_art_unapproved",
    "scope": "Fresh isolated corrected-palette Troll B run. It establishes exact binding, sampled native motion, one ordinary same-target HP loss, KillSingle fixture death, one native Collect, and strict Ready progression. It does not grant final art approval.",
    "profile": expected_profile(),
    "target": {
        "nativeChassis": "trollB",
        "nativePrefab": "enTroll02",
        "celComponentPathId": "137393",
        "rendererSourceId": 121256,
        "rendererPath": "enTroll01",
        "controller": "trollController",
        "boneSignature": BONE_SIGNATURE,
    },
    "provenance": {
        "catalog": {"path": relative(CATALOG), "sha256": sha(CATALOG)},
        "initialStage": {"path": relative(INITIAL_STAGE), "sha256": sha(INITIAL_STAGE)},
        "initialDeployment": {"path": relative(INITIAL_DEPLOYMENT), "sha256": sha(INITIAL_DEPLOYMENT)},
        "paletteRevisionStage": {"path": relative(REVISION_STAGE), "sha256": sha(REVISION_STAGE)},
        "paletteRevisionDeployment": {"path": relative(REVISION_DEPLOYMENT), "sha256": sha(REVISION_DEPLOYMENT)},
        "currentGameAssets": ASSETS,
        "coverageManifest": {"path": relative(COVERAGE), "sha256": sha(COVERAGE)},
    },
    "session": SESSION,
    "stageCase": {"path": relative(stage_path), "sha256": sha(stage_path), "status": stage["status"]},
    "exerciseCase": {"path": relative(case_path), "sha256": sha(case_path), "status": case["status"]},
    "binding": {
        "celRootName": case["initialRenderer"]["celRootName"],
        "celRelativeRendererPath": case["initialRenderer"]["celRelativeRendererPath"],
        "rendererKind": case["initialRenderer"]["rendererKind"],
        "mesh": case["initialRenderer"]["mesh"],
        "rootBone": case["initialRenderer"]["rootBone"],
        "boneSignature": case["initialRenderer"]["boneSignature"],
        "controller": case["initialRenderer"]["animator"]["controller"],
        "material": case["initialRenderer"]["materials"][0],
        "lease": case["initialRenderer"]["resourceLease"],
    },
    "captures": captures,
    "ordinaryAttack": case["attackRetrySummary"],
    "collect": case["collects"],
    "ready": case["finalReady"]["strictReady"],
    "selectedFrames": selected,
    "videos": videos,
    "visualReview": {
        "status": "corrected_palette_rendered_live_art_unapproved",
        "observed": "The revised PNG maps to dark blue-green basalt with copper and ember accents in live combat. Selected frames show a coherent silhouette during idle, native attack_troll, and deathHeavy_troll/ragdoll sampling.",
        "notAccepted": "The review does not establish all cameras, all animation intervals, portrait framing, culling behavior, resource disposal, or final artistic acceptance.",
    },
    "limitations": [
        "The ordinary attack records same-target HP only. It does not infer hit, block, dodge, damage source, or balance behavior.",
        "KillSingle is an explicit fixture, not ordinary lethal gameplay.",
        "The 120-frame death window observes a sampled ragdoll interval, not every possible death or cleanup state.",
        "The V1 color-inverted trial is a recorded revision finding, not art acceptance evidence for V2.",
    ],
}
write(OUT / "validation.json", validation)
integrity = {
    "status": "PASS",
    "validation": {"path": "validation.json", "sha256": sha(OUT / "validation.json")},
    "metadata": metadata,
    "sourceImages": {"count": len(image_pins), "pins": "source-image-pins.json"},
    "selected": selected,
    "videos": videos,
}
write(OUT / "integrity.json", integrity)
print(json.dumps({
    "status": "PASS",
    "metadata": len(metadata),
    "sourceImages": len(image_pins),
    "selected": len(selected),
    "videos": len(videos),
    "validationSha256": sha(OUT / "validation.json"),
}, indent=2))
