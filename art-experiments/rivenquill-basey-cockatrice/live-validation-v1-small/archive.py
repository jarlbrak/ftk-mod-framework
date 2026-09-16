#!/usr/bin/env python3
"""Create the immutable Rivenquill Cockatrice small-route evidence archive.

This script deliberately preserves test metadata and pins every source PNG
without copying a game binary, resource archive, or application payload.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "rivenquill-basey-cockatrice"
OUT = ASSET / "live-validation-v1-small"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
CASE = BASE / "case-2a8169e4710e463495e729dfb73633c7" / "case-result.json"
SETUP = BASE / "case-fc01bddf67e047a9aae16b4f765ce5d7" / "result.json"
SESSION = "4299e8d05d904dbea0eb5371ef50b16c"
SESSION_MARKER = BASE / f"new-run-session-{SESSION}.json"
CATALOG_SOURCE = GAME / "model-test-profiles.json"
REGISTRATION_SOURCE = GAME / "model-test-registration.json"
PROFILE = ASSET / "runtime-profile.json"
PREFLIGHT = ASSET / "route-preflight-health64.json"
STAGE_RECEIPT = ROOT / "scratch" / "rivenquill-basey-cockatrice-v1-stage" / "receipt.json"

EXPECTED_KEY = "ftkmf_modeltest_rivenquill_basey_cockatrice_small"
EXPECTED_NATIVE = "cockatriceC"
EXPECTED_RESOURCE = "enbaseycockatricesmall"
EXPECTED_RENDERER = "enBaseyCockatrice"
EXPECTED_SOURCE_RENDERER = 121694
EXPECTED_SIGNATURE = "c591e94b65dd3510b8677818f6c7cdc307a1339e20b36fa7ceb911d0cfbaddef"
EXPECTED_SCALE = [0.3499999940395355] * 3
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}

# These are original source PNGs reviewed in the live capture, not screenshots
# re-rendered by this archiver.  Each selection has a stable source hash below.
REVIEW_SPECS = (
    ("idle", "pass", 0, "Normal combat-camera idle: complete cobalt, jade, and copper silhouette is readable."),
    ("idle", "pass", 12, "Idle continuation: wings, legs, head, and plume tail remain attached and readable."),
    ("native-attack", "pass", 36, "Native enemy attack traversal: fast motion has normal capture blur, with no detached mesh pieces."),
    ("native-attack", "pass", 42, "Native enemy attack trajectory: articulated body remains coherent through the lunge."),
    ("native-attack", "pass", 48, "Attack recovery: visible body returns as one coherent bound model."),
    ("ordinary-hit", "attack", 0, "Pre-hit ordinary combat frame with the exact bound runtime model visible."),
    ("ordinary-hit", "attack", 13, "Observed same-target Damaged response after the ordinary no-focus player hit."),
    ("ordinary-hit", "attack", 23, "Damaged trigger sample: hit effect overlays the model, while the body remains coherent."),
    ("ordinary-hit", "attack", 30, "Post-hit continuation: bound form remains visually coherent after HP loss."),
    ("fixture-death", "kill-fixture", 0, "Pre-fixture-death combat frame with the exact bound runtime model visible."),
    ("fixture-death", "kill-fixture", 16, "Fixture-death onset with the full bound form still visible."),
    ("fixture-death", "kill-fixture", 23, "Native Death trigger sample from the explicit KillSingle fixture."),
    ("fixture-death", "kill-fixture", 30, "Collapse progression: legs, torso, wings, and tail remain joined."),
    ("fixture-death", "kill-fixture", 40, "Defeated body remains coherent during the native death sequence."),
    ("fixture-death", "kill-fixture", 60, "Late death sequence: collapsed model remains visually coherent."),
    ("fixture-death", "kill-fixture", 90, "Late death capture: coherent collapsed form remains in the combat arena."),
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_object(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    assert path.is_relative_to(ROOT), path
    return str(path.relative_to(ROOT))


def rooted(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {"path": relative(path), "sha256": sha256(path), "bytes": path.stat().st_size}


def snapshot_json(source: Path, archive: Path, expected_sha256: str | None = None) -> tuple[dict, dict]:
    assert source.is_file() and not source.is_symlink(), source
    raw = source.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if expected_sha256 is not None:
        assert digest == expected_sha256, (digest, expected_sha256)
    value = json.loads(raw)
    assert isinstance(value, dict), source
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    return value, {
        "archive": str(archive.relative_to(OUT)),
        "archiveSha256": sha256(archive),
        "sourceSha256": digest,
        "bytes": len(raw),
        "encoding": "gzip-lossless",
    }


def profile_for(document: dict, key: str) -> dict:
    profiles = document.get("profiles")
    assert isinstance(profiles, list), document
    matches = [item for item in profiles if isinstance(item, dict) and item.get("key") == key]
    assert len(matches) == 1, matches
    return matches[0]


def json_metadata(source: Path, mappings: list[dict]) -> None:
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN_SUFFIXES, source
    raw = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    mappings.append({
        "source": relative(source),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha256(destination),
        "encoding": "gzip-lossless",
    })


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", (
    "refusing to overwrite a completed archive"
)
OUT.mkdir(parents=True, exist_ok=True)

case = read_object(CASE)
setup = read_object(SETUP)
manifest = read_object(ASSET / "manifest.json")
profile_document = read_object(PROFILE)
preflight = read_object(PREFLIGHT)
stage_receipt = read_object(STAGE_RECEIPT)

assert case["session"] == SESSION and case["enemy"] == EXPECTED_KEY
assert case["status"] == "needs_visual_review"
assert case.get("error") is None
assert case["finalReady"]["ok"] is True
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert case["profileSha256"] == sha256(CATALOG_SOURCE)
assert stage_receipt["profileDocument"]["sha256"] == sha256(PROFILE)
assert stage_receipt["sourceCatalog"]["sha256"] == case["profileSha256"]
assert preflight["profileDocument"]["sha256"] == sha256(PROFILE)

profile = profile_for(profile_document, EXPECTED_KEY)
assert profile == {
    "key": EXPECTED_KEY,
    "baseEnemy": EXPECTED_NATIVE,
    "resourcePrefab": EXPECTED_RESOURCE,
    "displayName": "Rivenquill Hatchling",
    "minimumBaseHealth": 64,
    "combatProfile": "adc47b1916710812cc9e5415964285ad28314575ba83832db5a446c2d30dee23",
    "renderers": [{
        "rendererPath": EXPECTED_RENDERER,
        "glbFile": "rivenquill.glb",
        "textureFile": "rivenquill-palette.png",
        "disableNativeEmission": True,
    }],
}
assert setup["status"] == "binding_metadata_observed" and setup["session"] == SESSION
assert len(setup["matches"]) == 1
setup_match = setup["matches"][0]
assert setup_match["rendererPath"] == EXPECTED_RENDERER
assert setup_match["glbFile"] == "rivenquill.glb"
assert setup_match["boneSignature"] == EXPECTED_SIGNATURE

catalog, catalog_snapshot = snapshot_json(CATALOG_SOURCE, OUT / "inputs" / "model-test-profiles.json.gz", case["profileSha256"])
registration, registration_snapshot = snapshot_json(REGISTRATION_SOURCE, OUT / "inputs" / "model-test-registration.json.gz")
assert profile_for(catalog, EXPECTED_KEY) == profile
registered = [row for row in registration.get("registered", []) if isinstance(row, dict) and row.get("key") == EXPECTED_KEY]
assert len(registered) == 1
assert registered[0]["baseEnemy"] == EXPECTED_NATIVE
assert registered[0]["resourcePrefab"] == EXPECTED_RESOURCE
assert registered[0]["actualBaseHealth"] == 80

initial = case["initialRenderer"]
assert initial["ownerInstanceId"] == 369188
assert initial["celRelativeRendererPath"] == EXPECTED_RENDERER
assert initial["rendererKind"] == "SkinnedMeshRenderer"
assert initial["mesh"] == "ftkmf_glb_rivenquill.glb"
assert initial["boneSignature"] == EXPECTED_SIGNATURE
assert initial["celRootLocalScale"] == EXPECTED_SCALE
assert initial["isVisible"] is True and initial["active"] is True
assert len(initial["materials"]) == 1
material = initial["materials"][0]
assert material["shader"] == "Standard"
assert material["emissionKeyword"] is False
assert material["_MainTex"]["name"] == "ftkmf_rivenquill-palette.png"

actions = {item["action"]: item for item in case["actions"]}
assert set(actions) == {"pass", "attack", "kill-fixture"}
assert all(item["actionAccepted"] is True for item in actions.values())
action_specs = (
    ("pass", "idle-and-native-attack", "Idle observation and an exact native enemy attack trigger/capture."),
    ("attack", "ordinary-attack-and-nonlethal-hit", "Ordinary same-target, no-focus player attack; measured nonlethal HP loss and exact Damaged trigger/capture."),
    ("kill-fixture", "explicit-fixture-death", "Explicit KillSingle fixture and exact native Death trigger/capture; this is not ordinary lethal-damage proof."),
)

captures: list[dict] = []
capture_sources: dict[str, Path] = {}
capture_images: set[Path] = set()
metadata_sources: set[Path] = {
    CASE, SETUP, SESSION_MARKER,
    ASSET / "README.md", ASSET / "manifest.json", PROFILE, PREFLIGHT,
    ASSET / "route-preflight.json", ASSET / "build-report.json", ASSET / "original-geometry-proof.json",
    ASSET / "build_geometry.py", ASSET / "render_studio.py", ASSET / "verify_original_geometry.py",
    ASSET / "rivenquill.glb", ASSET / "rivenquill.source.json", ASSET / "rivenquill.pieces.json",
    STAGE_RECEIPT,
    OUT / "archive.py", OUT / "verify.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "Plugin.cs",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "run_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "record_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "exercise_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "motion_evidence.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "README.md",
}

for action_name, label, scope in action_specs:
    action = actions[action_name]
    raw_path = rooted(action["capture"]["rawCapture"]["path"])
    result_path = rooted(action["rawResult"]["path"])
    journal_path = rooted(action["journal"]["path"])
    assert raw_path.is_file() and result_path.is_file() and journal_path.is_file()
    raw = read_object(raw_path)
    frames = raw.get("frames")
    assert isinstance(frames, list) and frames
    boundary = action["capture"]["boundary"]
    images = sorted(raw_path.with_suffix("").glob("*.png"))
    assert len(images) == len(frames), (action_name, len(images), len(frames))
    assert [image.name for image in images] == [f"{index:04d}.png" for index in range(len(images))]
    assert all(frame["celRelativeRendererPath"] == EXPECTED_RENDERER for frame in frames)
    assert all(frame["mesh"] == "ftkmf_glb_rivenquill.glb" for frame in frames)
    assert all(frame["boneSignature"] == EXPECTED_SIGNATURE for frame in frames)
    assert all(frame["celRootLocalScale"] == EXPECTED_SCALE for frame in frames)
    assert raw["ok"] is True and boundary["completeCapture"] is True
    assert len(frames) == 120
    assert all(frame["isVisible"] is True and frame["active"] is True for frame in frames)
    capture_sources[action_name] = raw_path
    metadata_sources.update({raw_path, result_path, journal_path})
    capture_images.update(images)
    captures.append({
        "label": label,
        "action": action_name,
        "complete": boundary["completeCapture"],
        "termination": boundary["termination"],
        "rawCapture": pin(raw_path),
        "rawCaptureReportedOk": raw["ok"],
        "rawCaptureError": raw["error"],
        "captureBoundary": boundary,
        "frameCount": len(frames),
        "width": raw["width"],
        "height": raw["height"],
        "timingMode": raw["timingMode"],
        "celRootLocalScale": EXPECTED_SCALE,
        "causalMotion": action["motionEvidence"],
        "captureScope": scope,
    })

pass_evidence = actions["pass"]["motionEvidence"]
hit_evidence = actions["attack"]["motionEvidence"]
death_evidence = actions["kill-fixture"]["motionEvidence"]
assert pass_evidence["attack"]["nativeTrigger"]["trigger"] in {"Attack", "AttackProf", "AttackProf1"}
assert hit_evidence["hit"]["nativeTrigger"]["trigger"] == "Damaged"
assert hit_evidence["hit"]["nativeAction"]["primary"]["attackResponse"] == "Damaged"
assert death_evidence["death"]["nativeTrigger"]["trigger"] == "Death"
assert death_evidence["death"]["nativeAction"]["primary"]["attackResponse"] == "Death"
retry = case["attackRetrySummary"]
assert retry == [{
    "attempt": 1,
    "status": "nonlethal_hp_loss",
    "beforeHp": 72,
    "afterHp": 62,
    "boundary": "Observed same-target HP only; no inferred block, dodge, hit animation, or damage source.",
}]

for source in (ASSET / "rivenquill-palette.png", ASSET / "rivenquill-hero.png", ASSET / "rivenquill-side.png"):
    assert source.is_file()
    metadata_sources.add(source)

asset_hashes = manifest["originalAssets"]
assert isinstance(asset_hashes, dict) and asset_hashes
for name, digest in asset_hashes.items():
    assert sha256(ASSET / name) == digest, name
assert case["assetHashes"] == {
    "rivenquill-palette.png": asset_hashes["rivenquill-palette.png"],
    "rivenquill.glb": asset_hashes["rivenquill.glb"],
}

source_image_pins: dict[str, str] = {}
capture_image_pins: dict[str, str] = {}
lossless_mappings: list[dict] = []
for source in sorted(metadata_sources):
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN_SUFFIXES, source
    if source.suffix.lower() == ".png":
        source_image_pins[relative(source)] = sha256(source)
        continue
    json_metadata(source, lossless_mappings)
for source in sorted(capture_images):
    digest = sha256(source)
    source_image_pins[relative(source)] = digest
    capture_image_pins[relative(source)] = digest

write_json(OUT / "asset-pins.json", asset_hashes)
write_json(OUT / "source-image-pins.json", source_image_pins)
write_json(OUT / "capture-image-pins.json", capture_image_pins)

review_frames: list[dict] = []
selected: list[dict] = []
for category, action_name, index, observation in REVIEW_SPECS:
    source = capture_sources[action_name].with_suffix("") / f"{index:04d}.png"
    assert source in capture_images and source.is_file(), source
    digest = sha256(source)
    row = {
        "category": category,
        "action": action_name,
        "captureId": capture_sources[action_name].stem,
        "index": index,
        "path": relative(source),
        "sha256": digest,
        "observation": observation,
    }
    review_frames.append(row)
    destination = OUT / "selected" / f"{action_name}-{index:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha256(destination) == digest
    selected.append({**row, "archive": str(destination.relative_to(OUT))})

binding = {
    "nativeChassis": EXPECTED_NATIVE,
    "resourcePrefab": EXPECTED_RESOURCE,
    "rendererPath": EXPECTED_RENDERER,
    "sourceRendererId": EXPECTED_SOURCE_RENDERER,
    "runtimeOwnerInstanceId": initial["ownerInstanceId"],
    "runtimeRendererInstanceId": initial["instanceId"],
    "runtimeCelRootName": initial["celRootName"],
    "rendererKind": initial["rendererKind"],
    "mesh": "rivenquill.glb",
    "runtimeMesh": initial["mesh"],
    "boneSignature": EXPECTED_SIGNATURE,
    "observedCelRootLocalScale": EXPECTED_SCALE,
    "catalogSha256": case["profileSha256"],
    "material": {
        "shader": material["shader"],
        "mainTexture": material["_MainTex"]["name"],
        "emissionKeyword": material["emissionKeyword"],
        "emissionMap": material["_EmissionMap"],
    },
}
review = {
    "schema": "ftkmf.root-visual-review.v1",
    "reviewStatus": "reviewed_resource_enbaseycockatricesmall_binding_motion_gameplay_observed_camera_fit_accepted_limited_art_pending",
    "session": SESSION,
    "scope": "One fresh isolated cockatriceC / enbaseycockatricesmall / enBaseyCockatrice source pair only.",
    "binding": binding,
    "frames": review_frames,
    "cameraFit": {
        "accepted": True,
        "scope": "Selected original 1280x832 normal-combat capture frames across idle, native attack, ordinary hit, and fixture death.",
        "observation": "The complete bird-dragon silhouette remains readable in the normal combat camera without detached geometry or catastrophic stretching in the reviewed frames.",
    },
    "materialReview": {
        "acceptedLimited": True,
        "scope": "Selected normal-combat capture frames and runtime material metadata only.",
        "observation": "The reviewed cobalt, jade, and copper palette is readable; runtime metadata records Standard shader, the authored palette as _MainTex, and disabled native emission.",
    },
    "ordinaryHit": {
        "action": "attack",
        "cheat": "None",
        "focus": False,
        "beforeHp": 72,
        "afterHp": 62,
        "nativeTrigger": "Damaged",
        "scope": "same-target observed nonlethal native damage",
    },
    "ordinaryLethal": False,
    "explicitKillFixture": {
        "action": "kill-fixture",
        "accepted": True,
        "nativeTrigger": "Death",
        "captureTermination": "complete_120_frame_capture",
        "scope": "Explicit KillSingle fixture on the native damage path; not ordinary lethal-damage proof.",
    },
    "finalReady": case["finalReady"],
    "limits": [
        "Selected original frames were reviewed; this is not exhaustive review of every captured frame or ability variant.",
        "The fixture-death capture is complete at 120 frames; it is still an explicit fixture rather than ordinary lethal-damage proof.",
        "This evidence does not establish portraits, culling, collision, long-session resource lifetime, every ability variant, normal lethal damage, ragdoll behavior, or final art direction.",
        "This small source pair does not transfer to bossCockatrice / enbaseycockatriceboss or any direct Cockatrice renderer row.",
    ],
}
write_json(OUT / "visual-review.json", review)

videos: list[dict] = []
for capture in captures:
    action_name = capture["action"]
    source_directory = capture_sources[action_name].with_suffix("")
    video = OUT / f"{action_name}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(source_directory / "%04d.png"), "-frames:v", str(capture["frameCount"]),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == capture["frameCount"]
    videos.append({
        "action": action_name,
        "path": video.name,
        "sha256": sha256(video),
        "frames": capture["frameCount"],
        "width": int(stream["width"]),
        "height": int(stream["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative; raw capture metadata is the timing evidence.",
    })

validation = {
    "status": review["reviewStatus"],
    "revision": "V1-small",
    "session": SESSION,
    "sourceKind": "resource_prefab_override",
    "enemy": EXPECTED_KEY,
    "displayName": "Rivenquill Hatchling",
    "nativeChassis": EXPECTED_NATIVE,
    "nativeEnemy": EXPECTED_NATIVE,
    "baseEnemy": EXPECTED_NATIVE,
    "resourcePrefab": EXPECTED_RESOURCE,
    "rendererPaths": [EXPECTED_RENDERER],
    "sourceRendererIds": [EXPECTED_SOURCE_RENDERER],
    "profile": profile,
    "profileDocumentSha256": sha256(PROFILE),
    "catalogSha256": case["profileSha256"],
    "catalogSnapshot": catalog_snapshot,
    "registrationSnapshot": registration_snapshot,
    "assetHashes": asset_hashes,
    "binding": binding,
    "renderers": [binding],
    "captures": captures,
    "ordinaryHit": review["ordinaryHit"],
    "ordinaryLethal": review["ordinaryLethal"],
    "explicitKillFixture": review["explicitKillFixture"],
    "finalReady": review["finalReady"],
    "visualReview": review["reviewStatus"],
    "rootVisualReview": "visual-review.json",
    "cameraFit": review["cameraFit"],
    "materialReview": review["materialReview"],
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(source_image_pins),
    "captureImageCount": len(capture_image_pins),
    "sourceImagePins": "source-image-pins.json",
    "captureImagePins": "capture-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": lossless_mappings,
    "setupBinding": pin(SETUP),
    "caseResult": pin(CASE),
    "runtimeProfile": pin(PROFILE),
    "routePreflight": pin(PREFLIGHT),
    "stageReceipt": pin(STAGE_RECEIPT),
    "limits": review["limits"],
}
write_json(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    "# Rivenquill Cockatrice small-route live validation\n\n"
    "This immutable archive records a fresh isolated `cockatriceC` / `enbaseycockatricesmall` / `enBaseyCockatrice` trial in session `4299e8d05d904dbea0eb5371ef50b16c`. It pins the original Rivenquill GLB and palette, exact 50-bone signature, live owner/renderer binding, and all 360 capture source PNG hashes.\n\n"
    "The small profile bound the authored GLB to the exact resource renderer. Three complete 120-frame captures retain visible idle/native attack, an ordinary no-focus nonlethal player hit (HP 72 to 62 with native `Damaged`), and an explicit `KillSingle` fixture with native `Death`. The guarded sequence then reached strict Ready at level 0, room 2.\n\n"
    "Selected original frames accept a coherent, readable combat-camera model for this exact source pair with limited material review. The scope excludes portraits, culling, collision, long-session resource lifetime, every ability variant, normal lethal damage, ragdoll behavior, and final art direction. It does not validate the independently serialized `bossCockatrice` / `enbaseycockatriceboss` route.\n\n"
    "`archive.py` refuses to overwrite a completed archive, excludes native game payloads, preserves lossless metadata and PNG hash pins, and derives presentation videos. Run `python3 verify.py` to recheck exact identity, source snapshots, every pinned image, selected copies, event evidence, complete capture boundaries, and video frame counts.\n"
)

print(json.dumps({
    "validation": relative(OUT / "validation.json"),
    "validationSha256": sha256(OUT / "validation.json"),
    "selected": len(selected),
    "captureImages": len(capture_image_pins),
    "sourceImages": len(source_image_pins),
    "videos": len(videos),
    "losslessMappings": len(lossless_mappings),
}, indent=2))
