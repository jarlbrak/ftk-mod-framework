#!/usr/bin/env python3
"""Independently verify Tideglass Fishsmith's immutable V1 evidence archive."""
from __future__ import annotations

import gzip
import hashlib
import json
import subprocess
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
OUT = ROOT / "art-experiments" / "tideglass-fishsmith" / "live-validation-v1"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


validation = read(OUT / "validation.json")
integrity = read(OUT / "integrity.json")
asset_pins = read(OUT / "asset-pins.json")
image_pins = read(OUT / "source-image-pins.json")

assert validation["status"] == "native_character_creation_preview_and_idle_observed_art_revision_pending"
assert validation["provenance"]["gameAssets"] == asset_pins
assert integrity["validation"] == {"path": "validation.json", "sha256": sha(OUT / "validation.json")}
assert integrity["sourceImages"] == {"count": len(image_pins), "pins": "source-image-pins.json"}

metadata_count = 0
for mapping in integrity["metadata"]:
    archived = OUT / mapping["archive"]
    assert archived.is_file() and sha(archived) == mapping["archiveSha256"]
    payload = gzip.decompress(archived.read_bytes())
    assert hashlib.sha256(payload).hexdigest() == mapping["sourceSha256"]
    metadata_count += 1

for source, pin in image_pins.items():
    archived = OUT / pin["archive"]
    assert archived.is_file()
    assert sha(archived) == pin["sha256"]
    assert archived.stat().st_size == pin["bytes"]
    assert source.startswith(("art-experiments/", "scratch/"))

for selected in validation["selectedFrames"]:
    archive = OUT / selected["archive"]
    source_copy = OUT / image_pins[selected["source"]]["archive"]
    assert archive.is_file() and source_copy.is_file()
    assert sha(archive) == sha(source_copy) == selected["sha256"]

for video in validation["videos"]:
    path = OUT / video["path"]
    assert path.is_file() and sha(path) == video["sha256"]
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == video["frames"]
    assert int(probe["width"]) == video["width"]
    assert int(probe["height"]) == video["height"]

binding = validation["binding"]
assert binding["rendererPath"] == "playerFIsh"
assert binding["mesh"] == "ftkmf_glb_tideglass-body.glb"
renderers = {row["celRelativeRendererPath"]: row for row in validation["renderers"]}
assert renderers["playerFIsh"]["active"] is True and renderers["playerFIsh"]["isVisible"] is True
assert renderers["hairBottom"]["active"] is True and renderers["hairBottom"]["isVisible"] is True
assert renderers["hairTop"]["active"] is False and renderers["hairTop"]["isVisible"] is False
assert validation["preliminaryCapture"]["selectedForIdleReview"] is False
assert validation["captures"] == [validation["captures"][0]]
assert validation["captures"][0]["state"] == "standardIdle_handsDown"
assert validation["visualReview"]["status"] == "sampled_native_character_creation_preview_reviewed_art_revision_pending"

print(json.dumps({
    "status": "PASS",
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "metadataRoundTrips": metadata_count,
    "sourceImages": len(image_pins),
    "selected": len(validation["selectedFrames"]),
    "videos": [{"path": item["path"], "frames": item["frames"]} for item in validation["videos"]],
    "artStatus": validation["visualReview"]["status"],
}, indent=2))
