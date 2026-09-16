#!/usr/bin/env python3
"""Archive Tidecrown's scoped exact Sea King live evidence without game payloads."""
import gzip
import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments/tidecrown-sea-king"
OUT = ASSET / "live-validation-v1"
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
FOCUS_SESSION = "fb9c45f58bae421db60332bd1594667c"
NO_FOCUS_SESSION = "7f3625d876374db98d01f2aebb0bf10f"
FOCUS_CASE = BASE / "case-973cc0686a404bdf9cf30a2c18332478/case-result.json"
NO_FOCUS_CASE = BASE / "case-cb2e67a09f2d403492804259d7222b98/case-result.json"
FOCUS_SESSION_RECORD = BASE / f"new-run-session-{FOCUS_SESSION}.json"
NO_FOCUS_SESSION_RECORD = BASE / f"new-run-session-{NO_FOCUS_SESSION}.json"
REVIEW = ROOT / "scratch/tidecrown-root-visual-review-v1.json"
REVIEW_WRITER = ASSET / "write_live_review_v1.py"
PROFILE = ROOT / "scratch/mirewarden-game/model-test-profiles.json"
REGISTRATION = ROOT / "scratch/mirewarden-game/model-test-registration.json"
MANIFEST = ASSET / "manifest.json"
RUNTIME_PROFILE = ASSET / "runtime-profile.json"
STAGE = ROOT / "scratch/runtime-profile-413-tidecrown-sea-king"
DEPLOYMENT = ROOT / "scratch/mirewarden-game/deployment-backups/tidecrown-sea-king-413-20260911-163217/deployment.json"
FOCUS_BATCH = ROOT / "scratch/coverage-batch-20260911-164541.json"
NO_FOCUS_BATCH = ROOT / "scratch/coverage-batch-20260911-163734.json"
RUNNER = ROOT / "scratch/run-coverage-batch.py"
DEPLOYER = ROOT / "scratch/deploy-tidecrown-sea-king-413.py"
EXERCISE = ROOT / "tools/ai-model-pipeline/runtime-test/exercise_case.py"
RUN_CASE = ROOT / "tools/ai-model-pipeline/runtime-test/run_case.py"
RECORD_CASE = ROOT / "tools/ai-model-pipeline/runtime-test/record_case.py"
CAPTURE_BOUNDARY = ROOT / "tools/ai-model-pipeline/compare_kraken_native.py"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def pin(path: Path) -> dict:
    return {"source": relative(path), "sha256": sha(path), "bytes": path.stat().st_size}


def keyed_actions(case: dict) -> dict:
    values = {}
    for action in case["actions"]:
        key = (action["action"], action.get("attempt"))
        assert key not in values, key
        values[key] = action
    return values


def assert_raw_capture(case: dict, action: dict, expected_frames: int, expected_ok: bool) -> tuple[Path, dict]:
    capture = action["capture"]
    raw_path = Path(capture["rawCapture"]["path"])
    raw = read(raw_path)
    assert raw_path.is_file() and raw["session"] == case["session"]
    assert len(raw["frames"]) == expected_frames and raw["ok"] is expected_ok
    assert len(capture["images"]) == expected_frames
    for image in capture["images"]:
        image_path = Path(image["path"])
        assert image_path.is_file() and sha(image_path) == image["sha256"]
    return raw_path, raw


def record_capture(label: str, action: dict, raw_path: Path, raw: dict) -> dict:
    boundary = action["capture"]["boundary"]
    return {
        "label": label,
        "captureId": raw_path.stem,
        "frames": len(raw["frames"]),
        "rawCapture": {"source": relative(raw_path), "sha256": sha(raw_path), "ok": raw["ok"], "error": raw["error"]},
        "boundary": boundary,
        "action": action["action"],
        "attempt": action.get("attempt"),
        "focus": action.get("focus"),
        "classification": action.get("classification"),
    }


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
OUT.mkdir(parents=True, exist_ok=True)
focus = read(FOCUS_CASE)
no_focus = read(NO_FOCUS_CASE)
review = read(REVIEW)
focus_session_record = read(FOCUS_SESSION_RECORD)
no_focus_session_record = read(NO_FOCUS_SESSION_RECORD)
manifest = read(MANIFEST)
registration = read(REGISTRATION)
focus_actions = keyed_actions(focus)
no_focus_actions = keyed_actions(no_focus)

assert focus["schema"] == no_focus["schema"] == "ftkmf.exercise-case.v1"
assert focus["session"] == FOCUS_SESSION and no_focus["session"] == NO_FOCUS_SESSION
assert focus["enemy"] == no_focus["enemy"] == "ftkmf_modeltest_tidecrown_sea_king"
assert focus["rendererPath"] == no_focus["rendererPath"] == "enSeaKing"
assert focus["focusedAttack"] is True and focus["status"] == "needs_visual_review"
assert no_focus["focusedAttack"] is False and no_focus["status"] == "stopped"
assert no_focus["error"] == "Attack gate unmet after bounded native retry: no_hp_loss_unclassified"
assert focus["profileSha256"] == no_focus["profileSha256"] == sha(PROFILE)
assert review["session"] == FOCUS_SESSION and review["nativeChassis"] == "seaKing"
assert review["rendererPath"] == "enSeaKing" and len(review["frames"]) == 8
assert focus_session_record["session"] == FOCUS_SESSION
assert no_focus_session_record["session"] == NO_FOCUS_SESSION
assert focus["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert focus["collects"] == []
assert len(no_focus["attackRetrySummary"]) == 8
assert all(row["status"] == "no_hp_loss_unclassified" and row["beforeHp"] == row["afterHp"] == 720
           for row in no_focus["attackRetrySummary"])
assert set(focus_actions) == {
    ("pass", None), ("attack", 1), ("attack", 2), ("attack", 3), ("attack", 4), ("kill-fixture", None),
}
assert set(no_focus_actions) == {("pass", None), *{("attack", index) for index in range(1, 9)}}

renderer = focus["initialRenderer"]
assert renderer["mesh"] == "ftkmf_glb_tidecrown-sea-king.glb"
assert renderer["boneSignature"] == "4fd853bf7fe19398edeb4cb997a0aba409d3299091249c72a2d17ee01ca0ae6a"
assert renderer["rootBone"] == "Root_M" and renderer["ownerKind"] == "enemies"
assert renderer["animator"]["controller"] == "seaKingController"
assert renderer["materials"][0]["_MainTex"]["name"] == "ftkmf_tidecrown-sea-king_basecolor.png"
assert renderer["materials"][0]["emissionKeyword"] is False

focus_inputs = []
for key, action in focus_actions.items():
    is_death = key == ("kill-fixture", None)
    raw_path, raw = assert_raw_capture(focus, action, 91 if is_death else 120, not is_death)
    if is_death:
        boundary = action["capture"]["boundary"]
        assert boundary["partial"] is True and boundary["termination"] == "renderer_destroyed"
        assert boundary["retainedFrameCount"] == 91 and raw["error"] == boundary["rawCaptureError"]
    else:
        assert action["capture"]["boundary"]["completeCapture"] is True
    focus_inputs.append((key, action, raw_path, raw))
hit_action = focus_actions[("attack", 4)]
assert hit_action["actionResult"]["result"]["committed"] == "Attack(focus)"
assert hit_action["hpOutcome"]["status"] == "nonlethal_hp_loss"
assert (hit_action["hpOutcome"]["beforeHp"], hit_action["hpOutcome"]["afterHp"]) == (720, 719)
death_action = focus_actions[("kill-fixture", None)]
assert death_action["actionResult"]["result"]["committed"] == "KillSingle"

no_focus_inputs = []
for key, action in no_focus_actions.items():
    raw_path, raw = assert_raw_capture(no_focus, action, 120, True)
    assert action["capture"]["boundary"]["completeCapture"] is True
    no_focus_inputs.append((key, action, raw_path, raw))

raw_death = next(raw for key, action, raw_path, raw in focus_inputs if key == ("kill-fixture", None))
first_dynamic = next(index for index, sample in enumerate(raw_death["frames"])
                     if any(not body["isKinematic"] for body in sample["ragdoll"]["rigidbodies"]))
assert first_dynamic == 28
assert sum(not body["isKinematic"] for body in raw_death["frames"][30]["ragdoll"]["rigidbodies"]) == 12
assert sum(not body["isKinematic"] for body in raw_death["frames"][60]["ragdoll"]["rigidbodies"]) == 11
assert raw_death["frames"][90]["active"] is False and raw_death["frames"][90]["isVisible"] is False

for name, digest in manifest["files"].items():
    assert sha(ASSET / name) == digest, name
for name, digest in focus["assetHashes"].items():
    assert sha(ASSET / name) == digest, name
registered = [row for row in registration["registered"] if row["key"] == focus["enemy"]]
assert registration["status"] == "registered" and len(registered) == 1
assert registered[0]["portraitMarkerPath"] == "Root_M/BackA_M/BackB_M/Chest_M/Neck_M/Head_M/PortraitCam"

focus_batch = read(FOCUS_BATCH)
no_focus_batch = read(NO_FOCUS_BATCH)
assert len(focus_batch) == len(no_focus_batch) == 1
assert focus_batch[0]["case"] == FOCUS_CASE.parent.name and focus_batch[0]["status"] == "needs_visual_review"
assert no_focus_batch[0]["case"] == NO_FOCUS_CASE.parent.name and no_focus_batch[0]["status"] == "stopped"

sources = {
    FOCUS_CASE, FOCUS_CASE.with_name("journal.jsonl"), FOCUS_SESSION_RECORD,
    NO_FOCUS_CASE, NO_FOCUS_CASE.with_name("journal.jsonl"), NO_FOCUS_SESSION_RECORD,
    REVIEW, REVIEW_WRITER, PROFILE, REGISTRATION, MANIFEST, RUNTIME_PROFILE,
    STAGE / "receipt.json", STAGE / "stage-script.py", DEPLOYMENT,
    FOCUS_BATCH, NO_FOCUS_BATCH, RUNNER, DEPLOYER, EXERCISE, RUN_CASE, RECORD_CASE, CAPTURE_BOUNDARY,
    Path(__file__),
}
for session_record in (focus_session_record, no_focus_session_record):
    journal = Path(session_record["journal"])
    sources.add(journal)
    result = BASE / f"case-{session_record['case']}" / "result.json"
    if result.is_file():
        sources.add(result)
for name in manifest["files"]:
    candidate = ASSET / name
    if candidate.suffix.lower() in {".py", ".json", ".md"}:
        sources.add(candidate)
for _, action, raw_path, _ in [*focus_inputs, *no_focus_inputs]:
    sources.update((raw_path, Path(action["journal"]["path"]), Path(action["rawResult"]["path"])))

# Preserve metadata losslessly and pin every source screenshot.  Binaries and
# source asset payloads stay outside the archive; the two authored assets have
# independent hashes below.
pending_journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals = set()
excluded = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d", ".blend", ".glb"}
while pending_journals:
    journal = pending_journals.pop()
    if journal in seen_journals or not journal.is_file():
        continue
    seen_journals.add(journal)
    for line in journal.read_text().splitlines():
        candidate = Path(json.loads(line).get("data", {}).get("path", ""))
        if not candidate.is_file() or candidate.suffix.lower() in excluded:
            continue
        try:
            candidate.relative_to(ROOT)
        except ValueError:
            continue
        sources.add(candidate)
        if candidate.suffix == ".jsonl":
            pending_journals.add(candidate)
for source in sources:
    assert source.is_file() and not source.is_symlink() and source.suffix.lower() not in excluded, source

image_pins = {}
for _, action, _, _ in [*focus_inputs, *no_focus_inputs]:
    for image in action["capture"]["images"]:
        image_path = Path(image["path"])
        image_pins[relative(image_path)] = {"sha256": sha(image_path), "bytes": image_path.stat().st_size}

mappings = []
for source in sorted(sources):
    raw = source.read_bytes()
    source_relative = relative(source)
    if source.suffix.lower() == ".png":
        image_pins[source_relative] = {"sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw)}
        continue
    destination = OUT / "metadata" / (source_relative + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    mappings.append({
        "source": source_relative,
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })
write(OUT / "source-image-pins.json", image_pins)
write(OUT / "asset-pins.json", focus["assetHashes"])

selected = []
for reviewed in review["frames"]:
    source = ROOT / reviewed["path"]
    assert source.is_file() and sha(source) == reviewed["sha256"]
    suffix = f"{reviewed['action']}-"
    if reviewed["attempt"] is not None:
        suffix += f"attempt-{reviewed['attempt']}-"
    destination = OUT / "selected" / f"{suffix}{reviewed['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(destination) == reviewed["sha256"]
    selected.append({
        "source": reviewed["path"], "archive": str(destination.relative_to(OUT)),
        "sha256": reviewed["sha256"], "action": reviewed["action"],
        "attempt": reviewed["attempt"], "index": reviewed["index"], "observation": reviewed["observation"],
    })

videos = []
for label, action in (("focus-hit-attempt-4", hit_action), ("fixture-ragdoll", death_action)):
    raw_path = Path(action["capture"]["rawCapture"]["path"])
    raw = read(raw_path)
    video = OUT / "presentation" / f"{label}.mp4"
    video.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([
        "ffmpeg", "-nostdin", "-n", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(raw_path.with_suffix("") / "%04d.png"), "-frames:v", str(len(raw["frames"])),
        "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", str(video),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height,r_frame_rate", "-of", "json", str(video),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == len(raw["frames"])
    videos.append({
        "label": label, "captureId": raw_path.stem, "path": str(video.relative_to(OUT)), "sha256": sha(video),
        "frames": len(raw["frames"]), "width": int(probe["width"]), "height": int(probe["height"]),
        "playbackFps": 12, "timing": "Presentation derivative, not unperturbed timing", "rawCapture": relative(raw_path),
    })

focus_captures = [record_capture(
    "pass" if key == ("pass", None) else ("fixture-ragdoll" if key == ("kill-fixture", None) else f"focus-attack-attempt-{key[1]}"),
    action, raw_path, raw,
) for key, action, raw_path, raw in focus_inputs]
no_focus_captures = [record_capture(
    "ordinary-pass" if key == ("pass", None) else f"ordinary-attack-attempt-{key[1]}",
    action, raw_path, raw,
) for key, action, raw_path, raw in no_focus_inputs]

validation = {
    "status": "fresh_catalog_413_original_binding_focus_hit_explicit_ragdoll_fixture_and_strict_ready",
    "session": FOCUS_SESSION,
    "enemy": focus["enemy"],
    "displayName": "Tidecrown Sovereign",
    "nativeChassis": "seaKing",
    "referenceRendererId": 121357,
    "rendererPath": "enSeaKing",
    "ownerInstanceId": renderer["ownerInstanceId"],
    "profileSha256": focus["profileSha256"],
    "assetHashes": focus["assetHashes"],
    "binaryPins": focus["binaryPins"],
    "portraitMarkerRegistration": registered[0]["portraitMarkerPath"],
    "binding": review["binding"],
    "focusHit": review["focusHit"],
    "noFocusDiagnostic": {
        **review["noFocusDiagnostic"],
        "batch": pin(NO_FOCUS_BATCH),
        "captures": no_focus_captures,
    },
    "focusTrial": {
        "batch": pin(FOCUS_BATCH),
        "captures": focus_captures,
        "boundedRetrySummary": focus["attackRetrySummary"],
    },
    "fixtureDeath": review["fixtureDeath"],
    "progression": review["progression"],
    "rootVisualReview": relative(REVIEW),
    "selectedPNGs": selected,
    "videos": videos,
    "historicalSourceSnapshots": [{
        "source": relative(MANIFEST),
        "snapshotSha256": sha(MANIFEST),
        "reason": "The root manifest is finalized with this archive's hash afterward, so this lossless metadata copy is the capture-time manifest rather than an assertion about its later current contents.",
    }],
    "sourceImageCount": len(image_pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(f'''# Tidecrown Sovereign exact Sea King live validation V1

This archive records the fresh catalog-413 focus supplement in session `{FOCUS_SESSION}`. The exact `seaKing` / `enSeaKing` owner bound `ftkmf_glb_tidecrown-sea-king.glb` with its authored PNG texture, the expected 60-bone signature, native `seaKingController`, Standard `matLoot (Instance)` with emission disabled, and the native external trident. A bounded native `Attack(focus)` retry produced one measured same-target hit on attempt four, changing HP from 720 to 719. That fact is target-HP evidence only; no combat cause beyond the accepted recorded action is inferred.

The explicit `KillSingle` fixture retained 91 fixed-step frames before the custom renderer was destroyed. The raw capture shows ragdoll dynamics beginning at frame 28, with 12 recorded dynamic bodies at frame 30 and 11 at frame 60; the exact renderer is inactive and invisible in the final retained frame. This is fixture ragdoll evidence, not ordinary lethal-damage, settled-corpse, or final-resource-disposal proof. The guarded sequence reached native strict Ready at level 0 room 2 without a Collect submission in this fixture.

The separate fresh no-focus diagnostic is preserved in full metadata: all eight bounded ordinary attacks kept the exact 720-HP target unchanged and remain `no_hp_loss_unclassified`. It is a controller outcome boundary, not a model failure and not evidence of a cause such as a block or dodge.

`selected/` has eight root-reviewed source PNGs. `presentation/` contains the 120-frame focus-hit and 91-frame fixture-ragdoll videos at 12 fps; these are display derivatives, while every source screenshot is independently pinned in `source-image-pins.json`. `metadata/` contains lossless non-payload source records. The root manifest captured under `metadata/` is explicitly historical because finalizing this archive updates the live-evidence hash. `archive.py` refuses to overwrite a completed validation.
''')
print(json.dumps({
    "validation": relative(OUT / "validation.json"), "validationSha256": sha(OUT / "validation.json"),
    "sourceImages": len(image_pins), "losslessMappings": len(mappings), "selected": len(selected), "videos": len(videos),
}, indent=2))
