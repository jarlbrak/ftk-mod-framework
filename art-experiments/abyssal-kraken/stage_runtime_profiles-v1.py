#!/usr/bin/env python3
"""Stage all original modern Kraken profiles into a fresh isolated-game catalog.

This script only creates an independently hash-pinned candidate directory. It
never changes the supplied game root. Deploy the resulting stage with
``deploy_runtime_profiles.py`` after reviewing its receipt.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import shutil
from pathlib import Path

import jsonschema


ASSETS = (
    "abyssal-crown-kraken-head.glb",
    "abyssal-crown-kraken-head.png",
    "sargassum-kraken-tentacle.glb",
    "sargassum-kraken-tentacle.png",
    "sargassum-seaking-tentacle.glb",
    "sargassum-seaking-tentacle.png",
)

# Each original row must preserve the production target identity established by
# its existing calibration row. The original art deliberately has no health,
# material, resource-prefab, FallOff, or legacy-binding override.
CALIBRATION = {
    "ftkmf_modeltest_abyssal_crown_kraken_head": "ftkmf_modeltest_probe_krakenhead",
    "ftkmf_modeltest_sargassum_kraken_tentacle": "ftkmf_modeltest_probe_krakententacle",
    "ftkmf_modeltest_sargassum_kraken_tentacle_mirror": "ftkmf_modeltest_probe_krakententaclemirror",
    "ftkmf_modeltest_royal_sargassum_seaking_tentacle_a": "ftkmf_modeltest_probe_seakingtentaclea",
    "ftkmf_modeltest_royal_sargassum_seaking_tentacle_b": "ftkmf_modeltest_probe_seakingtentacleb",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def files(root: Path) -> dict[str, str]:
    return {path.name: sha(path) for path in sorted(root.iterdir()) if path.is_file()}


def repo_from_here() -> Path:
    return next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--repo", type=Path)
parser.add_argument("--game-root", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()

REPO = args.repo.resolve() if args.repo else repo_from_here()
ART = REPO / "art-experiments/abyssal-kraken"
GAME = args.game_root.resolve()
OUT = args.output.resolve()
assert GAME.is_dir() and not GAME.is_symlink(), "Game root must be a real directory."
assert GAME.parent == REPO / "scratch", "Refusing a non-isolated game root."
assert not OUT.exists(), "Existing output refused: preserve prior stage evidence and choose a new path."
assert OUT.parent == REPO / "scratch", "Stage must be an isolated scratch sibling."

source_profiles = read(ART / "runtime-profiles.json")
assert source_profiles.get("version") == 1
profiles = source_profiles["profiles"]
assert len(profiles) == len(CALIBRATION) == 5
assert [profile["key"] for profile in profiles] == list(CALIBRATION)
assert {renderer["glbFile"] for profile in profiles for renderer in profile["renderers"]} == {
    "abyssal-crown-kraken-head.glb",
    "sargassum-kraken-tentacle.glb",
    "sargassum-seaking-tentacle.glb",
}
assert {renderer["textureFile"] for profile in profiles for renderer in profile["renderers"]} == {
    "abyssal-crown-kraken-head.png",
    "sargassum-kraken-tentacle.png",
    "sargassum-seaking-tentacle.png",
}
for profile in profiles:
    assert not ({"minimumBaseHealth", "resourcePrefab", "bindingKind", "tint", "fallOffPolicy", "visualScale", "portraitMarkerPath"} & profile.keys()), profile["key"]
    for renderer in profile["renderers"]:
        assert not ({"disableNativeEmission", "materialSlots"} & renderer.keys()), profile["key"]

catalog_path = GAME / "model-test-profiles.json"
models = GAME / "BepInEx/plugins/FTKModFramework_content/models"
assert catalog_path.is_file() and models.is_dir() and not models.is_symlink()
current = read(catalog_path)
assert current.get("version") == 1 and isinstance(current.get("profiles"), list)
current_rows = current["profiles"]
assert not {profile["key"] for profile in profiles} & {row["key"] for row in current_rows}

calibrations = {}
for profile in profiles:
    probe_key = CALIBRATION[profile["key"]]
    probe = next(row for row in current_rows if row["key"] == probe_key)
    assert probe["baseEnemy"] == profile["baseEnemy"], profile["key"]
    assert probe["combatProfile"] == profile["combatProfile"], profile["key"]
    assert len(probe["renderers"]) == len(profile["renderers"]) == 1, profile["key"]
    assert probe["renderers"][0]["rendererPath"] == profile["renderers"][0]["rendererPath"], profile["key"]
    calibrations[profile["key"]] = {
        "profileKey": probe_key,
        "baseEnemy": probe["baseEnemy"],
        "combatProfile": probe["combatProfile"],
        "rendererPath": probe["renderers"][0]["rendererPath"],
    }

candidate = copy.deepcopy(current)
candidate["profiles"].extend(copy.deepcopy(profiles))
jsonschema.validate(candidate, read(REPO / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json"))
prior_assets = files(models)
new_assets = {name: sha(ART / name) for name in ASSETS}
assert not set(new_assets) & set(prior_assets), "Original asset names already exist in game copy."

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
    "runtime-profiles.json",
    "build-report.json",
    "original-geometry-proof.json",
    "target-bindings-proof.json",
    "verify_original_geometry.py",
    "verify_target_bindings.py",
):
    shutil.copy2(ART / name, OUT / name)

receipt = {
    "status": "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_ORIGINALS",
    "sourceCatalog": {"path": str(catalog_path.relative_to(REPO)), "sha256": sha(catalog_path)},
    "catalog": {"path": "model-test-profiles.json", "sha256": sha(OUT / "model-test-profiles.json")},
    "profileRows": {
        "source": len(current_rows),
        "candidate": len(candidate["profiles"]),
        "preservedSourceRows": candidate["profiles"][:len(current_rows)] == current_rows,
        "topMetadataPreserved": {key: value for key, value in candidate.items() if key != "profiles"} == {key: value for key, value in current.items() if key != "profiles"},
    },
    "priorAssets": prior_assets,
    "assetSha256": files(OUT / "models"),
    "allPriorAssetsUnchanged": all(sha(OUT / "models" / name) == digest for name, digest in prior_assets.items()),
    "newAssets": new_assets,
    "profiles": profiles,
    "calibrations": calibrations,
    "authoringFiles": {name: sha(ART / name) for name in (
        "runtime-profiles.json",
        "build_geometry.py",
        "build_blender.py",
        "build-report.json",
        "original-geometry-proof.json",
        "target-bindings-proof.json",
        "verify_original_geometry.py",
        "verify_target_bindings.py",
    )},
    "stageScriptSha256": sha(OUT / "stage-script.py"),
    "limits": "Stage proof only. It establishes catalog and asset preservation for the disposable game copy. Live renderer replacement, animation, combat, death, cleanup, Ready progression, materials, culling, portraits, and art review remain separate checks.",
}
(OUT / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({"receipt": str(OUT / "receipt.json"), "catalogSha256": receipt["catalog"]["sha256"], "profileKeys": [profile["key"] for profile in profiles]}, indent=2))
