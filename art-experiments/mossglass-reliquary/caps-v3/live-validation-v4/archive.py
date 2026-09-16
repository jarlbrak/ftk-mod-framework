#!/usr/bin/env python3
"""Archive the fresh Mossglass Cube A V3 trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "mossglass-reliquary" / "caps-v3"
OUT = ASSET / "live-validation-v4"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "022120d503914c8f8d0e584a21477f41"
CASE = BASE / "case-b08797d373a24b0eaaf6e1bc7f0ebbf5" / "case-result.json"
JOURNAL = BASE / "case-b08797d373a24b0eaaf6e1bc7f0ebbf5" / "journal.jsonl"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
REVIEW = ROOT / "scratch" / "mossglass-root-visual-review.json"
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
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_mossglass_caps_v3"
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3 and len(case["collects"]) == 1
assert review["session"] == SESSION and len(review["frames"]) == 6
assert review["binding"] == {
    "nativeChassis": "cubeA",
    "rendererPath": "enJellyCube",
    "rendererId": 121012,
    "ownerInstanceId": 369188,
    "boneSignature": "320e13b7f80ce3886d35d8718ec2f4b47f84df8210e0ca0ded4513d7c59e65eb",
    "visualScale": 1.0,
}

session_file = BASE / f"new-run-session-{SESSION}.json"
sources = {CASE, JOURNAL, session_file, PROFILE, REVIEW, MANIFEST, RUNTIME_PROFILE}
for path in (
    ASSET / "README.md", ASSET / "audit_caps.py", ASSET / "build_caps.py", ASSET / "build_blender.py",
    ASSET / "finalize_manifest.py", ASSET / "change-audit.json", ASSET / "corrected-cap-audit.json",
    ASSET / "original-geometry-proof.json", ASSET / "root-v1-v2-cap-audit.json", ASSET / "root-v3-cap-audit.json",
    ASSET / "v1-v2-cap-diagnosis.json", ASSET / "mossglass_caps_v3.glb", ASSET / "mossglass_caps_v3.blend",
    ASSET / "mossglass_caps_v3-studio.blend", ASSET / "mossglass_caps_v3.source.json",
    ASSET / "mossglass_caps_v3.validation.json", ASSET / "mossglass_caps_v3-reopened.glb",
    ASSET / "mossglass_caps_v3-reopened.source.json", ASSET / "mossglass_caps_v3-reopened.validation.json",
    ASSET / "mossglass.slot0.png", ASSET / "jade-v2" / "mossglass_jade_v2.slot1.png",
    ASSET / "jade-v2" / "manifest.json", ASSET / "jade-v2" / "runtime-profile.json",
    ASSET / "jade-v2" / "live-validation-v2.json", ASSET / "live-validation-v3.json",
):
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
    raw, journal, result = Path(action["rawCapture"]["path"]), Path(action["journal"]["path"]), Path(action["rawResult"]["path"])
    assert raw.is_file() and journal.is_file() and result.is_file()
    pngs = sorted(raw.with_suffix("").glob("*.png"))
    assert len(pngs) == 120
    sources.update((raw, journal, result, *pngs))
    capture_ids.append(raw.stem)

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
write(OUT / "asset-pins.json", {a["file"]: a["sha256"] for a in manifest.get("assets", [])})

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({"source": frame["path"], "archive": str(destination.relative_to(OUT)), "sha256": sha(destination),
                     "action": frame["action"], "index": frame["index"], "observation": frame["observation"]})

videos = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    video = OUT / f"{label}.mp4"
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
                    "-i", str(BASE / capture_id / "%04d.png"), "-frames:v", "120", "-c:v", "libx264",
                    "-crf", "18", "-pix_fmt", "yuv420p", str(video)], check=True)
    info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)]))["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append({"label": label, "captureId": capture_id, "path": video.name, "sha256": sha(video), "frames": 120,
                   "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12,
                   "timing": "Presentation derivative, not unperturbed timing"})

captures = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    raw_path, raw = BASE / f"{capture_id}.json", read(BASE / f"{capture_id}.json")
    assert len(raw["frames"]) == 120
    captures.append({"label": label, "captureId": capture_id, "frames": 120, "rawSha256": sha(raw_path),
                     "width": raw["width"], "height": raw["height"], "requestedFps": raw["requestedFps"]})

validation = {
    "status": "fresh_catalog_411_binding_material_appearance_motion_and_progression_observed",
    "session": SESSION, "enemy": "ftkmf_modeltest_mossglass_caps_v3", "displayName": "Mossglass Reliquary",
    "nativeChassis": "cubeA", "rendererPath": "enJellyCube", "rendererId": 121012, "ownerInstanceId": 369188,
    "boneSignature": review["binding"]["boneSignature"], "visualScale": 1.0,
    "catalogSha256": case["profileSha256"], "profileSha256": case["profileSha256"],
    "assetHashes": {a["file"]: a["sha256"] for a in manifest.get("assets", [])}, "captures": captures,
    "ordinaryHit": {"beforeHp": 58, "afterHp": 50, "cheat": "None", "focus": False}, "ordinaryLethal": False,
    "explicitKillFixture": True, "nativeCollects": len(case["collects"]),
    "finalReady": {"ok": case["finalReady"]["strictReady"]["ok"], "buttonCount": case["finalReady"]["strictReady"]["buttonCount"],
                   "level": case["finalReady"]["strictReady"]["level"], "room": case["finalReady"]["strictReady"]["room"]},
    "materialSlots": review["materialReview"],
    "progression": "One native Collect was accepted and strict native Ready was observed at level 0 room 2.",
    "selectedPNGs": selected, "videos": videos, "sourceImageCount": len(pins), "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json", "losslessMappings": mappings, "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text("""# Mossglass Reliquary Cube A fresh live trial V4

This supplement records the fresh catalog-411 run in session `022120d503914c8f8d0e584a21477f41` using the corrected V3 cap asset on Cube A, renderer `enJellyCube` (121012), owner `369188`, and visual scale `1.0`. Both authored primitives bound through the exact three-joint palette; the jade shell and ivory/amber interior remained visibly distinct.

The pass and attack are complete 120-frame recordings. The ordinary attack changed the same target from HP 58 to 50 with `cheat=None` and no focus. The explicit `KillSingle` fixture completed 120 frames, removed the enemy, accepted one native Collect and reached strict native Ready at level 0 room 2. The fixture death is not ordinary lethal damage.

Root reviewed two idle frames, two poison-attack frames and the fixture endpoints. The corrected shell cap, open rib cage and amber core are readable in the selected views. Native effects, hero/UI occlusion and inherited emission limit fine cap, scroll-phase, culling and deformation review; the terminal view is the native victory/item-choice surface.

`validation.json` preserves the case result, setup/action journals, helper responses, V3 authoring/history sources, profile/runtime/manifest metadata as gzip-lossless records, all 360 source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
""")
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(mappings),
                  "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}))
