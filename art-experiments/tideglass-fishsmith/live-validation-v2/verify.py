#!/usr/bin/env python3
"""Independently verify Tideglass Fishsmith's immutable V2 preview archive."""
from __future__ import annotations

import gzip
import hashlib
import json
import subprocess
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
OUT = ROOT / "art-experiments" / "tideglass-fishsmith" / "live-validation-v2"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


validation = read(OUT / "validation.json")
integrity = read(OUT / "integrity.json")
assets = read(OUT / "asset-pins.json")
images = read(OUT / "source-image-pins.json")

assert validation["status"] == "native_character_creation_preview_and_idle_observed_v2_preview_art_accepted"
assert validation["provenance"]["gameAssets"] == assets
assert integrity["validation"] == {"path": "validation.json", "sha256": sha(OUT / "validation.json")}
assert integrity["sourceImages"] == {"count": len(images), "pins": "source-image-pins.json"}

metadata_count = 0
for mapping in integrity["metadata"]:
    archived = OUT / mapping["archive"]
    assert archived.is_file() and sha(archived) == mapping["archiveSha256"]
    assert hashlib.sha256(gzip.decompress(archived.read_bytes())).hexdigest() == mapping["sourceSha256"]
    metadata_count += 1

for source, pin in images.items():
    archived = OUT / pin["archive"]
    assert source.startswith(("art-experiments/", "scratch/"))
    assert archived.is_file() and sha(archived) == pin["sha256"] and archived.stat().st_size == pin["bytes"]

for selected in validation["selectedFrames"]:
    archived = OUT / selected["archive"]
    source = OUT / images[selected["source"]]["archive"]
    assert archived.is_file() and source.is_file() and sha(archived) == sha(source) == selected["sha256"]

sheet = validation["contactSheet"]
assert (OUT / sheet["archive"]).is_file() and sha(OUT / sheet["archive"]) == sheet["sha256"]
assert sheet["frames"] == [0, 5, 11, 17, 23]

for video in validation["videos"]:
    path = OUT / video["path"]
    assert path.is_file() and sha(path) == video["sha256"]
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == video["frames"]
    assert int(stream["width"]) == video["width"] and int(stream["height"]) == video["height"]

binding = validation["binding"]
assert binding["rendererPath"] == "playerFIsh" and binding["mesh"] == "ftkmf_glb_tideglass-body.glb"
preview_owner = validation["avatarOwners"]["preview"]
assert preview_owner == {
    "status": "observed_actual_native_player_preview",
    "observedAvatars": 1,
    "ownerInstanceId": -18950,
    "celInstanceId": -260066,
    "rendererPath": "playerFIsh",
    "visibleCustomRenderers": ["playerFIsh", "hairBottom"],
    "boundInactiveCustomRenderers": ["hairTop"],
}
renderers = {row["celRelativeRendererPath"]: row for row in validation["renderers"]}
assert renderers["playerFIsh"]["active"] is True and renderers["playerFIsh"]["isVisible"] is True
assert renderers["hairBottom"]["active"] is True and renderers["hairBottom"]["isVisible"] is True
assert renderers["hairTop"]["active"] is False and renderers["hairTop"]["isVisible"] is False
assert validation["visualReview"]["status"] == "sampled_native_character_creation_preview_reviewed_v2_acceptable"

print(json.dumps({"status": "PASS", "validation": str((OUT / "validation.json").relative_to(ROOT)),
                  "metadataRoundTrips": metadata_count, "sourceImages": len(images),
                  "selected": len(validation["selectedFrames"]), "artStatus": validation["visualReview"]["status"]}, indent=2))
