#!/usr/bin/env python3
"""Archive a fresh Verdigrin combat trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "verdigrin-mimic"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "956d5a3eecae4fc0b9aee0bf11d2293f"
CASE = BASE / "case-59e645b391ef4c60a20adaca81ecc09f" / "case-result.json"
JOURNAL = BASE / "case-59e645b391ef4c60a20adaca81ecc09f" / "journal.jsonl"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
REVIEW = ROOT / "scratch" / "verdigrin-root-visual-review.json"
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
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_verdigrin"
assert case["status"] == "stopped" and "strict Ready" in case["error"]
assert len(case["actions"]) == 3 and len(case["collects"]) == 1
assert review["session"] == SESSION and len(review["frames"]) == 6

sources = {
    CASE,
    JOURNAL,
    BASE / f"new-run-session-{SESSION}.json",
    PROFILE,
    REVIEW,
    MANIFEST,
    RUNTIME_PROFILE,
}
for name in ("native-material-metadata.json", "verdigrin.source.json", "verdigrin.validation.json"):
    path = ASSET / name
    if path.is_file():
        sources.add(path)

capture_ids = []
for action in case["actions"]:
    raw = Path(action["capture"]["rawCapture"]["path"])
    journal = Path(action["journal"]["path"])
    result = Path(action["rawResult"]["path"])
    assert raw.is_file() and journal.is_file() and result.is_file()
    assert len(list(raw.with_suffix("").glob("*.png"))) == 120
    sources.update((raw, journal, result))
    sources.update(raw.with_suffix("").glob("*.png"))
    capture_ids.append(raw.stem)

for line in JOURNAL.read_text().splitlines():
    entry = json.loads(line)
    path = entry.get("data", {}).get("path")
    if path and Path(path).is_file() and Path(path).is_relative_to(ROOT):
        sources.add(Path(path))
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
    mappings.append(
        {
            "source": str(relative),
            "sourceSha256": hashlib.sha256(raw).hexdigest(),
            "archive": str(archive.relative_to(OUT)),
            "archiveSha256": sha(archive),
            "encoding": "gzip-lossless",
        }
    )
write(OUT / "source-image-pins.json", pins)
write(
    OUT / "asset-pins.json",
    {
        name: manifest.get("assets", {}).get(name, {}).get("sha256")
        for name in ("verdigrin.glb", "verdigrin_basecolor.png")
        if name in manifest.get("assets", {})
    },
)

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append(
        {
            "source": frame["path"],
            "archive": str(destination.relative_to(OUT)),
            "sha256": sha(destination),
            "action": frame["action"],
            "index": frame["index"],
            "observation": frame["observation"],
        }
    )

videos = []
for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids):
    video = OUT / f"{label}.mp4"
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-framerate",
            "12",
            "-i",
            str(BASE / capture_id / "%04d.png"),
            "-frames:v",
            "120",
            "-c:v",
            "libx264",
            "-crf",
            "18",
            "-pix_fmt",
            "yuv420p",
            str(video),
        ],
        check=True,
    )
    info = json.loads(
        subprocess.check_output(
            [
                "ffprobe",
                "-v",
                "error",
                "-count_frames",
                "-select_streams",
                "v:0",
                "-show_entries",
                "stream=nb_read_frames,width,height,r_frame_rate",
                "-of",
                "json",
                str(video),
            ]
        )
    )["streams"][0]
    assert int(info["nb_read_frames"]) == 120
    videos.append(
        {
            "label": label,
            "captureId": capture_id,
            "path": video.name,
            "sha256": sha(video),
            "frames": 120,
            "width": int(info["width"]),
            "height": int(info["height"]),
            "playbackFps": 12,
            "timing": "Presentation derivative, not unperturbed timing",
        }
    )

validation = {
    "status": "fresh_catalog_411_binding_appearance_motion_and_partial_progression_observed",
    "session": SESSION,
    "enemy": "ftkmf_modeltest_verdigrin",
    "displayName": "Verdigrin Coffer",
    "nativeChassis": "mimicA",
    "rendererPath": "mimic01",
    "rendererId": 121192,
    "ownerInstanceId": 369188,
    "boneSignature": "7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba",
    "visualScale": 1.0,
    "catalogSha256": case["profileSha256"],
    "profileSha256": case["profileSha256"],
    "captures": [
        {
            "label": label,
            "captureId": capture_id,
            "frames": 120,
            "rawSha256": sha(BASE / f"{capture_id}.json"),
        }
        for label, capture_id in zip(("pass", "attack", "kill-fixture"), capture_ids)
    ],
    "ordinaryHit": {"beforeHp": 58, "afterHp": 54, "cheat": "None", "focus": False},
    "ordinaryLethal": False,
    "explicitKillFixture": True,
    "nativeCollects": 1,
    "finalReady": None,
    "progression": "One native Collect was accepted; strict Ready was not observed before the bounded runner stopped, and no second Collect was submitted.",
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
    """# Verdigrin Coffer fresh live trial V2

This supplement records the fresh catalog-411 run in session `956d5a3eecae4fc0b9aee0bf11d2293f` using `mimicA`, renderer `mimic01` (121192), owner `369188`, and visual scale `1.0`. The authored coffer bound to the exact native renderer and stayed readable across the pass, ordinary attack and explicit fixture captures.

The pass and attack are complete 120-frame recordings. The ordinary attack changed the same target from HP 58 to 54 with `cheat=None` and no focus. The explicit `KillSingle` fixture also completed 120 frames and reached the native item-choice surface. Root reviewed the hinged lid, teeth, tongue, lower panels and camera fit; hero and native overlays limit fine hinge/tongue inspection.

One native Collect was accepted, but strict Ready did not appear before the bounded runner stopped and no second Collect was submitted. This is partial progression evidence, not a full loot/next-room acceptance. The fixture death is not ordinary lethal damage, and the capture does not establish floor collision, sleeping, every animation interval, full culling, portrait lifetime, final resource disposal or finished-art acceptance.

`validation.json` preserves the case result, journals, helper responses, profile/runtime/authoring manifests as gzip-lossless metadata, all 360 source-image hashes, six selected originals and three 120-frame presentation videos. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
"""
)
print(
    json.dumps(
        {
            "validation": str(OUT / "validation.json"),
            "sourceImages": len(pins),
            "losslessMappings": len(mappings),
            "selected": len(selected),
            "videos": len(videos),
            "validationSha256": sha(OUT / "validation.json"),
        }
    )
)
