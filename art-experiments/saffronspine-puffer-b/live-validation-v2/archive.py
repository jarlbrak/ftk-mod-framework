#!/usr/bin/env python3
"""Archive the observed Saffronspine B live trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "saffronspine-puffer-b"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "saffronspine-b-standalone-case.json"
REVIEW = ROOT / "scratch" / "saffronspine-b-root-visual-review.json"
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
assert case["enemy"] == "ftkmf_modeltest_saffronspine_b"
assert len(case["actions"]) == 3
assert len(case["sessions"]) == 1
assert review["binding"]["nativeChassis"] == "pufferB"
assert review["binding"]["ownerInstanceId"] == 369188
assert review["binding"]["renderers"] == [{
    "rendererPath": "enBlowFishA",
    "rendererId": 121388,
    "capturedInstanceId": -245716,
    "glbFile": "saffronspine_b.glb",
    "boneSignature": "bf34c7d59eab649db018a302e77b35e222da714ef3ae4fe4f06f84e0f074fd2a",
}]
assert review["finalReady"]["ok"] is True and review["finalReady"]["level"] == 0 and review["finalReady"]["room"] == 2

manifest_assets = manifest.get("assets", [])
if isinstance(manifest_assets, dict):
    asset_hashes = {name: value["sha256"] for name, value in manifest_assets.items()}
    asset_paths = [ASSET / name for name in manifest_assets]
else:
    asset_hashes = {value.get("file", value.get("path")): value["sha256"] for value in manifest_assets}
    asset_paths = [ROOT / value.get("file", value.get("path")) for value in manifest_assets]

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
for value in (read(continuation).get("files") or {}).values():
    path = Path(value["path"])
    if path.is_file():
        sources.add(path)

sources.update(path for path in asset_paths if path.is_file())
for path in ASSET.rglob("*"):
    if not path.is_file() or OUT in path.parents or "__pycache__" in path.parts or path.suffix == ".pyc":
        continue
    sources.add(path)

# Follow helper-result paths from journals, but never import native payloads.
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

videos, captures = [], []
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
    videos.append({"label":label,"captureId":raw_path.stem,"path":video.name,"sha256":sha(video),"frames":count,"width":int(info["width"]),"height":int(info["height"]),"playbackFps":12,"timing":"Presentation derivative, not unperturbed timing"})
    captures.append({"label":label,"session":action["session"],"captureId":raw_path.stem,"frames":count,"rawSha256":sha(raw_path),"summarySha256":action["capture"]["summarySha256"],"width":raw["width"],"height":raw["height"],"requestedFps":raw["requestedFps"],"complete":True,"termination":None})

validation = {
    "status":"fresh_catalog_411_single_process_pufferB_binding_complete_attack_kill_loot_ready_observed",
    "sessions":case["sessions"],"processBoundary":case["processBoundary"],"enemy":case["enemy"],"displayName":case["displayName"],
    "nativeChassis":case["binding"]["nativeChassis"],"ownerInstanceId":case["binding"]["ownerInstanceId"],"visualScale":case["binding"]["visualScale"],"capturedNativeCelScale":case["binding"]["capturedNativeCelScale"],"renderers":case["binding"]["renderers"],
    "catalogSha256":case["profileSha256"],"profileSha256":case["profileSha256"],"assetHashes":asset_hashes,"captures":captures,"ordinaryAttack":case["ordinaryAttack"],"ordinaryLethal":False,"explicitKillFixture":True,"nativeCollects":case["collects"],"finalReady":case["finalReady"],"materialReview":case["materialReview"],
    "progression":"The native pass remains a pufferB pass/attack boundary and is not classified as death. A separate ordinary attack resolves HP 58→50 without a cheat. The same fresh process then supplies the explicit BlowFish_DeathDirect kill capture, two guarded native Collect clicks and strict Ready at level 0 room 2.",
    "selectedPNGs":selected,"videos":videos,"sourceImageCount":len(pins),"sourceImagePins":"source-image-pins.json","assetPins":"asset-pins.json","losslessMappings":mappings,"standaloneCase":str(CASE.relative_to(ROOT)),"rootVisualReview":str(REVIEW.relative_to(ROOT)),"limits":case["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text("""# Saffronspine Puffer fresh live trial V2 (pufferB)

This supplement records one fresh catalog-411 process for the exact pufferB chassis, renderer enBlowFishA, controller 5999, native CEL scale 1.0, and the authored saffronspine_b.glb with its observed 30-bone signature. The custom mesh bound under one live enemy owner (369188) and the authored basecolor was read back on the native Standard material with native emission disabled.

The process preserves the native puffer pass/attack behavior. The ordinary attack capture resolves the target from HP 58 to 50 without cheat or focus and is an appearance/motion record, not a lethal or death-animation verdict. The explicit KillSingle fixture then records BlowFish_DeathDirect across a complete 120-frame capture. Native Victory loot accepts two guarded Collect votes, with the first changing gold 11→41, and strict Ready is observed at level 0 / room 2 after one guarded Ready vote.

The seven selected originals show the intact round body, fins, lips, eyes and nubs in native combat staging, ordinary attack motion and the explicit death/victory boundary. Native targeting and effects, foreground hero occlusion, victory depth blur and the small combat view limit fine surface inspection, settled-body/culling-envelope claims, portrait/resource lifetime, indirect death and finished-art acceptance.

validation.json preserves the setup/action journal, all complete raw captures and PNG hashes, native Loot/Ready responses, authoring assets and prior diagnostics as gzip-lossless metadata. Native payloads and DLLs are excluded. archive.py is offline-only and refuses a completed destination.
""")
print(json.dumps({"validation":str(OUT/"validation.json"),"sourceImages":len(pins),"losslessMappings":len(mappings),"selected":len(selected),"videos":len(videos),"validationSha256":sha(OUT/"validation.json")},indent=2))
