#!/usr/bin/env python3
"""Archive the fresh Bronzehollow deathKnight live trial without game payloads."""
from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "bronzehollow-sentinel"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "bronzehollow-v2-standalone-case.json"
REVIEW = ROOT / "scratch" / "bronzehollow-v2-root-visual-review.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
SESSION = "e802009d7ed1492fa7a0bc4ed3effea5"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)
case = read(CASE)
review = read(REVIEW)
manifest = read(MANIFEST)
assert case["status"] == "needs_visual_review"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_bronzehollow"
assert [action["frames"] for action in case["actions"]] == [120, 120, 120]
assert all(action["complete"] for action in case["actions"])
assert case["ordinaryAttack"]["beforeHp"] == 58 and case["ordinaryAttack"]["afterHp"] == 58
assert case["ordinaryAttack"]["blocked"] is True
assert case["explicitKillFixture"]["beforeHp"] == 58 and case["explicitKillFixture"]["afterHp"] == 0
assert case["explicitKillFixture"]["method"] == "KillSingle"
assert case["nativeCollects"] == 1
assert case["finalReady"] == {"ok": True, "buttonCount": 1, "level": 0, "room": 2, "collectAvailable": False}
assert case["afterReadyState"]["enemyTypes"] == ["cubeA", "ftkmf_modeltest_probe_cultista"]
assert review["session"] == SESSION and len(review["frames"]) == 6

assets = manifest.get("files", {})
asset_hashes = {name: value for name, value in assets.items()}
asset_paths = [ASSET / name for name in assets]
sources = {CASE, REVIEW, PROFILE, MANIFEST}
sources.update(asset_paths)
sources.add(BASE / f"new-run-session-{SESSION}.json")
sources.add(ROOT / case["setup"]["bindingResult"])
sources.add(ROOT / case["afterReadyState"]["path"])
sources.add(ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "86eec62a8e7e462a9da9babed7950e5f.json")
sources.add(ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "359fdf9ef07141c0bcb53b2061187a6a.json")
sources.add(ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "1f54b9baa8794f12bae218bcb399ede5.json")
sources.add(ROOT / case["readyResult"]["path"])
for helper in case["helpers"]:
    sources.add(ROOT / helper["path"])
for action in case["actions"]:
    sources.update({ROOT / action["journal"], ROOT / action["rawResult"], ROOT / action["summaryPath"]})
    raw = ROOT / action["rawResult"]
    sources.update(raw.with_suffix("").glob("*.png"))
for frame in review["frames"]:
    sources.add(ROOT / frame["path"])
for path in ASSET.rglob("*"):
    if path.is_file() and not any(part.startswith("live-validation-") for part in path.parts) and "__pycache__" not in path.parts and path.suffix != ".pyc":
        sources.add(path)

# Follow journal-recorded paths, retaining raw provenance but never binaries.
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
        if path.suffix.lower() in FORBIDDEN:
            continue
        sources.add(path)
        if path.suffix == ".jsonl":
            journals.add(path)

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.suffix.lower() not in FORBIDDEN, source

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
write(OUT / "asset-pins.json", asset_hashes)

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

videos, captures = [], []
for action in case["actions"]:
    label = action["label"]
    raw_path = ROOT / action["rawResult"]
    raw = read(raw_path)
    count = action["frames"]
    frame_dir = raw_path.with_suffix("")
    assert len(raw.get("frames", [])) == count and len(list(frame_dir.glob("*.png"))) == count
    video = OUT / ("kill-fixture.mp4" if label == "kill-fixture" else f"{label}.mp4")
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", str(count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)
    ]))["streams"][0]
    assert int(info["nb_read_frames"]) == count
    videos.append({"label": label, "captureId": raw_path.stem, "path": video.name, "sha256": sha(video), "frames": count, "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12, "timing": "Presentation derivative, not unperturbed timing"})
    captures.append({"label": label, "captureId": raw_path.stem, "case": action["case"], "frames": count, "rawSha256": sha(raw_path), "summarySha256": sha(ROOT / action["summaryPath"]), "width": raw["width"], "height": raw["height"], "requestedFps": raw["requestedFps"], "complete": action["complete"], "termination": action["termination"], "captureReportedOk": action["captureReportedOk"], "captureError": action["captureError"], "actualGameSeconds": action["actualGameSeconds"]})

ready_result_path = ROOT / case["readyResult"]["path"]
validation = {
    "status": "fresh_catalog_411_single_renderer_binding_complete_blocked_attack_kill_fixture_ready_observed",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": case["displayName"],
    "nativeChassis": case["nativeChassis"],
    "catalogSha256": case["catalogSha256"],
    "profileSha256": case["profileSha256"],
    "assetHashes": asset_hashes,
    "binding": case["binding"],
    "captures": captures,
    "ordinaryAttack": case["ordinaryAttack"],
    "ordinaryLethal": False,
    "explicitKillFixture": case["explicitKillFixture"],
    "nativeCollects": case["nativeCollects"],
    "finalReady": case["finalReady"],
    "readyResult": {"path": str(ready_result_path.relative_to(ROOT)), "sha256": sha(ready_result_path), "result": read(ready_result_path)},
    "nextEncounter": case["afterReadyState"],
    "materialReview": case["materialReview"],
    "progression": "The fresh deathKnight process bound the authored body, completed pass/blocked-attack/KillSingle captures, observed ordinary HP 58 unchanged after Attack, accepted one native Collect into strict Ready at level 0 room 2, and accepted one guarded Ready vote into the normal Jelly Cube plus cultist-probe room.",
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "standaloneCase": str(CASE.relative_to(ROOT)),
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "nativeReadyContinuation": str((ROOT / case["afterReadyState"]["path"]).relative_to(ROOT)),
    "limits": case["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(f"""# Bronzehollow Sentinel fresh live trial V2

This supplement records one fresh catalog-411 process (`{SESSION}`) using the exact `deathknightA` chassis at public visual scale `1.0` (captured native CEL scale `1.2`). The authored `deathKnight` body stayed bound to one enemy owner while the native helmet, shield and weapon remained game-owned.

Pass, ordinary attack and explicit `KillSingle` captures each contain 120 unpaused frames. The ordinary Attack committed with no focus and resolved as native `BLOCKED`; enemy HP stayed `58` while the hero took the native counterattack. The explicit `KillSingle` fixture committed from HP `58` to `0` and reached the native victory/loot boundary. That fixture does not establish ordinary damaging or lethal acceptance.

The post-death surface required one guarded native Collect, then exposed strict Ready at level `0` / room `2`. One guarded Ready click advanced the dungeon to the normal Jelly Cube plus registered cultist-probe encounter. The body material reported the authored basecolor, disabled emission keyword and no emission map. The live palette remains darker/reddish than the studio render and needs a separate material/lighting decision.

Selected originals show the readable body under retained equipment in idle, blocked attack and victory/loot presentation. Native UI/effects and retained equipment limit fine intersections, complete deformation, collision/sleeping, culling, portraits, resource lifetime and finished-art acceptance. No sibling Deathknight variant acceptance is claimed.

`validation.json` preserves the standalone case, helper results, native Collect/Ready continuation, journals, complete raw captures, authoring manifest and source hashes as gzip-lossless metadata. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
""")
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(mappings), "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}, indent=2))
