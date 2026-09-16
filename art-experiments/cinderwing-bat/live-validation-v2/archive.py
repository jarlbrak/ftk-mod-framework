#!/usr/bin/env python3
"""Archive the Cinderwing native-scale live trial without game payloads."""
from pathlib import Path
import gzip, hashlib, json, os, shutil, subprocess

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "cinderwing-bat"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "cinderwing-v2-standalone-case.json"
REVIEW = ROOT / "scratch" / "cinderwing-v2-root-visual-review.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
SESSION = "b3478c0c658f47c6a3ba82b94b9a6f2a"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path): return json.loads(Path(path).read_text())
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")

assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)
case, review, manifest = read(CASE), read(REVIEW), read(MANIFEST)
assert case["status"] == "needs_visual_review" and case["session"] == SESSION
assert case["enemy"] == "ftkmf_modeltest_cinderwing"
assert [a["frames"] for a in case["actions"]] == [120, 120, 120] and all(a["complete"] for a in case["actions"])
assert case["ordinaryAttack"] == {"beforeHp": 58, "afterHp": 50, "cheat": "None", "focus": False}
assert case["explicitKillFixture"]["beforeHp"] == 50 and case["explicitKillFixture"]["afterHp"] == 0
assert case["nativeCollects"] == 1 and case["finalReady"] == {"ok": True, "buttonCount": 1, "level": 0, "room": 2, "collectAvailable": False}
assert review["session"] == SESSION and len(review["frames"]) == 5

asset_hashes = manifest.get("assets", {})
sources = {CASE, REVIEW, PROFILE, MANIFEST}
sources.update(ASSET / name for name in manifest.get("assets", {}))
sources.update({ASSET / "live-validation.json", ASSET / "live-validation-native-scale.json"})
sources.add(BASE / f"new-run-session-{SESSION}.json")
sources.add(ROOT / case["setup"]["bindingResult"])
sources.add(ROOT / case["afterReadyState"]["path"])
sources.add(ROOT / "scratch" / "cinderwing-scale-collect1.json")
sources.add(ROOT / "scratch" / "cinderwing-scale-after-collect1.json")
sources.add(ROOT / case["readyResult"]["path"])
for helper in case["helpers"]: sources.add(ROOT / helper["path"])
for action in case["actions"]:
    sources.update({ROOT / action["journal"], ROOT / action["rawResult"], ROOT / action["summaryPath"]})
    sources.update((ROOT / action["rawResult"]).with_suffix("").glob("*.png"))
for frame in review["frames"]: sources.add(ROOT / frame["path"])
for path in ASSET.rglob("*"):
    if path.is_file() and not any(part.startswith("live-validation-") for part in path.parts) and "__pycache__" not in path.parts and path.suffix != ".pyc": sources.add(path)

journals = {p for p in sources if p.suffix == ".jsonl"}; seen = set()
while journals:
    journal = journals.pop()
    if journal in seen or not journal.is_file(): continue
    seen.add(journal)
    for line in journal.read_text().splitlines():
        value = json.loads(line).get("data", {}).get("path")
        if not value: continue
        path = Path(value)
        if not path.is_file(): continue
        try: path.relative_to(ROOT)
        except ValueError: continue
        if path.suffix.lower() in FORBIDDEN: continue
        sources.add(path)
        if path.suffix == ".jsonl": journals.add(path)
for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.suffix.lower() not in FORBIDDEN, source

pins, mappings = {}, []
for source in sorted(sources):
    raw, relative = source.read_bytes(), source.relative_to(ROOT)
    if relative.suffix.lower() == ".png": pins[str(relative)] = hashlib.sha256(raw).hexdigest(); continue
    archive = OUT / "metadata" / (str(relative) + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True); archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    mappings.append({"source": str(relative), "sourceSha256": hashlib.sha256(raw).hexdigest(), "archive": str(archive.relative_to(OUT)), "archiveSha256": sha(archive), "encoding": "gzip-lossless"})
write(OUT / "source-image-pins.json", pins); write(OUT / "asset-pins.json", asset_hashes)

selected = []
for frame in review["frames"]:
    source, destination = ROOT / frame["path"], OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    assert source.is_file() and sha(source) == frame["sha256"]
    destination.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, destination)
    selected.append({"source": frame["path"], "archive": str(destination.relative_to(OUT)), "sha256": sha(destination), "action": frame["action"], "index": frame["index"], "observation": frame["observation"]})

videos, captures = [], []
for action in case["actions"]:
    raw_path, count = ROOT / action["rawResult"], action["frames"]
    raw, frame_dir = read(raw_path), (ROOT / action["rawResult"]).with_suffix("")
    assert len(raw.get("frames", [])) == count and len(list(frame_dir.glob("*.png"))) == count
    video = OUT / f"{action['label']}.mp4"
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12", "-i", str(frame_dir / "%04d.png"), "-frames:v", str(count), "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video)], check=True)
    info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)]))["streams"][0]
    assert int(info["nb_read_frames"]) == count
    videos.append({"label": action["label"], "captureId": raw_path.stem, "path": video.name, "sha256": sha(video), "frames": count, "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12, "timing": "Presentation derivative, not unperturbed timing"})
    captures.append({"label": action["label"], "captureId": raw_path.stem, "case": action["case"], "frames": count, "rawSha256": sha(raw_path), "summarySha256": sha(ROOT / action["summaryPath"]), "width": raw["width"], "height": raw["height"], "requestedFps": raw["requestedFps"], "complete": action["complete"], "termination": action["termination"], "captureReportedOk": action["captureReportedOk"], "captureError": action["captureError"], "actualGameSeconds": action["actualGameSeconds"]})

ready_path = ROOT / case["readyResult"]["path"]
validation = {
    "status": "fresh_native_scale_single_renderer_complete_hit_and_kill_ready_observed",
    "session": SESSION, "enemy": case["enemy"], "displayName": case["displayName"], "nativeChassis": case["nativeChassis"],
    "frameworkSha256": case["frameworkSha256"], "profileInputSha256": case["profileInputSha256"], "assetHashes": asset_hashes,
    "binding": case["binding"], "captures": captures, "ordinaryAttack": case["ordinaryAttack"], "ordinaryLethal": False,
    "explicitKillFixture": case["explicitKillFixture"], "nativeCollects": case["nativeCollects"], "finalReady": case["finalReady"],
    "readyResult": {"path": str(ready_path.relative_to(ROOT)), "sha256": sha(ready_path), "result": read(ready_path)},
    "afterReadyState": case["afterReadyState"], "materialReview": case["materialReview"],
    "historicalEvidence": case["historicalEvidence"], "scale": {"requestedFactor": 1.0, "nativePrefabRootLocalScale": [0.7799999713897705] * 3, "observedRendererWorldAxisLengths": [0.7800000033748864, 0.7799999713897705, 0.7800000033748864]},
    "progression": "The native-scale process bound the exact enBat01 mesh, completed pass/nonlethal-hit/KillSingle captures, preserved HP 58 to 50 ordinary damage, accepted one native Collect into strict Ready at level 0 room 2 and recorded the guarded Ready continuation boundary.",
    "selectedPNGs": selected, "videos": videos, "sourceImageCount": len(pins), "sourceImagePins": "source-image-pins.json", "assetPins": "asset-pins.json", "losslessMappings": mappings,
    "standaloneCase": str(CASE.relative_to(ROOT)), "rootVisualReview": str(REVIEW.relative_to(ROOT)), "limits": case["limits"]
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(f"""# Cinderwing native-scale live trial V2

This supplement records the completed native-scale catalog process (`{SESSION}`) for the exact `batA` / `enBat01` binding. Public visual factor `1.0` preserves the native bat root scale `0.78`; one authored renderer remains attached to one enemy owner.

Pass, ordinary nonlethal hit and explicit `KillSingle` captures each contain 120 unpaused frames. The ordinary native attack resolves HP `58→50` with `cheat=None`; the fixture kill commits `50→0`. The earlier old-framework lethal capture that stopped at 92 frames remains preserved in `live-validation.json` and is not overwritten by this completed native-scale run.

One guarded native Collect reaches strict Ready at level `0` / room `2`, and the guarded Ready continuation records the next native slot at room `3`. Selected stills show the small bat readable with coherent wing folds and an intact settled death pose at the captured scale. Small silhouette, hero/effect occlusion, earliest clip intervals, full culling and other controller variants remain open; the explicit kill is not ordinary lethal acceptance.

`validation.json` preserves the standalone case, continuation helpers, journals, complete raw captures, historical validation, authoring files and source hashes as gzip-lossless metadata. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
""")
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(mappings), "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}, indent=2))
