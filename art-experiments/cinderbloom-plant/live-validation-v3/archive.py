#!/usr/bin/env python3
"""Archive the fresh Cinderbloom multipart combat trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "cinderbloom-plant"
OUT = ASSET / "live-validation-v3"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "832c6a68d7084c4e967a53274884d574"
CASE = BASE / "case-44fe7934485641deb59df46a9a320e0e" / "case-result.json"
JOURNAL = BASE / "case-44fe7934485641deb59df46a9a320e0e" / "journal.jsonl"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
REVIEW = ROOT / "scratch" / "cinderbloom-root-visual-review.json"
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
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_cinderbloom"
assert case["status"] == "needs_visual_review"
assert len(case["actions"]) == 3 and len(case["collects"]) == 1
assert review["session"] == SESSION and len(review["frames"]) == 6
assert review["binding"]["nativeChassis"] == "plantA"
assert review["binding"]["ownerInstanceId"] == 369188
assert [item["rendererPath"] for item in review["binding"]["renderers"]] == ["enPlant01", "enPlant01Leaves"]

session_file = BASE / f"new-run-session-{SESSION}.json"
sources = {CASE, JOURNAL, session_file, PROFILE, REVIEW, MANIFEST, RUNTIME_PROFILE}
for path in (
    ASSET / "README.md", ASSET / "build_geometry.py", ASSET / "build_blender.py", ASSET / "finalize_manifest.py",
    ASSET / "hero.png", ASSET / "side.png", ASSET / "cinderbloom-studio.blend", ASSET / "cinderbloom_basecolor.png",
    ASSET / "cinderbloom_body.blend", ASSET / "cinderbloom_body.glb", ASSET / "cinderbloom_body.pieces.json",
    ASSET / "cinderbloom_body.source.json", ASSET / "cinderbloom_body.validation.json", ASSET / "cinderbloom_leaves.blend",
    ASSET / "cinderbloom_leaves.glb", ASSET / "cinderbloom_leaves.pieces.json", ASSET / "cinderbloom_leaves.source.json",
    ASSET / "cinderbloom_leaves.validation.json", ASSET / "live-validation-v1.json", ASSET / "live-validation-v2.json",
    ASSET / "live-validation-native-scale.json",
):
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
for action in case["actions"]:
    raw = Path(action["capture"]["rawCapture"]["path"])
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
    assert source.is_file(), source

pins, mappings = {}, []
for source in sorted(sources):
    raw, relative = source.read_bytes(), source.relative_to(ROOT)
    if relative.suffix.lower() == ".png":
        pins[str(relative)] = hashlib.sha256(raw).hexdigest()
        continue
    archive = OUT / "metadata" / (str(relative) + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    mappings.append({
        "source": str(relative), "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(archive.relative_to(OUT)), "archiveSha256": sha(archive), "encoding": "gzip-lossless",
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
        "source": frame["path"], "archive": str(destination.relative_to(OUT)), "sha256": sha(destination),
        "action": frame["action"], "index": frame["index"], "observation": frame["observation"],
    })

videos = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    video = OUT / f"{label}.mp4"
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(BASE / capture_id / "%04d.png"), "-frames:v", "120", "-c:v", "libx264",
        "-crf", "18", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    info = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)
    ]))["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append({
        "label": label, "captureId": capture_id, "path": video.name, "sha256": sha(video), "frames": 120,
        "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    })

captures = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    raw_path = BASE / f"{capture_id}.json"
    raw = read(raw_path)
    assert len(raw["frames"]) == 120
    captures.append({
        "label": label, "captureId": capture_id, "frames": len(raw["frames"]), "rawSha256": sha(raw_path),
        "width": raw["width"], "height": raw["height"], "requestedFps": raw["requestedFps"],
    })

validation = {
    "status": "fresh_catalog_411_multipart_binding_appearance_motion_and_progression_observed",
    "session": SESSION,
    "enemy": "ftkmf_modeltest_cinderbloom",
    "displayName": "Cinderbloom",
    "nativeChassis": "plantA",
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
    "nativeCollects": len(case["collects"]),
    "finalReady": {
        "ok": case["finalReady"]["strictReady"]["ok"],
        "buttonCount": case["finalReady"]["strictReady"]["buttonCount"],
        "level": case["finalReady"]["strictReady"]["level"],
        "room": case["finalReady"]["strictReady"]["room"],
    },
    "materialReview": review["materialReview"],
    "progression": "One native Collect action was accepted and strict native Ready was observed at level 0 room 2.",
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
    """# Cinderbloom fresh live trial V3

This supplement records the fresh catalog-411 run in session `832c6a68d7084c4e967a53274884d574` using plantA at public visual-scale factor `1.0` (captured native CEL scale `1.6`). Both authored parts bound to the same enemy owner: `enPlant01` uses the corrected facing body GLB and `enPlant01Leaves` uses the separate leaf GLB, with separate observed bone signatures.

The corrected V2 mouth, teeth, ember throat and jaw cup face the combat camera. Selected idle and attack views retain the stem, corolla, mouth, root leaves and separate leaf renderer without obvious multipart separation. Both runtime materials use the authored basecolor; the inventory reports black emission values with no emission maps while the inherited emission keyword remains present.

The pass and ordinary attack captures are complete 120-frame recordings. The ordinary attack changes the same target from HP 58 to 50 with `cheat=None` and no focus. The explicit `KillSingle` fixture completes 120 frames, removes the enemy, accepts one native Collect and reaches strict native Ready at level 0 room 2. The fixture death is not ordinary lethal damage.

Root reviewed two idle frames, two attack frames and the fixture endpoints. Native targeting UI/effects and the victory item surface limit fine teeth, jaw, leaf-intersection, complete deformation, culling-envelope, portrait/resource lifetime and finished-art acceptance.

`validation.json` preserves the case result, setup/action journals, helper responses, authoring assets and immutable V1/V2/native-scale records as gzip-lossless metadata, all current source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
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
