#!/usr/bin/env python3
"""Stage original FTK model profiles without changing the game copy.

The stage is a complete, pinned candidate catalog and models directory under
``scratch/``.  It can be inspected or passed to ``deploy_custom_model_stage.py``.
This script only accepts an isolated FTK copy that is a direct child of the
repository's ``scratch`` directory. A supplied profile is appended when absent,
must exactly match its existing catalog row when staging an asset revision, or
may replace one existing row in place only with the explicit migration flag.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil

import jsonschema


STATUS = "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
MODELS_RELATIVE = Path("BepInEx/plugins/FTKModFramework_content/models")
CATALOGS = {
    "enemy": ("model-test-profiles.json", "profiles.schema.json"),
    "player": ("model-test-player-profiles.json", "player-profiles.schema.json"),
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


def isolated_game(repo: Path, value: Path) -> Path:
    game = value.resolve()
    assert game.is_dir() and not game.is_symlink(), "Game root must be a real directory."
    assert game.parent == repo / "scratch", "Refusing a game root outside repository scratch/."
    return game


def fresh_stage(repo: Path, value: Path) -> Path:
    output = value.resolve()
    assert not output.exists(), "Existing stage refused. Preserve it as evidence and choose a fresh path."
    assert output.parent == repo / "scratch", "Stage must be a direct child of repository scratch/."
    return output


def flat_file_hashes(directory: Path) -> dict[str, str]:
    assert directory.is_dir() and not directory.is_symlink(), directory
    result: dict[str, str] = {}
    for path in sorted(directory.iterdir()):
        assert path.is_file() and not path.is_symlink(), f"Only ordinary model files are allowed: {path}"
        result[path.name] = sha(path)
    return result


def required_assets(profiles: list[dict]) -> set[str]:
    names: set[str] = set()
    for profile in profiles:
        for renderer in [*profile["renderers"], *profile.get("apparel", [])]:
            names.add(renderer["glbFile"])
            texture = renderer.get("textureFile")
            if texture:
                names.add(texture)
            for slot in renderer.get("materialSlots", []):
                texture = slot.get("textureFile")
                if texture:
                    names.add(texture)
    return names


def repository_file(repo: Path, path: Path, label: str) -> Path:
    value = path.resolve()
    assert value.is_file() and not value.is_symlink(), f"{label} must be an ordinary file: {value}"
    assert value.is_relative_to(repo), f"{label} must be inside this repository: {value}"
    return value


def relative(repo: Path, path: Path) -> str:
    return str(path.resolve().relative_to(repo))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path)
    parser.add_argument(
        "--catalog-kind",
        choices=sorted(CATALOGS),
        default="enemy",
        help="Target isolated enemy or player profile catalog.",
    )
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument(
        "--profile",
        type=Path,
        required=True,
        help="Schema-valid v1 profile document to append, or to verify unchanged for an asset revision.",
    )
    parser.add_argument("--asset-dir", type=Path, required=True, help="Directory containing every profile-declared GLB and PNG.")
    parser.add_argument("--pin", type=Path, action="append", default=[], help="Optional authored evidence file copied into the immutable stage.")
    parser.add_argument(
        "--replace-existing-assets",
        action="store_true",
        help="Explicitly allow a hash-pinned replacement of an already deployed declared asset.",
    )
    parser.add_argument(
        "--replace-existing-profile",
        action="store_true",
        help="Replace one changed existing isolated catalog row in place; requires exactly one supplied profile.",
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    repo = args.repo.resolve() if args.repo else repository_from_here()
    assert (repo / "FTKModFramework").is_dir() and (repo / "tools").is_dir(), "Invalid repository root."
    game = isolated_game(repo, args.game_root)
    output = fresh_stage(repo, args.output)
    catalog_name, schema_name = CATALOGS[args.catalog_kind]
    profile_path = repository_file(repo, args.profile, "Profile document")
    asset_dir = args.asset_dir.resolve()
    assert asset_dir.is_dir() and not asset_dir.is_symlink(), "Asset directory must be a real directory."
    assert asset_dir.is_relative_to(repo), "Asset directory must be inside this repository."
    schema_path = repo / "tools/ai-model-pipeline/runtime-test-content" / schema_name
    schema = read_json(schema_path)

    additions = read_json(profile_path)
    jsonschema.validate(additions, schema)
    profiles = additions["profiles"]
    keys = [profile["key"] for profile in profiles]
    assert len(keys) == len(set(keys)), "Candidate profile document has duplicate keys."

    catalog_path = game / catalog_name
    models_path = game / MODELS_RELATIVE
    assert catalog_path.is_file() and not catalog_path.is_symlink(), catalog_path
    current = read_json(catalog_path)
    jsonschema.validate(current, schema)
    current_by_key = {profile["key"]: profile for profile in current["profiles"]}
    current_index_by_key = {profile["key"]: index for index, profile in enumerate(current["profiles"])}
    existing_identical_keys: list[str] = []
    appended_profiles = [profile for profile in profiles if profile["key"] not in current_by_key]
    replacements: list[dict] = []
    if args.replace_existing_profile:
        assert len(profiles) == 1, "An explicit profile migration stages exactly one profile document row."
    for profile in profiles:
        existing = current_by_key.get(profile["key"])
        if existing is not None:
            if existing == profile:
                existing_identical_keys.append(profile["key"])
                continue
            assert args.replace_existing_profile, (
                "Existing catalog profile differs from the supplied document. "
                "Pass --replace-existing-profile only for one deliberate isolated catalog migration."
            )
            replacements.append({
                "key": profile["key"],
                "index": current_index_by_key[profile["key"]],
                "oldProfileSha256": profile_sha(existing),
                "newProfileSha256": profile_sha(profile),
            })
    if args.replace_existing_profile:
        assert len(replacements) == 1, (
            "--replace-existing-profile requires one changed existing catalog row; "
            "use ordinary staging for an identical or absent profile."
        )

    candidate = copy.deepcopy(current)
    for replacement in replacements:
        candidate["profiles"][replacement["index"]] = copy.deepcopy(
            next(profile for profile in profiles if profile["key"] == replacement["key"])
        )
    candidate["profiles"].extend(copy.deepcopy(appended_profiles))
    jsonschema.validate(candidate, schema)
    replaced_indexes = {replacement["index"] for replacement in replacements}
    assert all(
        candidate["profiles"][index] == current["profiles"][index]
        for index in range(len(current["profiles"])) if index not in replaced_indexes
    )
    assert {key: value for key, value in candidate.items() if key != "profiles"} == {
        key: value for key, value in current.items() if key != "profiles"
    }

    prior_assets = flat_file_hashes(models_path)
    declared_assets = required_assets(profiles)
    new_assets: dict[str, str] = {}
    replaced_assets: dict[str, dict[str, str]] = {}
    reused_assets: dict[str, str] = {}
    sources: dict[str, Path] = {}
    for name in sorted(declared_assets):
        source = asset_dir / name
        assert source.is_file() and not source.is_symlink(), f"Missing or unsafe declared asset: {source}"
        digest = sha(source)
        sources[name] = source
        if name in prior_assets:
            if prior_assets[name] == digest:
                reused_assets[name] = digest
            else:
                assert args.replace_existing_assets, (
                    f"Existing game asset differs from candidate asset: {name}. "
                    "Pass --replace-existing-assets only for a deliberate pinned asset revision."
                )
                replaced_assets[name] = {"old": prior_assets[name], "new": digest}
        else:
            new_assets[name] = digest

    pinned_paths = [repository_file(repo, pin, "Pinned authoring file") for pin in args.pin]
    pin_names = [path.name for path in pinned_paths]
    assert len(pin_names) == len(set(pin_names)), "Pinned authoring files need distinct basenames."

    output.mkdir(parents=True)
    staged_models = output / "models"
    shutil.copytree(models_path, staged_models)
    assert flat_file_hashes(staged_models) == prior_assets
    for name, source in sources.items():
        destination = staged_models / name
        if name in new_assets or name in replaced_assets:
            shutil.copy2(source, destination)
        assert destination.is_file() and sha(destination) == sha(source), name
    candidate_assets = flat_file_hashes(staged_models)
    assert candidate_assets == {
        **prior_assets,
        **new_assets,
        **{name: entry["new"] for name, entry in replaced_assets.items()},
    }

    staged_catalog = output / catalog_name
    staged_catalog.write_text(json.dumps(candidate, indent=2) + "\n")
    shutil.copy2(profile_path, output / "candidate-profile-document.json")
    shutil.copy2(schema_path, output / "profiles.schema.json")
    shutil.copy2(__file__, output / "stage-script.py")
    authoring = output / "authoring"
    authoring.mkdir()
    for path in pinned_paths:
        destination = authoring / path.name
        shutil.copy2(path, destination)
        assert sha(destination) == sha(path)

    receipt = {
        "status": STATUS,
        "catalogKind": args.catalog_kind,
        "catalogFile": catalog_name,
        "sourceCatalog": {"path": relative(repo, catalog_path), "sha256": sha(catalog_path)},
        "profileDocument": {"path": relative(repo, profile_path), "sha256": sha(profile_path)},
        "profileKeys": keys,
        "appendedProfileKeys": [profile["key"] for profile in appended_profiles],
        "existingIdenticalProfileKeys": existing_identical_keys,
        "replacedProfileKeys": [replacement["key"] for replacement in replacements],
        "profileReplacements": replacements,
        "catalog": {"path": catalog_name, "sha256": sha(staged_catalog)},
        "schema": {"path": relative(repo, schema_path), "sha256": sha(schema_path)},
        "priorProfileCount": len(current["profiles"]),
        "candidateProfileCount": len(candidate["profiles"]),
        "preservedCatalogRowsOutsideReplacements": all(
            candidate["profiles"][index] == current["profiles"][index]
            for index in range(len(current["profiles"])) if index not in replaced_indexes
        ),
        "preservedTopMetadata": {key: value for key, value in candidate.items() if key != "profiles"}
        == {key: value for key, value in current.items() if key != "profiles"},
        "priorAssets": prior_assets,
        "candidateAssets": candidate_assets,
        "newAssets": new_assets,
        "replacedAssets": replaced_assets,
        "reusedIdenticalAssets": reused_assets,
        "authoringPins": {
            relative(repo, path): sha(path) for path in [profile_path, *pinned_paths]
        },
        "stageScriptSha256": sha(output / "stage-script.py"),
        "limits": "Staged only. The game copy is unchanged. Binary validation, exact runtime binding, motion, gameplay, cleanup, and art review remain separate gates.",
    }
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(
        json.dumps(
            {
                "receipt": str(output / "receipt.json"),
                "catalogSha256": receipt["catalog"]["sha256"],
                "profileKeys": keys,
                "appendedProfileKeys": receipt["appendedProfileKeys"],
                "replacedProfileKeys": receipt["replacedProfileKeys"],
                "newAssets": new_assets,
                "replacedAssets": replaced_assets,
                "reusedIdenticalAssets": reused_assets,
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
