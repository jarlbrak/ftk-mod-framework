#!/usr/bin/env python3
"""Review or deploy one pinned custom-model stage into an isolated FTK copy.

Without ``--execute`` this performs all integrity checks and reports the exact
files that would change.  Deployment refuses a running game, stale catalog, or
any drift in the source/staged model directories. A staged in-place profile
migration is accepted only when its old and new canonical row hashes match the
stage receipt exactly.
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


STATUS = "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
MODELS_RELATIVE = Path("BepInEx/plugins/FTKModFramework_content/models")
CATALOG_NAMES = {
    "enemy": "model-test-profiles.json",
    "player": "model-test-player-profiles.json",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def profile_sha(profile: dict) -> str:
    return hashlib.sha256(
        json.dumps(profile, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()


def repository_from_here() -> Path:
    return next(
        path
        for path in Path(__file__).resolve().parents
        if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir()
    )


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), f"Expected JSON object: {path}"
    return value


def isolated_child(repo: Path, value: Path, label: str) -> Path:
    result = value.resolve()
    assert result.is_dir() and not result.is_symlink(), f"{label} must be a real directory."
    assert result.parent == repo / "scratch", f"{label} must be a direct child of repository scratch/."
    return result


def flat_file_hashes(directory: Path) -> dict[str, str]:
    assert directory.is_dir() and not directory.is_symlink(), directory
    result: dict[str, str] = {}
    for path in sorted(directory.iterdir()):
        assert path.is_file() and not path.is_symlink(), f"Only ordinary model files are allowed: {path}"
        result[path.name] = sha(path)
    return result


def assert_game_stopped(game: Path) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    commands = subprocess.check_output(["ps", "-axo", "command="], text=True).splitlines()
    assert not any(command == executable or command.startswith(executable + " ") for command in commands), (
        "Isolated FTK is running; no files were changed."
    )


def safe_child(root: Path, relative: str | Path) -> Path:
    value = Path(relative)
    assert not value.is_absolute() and ".." not in value.parts, value
    target = root / value
    assert target.resolve().is_relative_to(root.resolve()), target
    return target


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path)
    parser.add_argument(
        "--catalog-kind",
        choices=sorted(CATALOG_NAMES),
        default="enemy",
        help="Target isolated enemy or player profile catalog.",
    )
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--stage", type=Path, required=True)
    parser.add_argument("--label", default="custom-model", help="Safe backup label used only when --execute is supplied.")
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args()

    assert re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,63}", args.label), "Label must be lowercase letters, digits, _ or -."
    repo = args.repo.resolve() if args.repo else repository_from_here()
    game = isolated_child(repo, args.game_root, "Game root")
    stage = isolated_child(repo, args.stage, "Stage")
    catalog_name = CATALOG_NAMES[args.catalog_kind]
    assert_game_stopped(game)

    receipt_path = stage / "receipt.json"
    staged_catalog = stage / catalog_name
    staged_models = stage / "models"
    receipt = read_json(receipt_path)
    assert receipt.get("status") == STATUS, "Unexpected stage receipt status."
    assert receipt.get("catalogKind", "enemy") == args.catalog_kind, "Stage catalog kind differs from requested deployment."
    assert receipt.get("catalogFile", "model-test-profiles.json") == catalog_name, "Stage catalog file differs from requested deployment."
    catalog = game / catalog_name
    models = game / MODELS_RELATIVE
    assert sha(catalog) == receipt["sourceCatalog"]["sha256"], "Current catalog differs from stage baseline."
    assert sha(staged_catalog) == receipt["catalog"]["sha256"], "Staged catalog hash mismatch."
    assert flat_file_hashes(models) == receipt["priorAssets"], "Current models differ from stage baseline."
    assert flat_file_hashes(staged_models) == receipt["candidateAssets"], "Staged models hash mismatch."
    current = read_json(catalog)
    candidate = read_json(staged_catalog)
    keys = receipt["profileKeys"]
    appended_keys = receipt.get("appendedProfileKeys", [])
    replacement_rows = receipt.get("profileReplacements", [])
    replaced_keys = receipt.get("replacedProfileKeys", [])
    assert isinstance(appended_keys, list) and all(isinstance(key, str) for key in appended_keys)
    assert isinstance(replaced_keys, list) and all(isinstance(key, str) for key in replaced_keys)
    assert isinstance(replacement_rows, list)
    assert len(current["profiles"]) == receipt["priorProfileCount"]
    assert len(candidate["profiles"]) == receipt["candidateProfileCount"]
    replacement_indexes: set[int] = set()
    replacement_keys: list[str] = []
    for raw_replacement in replacement_rows:
        replacement = raw_replacement if isinstance(raw_replacement, dict) else {}
        key = replacement.get("key")
        index = replacement.get("index")
        old_hash = replacement.get("oldProfileSha256")
        new_hash = replacement.get("newProfileSha256")
        assert isinstance(key, str) and isinstance(index, int) and 0 <= index < len(current["profiles"])
        assert isinstance(old_hash, str) and isinstance(new_hash, str)
        assert index not in replacement_indexes
        assert current["profiles"][index].get("key") == key
        assert candidate["profiles"][index].get("key") == key
        assert profile_sha(current["profiles"][index]) == old_hash
        assert profile_sha(candidate["profiles"][index]) == new_hash
        replacement_indexes.add(index)
        replacement_keys.append(key)
    assert replacement_keys == replaced_keys
    assert len(replacement_keys) == len(set(replacement_keys))
    assert all(
        candidate["profiles"][index] == current["profiles"][index]
        for index in range(len(current["profiles"])) if index not in replacement_indexes
    )
    appended = candidate["profiles"][len(current["profiles"]) :]
    assert [profile["key"] for profile in appended] == appended_keys
    assert not {profile["key"] for profile in current["profiles"]}.intersection(appended_keys)
    assert {key: value for key, value in current.items() if key != "profiles"} == {
        key: value for key, value in candidate.items() if key != "profiles"
    }

    changes: dict[str, Path] = {}
    if receipt["catalog"]["sha256"] != receipt["sourceCatalog"]["sha256"]:
        changes[catalog_name] = staged_catalog
    for name, digest in receipt["newAssets"].items():
        target = models / name
        source = staged_models / name
        assert not target.exists(), f"New asset unexpectedly exists in current game: {name}"
        assert sha(source) == digest, name
        changes[str(MODELS_RELATIVE / name)] = source
    for name, entry in receipt["replacedAssets"].items():
        target = models / name
        source = staged_models / name
        assert sha(target) == entry["old"], name
        assert sha(source) == entry["new"], name
        changes[str(MODELS_RELATIVE / name)] = source
    planned = {name: sha(path) for name, path in changes.items()}
    review = {
        "status": "PINNED_INPUTS_VERIFIED",
        "sourceCatalogSha256": receipt["sourceCatalog"]["sha256"],
        "candidateCatalogSha256": receipt["catalog"]["sha256"],
        "catalogKind": args.catalog_kind,
        "catalogFile": catalog_name,
        "profileKeys": keys,
        "appendedProfileKeys": appended_keys,
        "replacedProfileKeys": replaced_keys,
        "profileReplacements": replacement_rows,
        "changes": planned,
        "replacedAssets": receipt["replacedAssets"],
        "reusedIdenticalAssets": receipt["reusedIdenticalAssets"],
        "preExistingProfileRows": receipt["priorProfileCount"],
        "preservedProfileRowsOutsideReplacements": receipt["priorProfileCount"] - len(replacement_rows),
        "preservedAssetCount": len(receipt["priorAssets"]),
        "liveAcceptance": False,
    }
    if not args.execute:
        print(json.dumps(review, indent=2))
        return

    assert_game_stopped(game)
    backup = game / "deployment-backups" / (
        args.label + "-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    )
    assert not backup.exists(), backup
    backup.mkdir(parents=True)
    metadata_paths = {
        catalog_name,
        "model-test-session.json",
        "model-test-registration.json",
        "model-test-player-registration.json",
        "BepInEx/LogOutput.log",
        "launch-test.log",
        "player-test.log",
    }
    backup_paths = [catalog_name, *receipt["newAssets"].keys(), *receipt["replacedAssets"].keys(), *sorted(metadata_paths - {catalog_name})]
    old: dict[str, str | None] = {}
    for value in backup_paths:
        relative_path = value if value in metadata_paths else str(MODELS_RELATIVE / value)
        source = safe_child(game, relative_path)
        old[relative_path] = sha(source) if source.exists() else None
        if source.exists():
            destination = safe_child(backup, relative_path)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            assert sha(destination) == old[relative_path]
    shutil.copy2(receipt_path, backup / "stage-receipt.json")
    record = {
        "status": "IN_PROGRESS",
        "root": str(game),
        "stageReceipt": str(receipt_path),
        "old": old,
        "intendedChanges": planned,
        "completedCopies": [],
        "review": review,
    }
    deployment = backup / "deployment.json"
    deployment.write_text(json.dumps(record, indent=2) + "\n")
    try:
        assert_game_stopped(game)
        for name, source in changes.items():
            target = safe_child(game, name)
            if name == catalog_name:
                assert target.exists() and sha(target) == receipt["sourceCatalog"]["sha256"]
            elif name in {str(MODELS_RELATIVE / asset) for asset in receipt["replacedAssets"]}:
                asset_name = Path(name).name
                assert target.exists() and sha(target) == receipt["replacedAssets"][asset_name]["old"]
            else:
                assert not target.exists(), target
            assert sha(source) == planned[name]
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            assert sha(target) == planned[name]
            record["completedCopies"].append(name)
            deployment.write_text(json.dumps(record, indent=2) + "\n")
        assert sha(catalog) == receipt["catalog"]["sha256"]
        assert flat_file_hashes(models) == receipt["candidateAssets"]
        record.update(status="VERIFIED_COMPLETE", new=planned)
        deployment.write_text(json.dumps(record, indent=2) + "\n")
    except Exception as error:
        record.update(status="FAILED_OR_PARTIAL", error=repr(error))
        deployment.write_text(json.dumps(record, indent=2) + "\n")
        raise
    print(json.dumps({"status": record["status"], "deployment": str(deployment), "new": planned}, indent=2))


if __name__ == "__main__":
    main()
