#!/usr/bin/env python3
"""Independently verify Sablevine Serpent's immutable V2 scale-0.55 archive."""
from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
OUT = ROOT / "art-experiments" / "sablevine-basey-snake" / "live-validation-v2-scale055"
EXPECTED_KEY = "ftkmf_modeltest_sablevine_basey_snake_fit055"
EXPECTED_SIGNATURE = "dc3975c33683e227cca8a762d40e7d65c8af7b4167d2928dd4efc2cd0d421c51"
EXPECTED_SCALE = [0.550000011920929] * 3


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def child(root: Path, value: str) -> Path:
    relative = Path(value)
    assert not relative.is_absolute() and ".." not in relative.parts, value
    result = root / relative
    assert result.resolve().is_relative_to(root.resolve()), result
    return result


def verify_snapshot(entry: dict, expected_source_sha256: str | None = None) -> dict:
    path = child(OUT, entry["archive"])
    assert path.is_file() and not path.is_symlink(), path
    assert sha(path) == entry["archiveSha256"]
    raw = gzip.decompress(path.read_bytes())
    assert hashlib.sha256(raw).hexdigest() == entry["sourceSha256"]
    if expected_source_sha256 is not None:
        assert entry["sourceSha256"] == expected_source_sha256
    assert entry["bytes"] == len(raw)
    value = json.loads(raw)
    assert isinstance(value, dict), path
    return value


def archived_mapping(entry: dict) -> bytes:
    path = child(OUT, entry["archive"])
    assert path.is_file() and not path.is_symlink(), path
    assert sha(path) == entry["archiveSha256"]
    raw = gzip.decompress(path.read_bytes())
    assert hashlib.sha256(raw).hexdigest() == entry["sourceSha256"]
    return raw


validation = read(OUT / "validation.json")
assets = read(OUT / "asset-pins.json")
images = read(OUT / "source-image-pins.json")
capture_images = read(OUT / "capture-image-pins.json")

assert validation["status"] == "reviewed_resource_enbaseysnake_binding_motion_gameplay_observed_camera_fit_accepted_limited_art_pending"
assert validation["revision"] == "V2-scale055"
assert validation["assetHashes"] == assets
assert validation["nativeChassis"] == validation["baseEnemy"] == "snakeJungleA"
assert validation["resourcePrefab"] == "enbaseysnake"
assert validation["rendererPaths"] == ["enSnake_Basey"]
assert validation["profile"]["key"] == validation["enemy"] == EXPECTED_KEY
assert validation["profile"]["visualScale"] == 0.55

catalog = verify_snapshot(validation["catalogSnapshot"], validation["catalogSha256"])
registration = verify_snapshot(validation["registrationSnapshot"])
assert [row for row in catalog["profiles"] if row["key"] == EXPECTED_KEY] == [validation["profile"]]
registered = [row for row in registration["registered"] if row.get("key") == EXPECTED_KEY]
assert len(registered) == 1
assert registered[0]["resourcePrefab"] == "enbaseysnake"
assert registered[0]["visualScale"] == EXPECTED_SCALE[0]
assert registered[0]["nativePrefabRootLocalScale"] == [1.0, 1.0, 1.0]

mappings = {entry["source"]: entry for entry in validation["losslessMappings"]}
assert len(mappings) == len(validation["losslessMappings"])
for source, entry in mappings.items():
    assert source.startswith(("art-experiments/", "scratch/", "tools/")), source
    archived_mapping(entry)

for pin_name in ("setupBinding", "caseResult", "runtimeProfile", "profileRevision", "routePreflight", "stageReceipt", "binaryDeploymentReceipt"):
    pin = validation[pin_name]
    assert pin["path"] in mappings
    assert mappings[pin["path"]]["sourceSha256"] == pin["sha256"]

for name, digest in assets.items():
    source = f"art-experiments/sablevine-basey-snake/{name}"
    if source.endswith(".png"):
        assert images[source] == digest
    else:
        assert source in mappings and mappings[source]["sourceSha256"] == digest

assert validation["sourceImageCount"] == len(images)
assert validation["captureImageCount"] == len(capture_images) == 360
for source, digest in images.items():
    assert source.startswith(("art-experiments/", "scratch/")), source
    assert isinstance(digest, str) and len(digest) == 64, source
for source, digest in capture_images.items():
    assert source.startswith("scratch/mirewarden-game/model-test-output/"), source
    assert images[source] == digest

selected = validation["selectedPNGs"]
assert len(selected) == 11
assert len({row["archive"] for row in selected}) == len(selected)
for row in selected:
    source = row["source"]
    assert capture_images[source] == row["sha256"]
    archived = child(OUT, row["archive"])
    assert archived.is_file() and not archived.is_symlink(), archived
    assert sha(archived) == row["sha256"]

captures = validation["captures"]
assert [capture["action"] for capture in captures] == ["pass", "attack", "kill-fixture"]
by_action = {capture["action"]: capture for capture in captures}
for capture in captures:
    assert capture["complete"] is True and capture["frameCount"] == 120
    assert capture["pausedFrameCount"] == 0 and capture["meshIdentityStable"] is True
    assert capture["celRootLocalScale"] == EXPECTED_SCALE
    raw_pin = capture["rawCapture"]
    assert raw_pin["path"] in mappings and mappings[raw_pin["path"]]["sourceSha256"] == raw_pin["sha256"]
    raw = json.loads(archived_mapping(mappings[raw_pin["path"]]))
    assert raw["ok"] is True and len(raw["frames"]) == 120
    for frame in raw["frames"]:
        assert frame["celRelativeRendererPath"] == "enSnake_Basey"
        assert frame["mesh"] == "ftkmf_glb_sablevine.glb"
        assert frame["boneSignature"] == EXPECTED_SIGNATURE
        assert frame["celRootLocalScale"] == EXPECTED_SCALE
        assert frame["isVisible"] is True and frame["active"] is True
    summary = ROOT / capture["summary"]["path"]
    assert summary.is_file() and not summary.is_symlink(), summary
    assert sha(summary) == capture["summary"]["sha256"]
    summarized = read(summary)
    assert summarized["captureId"] == raw_pin["path"].split("/")[-1].removesuffix(".json")
    assert summarized["frameCount"] == capture["frameCount"]
    assert summarized["meshIdentityStable"] is True

assert by_action["pass"]["causalMotion"]["idle"]["sampleIndex"] == 0
assert by_action["pass"]["causalMotion"]["attack"]["nativeTrigger"]["trigger"] == "Attack"
assert by_action["attack"]["causalMotion"]["hit"]["nativeTrigger"]["trigger"] == "Damaged"
assert by_action["attack"]["causalMotion"]["hit"]["nativeAction"]["primary"]["newHealth"] == 45
assert by_action["attack"]["causalMotion"]["hit"]["nativeAction"]["postTargetHealth"] == 58
assert by_action["kill-fixture"]["causalMotion"]["death"]["nativeTrigger"]["trigger"] == "Death"

scale = validation["scaleContract"]
assert scale == {
    "requestedFactor": 0.55,
    "nativePrefabRootLocalScale": [1.0, 1.0, 1.0],
    "expectedSpawnedCelRootLocalScale": [0.55, 0.55, 0.55],
    "observedSpawnedCelRootLocalScale": EXPECTED_SCALE,
    "tolerance": 0.0001,
    "scope": "Registered public profile, binding probe, and every frame of all three complete captures.",
}
binding = validation["binding"]
assert binding["resourcePrefab"] == "enbaseysnake"
assert binding["rendererPath"] == "enSnake_Basey" and binding["mesh"] == "sablevine.glb"
assert binding["catalogSha256"] == validation["catalogSha256"]
assert binding["requestedVisualScale"] == 0.55
assert binding["nativePrefabRootLocalScale"] == [1.0, 1.0, 1.0]
assert binding["spawnedCelRootLocalScale"] == EXPECTED_SCALE
assert validation["cameraFit"]["accepted"] is True
ordinary = validation["ordinaryHit"]
assert ordinary == {
    "action": "attack", "cheat": "None", "focus": False,
    "beforeHp": 58, "afterHp": 45, "clip": "Snake_HitSmall",
    "scope": "same-target observed nonlethal native damage",
}
assert validation["ordinaryLethal"] is False
assert validation["explicitKillFixture"]["action"] == "kill-fixture"
assert validation["finalReady"]["ok"] is True
assert validation["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

review_source = validation["rootVisualReview"]
review = json.loads(archived_mapping(mappings[review_source]))
assert review["reviewStatus"] == validation["visualReview"]
assert review["cameraFit"]["accepted"] is True
assert len(review["frames"]) == 11

v1 = validation["v1Reference"]
v1_path = ROOT / v1["validation"]
assert v1_path.is_file() and sha(v1_path) == v1["validationSha256"]
assert read(v1_path)["status"] == v1["status"]

for video in validation["videos"]:
    path = child(OUT, video["path"])
    assert path.is_file() and not path.is_symlink(), path
    assert sha(path) == video["sha256"]
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == video["frames"] == 120
    assert int(stream["width"]) == video["width"] and int(stream["height"]) == video["height"]

print(json.dumps({
    "status": "PASS",
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "metadataRoundTrips": len(mappings),
    "captureImages": len(capture_images),
    "selected": len(selected),
    "captures": len(captures),
    "videos": len(validation["videos"]),
    "scale": EXPECTED_SCALE,
}, indent=2))
