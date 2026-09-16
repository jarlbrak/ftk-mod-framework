#!/usr/bin/env python3
"""Archive scoped Bramblecoil live evidence without copying game payloads."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments/bramblecoil-jungle-snake"
OUT = ASSET / "live-validation-v1"
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
SEMANTIC_SUMMARY = ROOT / "scratch/bramblecoil-deathlight-trigger-v1-20260911-183256.json"
OPTED_SUMMARY = ROOT / "scratch/bramblecoil-falloff-opted-v2-20260911-183817.json"
CONTROL_SUMMARY = ROOT / "scratch/bramblecoil-falloff-native-control-v2-20260911-184006.json"
CLEANUP_SUMMARY = ROOT / "scratch/bramblecoil-falloff-cleanup-v1-20260911-184717.json"
PAIR_REPORT = ASSET / "falloff-policy-comparison-v2.json"
RECEIPT = ROOT / "scratch/bramblecoil-policy-control-idle-gate-419/receipt.json"
MESH = "ftkmf_glb_bramblecoil-jungle-snake.glb"
PROFILE = "ftkmf_modeltest_bramblecoil_jungle_snake"
CONTROL = "ftkmf_modeltest_bramblecoil_jungle_snake_native_falloff_control"
POLICY = "preserve-custom-body"


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


def source_path(entry: dict) -> Path:
    value = Path(entry["path"])
    return value if value.is_absolute() else ROOT / value


def assert_pin(entry: dict) -> Path:
    path = source_path(entry)
    assert path.is_file(), path
    assert sha(path) == entry["sha256"], path
    return path


def raw_capture(summary: dict) -> tuple[Path, dict]:
    path = assert_pin(summary["capture"])
    raw = read(path)
    assert raw.get("ok") is True and raw.get("session") == summary["session"]
    return path, raw


def raw_images(path: Path, frames: list[int]) -> dict:
    directory = path.with_suffix("")
    result = {}
    for frame in frames:
        image = directory / f"{frame:04d}.png"
        assert image.is_file() and image.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), image
        result[str(frame)] = pin(image)
    return result


def contact_sheet(path: Path, label: str, frames: list[int], destination: Path) -> dict:
    panels = []
    for frame in frames:
        source = path.with_suffix("") / f"{frame:04d}.png"
        image = Image.open(source).convert("RGB")
        image.thumbnail((480, 270))
        panel = Image.new("RGB", (500, 310), (20, 20, 24))
        panel.paste(image, ((500 - image.width) // 2, 28))
        ImageDraw.Draw(panel).text((12, 7), f"{label} — frame {frame}", fill=(245, 245, 245))
        panels.append(panel)
    columns = 3
    rows = (len(panels) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * 500, rows * 310), (8, 8, 10))
    for index, panel in enumerate(panels):
        sheet.paste(panel, ((index % columns) * 500, (index // columns) * 310))
    sheet.save(destination)
    return {"archive": str(destination.relative_to(OUT)), "sha256": sha(destination), "bytes": destination.stat().st_size,
            "sourceFrames": raw_images(path, frames)}


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
semantic_summary = read(SEMANTIC_SUMMARY)
opted_summary = read(OPTED_SUMMARY)
control_summary = read(CONTROL_SUMMARY)
cleanup_summary = read(CLEANUP_SUMMARY)
pair = read(PAIR_REPORT)
receipt = read(RECEIPT)

assert semantic_summary["status"] == "PASS_SEMANTIC_DEATHLIGHT_TRIGGER_FIXTURE"
assert cleanup_summary["status"] == "PASS_EXPLICIT_FALLOFF_FIXTURE_AND_NATIVE_COLLECT_READY"
assert pair["status"] == "PASS_PAIRED_EXPLICIT_FALLOFF_POLICY_FIXTURE"
assert opted_summary["profile"] == cleanup_summary["profile"] == semantic_summary["profile"] == PROFILE
assert control_summary["profile"] == CONTROL
assert semantic_summary["expectedPolicy"] == cleanup_summary["expectedPolicy"] == POLICY
assert opted_summary["registrationEntry"]["fallOffPolicy"] == POLICY
assert control_summary["registrationEntry"].get("fallOffPolicy") is None
assert cleanup_summary["finalReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert len(cleanup_summary["collects"]) == 1
assert cleanup_summary["collects"][0]["result"]["method"] == "VoteButton.OnLeftClick(Collect)"
assert pair["sharedDeployment"]["receipt"]["sha256"] == sha(RECEIPT)
assert set(receipt["newPluginDlls"]) == set(pair["sharedDeployment"]["plugins"])
for name, item in receipt["newPluginDlls"].items():
    assert item["sha256"] == pair["sharedDeployment"]["plugins"][name]["sha256"], name
assert pair["sharedDeployment"]["catalog"]["sha256"] == receipt["catalog"]["sha256"]

semantic_raw_path, semantic_raw = raw_capture(semantic_summary)
opted_raw_path, opted_raw = raw_capture(opted_summary)
control_raw_path, control_raw = raw_capture(control_summary)
cleanup_raw_path, cleanup_raw = raw_capture(cleanup_summary)
assert len(semantic_raw["frames"]) == 24 and len(opted_raw["frames"]) == len(control_raw["frames"]) == len(cleanup_raw["frames"]) == 120
assert semantic_summary["captureVerdict"] == {
    "frames": 24,
    "firstGameSeconds": 0.0,
    "lastGameSeconds": semantic_raw["frames"][-1]["gameSeconds"],
    "allFramesExactCustomBody": True,
    "allFramesDeathLight": True,
    "allFramesVisible": True,
}
assert all(frame.get("mesh") == MESH and frame.get("lastCombatTrigger") == "DeathLight"
           and frame.get("active") is True and frame.get("isVisible") is True
           for frame in semantic_raw["frames"])
assert all(frame.get("mesh") == MESH for raw in (opted_raw, control_raw, cleanup_raw) for frame in raw["frames"])

# The nonvolatile stage/fixture records and journals preserve the original
# registration snapshots even though the isolated game's current report is
# rewritten by each fresh launch.
sources = {
    SEMANTIC_SUMMARY, OPTED_SUMMARY, CONTROL_SUMMARY, CLEANUP_SUMMARY, PAIR_REPORT, RECEIPT,
    ASSET / "README.md", ASSET / "runtime-profile.json", ASSET / "death-handoff-analysis-v1.json",
    ASSET / "run_deathlight_trigger_v1.py", ASSET / "run_falloff_fixture_v2.py",
    ASSET / "verify_falloff_fixture_v2.py", ASSET / "run_falloff_cleanup_v1.py",
    ASSET / "finalize_manifest.py", OUT / "README.md", OUT / "archive.py",
    ROOT / "scratch/stage-bramblecoil-policy-control-idle-gate.py",
    ROOT / "scratch/deploy-bramblecoil-policy-control-idle-gate.py",
    semantic_raw_path, opted_raw_path, control_raw_path, cleanup_raw_path,
}
for summary in (semantic_summary, opted_summary, control_summary, cleanup_summary):
    for key in ("stage", "fixture"):
        if key in summary:
            path = assert_pin(summary[key])
            sources.add(path)
            journal = path.with_name("journal.jsonl")
            if journal.is_file():
                sources.add(journal)
    if "cleanupJournal" in summary:
        sources.add(assert_pin(summary["cleanupJournal"]))
for command_key in ("inventory", "materialState"):
    if command_key in semantic_summary:
        sources.add(assert_pin(semantic_summary[command_key]))
for source in sources:
    assert source.is_file() and not source.is_symlink(), source

# Raw screenshots stay outside metadata. Pin all available image frames for
# the four captures, then copy only the manually reviewed selections.
image_pins = {}
for raw_path, raw in ((semantic_raw_path, semantic_raw), (opted_raw_path, opted_raw),
                      (control_raw_path, control_raw), (cleanup_raw_path, cleanup_raw)):
    directory = raw_path.with_suffix("")
    assert directory.is_dir()
    for index in range(len(raw["frames"])):
        image = directory / f"{index:04d}.png"
        assert image.is_file() and image.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), image
        image_pins[rel(image)] = {"sha256": sha(image), "bytes": image.stat().st_size}

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
                     "archive": str(destination.relative_to(OUT)), "archiveSha256": sha(destination),
                     "encoding": "gzip-lossless"})
write(OUT / "source-image-pins.json", image_pins)

reviewed = OUT / "reviewed-frames"
reviewed.mkdir(exist_ok=True)
semantic_sheet = contact_sheet(semantic_raw_path, "semantic DeathLight", [0, 11, 23], reviewed / "semantic-deathlight.png")
opted_sheet = contact_sheet(opted_raw_path, "opted normal Death", [20, 25, 27, 28, 40, 60, 119], reviewed / "opted-normal-death.png")
control_sheet = contact_sheet(control_raw_path, "omitted-policy control", [20, 25, 27, 28, 40, 60, 119], reviewed / "native-control-normal-death.png")
cleanup_sheet = contact_sheet(cleanup_raw_path, "opted cleanup fixture", [0, 27, 28, 60, 119], reviewed / "opted-cleanup.png")
review = {
    "status": "ROOT_REVIEWED_LIMITED_FRAME_SETS",
    "reviewedBy": "Codex root agent",
    "reviewedFrames": {"semanticDeathLight": semantic_sheet, "optedNormalDeath": opted_sheet,
                       "nativeControlNormalDeath": control_sheet, "optedCleanup": cleanup_sheet},
    "observations": [
        "The semantic sheet keeps the jade-and-teal custom serpent visibly rendered throughout the bounded DeathLight capture.",
        "The opted normal-death sheet keeps the custom serpent visible as its native ragdoll settles; the omitted-policy control instead shows the native fall-off fragments after the custom body disappears.",
        "The opted cleanup sheet keeps the custom serpent visible into the victory/loot presentation before the guarded native Collect vote reaches strict Ready.",
    ],
    "limitations": "These selected screenshots support only a limited visual review. They do not prove every animation instant, camera angle, portrait, material lifetime, or finished-art acceptance.",
}
write(OUT / "visual-review-v1.json", review)

asset_pins = {name: sha(ASSET / name) for name in ("bramblecoil-jungle-snake.glb", "bramblecoil-jungle-snake_basecolor.png")}
write(OUT / "asset-pins.json", asset_pins)
validation = {
    "status": "fresh_catalog_419_opted_falloff_policy_semantic_deathlight_paired_normal_death_and_native_ready",
    "scope": "Exact original Bramblecoil binding on isolated FTK only. KillSingle remains an explicit fixture, not ordinary lethal combat.",
    "enemy": PROFILE,
    "nativeChassis": "snakeJungleC",
    "referenceRendererId": 121424,
    "rendererPath": "enJungleSnakeC",
    "assetHashes": asset_pins,
    "sharedDeployment": pair["sharedDeployment"],
    "semanticDeathLight": {
        "summary": pin(SEMANTIC_SUMMARY), "session": semantic_summary["session"], "capture": pin(semantic_raw_path),
        "provenance": semantic_raw["provenance"], "trigger": semantic_raw["combatTrigger"],
        "verdict": semantic_summary["captureVerdict"],
    },
    "pairedNormalDeath": {
        "report": pin(PAIR_REPORT), "opted": pair["opted"], "nativeControl": pair["nativeControl"],
        "observedDifference": pair["observedDifference"],
    },
    "nativeCleanup": {
        "summary": pin(CLEANUP_SUMMARY), "session": cleanup_summary["session"], "capture": pin(cleanup_raw_path),
        "startDungeon": cleanup_summary["startDungeon"], "collects": cleanup_summary["collects"],
        "finalReady": cleanup_summary["finalReady"], "postDeathBody": cleanup_summary["postDeathBody"],
    },
    "visualReview": review,
    "limitations": [
        "No ordinary attack hit, ordinary lethal-damage, or native combat-cause claim is made; the existing ordinary and focus attack diagnostics recorded no same-target HP loss.",
        "The fixture and cleanup evidence does not prove portrait pixels, all-angle culling, material lifetime, corpse presentation quality, or full campaign progression.",
        "The policy is intentionally narrow: it only protects an exact explicitly leased FallOffLimb renderer; unrelated renderers retain native behavior.",
    ],
    "metadata": mappings,
    "sourceImages": pin(OUT / "source-image-pins.json"),
}
write(OUT / "validation.json", validation)
print(json.dumps({"archive": str(OUT), "status": validation["status"], "metadata": len(mappings), "sourceImages": len(image_pins)}, indent=2))
