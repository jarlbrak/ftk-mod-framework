#!/usr/bin/env python3
"""Review and, only with --execute, deploy the V3 Kraken head static-eye stage."""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

MODELS_REL = Path("BepInEx/plugins/FTKModFramework_content/models")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def child(root: Path, name: str | Path) -> Path:
    relative = Path(name)
    assert not relative.is_absolute() and ".." not in relative.parts, relative
    path = root / relative
    assert path.resolve().is_relative_to(root.resolve()), path
    return path


def files(root: Path) -> dict[str, str]:
    return {path.name: sha(path) for path in sorted(root.iterdir()) if path.is_file()}


def repo_from_here() -> Path:
    return next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())


def assert_game_stopped(game: Path) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    commands = subprocess.check_output(["ps", "-axo", "command="], text=True).splitlines()
    assert not any(command == executable or command.startswith(executable + " ") for command in commands), "Isolated FTK is running; no files were changed."


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--repo", type=Path)
parser.add_argument("--game-root", type=Path, required=True)
parser.add_argument("--stage", type=Path, required=True)
parser.add_argument("--execute", action="store_true")
args = parser.parse_args()

REPO = args.repo.resolve() if args.repo else repo_from_here()
GAME = args.game_root.resolve()
STAGE = args.stage.resolve()
assert GAME.is_dir() and not GAME.is_symlink() and GAME.parent == REPO / "scratch", "Refusing a non-isolated game root."
assert STAGE.is_dir() and not STAGE.is_symlink() and STAGE.parent == REPO / "scratch", "Refusing a non-isolated stage."
assert_game_stopped(GAME)

receipt = read(STAGE / "receipt.json")
assert receipt["status"] == "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V3_HEAD_STATIC_EYE"
catalog = GAME / "model-test-profiles.json"
models = GAME / MODELS_REL
staged_catalog = STAGE / "model-test-profiles.json"
staged_models = STAGE / "models"
assert sha(catalog) == receipt["sourceCatalog"]["sha256"]
assert sha(staged_catalog) == receipt["catalog"]["sha256"]
assert files(models) == receipt["priorAssets"]
assert files(staged_models) == receipt["assetSha256"]
for name, digest in receipt["newAssets"].items():
    assert sha(staged_models / name) == digest and not (models / name).exists(), name

old = read(catalog)
new = read(staged_catalog)
assert {key: value for key, value in old.items() if key != "profiles"} == {key: value for key, value in new.items() if key != "profiles"}
assert len(old["profiles"]) == len(new["profiles"])
indexes = {replacement["index"] for replacement in receipt["replacements"]}
for replacement in receipt["replacements"]:
    index = replacement["index"]
    assert old["profiles"][index] == replacement["old"]
    assert new["profiles"][index] == replacement["new"]
assert all(new["profiles"][index] == row for index, row in enumerate(old["profiles"]) if index not in indexes)

changes: dict[str, Path] = {"model-test-profiles.json": staged_catalog}
changes.update({str(MODELS_REL / name): staged_models / name for name in sorted(receipt["newAssets"])})
expected = {name: sha(source) for name, source in changes.items()}
review = {
    "status": "PINNED_INPUTS_VERIFIED",
    "sourceCatalogSha256": receipt["sourceCatalog"]["sha256"],
    "candidateCatalogSha256": receipt["catalog"]["sha256"],
    "replacedKeys": [replacement["old"]["key"] for replacement in receipt["replacements"]],
    "changes": expected,
    "preservedProfileRows": receipt["preservedRowCount"],
    "preservedAssetCount": len(receipt["priorAssets"]),
    "retainedEarlierKrakenAssets": receipt["retainedEarlierKrakenAssets"],
    "liveAcceptance": False,
}
if not args.execute:
    print(json.dumps(review, indent=2))
    raise SystemExit(0)

assert_game_stopped(GAME)
backup = GAME / "deployment-backups" / ("abyssal-kraken-v3-head-static-eye-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
backup.mkdir(parents=True)
backup_names = [*changes, "model-test-session.json", "model-test-registration.json", "BepInEx/LogOutput.log", "launch-test.log", "player-test.log"]
old_pins: dict[str, str | None] = {}
for name in backup_names:
    source = child(GAME, name)
    old_pins[name] = sha(source) if source.exists() else None
    if source.exists():
        destination = child(backup, name)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        assert sha(destination) == old_pins[name]
record = {"status": "IN_PROGRESS", "root": str(GAME), "stageReceipt": str(STAGE / "receipt.json"), "old": old_pins, "intendedChanges": expected, "completedCopies": [], "review": review}
deployment = backup / "deployment.json"
deployment.write_text(json.dumps(record, indent=2) + "\n")
try:
    assert_game_stopped(GAME)
    for name, source in changes.items():
        target = child(GAME, name)
        assert sha(source) == expected[name]
        if old_pins[name] is None:
            assert not target.exists(), target
        else:
            assert target.exists() and sha(target) == old_pins[name], target
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        assert sha(target) == expected[name]
        record["completedCopies"].append(name)
        deployment.write_text(json.dumps(record, indent=2) + "\n")
    assert sha(catalog) == receipt["catalog"]["sha256"]
    assert files(models) == dict(receipt["priorAssets"], **receipt["newAssets"])
    record.update(status="VERIFIED_COMPLETE", new=expected)
    deployment.write_text(json.dumps(record, indent=2) + "\n")
except Exception as error:
    record.update(status="FAILED_OR_PARTIAL", error=repr(error))
    deployment.write_text(json.dumps(record, indent=2) + "\n")
    raise
print(json.dumps({"status": record["status"], "deployment": str(deployment), "new": expected}, indent=2))
