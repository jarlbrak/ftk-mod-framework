#!/usr/bin/env python3
"""Independently verify the immutable Rivenquill Cockatrice boss archive."""
from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
OUT = ROOT / "art-experiments" / "rivenquill-basey-cockatrice" / "live-validation-v1-boss"
EXPECTED_KEY = "ftkmf_modeltest_rivenquill_basey_cockatrice_boss"
EXPECTED_NATIVE = "bossCockatrice"
EXPECTED_RESOURCE = "enbaseycockatriceboss"
EXPECTED_RENDERER = "enBaseyCockatrice"
EXPECTED_SOURCE_RENDERER = 121693
EXPECTED_SIGNATURE = "c591e94b65dd3510b8677818f6c7cdc307a1339e20b36fa7ceb911d0cfbaddef"
EXPECTED_SCALE = [0.8999999761581421] * 3


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_object(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def child(root: Path, value: str) -> Path:
    relative = Path(value)
    assert not relative.is_absolute() and ".." not in relative.parts, value
    path = root / relative
    assert path.resolve().is_relative_to(root.resolve()), path
    return path


def mapped_raw(mapping: dict) -> bytes:
    archive = child(OUT, mapping["archive"])
    assert archive.is_file() and not archive.is_symlink(), archive
    assert sha256(archive) == mapping["archiveSha256"]
    raw = gzip.decompress(archive.read_bytes())
    assert hashlib.sha256(raw).hexdigest() == mapping["sourceSha256"]
    assert mapping["encoding"] == "gzip-lossless"
    return raw


validation = read_object(OUT / "validation.json")
assets = read_object(OUT / "asset-pins.json")
source_images = read_object(OUT / "source-image-pins.json")
capture_images = read_object(OUT / "capture-image-pins.json")
review = read_object(OUT / "visual-review.json")

assert validation["status"] == "reviewed_resource_enbaseycockatriceboss_binding_motion_gameplay_observed_camera_fit_accepted_limited_art_pending"
assert validation["revision"] == "V1-boss"
assert validation["enemy"] == validation["profile"]["key"] == EXPECTED_KEY
assert validation["nativeChassis"] == validation["baseEnemy"] == EXPECTED_NATIVE
assert validation["resourcePrefab"] == EXPECTED_RESOURCE
assert validation["rendererPaths"] == [EXPECTED_RENDERER]
assert validation["sourceRendererIds"] == [EXPECTED_SOURCE_RENDERER]
assert validation["assetHashes"] == assets
assert validation["visualReview"] == review["reviewStatus"]
assert validation["rootVisualReview"] == "visual-review.json"

catalog = validation["catalogSnapshot"]
registration = validation["registrationSnapshot"]
catalog_raw = mapped_raw(catalog)
assert hashlib.sha256(catalog_raw).hexdigest() == validation["catalogSha256"]
catalog_doc = json.loads(catalog_raw)
assert [row for row in catalog_doc["profiles"] if row.get("key") == EXPECTED_KEY] == [validation["profile"]]
registration_doc = json.loads(mapped_raw(registration))
registered = [row for row in registration_doc["registered"] if row.get("key") == EXPECTED_KEY]
assert len(registered) == 1
assert registered[0]["baseEnemy"] == EXPECTED_NATIVE and registered[0]["resourcePrefab"] == EXPECTED_RESOURCE

mappings = {row["source"]: row for row in validation["losslessMappings"]}
assert len(mappings) == len(validation["losslessMappings"])
for source, mapping in mappings.items():
    assert source.startswith(("art-experiments/", "scratch/", "tools/")), source
    mapped_raw(mapping)
for name in ("setupBinding", "caseResult", "runtimeProfile", "routePreflight", "stageReceipt"):
    pin = validation[name]
    assert pin["path"] in mappings
    assert mappings[pin["path"]]["sourceSha256"] == pin["sha256"]
for name, digest in assets.items():
    source = f"art-experiments/rivenquill-basey-cockatrice/{name}"
    if source.endswith(".png"):
        assert source_images[source] == digest
    else:
        assert source in mappings and mappings[source]["sourceSha256"] == digest

assert validation["sourceImageCount"] == len(source_images)
assert validation["captureImageCount"] == len(capture_images) == 331
for source, digest in source_images.items():
    assert isinstance(digest, str) and len(digest) == 64, source
for source, digest in capture_images.items():
    assert source.startswith("scratch/mirewarden-game/model-test-output/"), source
    assert source_images[source] == digest

selected = validation["selectedPNGs"]
assert len(selected) == len(review["frames"]) == 15
assert len({row["archive"] for row in selected}) == len(selected)
for row in selected:
    assert capture_images[row["path"]] == row["sha256"]
    archive = child(OUT, row["archive"])
    assert archive.is_file() and not archive.is_symlink()
    assert sha256(archive) == row["sha256"]

captures = {capture["action"]: capture for capture in validation["captures"]}
assert set(captures) == {"pass", "attack", "kill-fixture"}
assert captures["pass"]["complete"] is True and captures["pass"]["frameCount"] == 120
assert captures["attack"]["complete"] is True and captures["attack"]["frameCount"] == 120
assert captures["kill-fixture"]["complete"] is False and captures["kill-fixture"]["frameCount"] == 91
assert captures["kill-fixture"]["termination"] == "renderer_destroyed"
for action, capture in captures.items():
    raw = json.loads(mapped_raw(mappings[capture["rawCapture"]["path"]]))
    assert len(raw["frames"]) == capture["frameCount"]
    for frame in raw["frames"]:
        assert frame["celRelativeRendererPath"] == EXPECTED_RENDERER
        assert frame["mesh"] == "ftkmf_glb_rivenquill.glb"
        assert frame["boneSignature"] == EXPECTED_SIGNATURE
        assert frame["celRootLocalScale"] == EXPECTED_SCALE
    if action != "kill-fixture":
        assert raw["ok"] is True
        assert all(frame["isVisible"] is True and frame["active"] is True for frame in raw["frames"])
    else:
        assert raw["ok"] is False and "Renderer destroyed during capture" in raw["error"]
        assert raw["frames"][-1]["isVisible"] is False and raw["frames"][-1]["active"] is False

assert captures["pass"]["causalMotion"]["attack"]["nativeTrigger"]["trigger"] in {"Attack", "AttackProf", "AttackProf1"}
assert captures["attack"]["causalMotion"]["hit"]["nativeTrigger"]["trigger"] == "Damaged"
assert captures["kill-fixture"]["causalMotion"]["death"]["nativeTrigger"]["trigger"] == "Death"
assert validation["ordinaryHit"] == {
    "action": "attack", "cheat": "None", "focus": False,
    "beforeHp": 540, "afterHp": 539, "nativeTrigger": "Damaged",
    "scope": "same-target observed nonlethal native damage",
}
assert validation["ordinaryLethal"] is False
assert validation["explicitKillFixture"]["action"] == "kill-fixture"
assert validation["explicitKillFixture"]["nativeTrigger"] == "Death"
assert validation["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

binding = validation["binding"]
assert binding["resourcePrefab"] == EXPECTED_RESOURCE
assert binding["rendererPath"] == EXPECTED_RENDERER
assert binding["sourceRendererId"] == EXPECTED_SOURCE_RENDERER
assert binding["mesh"] == "rivenquill.glb" and binding["runtimeMesh"] == "ftkmf_glb_rivenquill.glb"
assert binding["boneSignature"] == EXPECTED_SIGNATURE
assert binding["observedCelRootLocalScale"] == EXPECTED_SCALE
assert binding["material"] == {
    "shader": "Standard", "mainTexture": "ftkmf_rivenquill-palette.png",
    "emissionKeyword": False, "emissionMap": None,
}
assert review["cameraFit"]["accepted"] is True
assert review["materialReview"]["acceptedLimited"] is True

for video in validation["videos"]:
    path = child(OUT, video["path"])
    assert path.is_file() and sha256(path) == video["sha256"]
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == video["frames"]
    assert int(stream["width"]) == video["width"] and int(stream["height"]) == video["height"]

print(json.dumps({
    "status": "PASS",
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "metadataRoundTrips": len(mappings),
    "captureImages": len(capture_images),
    "selected": len(selected),
    "captures": len(captures),
    "videos": len(validation["videos"]),
}, indent=2))
