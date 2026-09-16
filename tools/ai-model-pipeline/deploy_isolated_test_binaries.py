#!/usr/bin/env python3
"""Review or reversibly deploy test-plugin binaries into one isolated FTK copy.

The source binaries must be ordinary files inside this repository. Deployment
refuses a running isolated game, saves the replaced binaries under that game's
``deployment-backups`` directory, pins every byte hash, and never reads game
logs. Without ``--execute`` it performs the same input checks and reports the
exact replacement plan without modifying the game copy.
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import uuid


MAX_BINARY_BYTES = 64 * 1024 * 1024
DESTINATIONS = {
    "framework": Path("BepInEx/plugins/FTKModFramework.dll"),
    "helper": Path("BepInEx/plugins/FtkRuntimeModelTest.dll"),
    "content": Path("BepInEx/plugins/FtkRuntimeModelTestContent.dll"),
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def repository_from_here() -> Path:
    return next(
        path
        for path in Path(__file__).resolve().parents
        if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir()
    )


def verify_unsymlinked_descendant(path: Path, anchor: Path, label: str) -> None:
    """Reject symlinks below an approved root without rejecting system parents."""
    current = path.absolute()
    approved = anchor.resolve()
    while True:
        if current.is_symlink():
            raise AssertionError(label + " must not have symlink ancestry: " + str(path))
        if current.resolve() == approved:
            return
        if current.parent == current:
            raise AssertionError(label + " must be inside approved root: " + str(path))
        current = current.parent


def ordinary(path: Path, label: str, repo: Path | None = None, container: Path | None = None) -> Path:
    if repo is not None and container is not None:
        raise AssertionError("Only one containment root is allowed.")
    value = path.resolve()
    if not value.is_file() or value.is_symlink():
        raise AssertionError(label + " must be an ordinary file: " + str(path))
    if repo is not None and not value.is_relative_to(repo):
        raise AssertionError(label + " must be inside this repository: " + str(path))
    if container is not None and not value.is_relative_to(container.resolve()):
        raise AssertionError(label + " must be inside approved root: " + str(path))
    anchor = repo if repo is not None else container
    if anchor is not None:
        verify_unsymlinked_descendant(path, anchor, label)
    size = value.stat().st_size
    if not 0 < size <= MAX_BINARY_BYTES:
        raise AssertionError(label + " has invalid size: " + str(path))
    return value


def isolated_game(repo: Path, value: Path) -> Path:
    game = value.resolve()
    if not game.is_dir() or game.is_symlink() or game.parent != repo / "scratch":
        raise AssertionError("Game root must be a real direct child of repository scratch/.")
    verify_unsymlinked_descendant(value, repo / "scratch", "Game root")
    return game


def assert_game_stopped(game: Path) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    commands = subprocess.check_output(["ps", "-axo", "command="], text=True).splitlines()
    if any(command == executable or command.startswith(executable + " ") for command in commands):
        raise AssertionError("Isolated FTK is running; no files were changed.")


def pin(path: Path) -> dict[str, object]:
    return {"sha256": sha(path), "bytes": path.stat().st_size}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--framework", type=Path)
    parser.add_argument("--helper", type=Path)
    parser.add_argument("--content", type=Path)
    parser.add_argument("--label", default="runtime-test-binaries")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,63}", args.label):
        raise AssertionError("Label must be lowercase letters, digits, _ or -.")
    repo = args.repo.resolve() if args.repo else repository_from_here()
    if not (repo / "FTKModFramework").is_dir() or not (repo / "tools").is_dir():
        raise AssertionError("Invalid repository root.")
    game = isolated_game(repo, args.game_root)
    selected: dict[str, Path] = {}
    for role in DESTINATIONS:
        value = getattr(args, role)
        if value is not None:
            selected[role] = ordinary(value, role + " source binary", repo)
    if not selected:
        raise AssertionError("Provide at least one of --helper or --content.")
    targets = {role: game / relative for role, relative in DESTINATIONS.items() if role in selected}
    for role, target in targets.items():
        ordinary(target, role + " deployed binary", container=game)
    assert_game_stopped(game)
    changes = {
        role: {
            "destination": str(DESTINATIONS[role]),
            "current": pin(targets[role]),
            "candidate": pin(selected[role]),
            "sameBytes": sha(targets[role]) == sha(selected[role]),
        }
        for role in selected
    }
    review = {
        "status": "PINNED_RUNTIME_BINARY_INPUTS_VERIFIED",
        "root": str(game),
        "changes": changes,
        "limits": "Binary deployment only. No catalog, asset, runtime, visual, motion, gameplay, or art acceptance claim.",
    }
    if not args.execute:
        print(json.dumps(review, indent=2))
        return

    assert_game_stopped(game)
    backup = game / "deployment-backups" / (args.label + "-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
    if backup.exists():
        raise AssertionError("Backup already exists: " + str(backup))
    backup.mkdir(parents=True)
    record = {
        "status": "IN_PROGRESS",
        "root": str(game),
        "review": review,
        "backup": str(backup),
        "old": {},
        "new": {},
        "completedCopies": [],
    }
    receipt = backup / "deployment.json"
    receipt.write_text(json.dumps(record, indent=2) + "\n")
    try:
        assert_game_stopped(game)
        for role, source in selected.items():
            target = targets[role]
            relative = DESTINATIONS[role]
            old = pin(target)
            backup_file = backup / relative
            backup_file.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, backup_file)
            if pin(backup_file) != old:
                raise AssertionError("Backup hash mismatch: " + str(relative))
            record["old"][str(relative)] = old
            receipt.write_text(json.dumps(record, indent=2) + "\n")
            candidate = pin(source)
            if candidate["sha256"] != old["sha256"]:
                temporary = target.with_name(target.name + ".incoming-" + uuid.uuid4().hex)
                if temporary.exists():
                    raise AssertionError("Unexpected temporary path: " + str(temporary))
                shutil.copy2(source, temporary)
                if pin(temporary) != candidate:
                    raise AssertionError("Incoming binary hash mismatch: " + str(relative))
                temporary.replace(target)
                if pin(target) != candidate:
                    raise AssertionError("Deployed binary hash mismatch: " + str(relative))
            record["new"][str(relative)] = pin(target)
            record["completedCopies"].append(str(relative))
            receipt.write_text(json.dumps(record, indent=2) + "\n")
        record["status"] = "VERIFIED_COMPLETE"
        receipt.write_text(json.dumps(record, indent=2) + "\n")
    except Exception as error:
        record["status"] = "FAILED_OR_PARTIAL"
        record["error"] = repr(error)
        receipt.write_text(json.dumps(record, indent=2) + "\n")
        raise
    print(json.dumps({"status": record["status"], "deployment": str(receipt), "new": record["new"]}, indent=2))


if __name__ == "__main__":
    main()
