#!/usr/bin/env python3
"""Deploy the built static-renderer framework support to one stopped isolated game copy."""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


BINARY_SOURCES = {
    "BepInEx/plugins/FTKModFramework.dll": "FTKModFramework/bin/Release/net35/FTKModFramework.dll",
    "BepInEx/plugins/FtkRuntimeModelTestContent.dll": "tools/ai-model-pipeline/runtime-test-content/bin/Release/net35/FtkRuntimeModelTestContent.dll",
    "BepInEx/plugins/FtkRuntimeModelTest.dll": "tools/ai-model-pipeline/runtime-test/bin/Release/net35/FtkRuntimeModelTest.dll",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def repo_from_here() -> Path:
    return next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())


def child(root: Path, name: str) -> Path:
    relative = Path(name)
    assert not relative.is_absolute() and ".." not in relative.parts
    path = root / relative
    assert path.resolve().is_relative_to(root.resolve())
    return path


def assert_game_stopped(game: Path) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    commands = subprocess.check_output(["ps", "-axo", "command="], text=True).splitlines()
    assert not any(command == executable or command.startswith(executable + " ") for command in commands), "Isolated FTK is running; no files were changed."


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--repo", type=Path)
parser.add_argument("--game-root", type=Path, required=True)
parser.add_argument("--execute", action="store_true")
args = parser.parse_args()

REPO = args.repo.resolve() if args.repo else repo_from_here()
GAME = args.game_root.resolve()
assert GAME.is_dir() and not GAME.is_symlink() and GAME.parent == REPO / "scratch", "Refusing a non-isolated game root."
assert_game_stopped(GAME)

sources = {target: (REPO / source).resolve() for target, source in BINARY_SOURCES.items()}
for path in sources.values():
    assert path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(REPO), path
expected = {target: sha(source) for target, source in sources.items()}
old = {target: sha(child(GAME, target)) for target in sources}
review = {
    "status": "PINNED_STATIC_RENDERER_BINARIES_VERIFIED",
    "root": str(GAME),
    "sources": {target: str(source.relative_to(REPO)) for target, source in sources.items()},
    "old": old,
    "new": expected,
    "scope": "Built framework, runtime content and helper support static MeshRenderer inventory and assignment. This does not deploy an art profile or establish live acceptance.",
}
if not args.execute:
    print(json.dumps(review, indent=2))
    raise SystemExit(0)

assert_game_stopped(GAME)
backup = GAME / "deployment-backups" / ("static-renderer-support-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
assert not backup.exists()
backup.mkdir(parents=True)
record = {"status": "IN_PROGRESS", "root": str(GAME), "old": old, "new": expected, "sources": review["sources"], "completedCopies": [], "review": review}
receipt = backup / "deployment.json"
receipt.write_text(json.dumps(record, indent=2) + "\n")
try:
    assert_game_stopped(GAME)
    for target, source in sources.items():
        destination = child(GAME, target)
        assert destination.is_file() and sha(destination) == old[target]
        assert sha(source) == expected[target]
        backup_path = child(backup, target)
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(destination, backup_path)
        assert sha(backup_path) == old[target]
        shutil.copy2(source, destination)
        assert sha(destination) == expected[target]
        record["completedCopies"].append(target)
        receipt.write_text(json.dumps(record, indent=2) + "\n")
    record["status"] = "VERIFIED_COMPLETE"
    receipt.write_text(json.dumps(record, indent=2) + "\n")
except Exception as error:
    record.update(status="FAILED_OR_PARTIAL", error=repr(error))
    receipt.write_text(json.dumps(record, indent=2) + "\n")
    raise
print(json.dumps({"status": record["status"], "deployment": str(receipt), "new": expected}, indent=2))
