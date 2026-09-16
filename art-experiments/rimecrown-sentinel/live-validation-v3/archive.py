#!/usr/bin/env python3
"""Archive the fresh Rimecrown scale-correction trial without game payloads."""
from pathlib import Path
import gzip
import hashlib
import json
import shutil
import subprocess

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "rimecrown-sentinel"
OUT = ASSET / "live-validation-v3"
if OUT.exists():
    assert not (OUT / "validation.json").exists(), OUT
else:
    OUT.mkdir()

sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
read = lambda p: json.loads(p.read_text())
write = lambda p, value: (p.parent.mkdir(parents=True, exist_ok=True), p.write_text(json.dumps(value, indent=2) + "\n"))[1]
SESSION = "35ba69223a7c4ee1a20c781289b181e4"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE = BASE / "case-0eb76cc564ee4f7db9676b740989ab92" / "case-result.json"
REVIEW = ROOT / "scratch" / "rimecrown-root-visual-review.json"
case = read(CASE)
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_rimecrown"
assert case["status"] == "needs_visual_review" and len(case["actions"]) == 3

sources = {
    CASE,
    REVIEW,
    ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json",
    ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "new-run-session-35ba69223a7c4ee1a20c781289b181e4.json",
    ASSET / "manifest.json",
    ASSET / "runtime-profile.json",
    ASSET / "native-material-metadata.json",
}
capture_ids = []
for action in case["actions"]:
    capture = action["capture"]
    raw = Path(capture["rawCapture"]["path"])
    sources.add(raw)
    sources.add(Path(action["journal"]["path"]))
    sources.add(Path(action["rawResult"]["path"]))
    capture_ids.append(raw.stem)
    png_dir = raw.with_suffix("")
    pngs = sorted(png_dir.glob("*.png"))
    assert [p.name for p in pngs] == [f"{i:04d}.png" for i in range(120)]
    for png in pngs:
        sources.add(png)
assert capture_ids == ["d39f564238244b5987e33a5ce1679554", "732e6ed9d3244dcf89813bca13158116", "492db27b423045d4a61a0319642679bc"]

claim = case.get("claim", {}).get("path")
if claim:
    sources.add(Path(claim))

for source in sources:
    assert source.is_file(), source

pins = {}
maps = []
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
    maps.append({"source": str(rel), "sourceSha256": hashlib.sha256(raw).hexdigest(), "archive": str(archive.relative_to(OUT)), "archiveSha256": sha(archive), "encoding": "gzip-lossless"})

write(OUT / "source-image-pins.json", pins)
selected = []
for frame in read(REVIEW)["frames"]:
    source = ROOT / frame["path"]
    assert sha(source) == frame["sha256"]
    destination = OUT / "selected" / source.parent.name / source.name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({"source": frame["path"], "archive": str(destination.relative_to(OUT)), "sha256": sha(destination), "action": frame["action"], "index": frame["index"], "observation": frame["observation"]})

videos = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    video = OUT / f"{label}.mp4"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-framerate", "12", "-i", str(BASE / capture_id / "%04d.png"), "-frames:v", "120", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video)], check=True)
    info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video)]))["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append({"label": label, "captureId": capture_id, "path": video.name, "sha256": sha(video), "frames": 120, "width": int(info["width"]), "height": int(info["height"]), "playbackFps": 12, "timing": "Presentation derivative, not unperturbed timing"})

if Path(__file__).resolve() != (OUT / "archive.py").resolve():
    shutil.copy2(Path(__file__), OUT / "archive.py")
(OUT / "README.md").write_text("""# Rimecrown Sentinel fresh scale-correction trial

This supplement records a fresh catalog-411 run in session `35ba69223a7c4ee1a20c781289b181e4` using the exact five-part `snowmanB` profile at visual scale `0.75`. All five authored renderers bound to one native enemy owner: hat, head, base, scarf and middle body.

The complete 120-frame pass and attack captures keep the crown, head, scarf, hands and base inside the combat camera. An ordinary attack records HP 58 to 50 with `cheat=None` and no focus. The explicit `KillSingle` capture completes 120 frames, shows the native smoke and upper-part falloff while the Root-weighted base remains, and reaches the native loot surface. Two guarded Collect actions reach strict Ready at level 0, room 2.

Root reviewed seven original PNGs across idle, attack, death and loot. Native effects and hero occlusion limit fine surface inspection. The death fixture is not ordinary lethal damage; base separation is not collision or physics-sleeping acceptance. This is selected-frame scale, binding, motion and gameplay evidence, not finished-art, every-animation, full-culling, material-fidelity or final-resource-lifetime acceptance. Earlier V1 camera-fit failure and V2 selected-frame correction remain unchanged.

Raw capture metadata, child results, journals, the case result, profile/manifest references, all 360 source PNG hashes, seven selected originals and three 120-frame presentation videos are retained. Native payloads, DLLs and decompiled C# are excluded. `archive.py` is offline-only and refuses a completed destination.
""")

validation = {
    "status": "fresh_catalog_411_scale_correction_binding_motion_and_gameplay_observed",
    "session": SESSION,
    "enemy": case["enemy"],
    "nativeChassis": "snowmanB",
    "rendererPaths": [x["rendererPath"] for x in case["initialRenderer"].get("renderers", [])] if isinstance(case.get("initialRenderer"), dict) and "renderers" in case["initialRenderer"] else ["SnowMan_Geo/enSnowmanHat", "SnowMan_Geo/enSnowmanHead", "SnowMan_Geo/enSnowmanBase", "SnowMan_Geo/enSnowmanScarf", "SnowMan_Geo/enSnowmanmiddleBody"],
    "visualScale": 0.75,
    "captures": [{"label": label, "captureId": cid, "frames": 120, "rawSha256": sha(BASE / f"{cid}.json")} for label, cid in zip(("pass", "attack", "kill-fixture"), capture_ids)],
    "ordinaryHit": {"beforeHp": 58, "afterHp": 50, "cheat": "None", "focus": False},
    "ordinaryLethal": False,
    "explicitKillFixture": True,
    "nativeCollects": 2,
    "finalReady": {"level": 0, "room": 2},
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "losslessMappings": maps,
    "rootVisualReview": "scratch/rimecrown-root-visual-review.json",
    "limits": ["Selected frames do not establish every animation interval or full culling envelope.", "KillSingle is an explicit fixture, not ordinary lethal damage.", "Native Root_M base weighting and upper-part falloff do not establish collision or physics sleeping.", "Material/tint fidelity and final native clone/resource disposal remain separate checks.", "Prototype and selected-frame evidence do not establish finished art for every snowman variant."],
}
validation["artifactSha256"] = {str(path.relative_to(OUT)): sha(path) for path in sorted(OUT.rglob("*")) if path.is_file()}
write(OUT / "validation.json", validation)
print(json.dumps({"validation": str(OUT / "validation.json"), "sourceImages": len(pins), "losslessMappings": len(maps), "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}))
