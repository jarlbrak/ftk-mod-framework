#!/usr/bin/env python3
"""Archive the fresh Thistlewick trials without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "thistlewick-hexer"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "thistlewick-standalone-case.json"
REVIEW = ROOT / "scratch" / "thistlewick-root-visual-review.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
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
assert case["status"] == "needs_visual_review"
assert case["enemy"] == "ftkmf_modeltest_thistlewick"
assert len(case["actions"]) == 3
assert len(case["sessions"]) == 3
assert review["binding"]["nativeChassis"] == "scourgeG"
assert review["binding"]["ownerInstanceId"] == 369188
assert review["binding"]["renderers"] == [{
    "rendererPath": "enScourgeLeprechaun",
    "rendererId": 121222,
    "capturedInstanceId": -245598,
    "glbFile": "thistlewick.glb",
    "boneSignature": "ff578d822b39be20026a8ea349afc605024816801a454f5e4f79a52c3b03140a",
}]
assert review["finalReady"]["ok"] is True and review["finalReady"]["level"] == 0 and review["finalReady"]["room"] == 2

manifest_assets = manifest.get("assets", [])
if isinstance(manifest_assets, dict):
    asset_hashes = {name: value["sha256"] for name, value in manifest_assets.items()}
    asset_paths = [ASSET / name for name in manifest_assets]
else:
    asset_hashes = {value["file"]: value["sha256"] for value in manifest_assets}
    asset_paths = [ASSET / value["file"] for value in manifest_assets]

sources = {CASE, REVIEW, PROFILE, MANIFEST, RUNTIME_PROFILE}
for setup in case["setup"]:
    sources.update({
        BASE / Path(setup["sessionRecord"]["path"]).name,
        ROOT / setup["result"]["path"],
        ROOT / setup["journal"]["path"],
    })
for action in case["actions"]:
    sources.update({
        ROOT / action["journal"]["path"],
        ROOT / action["rawResult"]["path"],
        ROOT / action["capture"]["path"],
        ROOT / action["capture"]["summaryPath"],
    })
    raw = ROOT / action["capture"]["path"]
    sources.update(raw.with_suffix("").glob("*.png"))
continuation = ROOT / case["nativeLootReadyContinuation"]["path"]
sources.add(continuation)
continuation_doc = read(continuation)
for value in (continuation_doc.get("files") or {}).values():
    path = ROOT / value["path"]
    if path.is_file():
        sources.add(path)

# Preserve the authoring source, the earlier live diagnostics and the exact
# manifest-listed files, while keeping the destination archive out of itself.
sources.update(path for path in asset_paths if path.is_file())
for path in ASSET.rglob("*"):
    if not path.is_file() or OUT in path.parents or "__pycache__" in path.parts or path.suffix == ".pyc":
        continue
    sources.add(path)

# Journals can point to helper responses and capture summaries not named in the
# case manifest. Follow only paths inside this workspace and never game payloads.
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

videos = []
captures = []
for action in case["actions"]:
    label = action["action"]
    raw_path = ROOT / action["capture"]["path"]
    raw = read(raw_path)
    count = action["capture"]["frames"]
    frame_dir = raw_path.with_suffix("")
    assert action["capture"]["complete"] is True and count == 120
    assert len(raw["frames"]) == count and len(list(frame_dir.glob("*.png"))) == count
    video = OUT / f"{label}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", str(count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
    ]))["streams"][0]
    assert int(info["nb_read_frames"]) == count
    videos.append({
        "label": label,
        "captureId": raw_path.stem,
        "path": video.name,
        "sha256": sha(video),
        "frames": count,
        "width": int(info["width"]),
        "height": int(info["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    })
    captures.append({
        "label": label,
        "session": action["session"],
        "captureId": raw_path.stem,
        "frames": count,
        "rawSha256": sha(raw_path),
        "summarySha256": action["capture"]["summarySha256"],
        "width": raw["width"],
        "height": raw["height"],
        "requestedFps": raw["requestedFps"],
        "complete": True,
        "termination": None,
    })

validation = {
    "status": "fresh_catalog_411_three_process_scourgeG_binding_complete_attack_kill_loot_ready_observed",
    "sessions": case["sessions"],
    "processBoundary": case["processBoundary"],
    "enemy": case["enemy"],
    "displayName": case["displayName"],
    "nativeChassis": case["binding"]["nativeChassis"],
    "ownerInstanceId": case["binding"]["ownerInstanceId"],
    "visualScale": case["binding"]["visualScale"],
    "capturedNativeCelScale": case["binding"]["capturedNativeCelScale"],
    "renderers": case["binding"]["renderers"],
    "catalogSha256": case["profileSha256"],
    "profileSha256": case["profileSha256"],
    "assetHashes": asset_hashes,
    "captures": captures,
    "ordinaryAttack": case["ordinaryAttack"],
    "ordinaryLethal": True,
    "explicitKillFixture": True,
    "nativeCollects": case["collects"],
    "finalReady": case["finalReady"],
    "materialReview": case["materialReview"],
    "progression": "The pass case ended through native scourge behavior and is not classified as death. A separate ordinary attack resolved the same target from HP 58 to 0 without a cheat. A third fresh process supplied the explicit deathHeavy_imp capture, two guarded native Collect clicks, and strict Ready at level 0 room 2.",
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "standaloneCase": str(CASE.relative_to(ROOT)),
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "limits": case["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(
    """# Thistlewick Hexer fresh live trial V2

This supplement records three independent fresh catalog-411 processes for the exact scourgeG chassis, renderer enScourgeLeprechaun, controller 5934, native CEL scale 1.0, and the authored thistlewick.glb with its observed 36-bone signature. Each process bound the custom mesh under one live enemy owner (369188) with the authored basecolor on the native Standard material.

The pass process preserves the native scourge pass/flee removal behavior without classifying it as death. A separate ordinary attack process resolves the same target from HP 58 to 0 with no cheat or focus; its capture is an appearance/motion record, not a death-animation verdict. The third process uses the explicit KillSingle fixture and records deathHeavy_imp across a complete 120-frame capture. Native Victory loot then accepts two guarded Collect votes, and strict Ready is observed at level 0 / room 2 after one guarded Ready vote.

The six selected originals show the intact coat, face, fingers and root silhouette in native combat staging, the ordinary attack capture, and the explicit death/victory boundary. Native targeting and effects, foreground hero occlusion, victory depth blur and the small combat view limit fine surface inspection, settled-body/culling-envelope claims, portrait/resource lifetime, global Fergus haunt behavior and finished-art acceptance.

validation.json preserves the three setup/action journals, all complete raw captures and PNG hashes, native Loot/Ready responses, authoring assets and prior Thistlewick diagnostics as gzip-lossless metadata. Native payloads and DLLs are excluded. archive.py is offline-only and refuses a completed destination.
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
