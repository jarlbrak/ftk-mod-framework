#!/usr/bin/env python3
"""Archive scoped Duneshade live evidence without copying game payloads."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments/duneshade-desert-asp"
OUT = ASSET / "live-validation-v1"
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
CASE = BASE / "case-faa61b12f786493ba1cab23892877e01/case-result.json"
STAGE = BASE / "case-2dd23efe0bd64896897963d7af435934/result.json"
BATCH = ROOT / "scratch/coverage-batch-20260911-182833.json"
RECEIPT = ROOT / "scratch/snake-policy-and-desert-417/receipt.json"
DEPLOYMENT = ROOT / "scratch/mirewarden-game/deployment-backups/snake-policy-and-desert-417-20260911-181205/deployment.json"
PROFILE = "ftkmf_modeltest_duneshade_desert_asp"
RENDERER = "enDesertSnakeA"
MESH = "ftkmf_glb_duneshade-desert-asp.glb"
BONES = "2e981c4730eb3c504e878be0f72fdeb50be7adc5e987d5377852d4c23f2e1d23"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def rel(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def pin(path: Path) -> dict:
    return {"source": rel(path), "sha256": sha(path), "bytes": path.stat().st_size}


def path_from(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def state_names(frame: dict) -> list[str]:
    return [str(clip.get("name")) for layer in ((frame.get("animator") or {}).get("layers") or [])
            for clip in (layer.get("playing") or [])]


def dynamic_count(frame: dict) -> int:
    return sum(body.get("isKinematic") is False for body in ((frame.get("ragdoll") or {}).get("rigidbodies") or []))


def image_pin(path: Path) -> dict:
    assert path.is_file() and path.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), path
    return {"sha256": sha(path), "bytes": path.stat().st_size}


def contact_sheet(raw_path: Path, title: str, frames: list[int], output: Path) -> dict:
    panels = []
    for index in frames:
        source = raw_path.with_suffix("") / f"{index:04d}.png"
        image = Image.open(source).convert("RGB")
        image.thumbnail((480, 270))
        panel = Image.new("RGB", (500, 310), (20, 20, 24))
        panel.paste(image, ((500 - image.width) // 2, 28))
        ImageDraw.Draw(panel).text((12, 7), f"{title} — frame {index}", fill=(245, 245, 245))
        panels.append(panel)
    columns = 3
    rows = (len(panels) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * 500, rows * 310), (8, 8, 10))
    for index, panel in enumerate(panels):
        sheet.paste(panel, ((index % columns) * 500, (index // columns) * 310))
    sheet.save(output)
    return {"archive": str(output.relative_to(OUT)), "sha256": sha(output), "bytes": output.stat().st_size,
            "sourceFrames": {str(index): {"source": rel(raw_path.with_suffix("") / f"{index:04d}.png"),
                                           **image_pin(raw_path.with_suffix("") / f"{index:04d}.png")}
                             for index in frames}}


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
case = read(CASE)
stage = read(STAGE)
batch = read(BATCH)
receipt = read(RECEIPT)
deployment = read(DEPLOYMENT)
assert case["schema"] == "ftkmf.exercise-case.v1" and case["status"] == "needs_visual_review"
assert case["session"] == "3abc7cb63c304703b4af61519f958feb"
assert case["enemy"] == PROFILE and case["rendererPath"] == RENDERER and case["focusedAttack"] is False
assert case["profileSha256"] == receipt["catalog"]["sha256"] == deployment["new"]["model-test-profiles.json"]
assert deployment["status"] == "VERIFIED_COMPLETE"
assert len(batch) == 1 and batch[0]["case"] == CASE.parent.name and batch[0]["status"] == "needs_visual_review"
assert batch[0]["stageCase"] == STAGE.parent.name and batch[0]["session"] == case["session"]
assert stage["session"] == case["session"] and stage["status"] == "binding_metadata_observed"

renderer = case["initialRenderer"]
assert renderer["mesh"] == MESH and renderer["boneSignature"] == BONES
assert renderer["celRelativeRendererPath"] == RENDERER and renderer["ownerKind"] == "enemies"
assert renderer["rootBone"] == "Root_M" and renderer["active"] is True and renderer["isVisible"] is True and renderer["enabled"] is True
assert renderer["materials"][0]["_MainTex"]["name"] == "ftkmf_duneshade-desert-asp_basecolor.png"
assert renderer["materials"][0]["emissionKeyword"] is False and renderer["materials"][0]["_EmissionMap"] is None
matrix = renderer["rendererLocalToWorld"]
assert abs(matrix[0] - .65) < .01 and abs(matrix[5] - .65) < .01 and abs(matrix[10] - .65) < .01
for name, digest in case["assetHashes"].items():
    assert sha(ASSET / name) == digest
for key, source in (("framework", "FTKModFramework.dll"), ("helper", "FtkRuntimeModelTest.dll")):
    assert case["binaryPins"][key]["sha256"] == deployment["new"][f"BepInEx/plugins/{source}"]

by_action = {(entry["action"], entry.get("attempt")): entry for entry in case["actions"]}
assert set(by_action) == {("pass", None), ("attack", 1), ("kill-fixture", None)}
raws = {}
for key, action in by_action.items():
    capture = action["capture"]
    raw_entry = capture["rawCapture"]
    raw_path = path_from(raw_entry["path"])
    assert raw_path.is_file() and sha(raw_path) == raw_entry["sha256"]
    raw = read(raw_path)
    assert raw["ok"] is True and raw["session"] == case["session"] and len(raw["frames"]) == 120
    assert capture["boundary"]["completeCapture"] is True and capture["boundary"]["retainedFrameCount"] == 120
    assert all(frame.get("mesh") == MESH and frame.get("boneSignature") == BONES
               and frame.get("ownerInstanceId") == renderer["ownerInstanceId"]
               and frame.get("active") is True and frame.get("isVisible") is True and frame.get("enabled") is True
               for frame in raw["frames"])
    raws[key] = (raw_path, raw)

attack = by_action[("attack", 1)]
assert attack["actionResult"]["result"]["committed"] == "Attack"
assert attack["hpOutcome"]["status"] == "nonlethal_hp_loss"
assert (attack["hpOutcome"]["beforeHp"], attack["hpOutcome"]["afterHp"]) == (58, 48)
attack_frames = raws[("attack", 1)][1]["frames"]
assert any("Snake_HitSmall" in state_names(frame) and frame.get("lastCombatTrigger") == "Damaged" for frame in attack_frames)

death = by_action[("kill-fixture", None)]
assert death["actionResult"]["result"]["committed"] == "KillSingle"
death_path, death_raw = raws[("kill-fixture", None)]
death_frames = death_raw["frames"]
first_death = next(index for index, frame in enumerate(death_frames) if any(name.startswith("Snake_Death") for name in state_names(frame)))
assert first_death == 27
assert len(((death_frames[first_death].get("ragdoll") or {}).get("rigidbodies") or [])) == 13
assert dynamic_count(death_frames[first_death]) == 0
assert dynamic_count(death_frames[first_death + 1]) == 13
assert all(dynamic_count(frame) == 13 for frame in death_frames[first_death + 1:])

assert len(case["collects"]) == 2
assert all(item["result"]["status"] == "clicked" and item["result"]["method"] == "VoteButton.OnLeftClick(Collect)"
           and all(item["result"].get(k) == v for k, v in item["before"].items()) for item in case["collects"])
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

sources = {
    CASE, CASE.with_name("journal.jsonl"), STAGE, STAGE.with_name("journal.jsonl"), BATCH, RECEIPT, DEPLOYMENT,
    ROOT / "scratch/stage-snake-policy-and-desert-417.py", ROOT / "scratch/deploy-snake-policy-and-desert-417.py",
    ROOT / "scratch/run-coverage-batch.py", ASSET / "README.md", ASSET / "runtime-profile.json",
    ASSET / "finalize_manifest.py", ASSET / "stage_runtime_profile.py", ASSET / "original-geometry-proof.json",
    ASSET / "target-binding-proof.json", OUT / "README.md", OUT / "archive.py",
}
for action in by_action.values():
    raw_result = path_from(action["rawResult"]["path"])
    journal = path_from(action["journal"]["path"])
    raw_path = path_from(action["capture"]["rawCapture"]["path"])
    for source, entry in ((raw_result, action["rawResult"]), (journal, action["journal"]), (raw_path, action["capture"]["rawCapture"])):
        assert source.is_file() and sha(source) == entry["sha256"], source
        sources.add(source)
    summary = raw_result.with_name("capture-summary.json")
    if summary.is_file(): sources.add(summary)
for source in sources:
    assert source.is_file() and not source.is_symlink(), source

# Pin every original screenshot but retain only the reviewed selections.
image_pins = {}
for raw_path, raw in raws.values():
    folder = raw_path.with_suffix("")
    for index in range(len(raw["frames"])):
        image = folder / f"{index:04d}.png"
        image_pins[rel(image)] = image_pin(image)

excluded = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d", ".blend", ".glb"}
mappings = []
for source in sorted(sources):
    assert source.suffix.lower() not in excluded
    raw = source.read_bytes()
    destination = OUT / "metadata" / (rel(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    mappings.append({"source": rel(source), "sourceSha256": hashlib.sha256(raw).hexdigest(),
                     "archive": str(destination.relative_to(OUT)), "archiveSha256": sha(destination), "encoding": "gzip-lossless"})
write(OUT / "source-image-pins.json", image_pins)

reviewed = OUT / "reviewed-frames"
reviewed.mkdir(exist_ok=True)
pass_sheet = contact_sheet(raws[("pass", None)][0], "native pass", [12, 24], reviewed / "pass.png")
attack_sheet = contact_sheet(raws[("attack", 1)][0], "ordinary attack", [6, 12, 24, 27, 30], reviewed / "attack.png")
death_sheet = contact_sheet(death_path, "KillSingle ragdoll fixture", [0, 24, 27, 28, 40, 60, 119], reviewed / "death.png")
review = {
    "status": "ROOT_REVIEWED_LIMITED_FRAME_SETS",
    "reviewedBy": "Codex root agent",
    "reviewedFrames": {"pass": pass_sheet, "attack": attack_sheet, "death": death_sheet},
    "observations": [
        "The original moon-violet/teal/orange serpent remains coherent and visibly bound in the pass and ordinary attack samples.",
        "The attack capture contains the native Snake_HitSmall/Damaged segment while preserving the exact custom renderer identity.",
        "The explicit death fixture keeps the full custom silhouette visible as the ordinary Desert A skeleton ragdoll becomes dynamic and settles; no native fragment substitution is visible in the selected frames.",
    ],
    "limitations": "Selected screenshots are a limited visual review only. They do not prove all motion, all camera angles, portrait rendering, material lifetime, or finished-art acceptance.",
}
write(OUT / "visual-review-v1.json", review)
asset_pins = {name: sha(ASSET / name) for name in ("duneshade-desert-asp.glb", "duneshade-desert-asp_basecolor.png")}
write(OUT / "asset-pins.json", asset_pins)
validation = {
    "status": "fresh_catalog_417_exact_binding_nonlethal_hit_explicit_ragdoll_fixture_and_strict_ready",
    "scope": "Exact original Duneshade binding in a fresh isolated FTK session. KillSingle is an explicit fixture, not ordinary lethal damage.",
    "session": case["session"], "enemy": PROFILE, "displayName": "Duneshade Asp", "nativeChassis": "snakeDesertA",
    "referenceRendererId": 121552, "rendererPath": RENDERER, "ownerInstanceId": renderer["ownerInstanceId"],
    "profileSha256": case["profileSha256"], "assetHashes": asset_pins, "binaryPins": case["binaryPins"],
    "deployment": {"receipt": pin(RECEIPT), "deployment": pin(DEPLOYMENT), "batch": pin(BATCH)},
    "binding": renderer,
    "ordinaryAttack": {"action": "Attack", "sameTargetHp": attack["hpOutcome"],
                       "meaning": "One accepted ordinary action measured target HP 58 to 48. This is same-target HP evidence only; no combat cause is inferred."},
    "explicitDeathFixture": {"action": "KillSingle", "capture": pin(death_path), "firstNativeDeathFrame": first_death,
                              "firstDynamicFrame": first_death + 1, "rigidbodyCount": 13,
                              "allFramesCustomBodyActiveVisibleEnabled": True,
                              "finalFrame": {"frame": 119, "dynamicBodies": dynamic_count(death_frames[-1]),
                                             "active": death_frames[-1]["active"], "visible": death_frames[-1]["isVisible"]}},
    "nativeProgression": {"collects": case["collects"], "finalReady": case["finalReady"]["strictReady"]},
    "visualReview": review,
    "limitations": [
        "The death segment is explicit KillSingle fixture evidence, not ordinary lethal-damage evidence.",
        "The ordinary attack establishes same-target HP loss only; it does not infer a hit, block, damage source, or animation completion from that measurement.",
        "This archive does not establish portrait pixels, all-angle culling, material lifetime, corpse presentation quality, or full campaign progression.",
    ],
    "metadata": mappings, "sourceImages": pin(OUT / "source-image-pins.json"),
}
write(OUT / "validation.json", validation)
print(json.dumps({"archive": str(OUT), "status": validation["status"], "metadata": len(mappings), "sourceImages": len(image_pins)}, indent=2))
