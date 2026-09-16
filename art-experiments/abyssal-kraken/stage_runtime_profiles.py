#!/usr/bin/env python3
"""Stage the V5 reattached-eye correction without changing the game.

The baseline contains the rejected V4 placement trial and the separately
archived V3 Sea King correction. This stage changes only the modern Kraken
head row to use one reattached static eye asset. It retains every prior asset.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

import jsonschema

BASELINE = "runtime-profiles-v4-head-compact-eye.json"
CANDIDATE = "runtime-profiles.json"
CHANGED_KEY = "ftkmf_modeltest_abyssal_crown_kraken_head"
NEW_ASSETS = {"abyssal-crown-kraken-eye-v5.glb", "abyssal-crown-kraken-eye-v5.png"}
RETIRED_PROFILE_ASSETS = {"abyssal-crown-kraken-eye-v4.glb", "abyssal-crown-kraken-eye-v4.png"}
STATIC_PATH = "Root_M/base/body/neck/eye/kraken2_eye"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def files(root: Path) -> dict[str, str]:
    return {path.name: sha(path) for path in sorted(root.iterdir()) if path.is_file()}


def repo_from_here() -> Path:
    return next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())


def asset_names(profiles: list[dict]) -> set[str]:
    return {name for profile in profiles for renderer in profile["renderers"] for name in (renderer["glbFile"], renderer.get("textureFile")) if name}


def expected_head_v5_row(old: dict) -> dict:
    assert old["displayName"] == "Abyssal Crown V4"
    assert old["renderers"] == [{
        "rendererPath": "kraken2",
        "glbFile": "abyssal-crown-kraken-head-v2.glb",
        "textureFile": "abyssal-crown-kraken-head-v2.png",
        "disableNativeEmission": True,
    }, {
        "rendererPath": STATIC_PATH,
        "rendererKind": "MeshRenderer",
        "glbFile": "abyssal-crown-kraken-eye-v4.glb",
        "textureFile": "abyssal-crown-kraken-eye-v4.png",
        "disableNativeEmission": True,
    }]
    new = copy.deepcopy(old)
    new["displayName"] = "Abyssal Crown V5"
    new["renderers"][1].update({
        "glbFile": "abyssal-crown-kraken-eye-v5.glb",
        "textureFile": "abyssal-crown-kraken-eye-v5.png",
    })
    return new


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--repo", type=Path)
parser.add_argument("--game-root", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()

REPO = args.repo.resolve() if args.repo else repo_from_here()
ART = REPO / "art-experiments/abyssal-kraken"
GAME = args.game_root.resolve()
OUT = args.output.resolve()
assert GAME.is_dir() and not GAME.is_symlink() and GAME.parent == REPO / "scratch", "Refusing a non-isolated game root."
assert not OUT.exists() and OUT.parent == REPO / "scratch", "Choose one fresh scratch stage directory."

baseline = read(ART / BASELINE)
candidate_source = read(ART / CANDIDATE)
assert baseline["version"] == candidate_source["version"] == 1
old_profiles = baseline["profiles"]
new_profiles = candidate_source["profiles"]
assert len(old_profiles) == len(new_profiles) == 5
assert [row["key"] for row in old_profiles] == [row["key"] for row in new_profiles]
changes = [(old, new) for old, new in zip(old_profiles, new_profiles) if old != new]
assert len(changes) == 1 and changes[0][0]["key"] == CHANGED_KEY, "Only the modern Kraken head may change."
assert changes[0][1] == expected_head_v5_row(changes[0][0]), "Unexpected V5 reattached-eye correction."
assert all(old == new for old, new in zip(old_profiles, new_profiles) if old["key"] != CHANGED_KEY)
assert asset_names(new_profiles) - asset_names(old_profiles) == NEW_ASSETS
assert asset_names(old_profiles) - asset_names(new_profiles) == RETIRED_PROFILE_ASSETS

catalog_path = GAME / "model-test-profiles.json"
models = GAME / "BepInEx/plugins/FTKModFramework_content/models"
assert catalog_path.is_file() and models.is_dir() and not models.is_symlink()
current = read(catalog_path)
assert current.get("version") == 1 and isinstance(current.get("profiles"), list)
rows = current["profiles"]
indexes = []
for old, new in zip(old_profiles, new_profiles):
    matches = [index for index, row in enumerate(rows) if row.get("key") == old["key"]]
    assert len(matches) == 1, old["key"]
    index = matches[0]
    assert rows[index] == old, f"Current row drifted from the V4 head baseline: {old['key']}"
    indexes.append(index)

candidate = copy.deepcopy(current)
for index, new in zip(indexes, new_profiles):
    candidate["profiles"][index] = copy.deepcopy(new)
jsonschema.validate(candidate, read(REPO / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json"))
prior_assets = files(models)
assert all(name in prior_assets for name in asset_names(old_profiles)), "Pinned baseline assets are missing from the isolated game copy."
assert not NEW_ASSETS & set(prior_assets), "Static eye asset collision in isolated game copy."
new_assets = {name: sha(ART / name) for name in sorted(NEW_ASSETS)}

OUT.mkdir(parents=True)
shutil.copytree(models, OUT / "models")
for name, digest in new_assets.items():
    source = ART / name
    assert sha(source) == digest
    shutil.copy2(source, OUT / "models" / name)
assert files(OUT / "models") == dict(prior_assets, **new_assets)
(OUT / "model-test-profiles.json").write_text(json.dumps(candidate, indent=2) + "\n")
shutil.copy2(__file__, OUT / "stage-script.py")
for name in (
    "runtime-profiles-v1.json", "runtime-profiles-v2.json", "runtime-profiles-v2.1.json", "runtime-profiles-v3-seaking.json", "runtime-profiles-v3-head-static-eye.json", BASELINE, CANDIDATE,
    "build_geometry.py", "build_blender.py", "verify_original_geometry.py", "verify_target_bindings.py",
    "build_static_eye.py", "build_static_eye_blender.py", "abyssal-crown-kraken-eye-v3.source.json",
    "abyssal-crown-kraken-eye-v3.pieces.json", "abyssal-crown-kraken-eye-v3.validation.json",
    "abyssal-crown-kraken-eye-v3-blend-validation.json", "build_static_eye_v4.py", "build_static_eye_v4_blender.py",
    "abyssal-crown-kraken-eye-v4.source.json", "abyssal-crown-kraken-eye-v4.pieces.json",
    "abyssal-crown-kraken-eye-v4.validation.json", "abyssal-crown-kraken-eye-v4-blend-validation.json",
    "build_static_eye_v5.py", "build_static_eye_v5_blender.py", "abyssal-crown-kraken-eye-v5.source.json",
    "abyssal-crown-kraken-eye-v5.pieces.json", "abyssal-crown-kraken-eye-v5.validation.json",
    "abyssal-crown-kraken-eye-v5-blend-validation.json",
    "stage_runtime_profiles-v3-seaking.py", "deploy_runtime_profiles-v3-seaking.py", "stage_runtime_profiles-v3-head-static-eye.py", "deploy_runtime_profiles-v3-head-static-eye.py", "stage_runtime_profiles-v4-head-compact-eye.py", "deploy_runtime_profiles-v4-head-compact-eye.py",
):
    shutil.copy2(ART / name, OUT / name)

replacements = [{"index": index, "old": old, "new": new} for index, old, new in zip(indexes, old_profiles, new_profiles) if old != new]
retained_names = {
    "abyssal-crown-kraken-head-v2.glb", "abyssal-crown-kraken-head-v2.png",
    "sargassum-kraken-tentacle-v2.glb", "sargassum-kraken-tentacle-v2.png",
    "royal-sargassum-seaking-tentacle-v3.glb", "royal-sargassum-seaking-tentacle-v3.png",
    "abyssal-crown-kraken-eye-v3.glb", "abyssal-crown-kraken-eye-v3.png",
    "abyssal-crown-kraken-eye-v4.glb", "abyssal-crown-kraken-eye-v4.png",
}
assert retained_names <= set(prior_assets)
receipt = {
    "status": "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V5_HEAD_REATTACHED_EYE",
    "sourceCatalog": {"path": str(catalog_path.relative_to(REPO)), "sha256": sha(catalog_path)},
    "catalog": {"path": "model-test-profiles.json", "sha256": sha(OUT / "model-test-profiles.json")},
    "priorAssets": prior_assets,
    "assetSha256": files(OUT / "models"),
    "newAssets": new_assets,
    "retiredProfileAssetsRetainedOnDisk": {name: prior_assets[name] for name in sorted(RETIRED_PROFILE_ASSETS)},
    "replacements": replacements,
    "preservedRowCount": len(rows) - len(replacements),
    "preservedOtherRows": all(candidate["profiles"][index] == row for index, row in enumerate(rows) if index not in {entry["index"] for entry in replacements}),
    "topMetadataPreserved": {key: value for key, value in candidate.items() if key != "profiles"} == {key: value for key, value in current.items() if key != "profiles"},
    "retainedEarlierKrakenAssets": {name: prior_assets[name] for name in sorted(retained_names)},
    "authoringFiles": {name: sha(ART / name) for name in (
        BASELINE, CANDIDATE, "build_geometry.py", "build_blender.py", "verify_original_geometry.py", "verify_target_bindings.py",
        "build_static_eye.py", "build_static_eye_blender.py", "abyssal-crown-kraken-eye-v3.source.json",
        "abyssal-crown-kraken-eye-v3.pieces.json", "abyssal-crown-kraken-eye-v3.validation.json",
        "abyssal-crown-kraken-eye-v3-blend-validation.json", "build_static_eye_v4.py", "build_static_eye_v4_blender.py",
        "abyssal-crown-kraken-eye-v4.source.json", "abyssal-crown-kraken-eye-v4.pieces.json",
        "abyssal-crown-kraken-eye-v4.validation.json", "abyssal-crown-kraken-eye-v4-blend-validation.json",
        "build_static_eye_v5.py", "build_static_eye_v5_blender.py", "abyssal-crown-kraken-eye-v5.source.json",
        "abyssal-crown-kraken-eye-v5.pieces.json", "abyssal-crown-kraken-eye-v5.validation.json",
        "abyssal-crown-kraken-eye-v5-blend-validation.json",
    )},
    "stageScriptSha256": sha(OUT / "stage-script.py"),
    "limits": "Stage proof only. The static contract and Blender source checks do not establish live binding, parent animation, culling, gameplay, death, Ready, resource lifetime, or art acceptance.",
}
(OUT / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"receipt": str(OUT / "receipt.json"), "catalogSha256": receipt["catalog"]["sha256"], "replacedKeys": [row["old"]["key"] for row in replacements]}, indent=2))
