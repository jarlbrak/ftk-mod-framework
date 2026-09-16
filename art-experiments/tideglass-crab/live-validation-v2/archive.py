#!/usr/bin/env python3
"""Archive the fresh Tideglass Crab B combat trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "tideglass-crab"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "0b328315443b47e4bc9515cfa4808a65"
CASE = BASE / "case-d2beb180d64f4528a46bf06514ffea47" / "case-result.json"
JOURNAL = BASE / "case-d2beb180d64f4528a46bf06514ffea47" / "journal.jsonl"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
REVIEW = ROOT / "scratch" / "tideglass-root-visual-review.json"
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
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_tideglass"
assert case["status"] == "needs_visual_review"
assert len(case["actions"]) == 3 and len(case["collects"]) == 2
assert review["session"] == SESSION and len(review["frames"]) == 6
assert review["binding"] == {
    "nativeChassis": "crabB",
    "rendererPath": "enCrabWizard",
    "rendererId": 121411,
    "ownerInstanceId": 369188,
    "boneSignature": "e51916d0e0a577fa1442bdcf75dff8a51102a2eaf3d2edc36c11ddaf8c441e96",
    "visualScale": 1.0,
}

session_file = BASE / f"new-run-session-{SESSION}.json"
sources = {CASE, JOURNAL, session_file, PROFILE, REVIEW, MANIFEST, RUNTIME_PROFILE}
for name in (
    "native-material-metadata.json",
    "tideglass.source.json",
    "tideglass.validation.json",
    "tideglass.pieces.json",
    "live-validation.json",
):
    path = ASSET / name
    if path.is_file():
        sources.add(path)

session_record = read(session_file)
setup_journal = Path(session_record["journal"])
if setup_journal.is_file():
    sources.add(setup_journal)
setup_case = BASE / f"case-{session_record['case']}" / "case-result.json"
if setup_case.is_file():
    sources.add(setup_case)

capture_ids = []
for action in case["actions"]:
    raw = Path(action["rawCapture"]["path"])
    journal = Path(action["journal"]["path"])
    result = Path(action["rawResult"]["path"])
    assert raw.is_file() and journal.is_file() and result.is_file()
    pngs = sorted(raw.with_suffix("").glob("*.png"))
    assert len(pngs) == 120
    sources.update((raw, journal, result, *pngs))
    capture_ids.append(raw.stem)

# Helper responses referenced by retained journals are useful for replay audits.
journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals = set()
while journals:
    journal = journals.pop()
    if journal in seen_journals or not journal.is_file():
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        entry = json.loads(line)
        path_value = entry.get("data", {}).get("path")
        if not path_value:
            continue
        path = Path(path_value)
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
    assert source.is_file(), source

pins = {}
mappings = []
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
write(OUT / "asset-pins.json", {
    name: manifest.get("assets", {}).get(name, {}).get("sha256")
    for name in ("tideglass.glb", "tideglass_basecolor.png")
    if name in manifest.get("assets", {})
})

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
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    video = OUT / f"{label}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-framerate", "12", "-i", str(BASE / capture_id / "%04d.png"),
        "-frames:v", "120", "-c:v", "libx264", "-crf", "18",
        "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)
    ]))["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append({
        "label": label,
        "captureId": capture_id,
        "path": video.name,
        "sha256": sha(video),
        "frames": 120,
        "width": int(info["width"]),
        "height": int(info["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    })

captures = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    raw_path = BASE / f"{capture_id}.json"
    raw = read(raw_path)
    assert len(raw["frames"]) == 120
    captures.append({
        "label": label,
        "captureId": capture_id,
        "frames": len(raw["frames"]),
        "rawSha256": sha(raw_path),
        "width": raw["width"],
        "height": raw["height"],
        "requestedFps": raw["requestedFps"],
    })

validation = {
    "status": "fresh_catalog_411_binding_appearance_motion_and_progression_observed",
    "session": SESSION,
    "enemy": "ftkmf_modeltest_tideglass",
    "displayName": "Tideglass Crab",
    "nativeChassis": "crabB",
    "rendererPath": "enCrabWizard",
    "rendererId": 121411,
    "ownerInstanceId": 369188,
    "boneSignature": review["binding"]["boneSignature"],
    "visualScale": 1.0,
    "catalogSha256": case["profileSha256"],
    "profileSha256": case["profileSha256"],
    "assetHashes": {
        "tideglass.glb": manifest["assets"]["tideglass.glb"]["sha256"],
        "tideglass_basecolor.png": manifest["assets"]["tideglass_basecolor.png"]["sha256"],
    },
    "captures": captures,
    "ordinaryHit": {"beforeHp": 58, "afterHp": 56, "cheat": "None", "focus": False},
    "ordinaryLethal": False,
    "explicitKillFixture": True,
    "nativeCollects": len(case["collects"]),
    "finalReady": {
        "ok": case["finalReady"]["strictReady"]["ok"],
        "buttonCount": case["finalReady"]["strictReady"]["buttonCount"],
        "level": case["finalReady"]["strictReady"]["level"],
        "room": case["finalReady"]["strictReady"]["room"],
    },
    "progression": "Two native Collect actions were accepted and strict native Ready was observed at level 0 room 2.",
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
    """# Tideglass Crab fresh live trial V2

This supplement records the fresh catalog-411 run in session `0b328315443b47e4bc9515cfa4808a65` using Crab B, renderer `enCrabWizard` (121411), owner `369188`, and visual scale `1.0`. The authored teal crab surface bound to the exact native renderer and stayed readable across the pass, ordinary attack and explicit fixture captures.

The pass and attack are complete 120-frame recordings. The ordinary attack changed the same target from HP 58 to 56 with `cheat=None` and no focus. The explicit `KillSingle` fixture also completed 120 frames, removed the enemy, accepted two native Collect actions and reached strict native Ready at level 0 room 2. The fixture death is not ordinary lethal damage.

Root reviewed two settled idle frames, two ordinary-attack frames and the fixture endpoints. The teal carapace, eye stalks, six legs and separated copper-and-ivory pincers remain readable, with the native purple wizard hat retained. Hero/UI/effects and the terminal victory surface limit fine deformation, accessory, corpse and floor-contact inspection.

`validation.json` preserves the case result, setup/action journals, helper responses, profile/runtime/authoring manifests and the earlier live baseline as gzip-lossless metadata, all 360 source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
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
