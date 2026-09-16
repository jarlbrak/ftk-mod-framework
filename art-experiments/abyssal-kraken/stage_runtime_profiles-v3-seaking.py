#!/usr/bin/env python3
"""Stage a hash-pinned V3 Sea King art correction.

V1, V2, and V2.1 evidence remains immutable. This replaces only the two Sea
King rows with V3 assets, retains every prior asset in the isolated game copy,
and writes a fresh stage under scratch without changing the game itself.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

import jsonschema

BASELINE = "runtime-profiles-v2.1.json"
CANDIDATE = "runtime-profiles.json"
CHANGED_KEYS = (
    "ftkmf_modeltest_royal_sargassum_seaking_tentacle_a",
    "ftkmf_modeltest_royal_sargassum_seaking_tentacle_b",
)
NEW_ASSETS = {"royal-sargassum-seaking-tentacle-v3.glb", "royal-sargassum-seaking-tentacle-v3.png"}
OLD_ASSETS = {"sargassum-seaking-tentacle-v2.glb", "sargassum-seaking-tentacle-v2.png"}


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


def expected_v3_row(old: dict) -> dict:
    new = copy.deepcopy(old)
    new["displayName"] = new["displayName"].replace("V2.1", "V3")
    renderer = new["renderers"][0]
    renderer.update(glbFile="royal-sargassum-seaking-tentacle-v3.glb", textureFile="royal-sargassum-seaking-tentacle-v3.png", disableNativeEmission=True)
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
assert [old["key"] for old, _ in changes] == list(CHANGED_KEYS), "V3 may change only the Sea King art rows."
assert all(new == expected_v3_row(old) for old, new in changes), "Unexpected V3 row change."
assert all(old == new for old, new in zip(old_profiles, new_profiles) if old["key"] not in CHANGED_KEYS)
assert asset_names(new_profiles) - asset_names(old_profiles) == NEW_ASSETS
assert asset_names(old_profiles) - asset_names(new_profiles) == OLD_ASSETS

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
    assert rows[index] == old, f"Current row drifted from retained V2.1 baseline: {old['key']}"
    indexes.append(index)

candidate = copy.deepcopy(current)
for index, new in zip(indexes, new_profiles):
    candidate["profiles"][index] = copy.deepcopy(new)
jsonschema.validate(candidate, read(REPO / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json"))
prior_assets = files(models)
assert all(name in prior_assets for name in asset_names(old_profiles)), "Pinned V2.1 assets are missing from the isolated game copy."
assert not NEW_ASSETS & set(prior_assets), "V3 asset collision in isolated game copy."
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
    "runtime-profiles-v1.json", "runtime-profiles-v2.json", BASELINE, CANDIDATE,
    "build_geometry.py", "build_blender.py", "build-report.json",
    "original-geometry-proof.json", "target-bindings-proof.json",
    "verify_original_geometry.py", "verify_target_bindings.py",
    "stage_runtime_profiles-v2.py", "deploy_runtime_profiles-v2.py",
    "stage_runtime_profiles-v2.1.py", "deploy_runtime_profiles-v2.1.py",
):
    shutil.copy2(ART / name, OUT / name)

replacements = [{"index": index, "old": old, "new": new} for index, old, new in zip(indexes, old_profiles, new_profiles) if old != new]
receipt = {
    "status": "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V3_SEAKING_ART_CORRECTION",
    "sourceCatalog": {"path": str(catalog_path.relative_to(REPO)), "sha256": sha(catalog_path)},
    "catalog": {"path": "model-test-profiles.json", "sha256": sha(OUT / "model-test-profiles.json")},
    "priorAssets": prior_assets,
    "assetSha256": files(OUT / "models"),
    "newAssets": new_assets,
    "replacements": replacements,
    "preservedRowCount": len(rows) - len(replacements),
    "preservedOtherRows": all(candidate["profiles"][index] == row for index, row in enumerate(rows) if index not in {entry["index"] for entry in replacements}),
    "topMetadataPreserved": {key: value for key, value in candidate.items() if key != "profiles"} == {key: value for key, value in current.items() if key != "profiles"},
    "retainedV2Assets": {name: prior_assets[name] for name in sorted(OLD_ASSETS)},
    "authoringFiles": {name: sha(ART / name) for name in ("runtime-profiles-v1.json", "runtime-profiles-v2.json", BASELINE, CANDIDATE, "build_geometry.py", "build_blender.py", "build-report.json", "original-geometry-proof.json", "target-bindings-proof.json", "verify_original_geometry.py", "verify_target_bindings.py")},
    "stageScriptSha256": sha(OUT / "stage-script.py"),
    "limits": "Correction-stage proof only. V1, V2, and V2.1 evidence remains retained. V3 requires fresh registration, exact owner binding, material observation, animation, gameplay, death, Ready, culling, and visual review.",
}
(OUT / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"receipt": str(OUT / "receipt.json"), "catalogSha256": receipt["catalog"]["sha256"], "replacedKeys": [row["old"]["key"] for row in replacements]}, indent=2))
