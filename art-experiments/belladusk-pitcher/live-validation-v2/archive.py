#!/usr/bin/env python3
"""Archive the fresh Belladusk corroborating run without game payloads."""
from pathlib import Path
import gzip
import hashlib
import json
import shutil
import subprocess

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "belladusk-pitcher"
OUT = ASSET / "live-validation-v2"
if OUT.exists():
    assert not (OUT / "validation.json").exists(), OUT
else:
    OUT.mkdir()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


SESSION = "8e7350b348ba499993a11279be206fb2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = BASE / "case-80c6539234ca4a8b9bbfe64831b0d5a6" / "case-result.json"
REVIEW = ROOT / "scratch" / "belladusk-root-visual-review.json"
case = read(CASE)
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_belladusk"
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3

sources = {
    CASE,
    REVIEW,
    ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json",
    BASE / "new-run-session-8e7350b348ba499993a11279be206fb2.json",
    ASSET / "manifest.json",
    ASSET / "runtime-profile.json",
    ASSET / "bind-candidate-manifest.json",
}
capture_ids = []
for action in case["actions"]:
    raw = Path(action["capture"]["rawCapture"]["path"])
    sources.add(raw)
    sources.add(Path(action["journal"]["path"]))
    sources.add(Path(action["rawResult"]["path"]))
    capture_ids.append(raw.stem)
    png_dir = raw.with_suffix("")
    pngs = sorted(png_dir.glob("*.png"))
    assert [p.name for p in pngs] == [f"{i:04d}.png" for i in range(120)]
    sources.update(pngs)
assert capture_ids == [
    "3371de9b584e4709a17733ab3608980e",
    "3881e9e1ae1244318d9bfde95bf84004",
    "2ecb49d3e5734818be633d31dae0af24",
]

claim = case.get("claim", {}).get("path")
if claim:
    sources.add(Path(claim))
for source in sources:
    assert source.is_file(), source

pins = {}
mappings = []
for source in sorted(sources):
    raw = source.read_bytes()
    rel = source.relative_to(ROOT)
    if rel.suffix == ".png":
        pins[str(rel)] = hashlib.sha256(raw).hexdigest()
        continue
    archive = OUT / "metadata" / (str(rel) + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw
    mappings.append({
        "source": str(rel),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(archive.relative_to(OUT)),
        "archiveSha256": sha(archive),
        "encoding": "gzip-lossless",
    })
write(OUT / "source-image-pins.json", pins)

selected = []
for frame in read(REVIEW)["frames"]:
    source = ROOT / frame["path"]
    assert sha(source) == frame["sha256"]
    destination = OUT / "selected" / source.parent.name / source.name
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.resolve() != destination.resolve():
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
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
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

(OUT / "README.md").write_text("""# Belladusk Pitcher fresh corroborating live trial

This supplement records a fresh catalog-411 run in session `8e7350b348ba499993a11279be206fb2` using the exact `plantE` chassis and `enJungleNibbler_A` renderer (121530). The authored Belladusk mesh and texture bound to one native enemy owner at visual scale `1.0`; the profile disables native emission.

The complete 120-frame pass and attack captures keep the curved stalk, four root leaves, plum hood, scalloped lip and warm throat inside the combat frame. An ordinary attack records HP 58 to 50 with `cheat=None` and no focus. The explicit `KillSingle` capture completes 120 frames, shows the native death motion and reaches the native loot surface. One guarded Collect action reaches strict Ready at level 0, room 2.

Root reviewed seven original PNGs across idle, attack, death and loot. Native effects and hero occlusion limit fine surface inspection. The death fixture is not ordinary lethal damage; fallen views do not establish floor collision or physics sleeping. This corroborating run supplements the historical V1 archive and does not replace it. It is selected-frame binding, appearance, motion and gameplay evidence, not finished-art, every-animation, full-culling, material-property or final-resource-lifetime acceptance.

Raw capture metadata, child results, journals, the case result, profile/manifest references, all 360 source PNG hashes, seven selected originals and three 120-frame presentation videos are retained. Native payloads, DLLs and decompiled C# are excluded. `archive.py` is offline-only and refuses a completed destination.
""")

validation = {
    "status": "fresh_catalog_411_corroborating_binding_appearance_motion_and_gameplay_observed",
    "session": SESSION,
    "enemy": case["enemy"],
    "nativeChassis": "plantE",
    "rendererPath": "enJungleNibbler_A",
    "rendererId": 121530,
    "ownerInstanceId": 369188,
    "boneSignature": "d5f27b166e2e9db8c2f9fc8aa2d17742ec00ec6fbec1c951901c1118896d1904",
    "visualScale": 1.0,
    "captures": [
        {"label": label, "captureId": capture_id, "frames": 120, "rawSha256": sha(BASE / f"{capture_id}.json")}
        for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids)
    ],
    "ordinaryHit": {"beforeHp": 58, "afterHp": 50, "cheat": "None", "focus": False},
    "ordinaryLethal": False,
    "explicitKillFixture": True,
    "nativeCollects": 1,
    "finalReady": {"level": 0, "room": 2},
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "losslessMappings": mappings,
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "historicalArchive": "art-experiments/belladusk-pitcher/live-validation-v1.json",
    "limits": [
        "Selected frames do not establish every animation interval, full culling envelope or mouth-intersection clearance.",
        "KillSingle is an explicit fixture and does not prove ordinary lethal damage.",
        "Floor contact and physics sleeping are not established by the fallen views.",
        "Native emission is disabled by the profile, but no independent live material-property measurement was taken.",
        "Final portrait-clone/resource lifetime and finished-art acceptance remain separate checks.",
    ],
}
validation["artifactSha256"] = {
    str(path.relative_to(OUT)): sha(path)
    for path in sorted(OUT.rglob("*"))
    if path.is_file()
}
write(OUT / "validation.json", validation)
print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "sourceImages": len(pins),
    "losslessMappings": len(mappings),
    "selected": len(selected),
    "videos": len(videos),
    "validationSha256": sha(OUT / "validation.json"),
}))
