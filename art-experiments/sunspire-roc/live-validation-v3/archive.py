#!/usr/bin/env python3
"""Archive the fresh constructed-native portrait supplement for Sunspire Roc."""
import gzip
import hashlib
import json
import os
import shutil
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments/sunspire-roc"
OUT = ASSET / "live-validation-v3"
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
SESSION = "1ce25c30c43f41428094cefea08dae6b"
REPORT = ROOT / "scratch/sunspire-roc-portrait-v1.json"
FIXTURE = BASE / "a3d6778fbdd846958c2db0aecb29f53f.json"
WATCH = BASE / "1a6a8aad908d4f0d92e37d48600bd078.json"
CASE = BASE / "case-fa687fe569c544eeb50b43b36497cff7/case-result.json"
JOURNAL = CASE.with_name("journal.jsonl")
SESSION_RECORD = BASE / f"new-run-session-{SESSION}.json"
REVIEW = ROOT / "scratch/sunspire-roc-root-portrait-review-v3.json"
REVIEW_WRITER = ASSET / "write_live_review_v3.py"
PROFILE = ROOT / "scratch/mirewarden-game/model-test-profiles.json"
REGISTRATION = ROOT / "scratch/mirewarden-game/model-test-registration.json"
MANIFEST = ASSET / "manifest.json"
RUNTIME_PROFILE = ASSET / "runtime-profile.json"
V1 = ASSET / "live-validation-v1/validation.json"
V2 = ASSET / "live-validation-v2/validation.json"
STAGE = ROOT / "scratch/runtime-profile-412-sunspire-roc"
DEPLOYMENT = ROOT / "scratch/mirewarden-game/deployment-backups/sunspire-roc-412-20260911-154046/deployment.json"
RUNNER = ROOT / "scratch/run-sunspire-roc-portrait-v1.py"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
OUT.mkdir(parents=True, exist_ok=True)
report = read(REPORT)
fixture = read(FIXTURE)
watch = read(WATCH)
case = read(CASE)
review = read(REVIEW)
session_record = read(SESSION_RECORD)
manifest = read(MANIFEST)
registration = read(REGISTRATION)
assert report["session"] == SESSION and report["profile"] == "ftkmf_modeltest_sunspire_roc"
assert report["rendererPath"] == "enRoc01"
assert report["exercise"] == {"path": str(CASE), "sha256": sha(CASE)}
assert report["portraitWatch"]["path"] == str(WATCH) and report["portraitWatch"]["sha256"] == sha(WATCH)
assert report["nativeRowFixture"]["path"] == str(FIXTURE) and report["nativeRowFixture"]["sha256"] == sha(FIXTURE)
assert fixture["ok"] is True and fixture["error"] is None
assert fixture["nativeInitializeInvoked"] is True and fixture["nativeInitializeReturned"] is True
assert fixture["sourceUnchanged"] is True
assert fixture["cleanup"]["complete"] is True
assert fixture["cleanup"]["nativeCloneUnityNull"] is True
assert fixture["cleanup"]["ownedTextureUnityNull"] is True
assert fixture["cleanup"]["ownedUiUnityNull"] is True
assert fixture["leaseObservation"]["wasNew"] is True
assert fixture["leaseObservation"]["leaseAbsent"] is True
assert fixture["leaseObservation"]["allPinnedResourcesUnityNull"] is True
assert fixture["nativeDimensions"]["cameraKey"] == "Portrait,328,280"
assert (fixture["nativeDimensions"]["width"], fixture["nativeDimensions"]["height"]) == (328, 280)
trace = fixture["passiveTrace"]
assert trace["row"] == report["profile"]
assert trace["identityResolution"] == "exact_row_forwarded_to_native_source"
assert trace["capturePoint"] == "Immediately before native DoRender, after native pose sampling and marker placement"
assert trace["marker"]["firstArgumentAtLastPrefix"] == "PortraitCam"
target = trace["target"]
assert target["name"] == "enRocA(Clone)" and target["nodeCount"] == 55 and len(target["renderers"]) == 1
renderer = target["renderers"][0]
assert renderer["path"] == "enRoc01" and renderer["boneCount"] == 36
assert renderer["meshName"] == "ftkmf_glb_sunspire-roc.glb"
assert renderer["sharedMaterials"][0]["mainTexture"]["name"] == "ftkmf_sunspire-roc_basecolor.png"
assert watch["ok"] is True and watch["session"] == SESSION
assert watch["records"] == [] and watch["provenance"] == "passive_native_portrait_before_DoRender"
assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["session"] == SESSION and case["enemy"] == report["profile"]
assert case["rendererPath"] == "enRoc01" and case["focusedAttack"] is False
assert case["status"] == "needs_visual_review" and case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert session_record["session"] == SESSION
assert sha(PROFILE) == case["profileSha256"] == report["catalogSha256"]
for name, digest in manifest["files"].items():
    assert sha(ASSET / name) == digest, name
for name, digest in case["assetHashes"].items():
    assert sha(ASSET / name) == digest, name
registered = [row for row in registration["registered"] if row["key"] == case["enemy"]]
assert registration["status"] == "registered" and len(registered) == 1
assert registered[0]["portraitMarkerPath"] == "Root_M/BackA_M/BackB_M/Chest_M/Neck_M/Head_M/PortraitCam"
assert review["session"] == SESSION and review["nativeChassis"] == "rocA"
assert review["fixture"]["sha256"] == sha(FIXTURE)
png = report["nativeRowFixture"]["png"]
png_path = Path(png["path"])
assert png_path.is_file() and png["sha256"] == sha(png_path)
assert (png["width"], png["height"]) == (328, 280)

actions = {action["action"]: action for action in case["actions"]}
assert set(actions) == {"pass", "attack", "kill-fixture"}
assert actions["attack"]["focus"] is False
assert actions["attack"]["actionResult"]["result"]["committed"] == "Attack"
assert actions["attack"]["hpOutcome"]["status"] == "nonlethal_hp_loss"
assert (actions["attack"]["hpOutcome"]["beforeHp"], actions["attack"]["hpOutcome"]["afterHp"]) == (81, 71)
assert actions["kill-fixture"]["actionResult"]["result"]["committed"] == "KillSingle"

sources = {
    REPORT, FIXTURE, WATCH, CASE, JOURNAL, SESSION_RECORD, REVIEW, REVIEW_WRITER,
    PROFILE, REGISTRATION, MANIFEST, RUNTIME_PROFILE, V1, V2,
    STAGE / "receipt.json", STAGE / "stage-script.py", DEPLOYMENT, RUNNER,
    ASSET / "sunspire-roc.glb", ASSET / "sunspire-roc_basecolor.png", png_path, Path(__file__),
}
setup_journal = Path(session_record["journal"])
sources.add(setup_journal)
setup_result = BASE / f"case-{session_record['case']}" / "result.json"
if setup_result.is_file():
    sources.add(setup_result)

# Preserve metadata losslessly and preserve only the reviewed portrait PNG itself.
# No game binary, bundle, or unreviewed combat pixel sequence enters this archive.
pending_journals = {path for path in sources if path.suffix == ".jsonl"}
seen_journals: set[Path] = set()
excluded = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
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

image_pins, mappings = {}, []
for source in sorted(sources):
    raw = source.read_bytes()
    source_relative = relative(source)
    if source.suffix.lower() == ".png":
        image_pins[source_relative] = hashlib.sha256(raw).hexdigest()
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
write(OUT / "asset-pins.json", case["assetHashes"])

portrait_destination = OUT / "selected/native-row-portrait.png"
portrait_destination.parent.mkdir(parents=True, exist_ok=True)
shutil.copy2(png_path, portrait_destination)
assert sha(portrait_destination) == png["sha256"]

validation = {
    "status": "fresh_catalog_412_ordinary_body_evidence_and_constructed_native_row_portrait_complete",
    "session": SESSION,
    "enemy": case["enemy"],
    "displayName": "Sunspire Roc",
    "nativeChassis": "rocA",
    "rendererPath": "enRoc01",
    "profileSha256": case["profileSha256"],
    "assetHashes": case["assetHashes"],
    "binaryPins": case["binaryPins"],
    "portraitMarkerRegistration": registered[0]["portraitMarkerPath"],
    "ordinaryBodyEvidence": {
        "source": relative(CASE),
        "sourceSha256": sha(CASE),
        "ordinaryNoFocusHit": "OBSERVED: one fresh native Attack without focus changed the same exact target from 81 to 71 HP.",
        "explicitKillFixture": "COMPLETE_120_FRAME_CAPTURE; this is not ordinary lethal damage.",
        "finalReady": case["finalReady"]["strictReady"],
        "limits": case["limitations"],
    },
    "portraitWatch": {
        "source": relative(WATCH),
        "sourceSha256": sha(WATCH),
        "provenance": watch["provenance"],
        "recordsBeforeFixture": len(watch["records"]),
        "meaning": "The passive observer was empty before the constructed fixture; identity evidence below comes from the native fixture trace.",
    },
    "nativeRowPortraitFixture": {
        "source": relative(FIXTURE),
        "sourceSha256": sha(FIXTURE),
        "nativeMethod": "uiEnemyEncounterPortrait.Initialize",
        "nativeInitializeInvoked": fixture["nativeInitializeInvoked"],
        "nativeInitializeReturned": fixture["nativeInitializeReturned"],
        "capturePoint": trace["capturePoint"],
        "identityResolution": trace["identityResolution"],
        "markerArgument": trace["marker"]["firstArgumentAtLastPrefix"],
        "targetBinding": {
            "name": target["name"],
            "nodeCount": target["nodeCount"],
            "rendererPath": renderer["path"],
            "boneCount": renderer["boneCount"],
            "meshName": renderer["meshName"],
            "material": renderer["sharedMaterials"][0]["name"],
            "textureName": renderer["sharedMaterials"][0]["mainTexture"]["name"],
        },
        "nativeDimensions": fixture["nativeDimensions"],
        "nativeCameraCache": fixture["nativeCameraCache"],
        "sourceUnchanged": fixture["sourceUnchanged"],
        "leaseObservation": fixture["leaseObservation"],
        "cleanup": fixture["cleanup"],
        "png": {
            "source": relative(png_path),
            "archive": str(portrait_destination.relative_to(OUT)),
            "sha256": sha(portrait_destination),
            "width": png["width"],
            "height": png["height"],
        },
    },
    "rootPortraitReview": relative(REVIEW),
    "historicalSourceSnapshots": [
        {
            "source": relative(MANIFEST),
            "snapshotSha256": sha(MANIFEST),
            "reason": "The root manifest is frozen after this archive is indexed, so this lossless metadata copy preserves the capture-time manifest rather than claiming to pin its later finalized state.",
        }
    ],
    "sourceImageCount": len(image_pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(f'''# Sunspire Roc native row-portrait supplement V3

This archive records fresh catalog-412 session `{SESSION}`. It first completed the same ordinary `rocA`/`enRoc01` body exercise used for V2: the authored Roc stayed on the exact profile, a normal no-focus native `Attack` reduced the target from 81 to 71 HP, the explicit `KillSingle` fixture captured 120 frames, and guarded native collection returned to strict Ready at level 0 room 2.

At that strict boundary, a fresh constructed native `uiEnemyEncounterPortrait.Initialize` caller made one row preview. Immediately before the native snapshot render, the trace forwarded the exact custom row to a cloned `enRoc01` with 36 bones, mesh `ftkmf_glb_sunspire-roc.glb`, and texture `ftkmf_sunspire-roc_basecolor.png`; the native `PortraitCam` marker was selected. Native initialization returned a 328 by 280 PNG. The reviewed image in `selected/` shows a clear beak, eye, faceted head, and neck mantle. The temporary clone, UI texture, and recorded preview lease assets were confirmed released; the native camera/cache remains native-owned and was only observed.

This is a constructed native UI caller, not an opened encounter menu or live combat HUD. It proves one exact native row-preview path and size, not every portrait layout, cache reuse path, or full art-direction acceptance. Whole-body animation and combat visibility remain in V1/V2. `metadata/` contains lossless non-payload records, `source-image-pins.json` pins every source PNG, and `archive.py` refuses to overwrite a completed result.

The archived root `manifest.json` is explicitly a capture-time historical snapshot: finalizing this V3 validation updates that manifest's own live-evidence hash afterward. Its compressed source hash remains verifiable inside this archive, but it is not expected to equal the later current manifest.
''')
print(json.dumps({
    "validation": relative(OUT / "validation.json"),
    "validationSha256": sha(OUT / "validation.json"),
    "sourceImages": len(image_pins),
    "losslessMappings": len(mappings),
    "portrait": str(portrait_destination.relative_to(OUT)),
}, indent=2))
