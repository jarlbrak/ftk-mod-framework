#!/usr/bin/env python3
"""Archive the reviewed Lichenfang resource-prefab trial without game payloads."""
from __future__ import annotations

import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "lichenfang-basey-wolf"
OUT = ASSET / "live-validation-v1"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = BASE / "case-49a1a5498c1141cfb762f2c30a6c2237" / "case-result.json"
SETUP = BASE / "case-d7b45c9a680c4d5394acb5230772adab" / "result.json"
REVIEW = ROOT / "scratch" / "lichenfang-root-visual-review-v1.json"
SESSION = "6aedf9b7ab6e4c4292425002505d64e4"
CATALOG_SNAPSHOT = OUT / "inputs" / "model-test-profiles.json.gz"
REGISTRATION_SNAPSHOT = OUT / "inputs" / "model-test-registration.json.gz"
SESSION_MARKER = BASE / f"new-run-session-{SESSION}.json"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def snapshot_json(path: Path, expected_source_sha256: str | None = None) -> tuple[dict, dict]:
    assert path.is_file() and not path.is_symlink(), path
    encoded = path.read_bytes()
    raw = gzip.decompress(encoded)
    source_sha256 = hashlib.sha256(raw).hexdigest()
    if expected_source_sha256 is not None:
        assert source_sha256 == expected_source_sha256, (source_sha256, expected_source_sha256)
    value = json.loads(raw)
    assert isinstance(value, dict), path
    return value, {
        "archive": str(path.relative_to(OUT)),
        "archiveSha256": hashlib.sha256(encoded).hexdigest(),
        "sourceSha256": source_sha256,
        "bytes": len(raw),
        "encoding": "gzip-lossless",
    }


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def rooted(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path), "bytes": path.stat().st_size}


def load_summarizer():
    module_path = ROOT / "tools" / "ai-model-pipeline" / "summarize_capture.py"
    spec = importlib.util.spec_from_file_location("lichenfang_capture_summary", module_path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", (
    "refusing to overwrite a completed archive"
)
OUT.mkdir(parents=True, exist_ok=True)

case = read(CASE)
setup = read(SETUP)
review = read(REVIEW)
manifest = read(ASSET / "manifest.json")
runtime_profile = read(ASSET / "runtime-profile.json")
catalog, catalog_snapshot = snapshot_json(CATALOG_SNAPSHOT, case["profileSha256"])
registration, registration_snapshot = snapshot_json(REGISTRATION_SNAPSHOT)

assert case["session"] == SESSION
assert case["enemy"] == "ftkmf_modeltest_lichenfang_basey_wolf"
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3
assert all(action["actionAccepted"] is True for action in case["actions"])
assert case["finalReady"]["ok"] is True and case["finalReady"]["strictReady"]["ok"] is True
assert review["session"] == SESSION and len(review["frames"]) == 11
assert review["binding"]["nativeChassis"] == "wolfA"
assert review["binding"]["resourcePrefab"] == "enbaseywolf"
assert review["binding"]["rendererPath"] == "Wolfie"
assert runtime_profile["profiles"][0]["baseEnemy"] == "wolfA"
assert runtime_profile["profiles"][0]["resourcePrefab"] == "enbaseywolf"
assert setup["status"] == "binding_metadata_observed"
assert len(setup["matches"]) == 1
assert setup["matches"][0]["rendererPath"] == "Wolfie"
assert setup["matches"][0]["glbFile"] == "lichenfang.glb"
assert [profile for profile in catalog["profiles"] if profile["key"] == case["enemy"]] == runtime_profile["profiles"]
assert isinstance(registration, dict)

actions = {action["action"]: action for action in case["actions"]}
assert list(actions) == ["pass", "attack", "kill-fixture"]
assert actions["pass"]["motionEvidence"]["attack"]["nativeTrigger"]["trigger"] == "AttackProf"
assert actions["attack"]["motionEvidence"]["hit"]["nativeTrigger"]["trigger"] == "Damaged"
assert actions["attack"]["motionEvidence"]["hit"]["nativeAction"]["primary"]["newHealth"] == 48
assert actions["attack"]["motionEvidence"]["hit"]["nativeAction"]["postTargetHealth"] == 58
assert actions["kill-fixture"]["motionEvidence"]["death"]["nativeTrigger"]["trigger"] == "Death"

asset_hashes = manifest["originalAssets"]
assert isinstance(asset_hashes, dict) and asset_hashes
for name, digest in asset_hashes.items():
    assert sha(ASSET / name) == digest, name

sources = {
    CASE,
    SETUP,
    REVIEW,
    SESSION_MARKER,
    ASSET / "README.md",
    ASSET / "manifest.json",
    ASSET / "runtime-profile.json",
    ASSET / "build-report.json",
    ASSET / "original-geometry-proof.json",
    ASSET / "route-preflight-health64.json",
    ASSET / "build_geometry.py",
    ASSET / "render_studio.py",
    ASSET / "verify_original_geometry.py",
    ASSET / "lichenfang.source.json",
    ASSET / "lichenfang.pieces.json",
    ASSET / "lichenfang.glb",
    ASSET / "lichenfang-palette.png",
    ASSET / "lichenfang-hero.png",
    ASSET / "lichenfang-side.png",
}

captures: list[dict] = []
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
    sources.update({capture_path, action_journal, action_result})
    image_directory = capture_path.with_suffix("")
    images = sorted(image_directory.glob("*.png"))
    assert len(images) == 120
    sources.update(images)
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
        "causalMotion": action["motionEvidence"],
        "captureScope": {
            "pass": "Idle observation and exact native enemy AttackProf trigger/capture.",
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
lossless_mappings: list[dict] = []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    if source.suffix.lower() == ".png":
        source_image_pins[str(relative)] = hashlib.sha256(raw).hexdigest()
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
    "status": "reviewed_resource_enbaseywolf_binding_appearance_idle_attack_hit_death_and_gameplay_observed",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": "Lichenfang Prowler",
    "nativeChassis": "wolfA",
    "baseEnemy": "wolfA",
    "resourcePrefab": "enbaseywolf",
    "rendererPaths": ["Wolfie"],
    "profile": runtime_profile["profiles"][0],
    "profileSha256": case["profileSha256"],
    "catalogSha256": case["profileSha256"],
    "catalogSnapshot": catalog_snapshot,
    "registrationSnapshot": registration_snapshot,
    "assetHashes": asset_hashes,
    "binding": review["binding"],
    "renderers": [review["binding"]],
    "captures": captures,
    "ordinaryHit": review["ordinaryHit"],
    "ordinaryLethal": False,
    "explicitKillFixture": review["explicitKillFixture"],
    "finalReady": review["finalReady"],
    "visualReview": review["reviewStatus"],
    "materialReview": review["materialReview"],
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(source_image_pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": lossless_mappings,
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "setupBinding": pin(SETUP),
    "caseResult": pin(CASE),
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    """# Lichenfang Prowler live validation V1

This archive records the exact resource-prefab enbaseywolf / wolfA / Wolfie
route in fresh session 6aedf9b7ab6e4c4292425002505d64e4. Runtime inventory
bound the authored lichenfang.glb to one owner with the expected 33-bone
signature.

Three reviewed, complete 120-frame native captures retain that mesh identity.
They record cidle_wolf, native AttackProf and attackProf_wolf, an ordinary
same-target player hit with Damaged and damaged_wolf, then a separate explicit
KillSingle fixture with Death and deathHeavy_wolf. The ordinary no-focus hit
lowers the target from HP 58 to 48. The fixture then drives native death and
the guarded sequence reaches strict Ready.

The root-reviewed frames show a coherent lichen-armored wolf in idle, attack,
damaged, and animator-driven death poses. Native effects and the foreground
hero partially obscure close detail. The fixture death is not ordinary
lethal-damage evidence, and the no-rigidbody record is not ragdoll evidence.
Culling, portraits, long-session resource lifetime, other native ability
variants, and final art-direction approval remain separate gates.

archive.py is offline-only and refuses to overwrite a completed archive. It
pins all source PNGs, copies reviewed originals, preserves metadata losslessly,
and derives one 120-frame presentation video per action. Native payloads and
DLLs are excluded. inputs/ preserves the exact catalog and registration
snapshots used by the live trial, so later isolated-catalog revisions cannot
change the recorded source configuration. Run python3 verify.py to independently
recheck the archive hashes, compressed metadata, selected frames, captured
summaries, causal motion evidence, and video frame counts.
"""
)

print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "validationSha256": sha(OUT / "validation.json"),
    "selected": len(selected),
    "sourceImages": len(source_image_pins),
    "videos": len(videos),
    "losslessMappings": len(lossless_mappings),
}, indent=2))
