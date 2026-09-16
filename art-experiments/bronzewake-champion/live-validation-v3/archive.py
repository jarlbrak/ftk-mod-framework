#!/usr/bin/env python3
"""Archive the observed Bronzewake bossGladiator V3 live trial without game payloads."""
from pathlib import Path
import gzip, hashlib, json, os, shutil, subprocess

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "bronzewake-champion"
OUT = ASSET / "live-validation-v3"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = ROOT / "scratch" / "bronzewake-v3-standalone-case.json"
REVIEW = ROOT / "scratch" / "bronzewake-v3-root-visual-review.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
RUNTIME_PROFILE = ASSET / "runtime-profile.json"
SESSION = "591ba03f086341369926e56e8f4dc444"

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
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_bronzewake"
assert len(case["actions"]) == 3
assert [a["frames"] for a in case["actions"]] == [120, 120, 91]
assert case["actions"][0]["complete"] and case["actions"][1]["complete"] and not case["actions"][2]["complete"]
assert case["actions"][2]["termination"] == "renderer_destroyed"
assert "Renderer destroyed" in case["actions"][2]["captureError"]
assert case["ordinaryAttack"] == {"beforeHp": 58, "afterHp": 48, "cheat": "None", "focus": False, "partyHpBefore": 991, "partyHpAfter": 990}
assert case["explicitKillFixture"]["beforeHp"] == 48 and case["explicitKillFixture"]["afterHp"] == 0
assert case["explicitKillFixture"]["method"] == "KillSingle" and not case["explicitKillFixture"]["deathCaptureComplete"]
assert case["nativeCollects"] == 0
assert case["finalReady"] == {"ok": True, "buttonCount": 1, "level": 0, "room": 2, "collectAvailable": False}
assert case["nextEncounter"]["room"] == 2 and case["nextEncounter"]["liveEnemies"] == 2
assert case["nextEncounter"]["enemyTypes"] == ["cubeA", "ftkmf_modeltest_probe_cultista"]
assert review["session"] == SESSION and len(review["frames"]) == 6

assets = manifest.get("assets", {})
asset_hashes = {name: value["sha256"] for name, value in assets.items() if isinstance(value, dict) and "sha256" in value}
asset_paths = [ASSET / name for name in assets]
sources = {CASE, REVIEW, PROFILE, MANIFEST, RUNTIME_PROFILE}
sources.update(asset_paths)
sources.add(BASE / f"new-run-session-{SESSION}.json")
sources.add(ROOT / case["setup"]["newRunJournal"])
sources.add(ROOT / case["setup"]["bindingResult"])
sources.add(ROOT / case["afterReadyState"]["path"])
for helper in case["helpers"]:
    sources.add(ROOT / helper["path"])
for action in case["actions"]:
    sources.update({ROOT / action["journal"], ROOT / action["rawResult"], ROOT / action["summaryPath"]})
    raw = ROOT / action["rawResult"]
    sources.update(raw.with_suffix("").glob("*.png"))
for frame in review["frames"]:
    sources.add(ROOT / frame["path"])
# Authoring sources are included recursively, but never an older/newer live archive.
for path in ASSET.rglob("*"):
    if path.is_file() and not any(part.startswith("live-validation-") for part in path.parts) and "__pycache__" not in path.parts and path.suffix != ".pyc":
        sources.add(path)

# Follow paths recorded by the runtime journals, retaining raw provenance without game binaries.
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
        "captureId": raw_path.stem,
        "case": action["case"],
        "frames": count,
        "rawSha256": sha(raw_path),
        "summarySha256": action["summarySha256"],
        "width": raw["width"],
        "height": raw["height"],
        "requestedFps": raw["requestedFps"],
        "complete": action["complete"],
        "termination": action["termination"],
        "captureReportedOk": action["captureReportedOk"],
        "captureError": action["captureError"],
        "actualGameSeconds": action["actualGameSeconds"],
    })

ready_result = read(ROOT / "scratch/mirewarden-game/model-test-output/dd1025d0443d4a95b740756211693164.json")
validation = {
    "status": "fresh_catalog_411_multipart_binding_complete_attack_kill_prefix_ready_observed",
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
    "nativeCollects": 0,
    "finalReady": case["finalReady"],
    "readyResult": {"path": "scratch/mirewarden-game/model-test-output/dd1025d0443d4a95b740756211693164.json", "sha256": sha(ROOT / "scratch/mirewarden-game/model-test-output/dd1025d0443d4a95b740756211693164.json"), "result": ready_result},
    "nextEncounter": case["nextEncounter"],
    "materialReview": case["materialReview"],
    "progression": "The fresh bossGladiator process bound all four authored renderers, completed pass/ordinary attack captures, committed KillSingle from HP 48 to 0 with the native RendererDestroyed death prefix, observed strict Ready at level 0 room 2, and accepted one guarded native Ready vote into the next room.",
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
(OUT / "README.md").write_text(f"""# Bronzewake Champion fresh live trial V3

This supplement records one fresh catalog-411 process (`{SESSION}`) using the exact `bossGladiator` chassis at public visual scale `1.0` (captured native CEL scale `1.1`). Four authored renderers stayed bound to one owner: `enBossGladiator`, `hairBottomBossGladiator`, `armorBossGladiator` and `bootsBossGladiator`; the native helmet and weapon/shield accessories remained game-owned.

Pass and ordinary attack captures contain 120 unpaused frames. The ordinary attack resolved the same target from HP `58` to `48` with `cheat=None` and no focus. The explicit `KillSingle` fixture committed from HP `48` to `0`; native death reaches the expected renderer-destroyed boundary during frame `91` of the requested 120, so the archived prefix is not a complete corpse or settled-ragdoll claim.

The native post-death surface was strict Ready at level `0` / room `2`, with no active Collect vote. One guarded native Ready click advanced the dungeon to room `2`; the subsequent live state presented the normal Jelly Cube plus the registered cultist probe. All four materials reported the authored basecolor and no active emission; the boots route also opted out of inherited native emission.

Selected originals show the readable multipart champion in idle and attack staging and the death-prefix victory boundary. Native UI/effects and the cleanup boundary limit fine plate/hair intersections, complete deformation, culling, portraits, resource lifetime, collision/sleeping and finished-art acceptance.

`validation.json` preserves the standalone case, helper results, native Ready continuation, journals, complete raw captures, the 91-frame death prefix, authoring/runtime manifests and source hashes as gzip-lossless metadata. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
""")
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(mappings), "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}, indent=2))
