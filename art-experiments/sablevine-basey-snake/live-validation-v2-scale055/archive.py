#!/usr/bin/env python3
"""Archive Sablevine Serpent V2 scale-0.55 evidence without game payloads."""
from __future__ import annotations

import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "sablevine-basey-snake"
OUT = ASSET / "live-validation-v2-scale055"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
CASE = BASE / "case-bbc04cf0107648aca221feaee611e43d" / "case-result.json"
SETUP = BASE / "case-cf73014301494d2d82433d03576368e0" / "result.json"
REVIEW = ROOT / "scratch" / "sablevine-root-visual-review-v2-scale055.json"
SESSION = "9c79f4374ece4e66b9a6ebbd3d411518"
SESSION_MARKER = BASE / f"new-run-session-{SESSION}.json"
CATALOG_SOURCE = GAME / "model-test-profiles.json"
REGISTRATION_SOURCE = GAME / "model-test-registration.json"
CATALOG_SNAPSHOT = OUT / "inputs" / "model-test-profiles.json.gz"
REGISTRATION_SNAPSHOT = OUT / "inputs" / "model-test-registration.json.gz"
PROFILE = ASSET / "runtime-profile-scale055.json"
REVISION = ASSET / "profile-revision-scale055.json"
PREFLIGHT = ASSET / "route-preflight-scale055.json"
STAGE_RECEIPT = ROOT / "scratch" / "sablevine-basey-snake-scale055-stage" / "receipt.json"
BINARY_RECEIPT = GAME / "deployment-backups" / "sablevine-scale055-runtime-20260912-114609" / "deployment.json"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
EXPECTED_SCALE = [0.550000011920929] * 3
EXPECTED_KEY = "ftkmf_modeltest_sablevine_basey_snake_fit055"
EXPECTED_SIGNATURE = "dc3975c33683e227cca8a762d40e7d65c8af7b4167d2928dd4efc2cd0d421c51"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def rooted(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path), "bytes": path.stat().st_size}


def snapshot_json(source: Path, archive: Path, expected_source_sha256: str | None = None) -> tuple[dict, dict]:
    assert source.is_file() and not source.is_symlink(), source
    raw = source.read_bytes()
    source_sha256 = hashlib.sha256(raw).hexdigest()
    if expected_source_sha256 is not None:
        assert source_sha256 == expected_source_sha256, (source_sha256, expected_source_sha256)
    value = json.loads(raw)
    assert isinstance(value, dict), source
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    return value, {
        "archive": str(archive.relative_to(OUT)),
        "archiveSha256": sha(archive),
        "sourceSha256": source_sha256,
        "bytes": len(raw),
        "encoding": "gzip-lossless",
    }


def load_summarizer():
    module_path = ROOT / "tools" / "ai-model-pipeline" / "summarize_capture.py"
    spec = importlib.util.spec_from_file_location("sablevine_v2_capture_summary", module_path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


assert not (OUT / "validation.json").exists() or __import__("os").environ.get("FTK_ARCHIVE_REBUILD") == "1", (
    "refusing to overwrite a completed archive"
)
OUT.mkdir(parents=True, exist_ok=True)

case = read(CASE)
setup = read(SETUP)
review = read(REVIEW)
manifest = read(ASSET / "manifest.json")
runtime_profile = read(PROFILE)
revision = read(REVISION)
preflight = read(PREFLIGHT)
assert case["session"] == SESSION and case["enemy"] == EXPECTED_KEY
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3
assert case["profileSha256"] == "dc191b0a778cd7d1a07a5641daa4e7cf57d0a6c05c6b361a92a1e2f1fbb2f1af"
assert all(action["actionAccepted"] is True for action in case["actions"])
assert case["finalReady"]["ok"] is True and case["finalReady"]["strictReady"] == {
    "ok": True, "level": 0, "room": 2, "buttonCount": 1,
}
assert review["reviewStatus"] == "reviewed_scoped_technical_motion_camera_fit_accepted_limited_art_pending"
assert review["session"] == SESSION and len(review["frames"]) == 11
assert review["binding"]["nativeChassis"] == "snakeJungleA"
assert review["binding"]["resourcePrefab"] == "enbaseysnake"
assert review["binding"]["rendererPath"] == "enSnake_Basey"
assert review["binding"]["spawnedCelRootLocalScale"] == EXPECTED_SCALE
assert runtime_profile["profiles"] == [
    {
        "key": EXPECTED_KEY,
        "baseEnemy": "snakeJungleA",
        "resourcePrefab": "enbaseysnake",
        "displayName": "Sablevine Serpent",
        "minimumBaseHealth": 64,
        "combatProfile": "372a8c83d1aaaa77eabd843bf619a3972af833c1c84bde20178462d7806cc261",
        "renderers": [{
            "rendererPath": "enSnake_Basey",
            "glbFile": "sablevine.glb",
            "textureFile": "sablevine-palette.png",
            "disableNativeEmission": True,
        }],
        "visualScale": 0.55,
    }
]
assert revision["candidate"]["profileSha256"] == sha(PROFILE)
assert preflight["profileDocument"]["sha256"] == sha(PROFILE)
assert setup["status"] == "binding_metadata_observed" and len(setup["matches"]) == 1
setup_match = setup["matches"][0]
assert setup_match["rendererPath"] == "enSnake_Basey"
assert setup_match["glbFile"] == "sablevine.glb"
assert setup_match["boneSignature"] == EXPECTED_SIGNATURE
assert setup_match["visualScale"]["requestedFactor"] == 0.55
assert setup_match["visualScale"]["nativePrefabRootLocalScale"] == [1.0, 1.0, 1.0]
assert setup_match["visualScale"]["actualSpawnedCelRootLocalScale"] == EXPECTED_SCALE

catalog, catalog_snapshot = snapshot_json(CATALOG_SOURCE, CATALOG_SNAPSHOT, case["profileSha256"])
registration, registration_snapshot = snapshot_json(REGISTRATION_SOURCE, REGISTRATION_SNAPSHOT)
assert [row for row in catalog["profiles"] if row["key"] == EXPECTED_KEY] == runtime_profile["profiles"]
registered = [row for row in registration["registered"] if row.get("key") == EXPECTED_KEY]
assert len(registered) == 1
assert registered[0]["resourcePrefab"] == "enbaseysnake"
assert registered[0]["visualScale"] == EXPECTED_SCALE[0]
assert registered[0]["nativePrefabRootLocalScale"] == [1.0, 1.0, 1.0]

asset_hashes = manifest["originalAssets"]
assert isinstance(asset_hashes, dict) and asset_hashes
for name, digest in asset_hashes.items():
    assert sha(ASSET / name) == digest, name
assert manifest["status"] == "ORIGINAL_ART_LIVE_INTEGRATION_REVIEWED_CAMERA_FIT_ACCEPTED_LIMITED_ART_PENDING"

sources = {
    CASE,
    SETUP,
    REVIEW,
    SESSION_MARKER,
    OUT / "archive.py",
    OUT / "verify.py",
    ASSET / "README.md",
    ASSET / "manifest.json",
    PROFILE,
    REVISION,
    PREFLIGHT,
    ASSET / "build-report.json",
    ASSET / "original-geometry-proof.json",
    ASSET / "build_geometry.py",
    ASSET / "render_studio.py",
    ASSET / "verify_original_geometry.py",
    ASSET / "sablevine.source.json",
    ASSET / "sablevine.pieces.json",
    ASSET / "sablevine.glb",
    ASSET / "sablevine-palette.png",
    ASSET / "sablevine-hero.png",
    ASSET / "sablevine-side.png",
    STAGE_RECEIPT,
    BINARY_RECEIPT,
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "Plugin.cs",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "run_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "record_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "runtime-test" / "exercise_case.py",
    ROOT / "tools" / "ai-model-pipeline" / "summarize_capture.py",
    ROOT / "tools" / "ai-model-pipeline" / "deploy_isolated_test_binaries.py",
}

captures: list[dict] = []
capture_images: set[Path] = set()
summarizer = load_summarizer()
for action in case["actions"]:
    source_action = action["action"]
    capture_path = rooted(action["capture"]["rawCapture"]["path"])
    action_journal = rooted(action["journal"]["path"])
    action_result = rooted(action["rawResult"]["path"])
    assert capture_path.is_file() and action_journal.is_file() and action_result.is_file()
    raw = read(capture_path)
    frames = raw.get("frames")
    assert raw["ok"] is True and isinstance(frames, list) and len(frames) == 120
    assert action["capture"]["boundary"]["completeCapture"] is True
    assert all(frame["celRelativeRendererPath"] == "enSnake_Basey" for frame in frames)
    assert all(frame["mesh"] == "ftkmf_glb_sablevine.glb" for frame in frames)
    assert all(frame["boneSignature"] == EXPECTED_SIGNATURE for frame in frames)
    assert all(frame["celRootLocalScale"] == EXPECTED_SCALE for frame in frames)
    assert all(frame["isVisible"] is True and frame["active"] is True for frame in frames)
    sources.update({capture_path, action_journal, action_result})
    image_directory = capture_path.with_suffix("")
    images = sorted(image_directory.glob("*.png"))
    assert len(images) == 120
    sources.update(images)
    capture_images.update(images)
    summary = summarizer.summarize(capture_path)
    summary_path = OUT / "capture-summaries" / f"{source_action}.json"
    write(summary_path, summary)
    captures.append({
        "label": {
            "pass": "idle",
            "attack": "nonlethal-hit",
            "kill-fixture": "kill-fixture",
        }[source_action],
        "action": source_action,
        "complete": True,
        "termination": None,
        "rawCapture": pin(capture_path),
        "summary": pin(summary_path),
        "frameCount": summary["frameCount"],
        "pausedFrameCount": summary["pausedFrameCount"],
        "actualGameSeconds": summary["actualGameSeconds"],
        "meshIdentityStable": summary["meshIdentityStable"],
        "meshSignatures": summary["meshSignatures"],
        "states": summary["states"],
        "ragdollEvidence": summary["ragdollEvidence"],
        "celRootLocalScale": EXPECTED_SCALE,
        "causalMotion": action["motionEvidence"],
        "captureScope": {
            "pass": "Idle observation and exact native enemy Attack trigger/capture.",
            "attack": "Ordinary same-target nonlethal player hit and exact Damaged trigger/capture.",
            "kill-fixture": "Explicit KillSingle fixture and exact native Death trigger/capture; not ordinary lethal-damage evidence.",
        }[source_action],
    })

for field in ("journal", "selectedFrameSheet"):
    entry = case.get(field)
    if isinstance(entry, dict) and isinstance(entry.get("path"), str):
        sources.add(rooted(entry["path"]))

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN, source

source_image_pins: dict[str, str] = {}
capture_image_pins: dict[str, str] = {}
lossless_mappings: list[dict] = []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    if source.suffix.lower() == ".png":
        digest = hashlib.sha256(raw).hexdigest()
        source_image_pins[str(relative)] = digest
        if source in capture_images:
            capture_image_pins[str(relative)] = digest
        continue
    destination = OUT / "metadata" / (str(relative) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    lossless_mappings.append({
        "source": str(relative),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })

write(OUT / "source-image-pins.json", source_image_pins)
write(OUT / "capture-image-pins.json", capture_image_pins)
write(OUT / "asset-pins.json", asset_hashes)

selected: list[dict] = []
for frame in review["frames"]:
    source = rooted(frame["path"])
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({
        "action": frame["action"],
        "sourceAction": frame["sourceAction"],
        "captureId": frame["captureId"],
        "index": frame["index"],
        "clip": frame["clip"],
        "source": frame["path"],
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": frame["observation"],
    })

videos: list[dict] = []
for capture in captures:
    raw_capture = rooted(capture["rawCapture"]["path"])
    source_action = capture["action"]
    video = OUT / f"{source_action}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(raw_capture.with_suffix("") / "%04d.png"), "-frames:v", str(capture["frameCount"]),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == capture["frameCount"]
    videos.append({
        "action": source_action,
        "path": video.name,
        "sha256": sha(video),
        "frames": capture["frameCount"],
        "width": int(stream["width"]),
        "height": int(stream["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing.",
    })

validation = {
    "status": "reviewed_resource_enbaseysnake_binding_motion_gameplay_observed_camera_fit_accepted_limited_art_pending",
    "revision": "V2-scale055",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": "Sablevine Serpent",
    "nativeChassis": "snakeJungleA",
    "baseEnemy": "snakeJungleA",
    "resourcePrefab": "enbaseysnake",
    "rendererPaths": ["enSnake_Basey"],
    "profile": runtime_profile["profiles"][0],
    "profileSha256": case["profileSha256"],
    "catalogSha256": case["profileSha256"],
    "catalogSnapshot": catalog_snapshot,
    "registrationSnapshot": registration_snapshot,
    "assetHashes": asset_hashes,
    "binding": review["binding"],
    "renderers": [review["binding"]],
    "scaleContract": {
        "requestedFactor": 0.55,
        "nativePrefabRootLocalScale": [1.0, 1.0, 1.0],
        "expectedSpawnedCelRootLocalScale": [0.55, 0.55, 0.55],
        "observedSpawnedCelRootLocalScale": EXPECTED_SCALE,
        "tolerance": 0.0001,
        "scope": "Registered public profile, binding probe, and every frame of all three complete captures.",
    },
    "captures": captures,
    "ordinaryHit": review["ordinaryHit"],
    "ordinaryLethal": False,
    "explicitKillFixture": review["explicitKillFixture"],
    "finalReady": review["finalReady"],
    "visualReview": review["reviewStatus"],
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
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "setupBinding": pin(SETUP),
    "caseResult": pin(CASE),
    "runtimeProfile": pin(PROFILE),
    "profileRevision": pin(REVISION),
    "routePreflight": pin(PREFLIGHT),
    "stageReceipt": pin(STAGE_RECEIPT),
    "binaryDeploymentReceipt": pin(BINARY_RECEIPT),
    "v1Reference": {
        "validation": "art-experiments/sablevine-basey-snake/live-validation-v1/validation.json",
        "validationSha256": "4ee012c8047b1bb54e790a6f64f18c269e564d2d26322ecfb027f63a8c90cee8",
        "status": "reviewed_resource_enbaseysnake_binding_motion_gameplay_observed_camera_fit_rejected",
        "relationship": "Same original asset bytes at the rejected V1 profile scale; V2 preserves V1 and proves the fresh 0.55 profile correction separately.",
    },
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    "# Sablevine Serpent live validation V2: scale 0.55\n\n"
    "This immutable archive records a fresh resource-prefab `enbaseysnake` / `snakeJungleA` / `enSnake_Basey` trial in session `9c79f4374ece4e66b9a6ebbd3d411518`. V2 reuses the exact original Sablevine GLB and palette from V1; its only profile revision is public `visualScale: 0.55` under a fresh key.\n\n"
    "The registration snapshot records a native prefab root scale of `[1, 1, 1]` and the public factor. Binding metadata and every one of the 360 captured renderer frames record the spawned CEL root at `[0.550000011920929, 0.550000011920929, 0.550000011920929]`. That is measured live scale evidence, not a fit inference from profile arithmetic.\n\n"
    "Three complete 120-frame native captures preserve the exact 44-bone Sablevine mesh through `Snake_Idle`, native `Attack` / `Snake_BiteAttack`, an ordinary same-target no-focus hit with `Damaged` / `Snake_HitSmall` (HP 58 to 45), and an explicit `KillSingle` fixture with `Death` / `Snake_DeathBig`. The guarded sequence reaches strict Ready at level 0, room 2. Root-reviewed idle and attack frames show the complete practical serpent silhouette inside normal combat framing; selected hit and death frames retain its coherent bound form.\n\n"
    "This accepts a usable limited-art combat-camera result for this exact source pair. It does not accept portraits, culling, collision, long-session resource lifetime, every native ability variant, ordinary lethal damage, ragdoll behavior, or final art direction. V1 remains a separate immutable camera-fit rejection.\n\n"
    "`archive.py` is offline-only and refuses to overwrite a completed archive. It pins all 360 source capture PNGs, preserves selected originals and lossless metadata, then derives one 120-frame presentation video per action. Native game payloads and DLLs are excluded. Run `python3 verify.py` to independently recheck snapshots, hashes, exact scale, selected frames, capture summaries, causal motion, and video frame counts.\n"
)

print(json.dumps({
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "validationSha256": sha(OUT / "validation.json"),
    "selected": len(selected),
    "captureImages": len(capture_image_pins),
    "sourceImages": len(source_image_pins),
    "videos": len(videos),
    "losslessMappings": len(lossless_mappings),
}, indent=2))
