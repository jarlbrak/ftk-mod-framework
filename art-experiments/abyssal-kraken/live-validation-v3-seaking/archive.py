#!/usr/bin/env python3
"""Archive the fresh Royal Sargassum V3 Sea King trials without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "abyssal-kraken"
OUT = ASSET / "live-validation-v3-seaking"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
PROFILE_SHA256 = "73c825a73a06b602041956b7fc383bdceb4092b6a126a67f33d8174da2b8c7e7"
BONE_SIGNATURE = "997fc6131589e9fca08c1920ca2a0237c16be9c2a665c7c8b6792ac180d55185"

CASES = {
    "a": {
        "case": "case-a71ba29c7b7b4bd2996adefff98c3aa2",
        "session": "23da4fe0d54e42f2bc832d37bc241ec7",
        "enemy": "ftkmf_modeltest_royal_sargassum_seaking_tentacle_a",
        "baseEnemy": "seaKingTentacleA",
        "controller": "krakenTentacleController",
        "coverageManifest": ROOT / "scratch" / "coverage-batch-20260911-201220.json",
        "motionGrid": ROOT / "scratch" / "royal-sargassum-v3-a-motion-grid-v1.png",
        "contact": ROOT / "scratch" / "royal-sargassum-v3-a-live-contact-v1.png",
    },
    "b": {
        "case": "case-8846c5d4b9ae43c9b048327a8b764003",
        "session": "b9f9c8abb1dd48b5a17b8cd0ceda3a5c",
        "enemy": "ftkmf_modeltest_royal_sargassum_seaking_tentacle_b",
        "baseEnemy": "seaKingTentacleB",
        "controller": "krakenTentacleControllerMirrored",
        "coverageManifest": ROOT / "scratch" / "coverage-batch-20260911-201527.json",
        "motionGrid": ROOT / "scratch" / "royal-sargassum-v3-b-motion-grid-v1.png",
    },
}

HISTORICAL_REJECTIONS = {
    "v2": {
        "case": ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-1f7c72bb798c439a9d2a5faf9ad54e2a" / "case-result.json",
        "coverageManifest": ROOT / "scratch" / "coverage-batch-20260911-195821.json",
        "contact": ROOT / "scratch" / "royal-sargassum-v2-a-live-contact-v1.png",
        "reason": "Rejected after visual review: the inherited matLoot emission remained active and produced a bright yellow stripe.",
    },
    "v2_1": {
        "case": ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-9cf212c821be4334813ec21acd6100ac" / "case-result.json",
        "coverageManifest": ROOT / "scratch" / "coverage-batch-20260911-200508.json",
        "contact": ROOT / "scratch" / "royal-sargassum-v2.1-a-live-contact-v1.png",
        "reason": "Rejected after visual review: emission was corrected, but the V2 silhouette and palette still read as a bright bamboo-like limb.",
    },
}

STAGE_RECEIPT = ROOT / "scratch" / "runtime-profile-421-abyssal-kraken-v3-seaking" / "receipt.json"
DEPLOYMENT_RECEIPT = ROOT / "scratch" / "mirewarden-game" / "deployment-backups" / "abyssal-kraken-v3-seaking-20260911-201012" / "deployment.json"
CATALOG = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"

AUTHORING = [
    "royal-sargassum-seaking-tentacle-v3.glb",
    "royal-sargassum-seaking-tentacle-v3.png",
    "royal-sargassum-seaking-tentacle-v3.source.json",
    "royal-sargassum-seaking-tentacle-v3.pieces.json",
    "royal-sargassum-seaking-tentacle-v3.validation.json",
    "royal-sargassum-seaking-tentacle-v3-reopened.glb",
    "royal-sargassum-seaking-tentacle-v3-reopened.png",
    "royal-sargassum-seaking-tentacle-v3-reopened.source.json",
    "royal-sargassum-seaking-tentacle-v3-reopened.validation.json",
    "royal-sargassum-seaking-tentacle-v3.blend",
    "royal-sargassum-seaking-tentacle-v3-studio.blend",
    "royal-sargassum-seaking-tentacle-v3-hero.png",
    "royal-sargassum-seaking-tentacle-v3-side.png",
    "build_geometry.py",
    "build_blender.py",
    "verify_original_geometry.py",
    "verify_target_bindings.py",
    "runtime-profiles-v2.json",
    "runtime-profiles-v2.1.json",
    "runtime-profiles.json",
    "build_geometry-v2.py",
    "build_blender-v2.py",
    "verify_original_geometry-v2.py",
    "verify_target_bindings-v2.py",
    "runtime-profiles-v1.json",
    "stage_runtime_profiles-v2.py",
    "deploy_runtime_profiles-v2.py",
    "stage_runtime_profiles-v2.1.py",
    "deploy_runtime_profiles-v2.1.py",
    "stage_runtime_profiles.py",
    "deploy_runtime_profiles.py",
    "sargassum-seaking-tentacle-v2.glb",
    "sargassum-seaking-tentacle-v2.png",
    "sargassum-seaking-tentacle-v2.source.json",
    "sargassum-seaking-tentacle-v2.pieces.json",
    "sargassum-seaking-tentacle-v2.validation.json",
]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def inside_root(path):
    try:
        Path(path).resolve().relative_to(ROOT)
        return True
    except ValueError:
        return False


def image_path(value):
    return Path(value).suffix.lower() == ".png"


def compact_material(material):
    return {
        "name": material["name"],
        "shader": material["shader"],
        "emissionKeyword": material["emissionKeyword"],
        "emissionColor": material["emissionColor"],
        "mainTexture": material["_MainTex"],
        "emissionMap": material["_EmissionMap"],
    }


def enemy_hp(snapshot, enemy):
    enemies = snapshot.get("combat", {}).get("enemies", [])
    matches = [item for item in enemies if item.get("type") == enemy]
    if len(matches) == 1:
        return matches[0].get("hp")
    return None


def assert_renderer(case, spec):
    renderer = case["initialRenderer"]
    assert case["session"] == spec["session"]
    assert case["enemy"] == spec["enemy"]
    assert case["status"] == "needs_visual_review"
    assert case["profileSha256"] == PROFILE_SHA256
    assert renderer["ownerRootName"] == "Enemy Dummy"
    assert renderer["celRootName"] == "enSeaKingTentacle(Clone)"
    assert renderer["celRelativeRendererPath"] == "KrakenGodTentacle"
    assert renderer["mesh"] == "ftkmf_glb_royal-sargassum-seaking-tentacle-v3.glb"
    assert renderer["boneSignature"] == BONE_SIGNATURE
    assert renderer["animator"]["controller"] == spec["controller"]
    assert len(renderer["materials"]) == 1
    material = renderer["materials"][0]
    assert material["emissionKeyword"] is False
    assert material["_EmissionMap"] is None
    assert material["_MainTex"]["name"] == "ftkmf_royal-sargassum-seaking-tentacle-v3.png"
    assert case["finalReady"]["strictReady"]["ok"] is True
    assert case["finalReady"]["strictReady"]["level"] == 0
    assert case["finalReady"]["strictReady"]["room"] == 2


def add_source(sources, path):
    path = Path(path)
    assert path.is_file() and not path.is_symlink(), path
    assert inside_root(path), path
    assert path.suffix.lower() not in FORBIDDEN_SUFFIXES, path
    sources.add(path.resolve())


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)

profiles = read(ASSET / "runtime-profiles.json")
profile_rows = {row["key"]: row for row in profiles["profiles"]}
assert profiles["version"] == 1
for key, spec in CASES.items():
    row = profile_rows[spec["enemy"]]
    assert row["baseEnemy"] == spec["baseEnemy"]
    assert row["displayName"] == f"Royal Sargassum V3 {key.upper()}"
    assert row["renderers"] == [{
        "rendererPath": "KrakenGodTentacle",
        "glbFile": "royal-sargassum-seaking-tentacle-v3.glb",
        "textureFile": "royal-sargassum-seaking-tentacle-v3.png",
        "disableNativeEmission": True,
    }]

stage = read(STAGE_RECEIPT)
deployment = read(DEPLOYMENT_RECEIPT)
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["review"]["candidateCatalogSha256"] == PROFILE_SHA256
assert deployment["new"]["model-test-profiles.json"] == PROFILE_SHA256
assert deployment["review"]["replacedKeys"] == [CASES["a"]["enemy"], CASES["b"]["enemy"]]
assert stage["catalog"]["sha256"] == PROFILE_SHA256
assert sha(CATALOG) == PROFILE_SHA256

sources = set()
for path in [Path(__file__), CATALOG, STAGE_RECEIPT, DEPLOYMENT_RECEIPT]:
    add_source(sources, path)
for relative in AUTHORING:
    add_source(sources, ASSET / relative)

case_records = {}
for key, spec in CASES.items():
    case_path = BASE / spec["case"] / "case-result.json"
    case = read(case_path)
    assert_renderer(case, spec)
    add_source(sources, case_path)
    add_source(sources, Path(case["journal"]["path"]))
    add_source(sources, Path(case["claim"]["path"]))
    session_path = BASE / f"new-run-session-{spec['session']}.json"
    session = read(session_path)
    assert session["session"] == spec["session"]
    add_source(sources, session_path)
    add_source(sources, Path(session["journal"]))
    add_source(sources, spec["coverageManifest"])
    add_source(sources, spec["motionGrid"])
    if spec.get("contact"):
        add_source(sources, spec["contact"])

    manifest_rows = read(spec["coverageManifest"])
    matching = [row for row in manifest_rows if row.get("profile") == spec["enemy"]]
    assert len(matching) == 1 and matching[0]["case"] == spec["case"]
    add_source(sources, Path(matching[0]["stageJournal"]))
    stage_case = BASE / matching[0]["stageCase"] / "result.json"
    add_source(sources, stage_case)

    captures = []
    for action in case["actions"]:
        label = action["action"]
        boundary = action["capture"]["boundary"]
        raw_capture = Path(action["capture"]["rawCapture"]["path"])
        raw = read(raw_capture)
        pngs = sorted(raw_capture.with_suffix("").glob("*.png"))
        assert raw_capture.is_file()
        assert len(raw["frames"]) == boundary["retainedFrameCount"]
        assert len(pngs) == boundary["retainedFrameCount"]
        if label in {"pass", "attack"}:
            assert boundary["completeCapture"] is True
            assert boundary["rawCaptureOk"] is True
            assert boundary["termination"] is None
            assert boundary["retainedFrameCount"] == 120
        elif label == "kill-fixture":
            assert boundary["completeCapture"] is False
            assert boundary["rawCaptureOk"] is False
            assert boundary["termination"] == "renderer_destroyed"
            assert boundary["retainedFrameCount"] == 91
        else:
            raise AssertionError(label)
        add_source(sources, raw_capture)
        add_source(sources, Path(action["journal"]["path"]))
        add_source(sources, Path(action["rawResult"]["path"]))
        for png in pngs:
            add_source(sources, png)
        captures.append({
            "label": label,
            "captureId": raw_capture.stem,
            "rawSha256": sha(raw_capture),
            "width": raw["width"],
            "height": raw["height"],
            "requestedFps": raw["requestedFps"],
            "frames": len(raw["frames"]),
            "complete": boundary["completeCapture"],
            "rawCaptureOk": boundary["rawCaptureOk"],
            "termination": boundary["termination"],
            "limitation": boundary["limitation"],
            "actionClassification": action["classification"],
            "enemyHpBefore": enemy_hp(action["before"], spec["enemy"]),
            "enemyHpAfter": enemy_hp(action["after"], spec["enemy"]),
        })
    assert [(item["label"], item["frames"], item["complete"]) for item in captures] == [
        ("pass", 120, True), ("attack", 120, True), ("kill-fixture", 91, False),
    ]
    assert captures[1]["enemyHpBefore"] == 270 and captures[1]["enemyHpAfter"] == 266
    assert captures[2]["enemyHpBefore"] == 266 and captures[2]["enemyHpAfter"] == 0
    case_records[key] = {"case": case, "captures": captures}

for rejection in HISTORICAL_REJECTIONS.values():
    for path in rejection.values():
        if isinstance(path, Path):
            add_source(sources, path)

# Preserve source files named in the journals without copying forbidden game payloads.
journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals = set()
while journals:
    journal = journals.pop()
    if journal in seen_journals:
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        value = json.loads(line)
        candidate = value.get("data", {}).get("path")
        if not candidate:
            continue
        path = Path(candidate)
        if not path.is_file() or not inside_root(path) or path.suffix.lower() in FORBIDDEN_SUFFIXES:
            continue
        add_source(sources, path)
        if path.suffix == ".jsonl":
            journals.add(path.resolve())

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.suffix.lower() not in FORBIDDEN_SUFFIXES, source

pins = {}
mappings = []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    if source.suffix.lower() == ".png":
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
asset_pins = {
    "royal-sargassum-seaking-tentacle-v3.glb": sha(ASSET / "royal-sargassum-seaking-tentacle-v3.glb"),
    "royal-sargassum-seaking-tentacle-v3.png": sha(ASSET / "royal-sargassum-seaking-tentacle-v3.png"),
    "royal-sargassum-seaking-tentacle-v3-reopened.glb": sha(ASSET / "royal-sargassum-seaking-tentacle-v3-reopened.glb"),
    "royal-sargassum-seaking-tentacle-v3-reopened.png": sha(ASSET / "royal-sargassum-seaking-tentacle-v3-reopened.png"),
}
write(OUT / "asset-pins.json", asset_pins)

selection_observations = {
    ("pass", 12): "Sampled native idle/pass frame retained for visual review.",
    ("pass", 24): "Second sampled native idle/pass frame retained for visual review.",
    ("attack", 6): "Sampled frame from the accepted ordinary attack capture.",
    ("attack", 12): "Second sampled frame from the accepted ordinary attack capture.",
    ("kill-fixture", 0): "First retained frame from the explicit KillSingle death-prefix capture.",
    ("kill-fixture", 90): "Final retained frame before the renderer-destroyed capture boundary.",
}
selected = []
for key, record in case_records.items():
    for frame in record["case"]["selectedFrames"]:
        source = Path(frame["path"])
        assert source.is_file() and sha(source) == frame["sha256"]
        destination = OUT / "selected" / key / f"{frame['action']}-{frame['index']:04d}.png"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        selected.append({
            "target": key.upper(),
            "source": str(source.relative_to(ROOT)),
            "archive": str(destination.relative_to(OUT)),
            "sha256": sha(destination),
            "action": frame["action"],
            "index": frame["index"],
            "observation": selection_observations[(frame["action"], frame["index"])],
        })
assert len(selected) == 12

review_artifacts = []
for key, spec in CASES.items():
    for label in ["motionGrid", "contact"]:
        source = spec.get(label)
        if not source:
            continue
        destination = OUT / "review" / f"{key}-{source.name}"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        review_artifacts.append({
            "target": key.upper(),
            "kind": label,
            "source": str(source.relative_to(ROOT)),
            "archive": str(destination.relative_to(OUT)),
            "sha256": sha(destination),
            "purpose": "Visual-review derivative; it does not replace the raw capture pins.",
        })

videos = []
for key, record in case_records.items():
    for capture in record["captures"]:
        video = OUT / "video" / f"{key}-{capture['label']}.mp4"
        video.parent.mkdir(parents=True, exist_ok=True)
        image_dir = BASE / capture["captureId"]
        subprocess.run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
            "-i", str(image_dir / "%04d.png"), "-frames:v", str(capture["frames"]),
            "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
        ], check=True)
        info = json.loads(subprocess.check_output([
            "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
            "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
        ]))["streams"][0]
        assert int(info["nb_read_frames"]) == capture["frames"]
        videos.append({
            "target": key.upper(),
            "label": capture["label"],
            "captureId": capture["captureId"],
            "path": str(video.relative_to(OUT)),
            "sha256": sha(video),
            "frames": capture["frames"],
            "width": int(info["width"]),
            "height": int(info["height"]),
            "playbackFps": 12,
            "timing": "Presentation derivative, not unperturbed timing.",
        })

historical = {}
for version, rejection in HISTORICAL_REJECTIONS.items():
    historical[version] = {
        "case": str(rejection["case"].relative_to(ROOT)),
        "caseSha256": sha(rejection["case"]),
        "coverageManifest": str(rejection["coverageManifest"].relative_to(ROOT)),
        "coverageManifestSha256": sha(rejection["coverageManifest"]),
        "contact": str(rejection["contact"].relative_to(ROOT)),
        "contactSha256": sha(rejection["contact"]),
        "reason": rejection["reason"],
    }

bindings = {}
for key, record in case_records.items():
    case = record["case"]
    renderer = case["initialRenderer"]
    bindings[key] = {
        "enemy": case["enemy"],
        "nativeBase": CASES[key]["baseEnemy"],
        "owner": {
            "kind": renderer["ownerKind"],
            "root": renderer["ownerRootName"],
            "instanceId": renderer["ownerInstanceId"],
            "celRoot": renderer["celRootName"],
            "celRelativeRendererPath": renderer["celRelativeRendererPath"],
        },
        "mesh": renderer["mesh"],
        "boneSignature": renderer["boneSignature"],
        "rootBone": renderer["rootBone"],
        "controller": renderer["animator"]["controller"],
        "material": compact_material(renderer["materials"][0]),
        "captures": record["captures"],
        "ordinaryAttack": record["case"]["attackRetrySummary"],
        "finalReady": {
            "ok": case["finalReady"]["strictReady"]["ok"],
            "level": case["finalReady"]["strictReady"]["level"],
            "room": case["finalReady"]["strictReady"]["room"],
            "buttonCount": case["finalReady"]["strictReady"]["buttonCount"],
        },
        "case": str((BASE / CASES[key]["case"] / "case-result.json").relative_to(ROOT)),
        "caseSha256": sha(BASE / CASES[key]["case"] / "case-result.json"),
        "session": case["session"],
    }

validation = {
    "status": "fresh_v3_seaking_a_b_exact_binding_material_emission_correction_sampled_motion_ordinary_hit_death_prefix_and_ready_observed",
    "scope": "This is scoped V3 evidence for the two Sea King tentacle profiles only. It does not accept the other Abyssal Kraken rows or claim full-art approval.",
    "catalogSha256": PROFILE_SHA256,
    "profileInput": str((ASSET / "runtime-profiles.json").relative_to(ROOT)),
    "profileInputSha256": sha(ASSET / "runtime-profiles.json"),
    "assetHashes": asset_pins,
    "stageReceipt": {
        "path": str(STAGE_RECEIPT.relative_to(ROOT)),
        "sha256": sha(STAGE_RECEIPT),
    },
    "deploymentReceipt": {
        "path": str(DEPLOYMENT_RECEIPT.relative_to(ROOT)),
        "sha256": sha(DEPLOYMENT_RECEIPT),
    },
    "historicalRejections": historical,
    "bindings": bindings,
    "sampledVisualReview": {
        "reviewArtifacts": review_artifacts,
        "selectedPNGs": selected,
        "observation": "The reviewed V3 samples show a dark teal curved limb remaining visually connected through the captured idle and attack motion for both controller variants. No obvious mesh separation or catastrophic deformation was seen in those samples.",
        "limits": [
            "The sample does not cover every animation frame, camera angle, culling condition, or encounter layout.",
            "Attack effects and UI partially obscure some frames.",
            "The explicit KillSingle capture ends after 91 frames when the renderer is destroyed. It is a death-prefix observation, not full death-animation coverage or native-cleanup causality.",
            "Ordinary attack acceptance records same-target HP 270 to 266. It does not infer a specific hit animation, block state, dodge state, or damage source.",
            "This archive does not establish portrait framing, resource lifetime, all-angle visibility, or finished-art acceptance.",
        ],
    },
    "videos": videos,
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "provenance": "The authored V3 geometry, texture, normals, UVs, indices, weights and materials are kept under the Abyssal Kraken package. This archive includes no native game mesh surfaces, texture pixels, animations, DLLs, or game asset payloads.",
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(
    """# Royal Sargassum V3 Sea King live evidence

This supplement records two fresh isolated-game trials for the V3 Sea King tentacle profiles: `seaKingTentacleA` with `krakenTentacleController`, and `seaKingTentacleB` with `krakenTentacleControllerMirrored`. Both profiles bind the exact `KrakenGodTentacle` renderer to the original V3 GLB and V3 texture with native emission disabled.

Each target has a complete 120-frame pass capture and a complete 120-frame ordinary attack capture. The ordinary attack changes the same target from HP `270` to `266`. The explicit `KillSingle` fixture changes HP `266` to `0`, then reaches the expected renderer-destroyed boundary after 91 retained frames. Both sessions reach strict Ready at level `0`, room `2`.

The V2 record was rejected because inherited `matLoot` emission produced a bright yellow stripe. V2.1 disabled emission but was also rejected because its silhouette and palette still read as a bright bamboo-like limb. V3 uses a narrower original dark teal tendril design. Sampled motion grids for both native controllers show it staying connected without an obvious catastrophic deformation in the reviewed frames.

This is not full visual acceptance. The reviewed samples do not cover every frame, camera angle, culling condition, encounter layout, portrait, or resource-lifetime path. The death records are renderer-destroyed prefixes rather than complete death captures, and the ordinary HP change does not identify a specific animation or damage event.

`validation.json` pins the exact catalog, deployment, authoring assets, raw captures, selected originals, six presentation videos, historical rejection records, and scoped limitations. Metadata is stored gzip-losslessly; source PNGs are retained by hash except for selected originals and review derivatives. Native game payloads and DLLs are excluded. `archive.py` is offline-only and refuses to overwrite a completed archive unless `FTK_ARCHIVE_REBUILD=1` is set.
"""
)
print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "validationSha256": sha(OUT / "validation.json"),
    "bindings": sorted(bindings),
    "sourceImages": len(pins),
    "losslessMappings": len(mappings),
    "selected": len(selected),
    "reviewArtifacts": len(review_artifacts),
    "videos": len(videos),
}, indent=2))
