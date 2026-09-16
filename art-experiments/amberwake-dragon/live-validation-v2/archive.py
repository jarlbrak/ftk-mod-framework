#!/usr/bin/env python3
"""Archive the fresh focused Amberwake Dragon trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "amberwake-dragon"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "8714c0e28f3a422a9f827c6914a503d8"
CASE = BASE / "case-7802338c69374ef791af98c37a3773be" / "case-result.json"
JOURNAL = CASE.with_name("journal.jsonl")
STAGE_SESSION = BASE / f"new-run-session-{SESSION}.json"
REVIEW = ROOT / "scratch" / "amberwake-root-visual-review-v2.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
V1 = ASSET / "live-validation-v1.json"
BATCH = ROOT / "scratch" / "coverage-batch-20260911-145425.json"
RUNNER = ROOT / "scratch" / "run-coverage-batch.py"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path):
    return str(path.resolve().relative_to(ROOT))


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)

case = read(CASE)
review = read(REVIEW)
stage_session = read(STAGE_SESSION)
manifest = read(MANIFEST)
assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_amberwake"
assert case["rendererPath"] == "enDragon" and case["focusedAttack"] is True
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3
assert review["session"] == SESSION and review["nativeChassis"] == "dragonFrost"
assert review["rendererPath"] == "enDragon" and review["ownerInstanceId"] == 369188
assert len(review["frames"]) == 9
assert stage_session["session"] == SESSION
assert sha(PROFILE) == case["profileSha256"]
assert case["finalReady"]["strictReady"]["ok"] is True
assert case["finalReady"]["strictReady"]["level"] == 0
assert case["finalReady"]["strictReady"]["room"] == 2

# The frozen authoring package is verified but is not needlessly copied a second time.
for name, digest in manifest["files"].items():
    assert sha(ASSET / name) == digest, name
for name, digest in case["assetHashes"].items():
    assert sha(ASSET / name) == digest, name

actions = {action["action"]: action for action in case["actions"]}
assert set(actions) == {"pass", "attack", "kill-fixture"}
assert actions["attack"]["focus"] is True
assert actions["attack"]["actionResult"]["result"]["committed"] == "Attack(focus)"
assert actions["attack"]["before"]["combat"]["enemies"][0]["hp"] == 675
assert actions["attack"]["after"]["combat"]["enemies"][0]["hp"] == 674
assert actions["kill-fixture"]["classification"] == "expected_death_capture_boundary"
assert actions["kill-fixture"]["capture"]["boundary"]["termination"] == "renderer_destroyed"

capture_counts = {"pass": 120, "attack": 120, "kill-fixture": 91}
sources = {
    CASE,
    JOURNAL,
    STAGE_SESSION,
    REVIEW,
    PROFILE,
    MANIFEST,
    V1,
    BATCH,
    RUNNER,
    ASSET / "amberwake.glb",
    ASSET / "amberwake_basecolor.png",
    Path(__file__),
}
setup_journal = Path(stage_session["journal"])
sources.add(setup_journal)
setup_result = BASE / f"case-{stage_session['case']}" / "result.json"
if setup_result.is_file():
    sources.add(setup_result)
sheet = CASE.with_name("selected-frames.html")
if sheet.is_file():
    sources.add(sheet)

capture_inputs = []
for label, expected_count in capture_counts.items():
    action = actions[label]
    raw_path = Path(action["capture"]["rawCapture"]["path"])
    raw = read(raw_path)
    assert raw_path.is_file() and raw["session"] == SESSION
    assert len(raw["frames"]) == expected_count
    assert raw_path.parent == BASE
    assert len(action["capture"]["images"]) == expected_count
    if expected_count == 120:
        assert raw["ok"] is True
        assert action["capture"]["boundary"]["completeCapture"] is True
    else:
        assert raw["ok"] is False
        assert action["capture"]["boundary"]["completeCapture"] is False
        assert action["capture"]["boundary"]["retainedFrameCount"] == 91
    for image in action["capture"]["images"]:
        image_path = Path(image["path"])
        assert image_path.is_file() and sha(image_path) == image["sha256"]
        sources.add(image_path)
    action_journal = Path(action["journal"]["path"])
    action_result = Path(action["rawResult"]["path"])
    assert action_journal.is_file() and action_result.is_file()
    sources.update((raw_path, action_journal, action_result))
    capture_inputs.append((label, action, raw_path, raw))

# Preserve helper responses referenced by journals while excluding game payloads.
pending_journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals = set()
excluded_suffixes = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
while pending_journals:
    journal = pending_journals.pop()
    if journal in seen_journals or not journal.is_file():
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        payload = json.loads(line).get("data", {}).get("path")
        if not payload:
            continue
        candidate = Path(payload)
        if not candidate.is_file() or candidate.suffix.lower() in excluded_suffixes:
            continue
        try:
            candidate.relative_to(ROOT)
        except ValueError:
            continue
        sources.add(candidate)
        if candidate.suffix == ".jsonl":
            pending_journals.add(candidate)

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.suffix.lower() not in excluded_suffixes, source

pins, mappings = {}, []
for source in sorted(sources):
    data = source.read_bytes()
    source_relative = relative(source)
    if source.suffix.lower() == ".png":
        pins[source_relative] = hashlib.sha256(data).hexdigest()
        continue
    archive = OUT / "metadata" / (source_relative + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(data, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == data
    mappings.append({
        "source": source_relative,
        "sourceSha256": hashlib.sha256(data).hexdigest(),
        "archive": str(archive.relative_to(OUT)),
        "archiveSha256": sha(archive),
        "encoding": "gzip-lossless",
    })

write(OUT / "source-image-pins.json", pins)
write(OUT / "asset-pins.json", case["assetHashes"])

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(destination) == frame["sha256"]
    selected.append({
        "source": frame["path"],
        "archive": str(destination.relative_to(OUT)),
        "sha256": frame["sha256"],
        "action": frame["action"],
        "index": frame["index"],
        "observation": frame["observation"],
    })

videos, captures, action_summary = [], [], []
for label, action, raw_path, raw in capture_inputs:
    count = capture_counts[label]
    capture_id = raw_path.stem
    video = OUT / f"{label}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(BASE / capture_id / "%04d.png"), "-frames:v", str(count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height,r_frame_rate", "-of", "json", str(video),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == count
    clips = {}
    for index, frame in enumerate(raw["frames"]):
        for layer in frame.get("animator", {}).get("layers", []):
            for clip in layer.get("playing", []):
                if clip.get("weight", 0) > 0:
                    clips.setdefault(clip["name"], []).append(index)
    captures.append({
        "label": label,
        "captureId": capture_id,
        "rawCapture": {
            "source": relative(raw_path),
            "sha256": sha(raw_path),
            "ok": raw["ok"],
            "error": raw["error"],
        },
        "frames": count,
        "requestedFrames": 120,
        "complete": count == 120,
        "termination": action["capture"]["boundary"]["termination"],
        "positiveClipRanges": {name: {"first": min(indices), "last": max(indices), "frames": len(indices)} for name, indices in clips.items()},
        "video": {
            "path": video.name,
            "sha256": sha(video),
            "width": int(probe["width"]),
            "height": int(probe["height"]),
            "playbackFps": 12,
            "timing": "Presentation derivative, not unperturbed timing",
        },
    })
    videos.append({
        "label": label,
        "captureId": capture_id,
        "path": video.name,
        "sha256": sha(video),
        "frames": count,
        "width": int(probe["width"]),
        "height": int(probe["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    })
    before = action["before"]["combat"]["enemies"][0]["hp"]
    after = action["after"]["combat"]["enemies"][0]["hp"]
    action_summary.append({
        "label": label,
        "focus": action["focus"],
        "classification": action["classification"],
        "accepted": action["actionAccepted"],
        "result": action["actionResult"]["result"],
        "enemyHpBefore": before,
        "enemyHpAfter": after,
        "journal": {"source": relative(Path(action["journal"]["path"])), "sha256": action["journal"]["sha256"]},
        "resultFile": {"source": relative(Path(action["rawResult"]["path"])), "sha256": action["rawResult"]["sha256"]},
    })

initial = case["initialRenderer"]
validation = {
    "status": "fresh_catalog_411_original_binding_focused_nonlethal_hit_visual_samples_and_fixture_death_prefix",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": "Amberwake Dragon",
    "nativeChassis": "dragonFrost",
    "rendererPath": "enDragon",
    "ownerInstanceId": initial["ownerInstanceId"],
    "visualScale": 0.25,
    "catalogSha256": case["profileSha256"],
    "assetHashes": case["assetHashes"],
    "binaryPins": case["binaryPins"],
    "binding": {
        "mesh": initial["mesh"],
        "rendererPath": initial["rendererPath"],
        "boneSignature": initial["boneSignature"],
        "rootBone": initial["rootBone"],
        "visible": initial["isVisible"],
        "enabled": initial["enabled"],
        "active": initial["active"],
        "material": initial["materials"],
    },
    "actions": action_summary,
    "captures": captures,
    "ordinaryNoFocusHit": "NOT_CLAIMED; the historical V1 ordinary attacks remained blocked.",
    "focusedHit": "OBSERVED; one native Attack(focus) changed the same enemy from HP 675 to 674.",
    "ordinaryLethal": "NOT_TESTED",
    "explicitKillFixture": "PARTIAL_91_OF_120_RENDERER_DESTROYED",
    "nativeCollects": len(case["collects"]),
    "finalReady": {
        "ok": case["finalReady"]["strictReady"]["ok"],
        "level": case["finalReady"]["strictReady"]["level"],
        "room": case["finalReady"]["strictReady"]["room"],
        "collectAvailable": case["finalReady"]["strictLootCollect"]["ok"],
    },
    "rootVisualReview": relative(REVIEW),
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(
    """# Amberwake Dragon fresh live trial V2

This supplement records fresh session `8714c0e28f3a422a9f827c6914a503d8` against the exact `dragonFrost` `enDragon` renderer. The authored `amberwake.glb` bound under one native enemy owner with the expected 70-bone signature at deliberate dungeon-fixture visual scale `0.25`. Its Standard material used the authored basecolor with emission disabled.

The pass and focused attack captures each retain 120 frames. The attack committed native `Attack(focus)` and changed the same enemy from HP 675 to 674. This supplements the immutable V1 ordinary no-focus boundary, where ordinary damaging hit was not observed; it does not establish ordinary no-focus hit behavior or ordinary lethal behavior.

Root reviewed nine stills from idle, the focused hit, and the explicit `KillSingle` prefix. At this test scale, the readable body, head, limbs and paired wings stay coherent in the reviewed samples, including changing wing positions across the action sequence. Native effects, HUD, blur and camera distance still obscure fine detail. The explicit death fixture retains 91 of 120 requested frames before the renderer is destroyed. It is preserved as a partial fixture-death prefix, not full death, corpse, ragdoll or cleanup acceptance. The fixture then reaches strict native Ready at level 0 room 2; no native Collect surface was available.

`validation.json` records the exact binding, actions, frames, source hashes and limits. `metadata/` stores lossless source metadata, `selected/` holds the nine reviewed originals, and the three videos are presentation derivatives at 12 fps. Native DLLs and game payloads are excluded. `archive.py` is offline-only and refuses to overwrite a completed archive.
"""
)
print(json.dumps({
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "validationSha256": sha(OUT / "validation.json"),
    "sourceImages": len(pins),
    "losslessMappings": len(mappings),
    "selected": len(selected),
    "videos": len(videos),
}))
