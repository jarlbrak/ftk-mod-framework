#!/usr/bin/env python3
"""Build the immutable Verdigrin V4 evidence supplement offline."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "verdigrin-mimic"
OUT = ASSET / "live-validation-v4"
V3 = ASSET / "live-validation-v3"
RUNNER = ROOT / "scratch" / "model-route-cace272650590c4f-directenemy-run.json"
RECONCILIATION = ROOT / "scratch" / "verdigrin-v4-runner-reconciliation.json"
CASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-1ddbba3613a04032be4ab35b87de18f0" / "case-result.json"
CASE_JOURNAL = CASE.with_name("journal.jsonl")
REVIEW = ROOT / "scratch" / "verdigrin-v4-root-visual-review.json"
PROFILE = ASSET / "runtime-profile.json"
MANIFEST = ASSET / "manifest.json"
GLB = ASSET / "verdigrin.glb"
TEXTURE = ASSET / "verdigrin_basecolor.png"
SESSION = "e087741594c041d29c7a5f19adf364ce"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def repo_path(value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = ROOT / path
    path = path.resolve()
    assert path.is_relative_to(ROOT), path
    return path


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {
        "path": str(path.relative_to(ROOT)),
        "sha256": sha(path),
        "bytes": path.stat().st_size,
    }


def clip_ranges(raw: dict) -> list[tuple[int, int, tuple[str, ...]]]:
    ranges: list[list[object]] = []
    for index, frame in enumerate(raw["frames"]):
        names: list[str] = []
        for layer in (frame.get("animator") or {}).get("layers", []):
            names.extend(item["name"] for item in layer.get("playing", []) if item.get("name"))
        key = tuple(names)
        if not ranges or ranges[-1][2] != key:
            ranges.append([index, index, key])
        else:
            ranges[-1][1] = index
    return [(int(first), int(last), tuple(clips)) for first, last, clips in ranges]


def verify_identity(raw: dict) -> None:
    assert raw["ok"] is True and raw["session"] == SESSION
    assert len(raw["frames"]) == 120
    expected = (
        -245498,
        369188,
        "ftkmf_glb_verdigrin.glb",
        "7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba",
    )
    observed = {
        (frame["instanceId"], frame["ownerInstanceId"], frame["mesh"], frame["boneSignature"])
        for frame in raw["frames"]
    }
    assert observed == {expected}, observed
    assert {frame["celRelativeRendererPath"] for frame in raw["frames"]} == {"mimic01"}
    assert all(frame["ragdoll"]["m_DoRagdoll"] is False for frame in raw["frames"])


def video_entry(raw_path: Path, destination: Path, action: str) -> dict:
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(raw_path.with_suffix("") / "%04d.png"), "-frames:v", "120",
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(destination),
    ], check=True)
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(destination),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == 120
    return {
        "action": action,
        "path": destination.name,
        "sha256": sha(destination),
        "frames": 120,
        "width": int(stream["width"]),
        "height": int(stream["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing.",
    }


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", (
    "refusing to overwrite a completed archive"
)
OUT.mkdir(parents=True, exist_ok=True)

runner = read(RUNNER)
reconciliation = read(RECONCILIATION)
case = read(CASE)
review = read(REVIEW)
profile_document = read(PROFILE)
manifest = read(MANIFEST)
v3 = read(V3 / "validation.json")

assert runner["status"] == "stopped" and runner["exerciseExitCode"] == 1
assert runner["session"] == SESSION
assert runner["caseResult"] == str(CASE.relative_to(ROOT))
assert runner["plan"]["topologyGroup"] == "cace272650590c4f"
assert runner["plan"]["coverageRouteKind"] == "directEnemy"
assert runner["plan"]["profile"]["key"] == "ftkmf_modeltest_verdigrin"
assert runner["plan"]["profile"]["document"]["sha256"] == sha(PROFILE)
assert runner["plan"]["catalog"]["sha256"] == "6c6c04041425c0e7fd7f983d3e96c0ab35ade5e7dbf11754b5052eba1dd2f144"
assert runner["plan"]["assets"] == [
    {"name": "verdigrin.glb", "sha256": "97838ca8be15265f429d24a33e021b55b45faf6472e05137481d303a3d30c0bb"},
    {"name": "verdigrin_basecolor.png", "sha256": "24b8512b9a647ef97362793f47eb8d0f1fcae5398401a22d11721c5e4986de2e"},
]
assert runner["plan"]["sourceAssignments"] == [{
    "targetType": "source_assignment",
    "sourceKind": "native_enemy_row",
    "nativeEnemy": "mimicA",
    "resourcePrefab": None,
    "rendererPath": "mimic01",
    "sourceRendererId": 121192,
}]
assert runner["plan"]["motionRendererPath"] == "mimic01"
assert runner["initialRenderer"]["instanceId"] == -245498
assert runner["initialRenderer"]["ownerInstanceId"] == 369188
assert runner["initialRenderer"]["mesh"] == "ftkmf_glb_verdigrin.glb"
assert runner["initialRenderer"]["boneSignature"] == "7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba"
assert runner["initialRenderer"]["celRelativeRendererPath"] == "mimic01"
assert runner["initialRenderer"]["ragdoll"]["m_DoRagdoll"] is False

assert reconciliation["status"] == "semantic_trial_identity_matches_current_campaign_snapshot_fields_only_changed"
assert sorted(reconciliation["snapshotOnlyMismatches"]) == ["queue", "stageReadiness"]
assert reconciliation["matchingSemanticFields"]["profile"]["documentSha256"] == sha(PROFILE)

assert case["status"] == "stopped"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_verdigrin"
assert case["error"] == "Native loot progress/strict Ready not observed; no collect retried"
assert [item["action"] for item in case["actions"]] == ["pass", "attack", "kill-fixture"]
assert all(item["actionAccepted"] is True for item in case["actions"])
assert all(item["capture"]["boundary"]["completeCapture"] is True for item in case["actions"])
assert all(item["capture"]["boundary"]["retainedFrameCount"] == 120 for item in case["actions"])
assert all(item["motionEvidence"]["schema"] == "ftkmf.exercise-motion-evidence.v1" for item in case["actions"])
assert sorted(case["actions"][0]["motionEvidence"]) == ["action", "attack", "idle", "limitations", "schema"]
assert sorted(case["actions"][1]["motionEvidence"]) == ["action", "hit", "idle", "limitations", "schema"]
assert sorted(case["actions"][2]["motionEvidence"]) == ["action", "death", "idle", "limitations", "schema"]
assert case["actions"][0]["partyHpBefore"] == [989] and case["actions"][0]["partyHpAfter"] == [975]
assert case["actions"][1]["hpOutcome"]["beforeHp"] == 58
assert case["actions"][1]["hpOutcome"]["afterHp"] == 52
assert case["actions"][2]["before"]["combat"]["enemies"][0]["hp"] == 52
assert case["actions"][2]["after"]["combat"]["enemies"][0]["hp"] == 0
assert len(case["collects"]) == 1

raw_paths = [repo_path(item["capture"]["rawCapture"]["path"]) for item in case["actions"]]
raws = [read(path) for path in raw_paths]
for raw in raws:
    verify_identity(raw)
assert clip_ranges(raws[0]) == [
    (0, 47, ("cidle_mimic",)),
    (48, 65, ("attack_mimic",)),
    (66, 119, ("cidle_mimic",)),
]
assert clip_ranges(raws[1]) == [
    (0, 26, ("cidle_mimic",)),
    (27, 33, ("damageSmall_mimic",)),
    (34, 71, ("cidle_mimic",)),
    (72, 85, ("chompAOE_mimic",)),
    (86, 119, ("cidle_mimic",)),
]
assert clip_ranges(raws[2]) == [
    (0, 25, ("cidle_mimic",)),
    (26, 119, ("deathHeavy_mimic",)),
]

assert review["reviewStatus"] == "reviewed_exact_source_with_scoped_limits"
assert review["session"] == SESSION and len(review["frames"]) == 7
assert review["binding"]["rendererInstanceId"] == -245498
assert review["binding"]["mesh"] == "ftkmf_glb_verdigrin.glb"
assert review["binding"]["boneSignature"] == "7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba"

assert sha(V3 / "validation.json") == "d17f969101d06e659301e9cdf437e140bbdc2db0a879693a73feb970e6b7b4e7"
assert v3["nativeChassis"] == "mimicA" and v3["rendererId"] == 121192
assert v3["ordinaryAttack"]["beforeHp"] == 58 and v3["ordinaryAttack"]["afterHp"] == 52
assert v3["ordinaryAttack"]["cheat"] == "None" and v3["ordinaryAttack"]["focus"] is False
assert v3["explicitKillFixture"]["method"] == "KillSingle"
assert v3["nativeCollects"] == 2
assert v3["finalReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert v3["nextEncounter"]["room"] == 2 and v3["nextEncounter"]["liveEnemies"] == 2

assert profile_document["profiles"][0]["baseEnemy"] == "mimicA"
assert profile_document["profiles"][0]["renderers"][0]["rendererPath"] == "mimic01"
assert sha(GLB) == "97838ca8be15265f429d24a33e021b55b45faf6472e05137481d303a3d30c0bb"
assert sha(TEXTURE) == "24b8512b9a647ef97362793f47eb8d0f1fcae5398401a22d11721c5e4986de2e"
assert manifest["assets"]["verdigrin.glb"]["sha256"] == sha(GLB)
assert manifest["assets"]["verdigrin_basecolor.png"]["sha256"] == sha(TEXTURE)

sources = {
    Path(__file__), RUNNER, RECONCILIATION, CASE, CASE_JOURNAL, REVIEW,
    PROFILE, MANIFEST, GLB, TEXTURE, V3 / "validation.json",
    V3 / "source-image-pins.json", V3 / "asset-pins.json", V3 / "README.md",
}
stage_result = repo_path(runner["stageResult"])
sources.add(stage_result)
stage_journal = stage_result.with_name("journal.jsonl")
if stage_journal.is_file():
    sources.add(stage_journal)
for action in case["actions"]:
    sources.add(repo_path(action["capture"]["rawCapture"]["path"]))
    sources.add(repo_path(action["journal"]["path"]))
    sources.add(repo_path(action["rawResult"]["path"]))

for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN, source

source_image_pins: dict[str, str] = {}
capture_image_pins: dict[str, str] = {}
for raw_path in raw_paths:
    images = sorted(raw_path.with_suffix("").glob("*.png"))
    assert len(images) == 120
    for image in images:
        relative = str(image.relative_to(ROOT))
        digest = sha(image)
        source_image_pins[relative] = digest
        capture_image_pins[relative] = digest

lossless_mappings: list[dict] = []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    destination = OUT / "metadata" / (str(relative) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    lossless_mappings.append({
        "source": str(relative),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "bytes": len(raw),
        "encoding": "gzip-lossless",
    })

selected: list[dict] = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    source_image_pins[frame["path"]] = frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({
        "action": frame["action"],
        "index": frame["index"],
        "clip": frame["clip"],
        "source": frame["path"],
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": frame["observation"],
    })

write(OUT / "source-image-pins.json", source_image_pins)
write(OUT / "capture-image-pins.json", capture_image_pins)
write(OUT / "asset-pins.json", {
    "verdigrin.glb": sha(GLB),
    "verdigrin_basecolor.png": sha(TEXTURE),
})

review_copy = OUT / "visual-review.json"
shutil.copy2(REVIEW, review_copy)
reconciliation_copy = OUT / "runner-reconciliation.json"
shutil.copy2(RECONCILIATION, reconciliation_copy)

videos = [
    video_entry(raw_paths[0], OUT / "native-idle-attack.mp4", "native-idle-attack"),
    video_entry(raw_paths[1], OUT / "ordinary-nonlethal-hit.mp4", "ordinary-nonlethal-hit"),
    video_entry(raw_paths[2], OUT / "kill-fixture.mp4", "kill-fixture"),
]

binding = {
    "nativeChassis": "mimicA",
    "sourceKind": "native_enemy_row",
    "nativeEnemy": "mimicA",
    "resourcePrefab": None,
    "ownerInstanceId": 369188,
    "rendererPath": "mimic01",
    "rendererId": 121192,
    "sourceRendererId": 121192,
    "rendererKind": "SkinnedMeshRenderer",
    "rendererInstanceId": -245498,
    "mesh": "ftkmf_glb_verdigrin.glb",
    "boneSignature": "7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba",
    "visualScale": 1.0,
    "material": runner["initialRenderer"]["materials"][0],
}

captures = [
    {
        "action": "pass",
        "label": "idle-and-native-attack",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[0]),
        "frameCount": 120,
        "sampledClips": ["cidle_mimic", "attack_mimic"],
        "causalMotion": case["actions"][0]["motionEvidence"],
        "captureScope": "Settled idle, native ordinary attack dealing 14 hero damage, and return to idle on the exact custom renderer.",
    },
    {
        "action": "attack",
        "label": "ordinary-nonlethal-hit-and-native-proficiency",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[1]),
        "frameCount": 120,
        "sampledClips": ["cidle_mimic", "damageSmall_mimic", "chompAOE_mimic"],
        "causalMotion": case["actions"][1]["motionEvidence"],
        "captureScope": "Ordinary no-focus player hit 58 to 52, native small-damage recoil, recovery, later native proficiency, and return to idle.",
    },
    {
        "action": "kill-fixture",
        "label": "kill-fixture-death",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[2]),
        "frameCount": 120,
        "sampledClips": ["cidle_mimic", "deathHeavy_mimic"],
        "causalMotion": case["actions"][2]["motionEvidence"],
        "captureScope": "Explicit KillSingle transition 52 to 0, native animated heavy death, and native Victory and Loot arrival.",
    },
]

limits = [
    "The fresh ordinary no-focus hit records HP 58 to 52 and damageSmall_mimic. Same-target HP and the causal native damage response are recorded without inferring another damage source.",
    "The explicit KillSingle fixture records HP 52 to 0 and deathHeavy_mimic. Fixture death is not ordinary lethal-damage evidence.",
    "The exact custom renderer reports m_DoRagdoll false. Its death is animated, not a body ragdoll.",
    "The fresh process accepted one native Collect and stopped without strict Ready. The prior V3 process separately supplies two guarded Collects, strict Ready and next-room progression.",
    "Hero, weapon, UI, depth blur and native effects obscure some surfaces. Every hinge and tongue interval, floor collision, sleeping, full culling, portraits, resource lifetime, final disposal and finished-art acceptance remain outside this archive.",
    "The retained Victory frame does not establish a precise visible corpse lifetime.",
]

validation = {
    "schema": "ftkmf.model-validation.v3",
    "revision": "verdigrin-live-validation-v4",
    "status": "reviewed_exact_mimicA_binding_appearance_idle_attack_hit_death_and_gameplay_observed",
    "displayName": "Verdigrin Coffer",
    "enemy": "ftkmf_modeltest_verdigrin",
    "nativeChassis": "mimicA",
    "nativeEnemy": "mimicA",
    "sourceKind": "native_enemy_row",
    "resourcePrefab": None,
    "rendererPaths": ["mimic01"],
    "sourceRendererIds": [121192],
    "sessions": [SESSION, v3["session"]],
    "profile": profile_document["profiles"][0],
    "profileDocumentSha256": sha(PROFILE),
    "catalogSha256": runner["plan"]["catalog"]["sha256"],
    "assetHashes": {
        "verdigrin.glb": sha(GLB),
        "verdigrin_basecolor.png": sha(TEXTURE),
    },
    "binding": binding,
    "renderers": [binding],
    "captures": captures,
    "ordinaryHit": {
        "beforeHp": 58,
        "afterHp": 52,
        "cheat": "None",
        "focus": False,
        "status": "ordinary_nonlethal_hit_with_damageSmall_mimic_observed",
        "sourceCapture": str(raw_paths[1].relative_to(ROOT)),
    },
    "ordinaryLethal": False,
    "explicitKillFixture": {
        "beforeHp": 52,
        "afterHp": 0,
        "method": "KillSingle",
        "deathCaptureComplete": True,
    },
    "nativeCollects": v3["nativeCollects"],
    "finalReady": v3["finalReady"],
    "readyResult": v3["readyResult"],
    "nextEncounter": v3["nextEncounter"],
    "priorArchive": {
        "path": "art-experiments/verdigrin-mimic/live-validation-v3/validation.json",
        "sha256": sha(V3 / "validation.json"),
        "scope": "Separate native loot, strict Ready and next-room progression evidence from a fresh exact mimicA process.",
    },
    "runnerRecord": pin(RUNNER),
    "runnerReconciliation": {
        "path": "runner-reconciliation.json",
        "sha256": sha(reconciliation_copy),
        "bytes": reconciliation_copy.stat().st_size,
    },
    "caseResult": pin(CASE),
    "runtimeProfile": pin(PROFILE),
    "visualReview": review["reviewStatus"],
    "rootVisualReview": "visual-review.json",
    "rootVisualReviewPin": {
        "path": "visual-review.json",
        "sha256": sha(review_copy),
        "bytes": review_copy.stat().st_size,
    },
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(source_image_pins),
    "sourceImagePins": "source-image-pins.json",
    "captureImageCount": len(capture_image_pins),
    "captureImagePins": "capture-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": lossless_mappings,
    "workflow": {
        "kind": "reconcile_snapshot_only_runner_drift_then_supplement_prior_progression",
        "rule": "If an immutable runner differs from the current campaign only in queue and stage snapshot hashes, compare every semantic trial identity field before reuse. Preserve the old record, conduct root review, and create a new archive that names the prior progression source.",
        "replayPolicy": "Do not rerun a complete retained trial solely because later campaign bookkeeping changed. Run again only for an unobserved evidence gate or a changed profile, catalog, asset, source renderer, or motion renderer.",
    },
    "limits": limits,
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    """# Verdigrin Coffer live validation V4

This immutable supplement closes the current source-specific idle and hit
evidence gaps for the exact `mimicA` / `mimic01` renderer 121192 route. It
reuses a complete retained trial after proving that its profile, catalog,
assets, source renderer and motion renderer still match the current campaign.
Only the queue and stage snapshot hashes changed after later routes completed.

The fresh retained pass capture records `cidle_mimic`, the native
`attack_mimic` action dealing 14 hero damage, and return to idle. The ordinary
no-focus player attack records HP 58 to 52 with `damageSmall_mimic`, followed
later by `chompAOE_mimic` and another settled idle. The explicit `KillSingle`
fixture records HP 52 to 0 and `deathHeavy_mimic` across a complete 120-frame
capture. `m_DoRagdoll` is false, so this is animated death rather than a body
ragdoll. Fixture death is not ordinary lethal-damage evidence.

The retained V3 archive separately supplies two guarded native Collects,
strict Ready at level 0 room 2 and progression into the next enemy room. The
fresh process stopped after one Collect without strict Ready, so the two
processes remain explicitly separate.

Root-reviewed originals show the authored lid, teeth, tongue and lower coffer
remaining coherent through idle, attack, hit, proficiency and death. Hero,
weapon, UI, depth blur and effects obscure some surfaces. Every hinge and
tongue interval, floor collision, sleeping, full culling, portraits, resource
lifetime, final disposal and finished-art acceptance remain separate gates.
The Victory frame does not establish a precise visible corpse lifetime.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 360 incremental capture images, copies the reviewed originals,
preserves metadata with gzip-lossless mappings and records three verified
120-frame presentation videos. Native payloads and DLLs are excluded.
"""
)

print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "validationSha256": sha(OUT / "validation.json"),
    "selected": len(selected),
    "sourceImages": len(source_image_pins),
    "captureImages": len(capture_image_pins),
    "videos": len(videos),
    "losslessMappings": len(lossless_mappings),
}, indent=2))
