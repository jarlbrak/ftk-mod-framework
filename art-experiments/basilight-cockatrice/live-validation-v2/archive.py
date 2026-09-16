#!/usr/bin/env python3
"""Archive the fresh Basilight Cockatrice trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "basilight-cockatrice"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "db97671239c04b4e899fb14569fd72e7"
CASE = BASE / "case-8910015d72c343199c115ba85fc8d8b1" / "case-result.json"
JOURNAL = BASE / "case-8910015d72c343199c115ba85fc8d8b1" / "journal.jsonl"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
REVIEW = ROOT / "scratch" / "basilight-root-visual-review.json"
MANIFEST = ASSET / "manifest.json"
RUNTIME_PROFILE = ASSET / "runtime-profile.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)

case = read(CASE)
review = read(REVIEW)
manifest = read(MANIFEST)
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_basilight"
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3
assert review["session"] == SESSION and len(review["frames"]) == 6
assert review["binding"]["nativeChassis"] == "cockatriceC"
assert review["binding"]["ownerInstanceId"] == 369188
assert [item["rendererPath"] for item in review["binding"]["renderers"]] == ["enChicken"]
assert case["finalReady"]["strictReady"]["ok"] is True

session_file = BASE / f"new-run-session-{SESSION}.json"
sources = {CASE, JOURNAL, session_file, PROFILE, REVIEW, MANIFEST, RUNTIME_PROFILE}
for relative in manifest.get("assets", {}):
    path = ASSET / relative
    if path.is_file():
        sources.add(path)
for directory in (ASSET / "live-v1", ASSET / "live"):
    if directory.is_dir():
        for path in directory.rglob("*"):
            if path.is_file():
                sources.add(path)

session_record = read(session_file)
setup_journal = Path(session_record["journal"])
if setup_journal.is_file():
    sources.add(setup_journal)
setup_case = BASE / f"case-{session_record['case']}" / "result.json"
if setup_case.is_file():
    sources.add(setup_case)

capture_ids = []
capture_counts = {}
for action in case["actions"]:
    label = action["action"]
    raw = Path(action["capture"]["rawCapture"]["path"])
    journal = Path(action["journal"]["path"])
    result = Path(action["rawResult"]["path"])
    assert raw.is_file() and journal.is_file() and result.is_file()
    boundary = action["capture"]["boundary"]
    count = boundary["retainedFrameCount"]
    assert count == len(read(raw)["frames"])
    assert count == len(list(raw.with_suffix("").glob("*.png")))
    assert boundary["completeCapture"] is True and boundary["termination"] is None
    sources.update((raw, journal, result, *raw.with_suffix("").glob("*.png")))
    capture_counts[label] = count
    capture_ids.append((label, raw.stem, count))
assert capture_counts == {"pass": 120, "attack": 120, "kill-fixture": 120}

journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals = set()
while journals:
    journal = journals.pop()
    if journal in seen_journals or not journal.is_file():
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        value = json.loads(line).get("data", {}).get("path")
        if not value:
            continue
        path = Path(value)
        if not path.is_file():
            continue
        try:
            path.relative_to(ROOT)
        except ValueError:
            continue
        if path.suffix.lower() in {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}:
            continue
        sources.add(path)
        if path.suffix == ".jsonl":
            journals.add(path)

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.suffix.lower() not in {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}, source

pins, mappings = {}, []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    if relative.suffix.lower() == ".png":
        pins[str(relative)] = hashlib.sha256(raw).hexdigest()
        continue
    archive = OUT / "metadata" / (str(relative) + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    mappings.append({
        "source": str(relative),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(archive.relative_to(OUT)),
        "archiveSha256": sha(archive),
        "encoding": "gzip-lossless",
    })

write(OUT / "source-image-pins.json", pins)
write(OUT / "asset-pins.json", {name: value["sha256"] for name, value in manifest.get("assets", {}).items()})

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({
        "source": frame["path"],
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "action": frame["action"],
        "index": frame["index"],
        "observation": frame["observation"],
    })

videos = []
for label, capture_id, count in capture_ids:
    video = OUT / f"{label}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(BASE / capture_id / "%04d.png"), "-frames:v", str(count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)
    ]))["streams"][0]
    assert int(info["nb_read_frames"]) == count
    videos.append({
        "label": label,
        "captureId": capture_id,
        "path": video.name,
        "sha256": sha(video),
        "frames": count,
        "width": int(info["width"]),
        "height": int(info["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    })

captures = []
for label, capture_id, count in capture_ids:
    raw_path = BASE / f"{capture_id}.json"
    raw = read(raw_path)
    assert len(raw["frames"]) == count
    captures.append({
        "label": label,
        "captureId": capture_id,
        "frames": len(raw["frames"]),
        "rawSha256": sha(raw_path),
        "width": raw["width"],
        "height": raw["height"],
        "requestedFps": raw["requestedFps"],
        "complete": True,
        "termination": None,
    })

validation = {
    "status": "fresh_catalog_411_single_renderer_binding_appearance_motion_progression_and_complete_kill_capture_observed",
    "session": SESSION,
    "enemy": "ftkmf_modeltest_basilight",
    "displayName": "Basilight Cockatrice",
    "nativeChassis": "cockatriceC",
    "ownerInstanceId": review["binding"]["ownerInstanceId"],
    "visualScale": review["binding"]["visualScale"],
    "capturedNativeCelScale": review["binding"]["capturedNativeCelScale"],
    "renderers": review["binding"]["renderers"],
    "catalogSha256": case["profileSha256"],
    "profileSha256": case["profileSha256"],
    "assetHashes": {name: value["sha256"] for name, value in manifest.get("assets", {}).items()},
    "captures": captures,
    "ordinaryHit": review["ordinaryHit"],
    "ordinaryLethal": False,
    "explicitKillFixture": review["explicitKillFixture"],
    "nativeCollects": len(case.get("collects", [])),
    "finalReady": {
        "ok": case["finalReady"]["strictReady"]["ok"],
        "buttonCount": case["finalReady"]["strictReady"]["buttonCount"],
        "level": case["finalReady"]["strictReady"]["level"],
        "room": case["finalReady"]["strictReady"]["room"],
        "collectAvailable": review["finalReady"]["collectAvailable"],
    },
    "materialReview": review["materialReview"],
    "progression": "The explicit kill fixture removed the enemy, one guarded native Collect was accepted, and strict native Ready was observed at level 0 room 2.",
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(
    """# Basilight Cockatrice fresh live trial V2

This supplement records the fresh catalog-411 run in session `db97671239c04b4e899fb14569fd72e7` using the exact cockatriceC chassis at public visual-scale factor `1.0` (captured native CEL scale `0.55`). The authored `basilight.glb` bound to `enChicken` under one live enemy owner with the expected 50-joint bone signature.

Selected idle and attack views show the colored body, crest, beak, wing panels, legs and claws staying connected through native motion. The runtime inventory reports the authored basecolor and the native Standard material `matMasterJungle (Instance)` with emission disabled/black and no emission map.

Pass, ordinary attack and explicit `KillSingle` fixture captures are complete 120-frame recordings. The ordinary attack changes the same target from HP 72 to 62 with `cheat=None` and no focus. The explicit kill fixture reaches the native Victory loot surface; one guarded native Collect is accepted and strict Ready is observed at level 0 room 2. The victory overlay and native UI/effects limit fine crest/feather detail, full death deformation, settled-ragdoll, culling-envelope, portrait/resource lifetime and finished-art acceptance.

`validation.json` preserves the fresh case result, setup/action journals, helper responses, authoring assets and the prior V1 live record as gzip-lossless metadata, all current source-image hashes, six selected originals and presentation videos for all three complete captures. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
"""
)
print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "sourceImages": len(pins),
    "losslessMappings": len(mappings),
    "selected": len(selected),
    "videos": len(videos),
    "validationSha256": sha(OUT / "validation.json"),
}))
