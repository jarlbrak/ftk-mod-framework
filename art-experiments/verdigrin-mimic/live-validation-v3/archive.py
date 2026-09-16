#!/usr/bin/env python3
"""Archive the observed Verdigrin mimicA V3 live trial without game payloads."""
from pathlib import Path
import gzip, hashlib, json, os, shutil, subprocess

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "verdigrin-mimic"
OUT = ASSET / "live-validation-v3"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "verdigrin-v3-standalone-case.json"
REVIEW = ROOT / "scratch" / "verdigrin-v3-root-visual-review.json"
CONTINUATION = ROOT / "scratch" / "verdigrin-v3-native-loot-ready.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
RUNTIME_PROFILE = ASSET / "runtime-profile.json"
SESSION = "abb0e9c1bb414743b45603efa7c7dd05"


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
continuation = read(CONTINUATION)
assert case["status"] == "needs_visual_review"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_verdigrin"
assert len(case["actions"]) == 3 and all(a["capture"]["complete"] and a["capture"]["frames"] == 120 for a in case["actions"])
assert len(case["helpers"]) == 7 and case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert len(case["collects"]) == 2 and case["afterReady"]["dungeon"]["room"] == 2
assert review["session"] == SESSION and len(review["frames"]) == 6
assert continuation["session"] == SESSION and len(continuation["files"]) >= 9

assets = manifest.get("assets", {})
asset_hashes = {name: value["sha256"] for name, value in assets.items() if isinstance(value, dict) and "sha256" in value}
asset_paths = [ASSET / name for name in assets]
sources = {CASE, REVIEW, CONTINUATION, PROFILE, MANIFEST, RUNTIME_PROFILE}
sources.update(asset_paths)
sources.add(BASE / f"new-run-session-{SESSION}.json")
sources.add(ROOT / case["setup"]["newRunJournal"])
sources.add(ROOT / case["setup"]["sessionRecord"])
sources.add(ROOT / case["setup"]["bindingInventory"])
sources.add(ROOT / case["afterReadyState"]["path"])
for helper in case["helpers"]:
    sources.add(ROOT / helper["path"])
for action in case["actions"]:
    sources.update({ROOT / action["journal"], ROOT / action["rawResult"], ROOT / action["capture"]["path"], ROOT / action["capture"]["summaryPath"]})
    raw = ROOT / action["capture"]["path"]
    sources.update(raw.with_suffix("").glob("*.png"))
for frame in review["frames"]:
    sources.add(ROOT / frame["path"])
for path in ASSET.rglob("*"):
    if path.is_file() and OUT not in path.parents and not any(part.startswith("live-validation-") for part in path.parts) and "__pycache__" not in path.parts and path.suffix != ".pyc":
        sources.add(path)

# Follow helper-result paths in journals, but never import native payloads.
journals = {path for path in sources if path.suffix == ".jsonl"}
seen = set()
while journals:
    journal = journals.pop()
    if journal in seen or not journal.is_file():
        continue
    seen.add(journal)
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
    mappings.append({"source": str(relative), "sourceSha256": hashlib.sha256(raw).hexdigest(), "archive": str(archive.relative_to(OUT)), "archiveSha256": sha(archive), "encoding": "gzip-lossless"})
write(OUT / "source-image-pins.json", pins)
write(OUT / "asset-pins.json", asset_hashes)

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({"source": frame["path"], "archive": str(destination.relative_to(OUT)), "sha256": sha(destination), "action": frame["action"], "index": frame["index"], "observation": frame["observation"]})

videos, captures = [], []
for action in case["actions"]:
    label = action["action"]
    raw_path = ROOT / action["capture"]["path"]
    raw = read(raw_path)
    frame_dir = raw_path.with_suffix("")
    assert raw.get("ok") is True and len(raw.get("frames", [])) == 120 and len(list(frame_dir.glob("*.png"))) == 120
    video = OUT / f"{label}.mp4"
    subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12", "-i", str(frame_dir / "%04d.png"), "-frames:v", "120", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video)], check=True)
    info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width,height,r_frame_rate", "-of", "json", str(video)]))["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append({"label": label, "captureId": raw_path.stem, "path": video.name, "sha256": sha(video), "frames": 120, "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12, "timing": "Presentation derivative, not unperturbed timing"})
    captures.append({"label": label, "session": SESSION, "captureId": raw_path.stem, "frames": 120, "rawSha256": sha(raw_path), "summarySha256": action["capture"]["summarySha256"], "width": raw["width"], "height": raw["height"], "requestedFps": raw["requestedFps"], "complete": True, "termination": None})

validation = {
    "status": "fresh_catalog_411_single_process_mimicA_binding_complete_attack_kill_loot_ready_observed",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": case["displayName"],
    "nativeChassis": case["binding"]["nativeChassis"],
    "rendererPath": case["binding"]["rendererPath"],
    "rendererId": case["binding"]["rendererId"],
    "ownerInstanceId": case["binding"]["ownerInstanceId"],
    "boneSignature": case["binding"]["boneSignature"],
    "visualScale": case["binding"]["visualScale"],
    "catalogSha256": case["catalogSha256"],
    "profileSha256": case["profileSha256"],
    "assetHashes": asset_hashes,
    "binding": case["binding"],
    "captures": captures,
    "ordinaryAttack": case["ordinaryAttack"],
    "ordinaryLethal": False,
    "explicitKillFixture": case["explicitKillFixture"],
    "nativeCollects": 2,
    "finalReady": {"ok": True, "level": 0, "room": 2, "buttonCount": 1},
    "readyResult": case["readyResult"],
    "nextEncounter": case["nextEncounter"],
    "materialReview": case["materialReview"],
    "progression": "The fresh mimicA process bound the exact authored mesh, completed pass/ordinary attack/KillSingle captures, accepted two guarded native Collect votes, observed strict Ready at level 0 room 2, and advanced one guarded Ready vote into the next native enemy encounter.",
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "standaloneCase": str(CASE.relative_to(ROOT)),
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "nativeLootReadyContinuation": str(CONTINUATION.relative_to(ROOT)),
    "limits": case["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(f"""# Verdigrin Coffer fresh live trial V3\n\nThis supplement records one fresh catalog-411 process (`{SESSION}`) using the exact `mimicA` chassis, `mimic01` renderer 121192, owner 369188, visual scale 1.0 and the authored `verdigrin.glb`. The custom mesh stayed on the same native owner and bone signature through the complete pass, ordinary attack and explicit death fixture captures.\n\nThe ordinary attack resolved the same target from HP 58 to 52 with `cheat=None` and no focus. The explicit `KillSingle` fixture resolved HP 52 to 0 and retained 120 unpaused frames. The first native Collect response was accepted but left the same current button and no reward delta; a second guarded Collect response advanced to strict Ready at level 0 / room 2. One guarded Ready click then entered the next native Enemy room (room 2), where the normal game presented Jelly Cube plus the registered cultist probe.\n\nSelected originals show the hinged lid, teeth, tongue and lower panels in readable native combat staging, plus the Victory/item-choice boundary. Hero, targeting overlays and native effects limit fine hinge/tongue inspection. The record does not claim ordinary lethal damage, floor collision/sleeping, complete culling, portrait/resource lifetime, final disposal or finished-art acceptance.\n\n`validation.json` preserves the standalone case, helper responses, journals, complete raw captures and PNG hashes, authoring/runtime manifests and prior diagnostics as gzip-lossless metadata. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.\n""")
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(mappings), "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}, indent=2))
