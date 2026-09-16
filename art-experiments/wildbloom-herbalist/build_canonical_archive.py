#!/usr/bin/env python3
"""Build the pinned Wildbloom Herbalist canonical player-route archive."""

from __future__ import annotations

import gzip
import hashlib
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = Path(__file__).resolve().parent / "live-validation-v2-canonical"

PREVIEW_SESSION = "e69928c5d0ab49df890edeef870d5feb"
COMBAT_SESSION = "c293b02ed0f34e589867d6aeeed747b2"

CAPTURES = {
    "idle-body": "b988f19a28b14602a9cb1bfbdf2beb41",
    "idle-hair-top": "d65626479c7e410fbb48179840b1f54c",
    "idle-hair-bottom": "67a77188fbbf49acab7bff877fceb1bf",
    "death-body": "ef9ef71d13694a559df648d5ae6f815f",
    "death-hair-top": "16ee482ff0794cd38d348c992b781a02",
    "death-hair-bottom": "bfb9d0dc1fa142e3874aaa1acc5f27db",
    "combat-body": "c0d5ee67d56d4bf68eb9bd90cbfa1b7e",
}

METADATA = {
    "preview-state.json": "scratch/mirewarden-game/model-test-output/a38ec7744f794284b1b7a080d7342426.json",
    "class-inspect.json": "scratch/mirewarden-game/model-test-output/43e081d86a4141e8811c572a268d190d.json",
    "class-submit.json": "scratch/mirewarden-game/model-test-output/3364c62652df4d61a98ee0c7a69c1afa.json",
    "party-start.json": "scratch/mirewarden-game/model-test-output/0d8dc59bc1614981b95c362a383e1532.json",
    "overworld-inventory.json": "scratch/mirewarden-game/model-test-output/80fab433286e47f385d541ffd4ca5980.json",
    "combat-inventory-first.json": "scratch/mirewarden-game/model-test-output/e8ac60140185444193d7e81ecc4ef5d8.json",
    "combat-inventory-second.json": "scratch/mirewarden-game/model-test-output/1877bd4aa1c348b7b5ad0aa3e1bd8b66.json",
    "equipment-second.json": "scratch/mirewarden-game/model-test-output/014c374c2516479a8f135777d9f65887.json",
    "combat-stage.json": "scratch/mirewarden-game/model-test-output/case-29bf85568df64a02b686032eb426b2ed/result.json",
    "combat-stage-journal.jsonl": "scratch/mirewarden-game/model-test-output/case-29bf85568df64a02b686032eb426b2ed/journal.jsonl",
    "combat-action.json": "scratch/mirewarden-game/model-test-output/case-16cdaef3b5ab4066b3aa0b5e0b6aa8a6/result.json",
    "combat-action-summary.json": "scratch/mirewarden-game/model-test-output/case-16cdaef3b5ab4066b3aa0b5e0b6aa8a6/capture-summary.json",
    "combat-action-journal.jsonl": "scratch/mirewarden-game/model-test-output/case-16cdaef3b5ab4066b3aa0b5e0b6aa8a6/journal.jsonl",
    "kill-fixture.json": "scratch/mirewarden-game/model-test-output/case-f0f07d2f09084c80b4fbe59f37fc3c0c/result.json",
    "loot-collect-1.json": "scratch/mirewarden-game/model-test-output/b43f5086af0e4d059da55fd6af05490f.json",
    "loot-collect-2.json": "scratch/mirewarden-game/model-test-output/fb308e9ac8334863a8d3501dc89678ef.json",
    "ready-room-2.json": "scratch/mirewarden-game/model-test-output/bc78a60493ee462ab00bbcf661e3b057.json",
    "second-combat-stage.json": "scratch/mirewarden-game/model-test-output/case-7a669b7973704b1ebb9286a4a8312708/result.json",
    "prior-v1-validation.json": "art-experiments/wildbloom-herbalist/live-validation-v1/validation.json",
    "prior-v1-integrity.json": "art-experiments/wildbloom-herbalist/live-validation-v1/integrity.json",
    "deployment.json": "scratch/mirewarden-game/deployment-backups/wildbloom-native-class-v3-20260916-091011/deployment.json",
}

ROUTES = {
    "body": {
        "path": "player_Herbalist",
        "mesh": "ftkmf_glb_wildbloom-body.glb",
        "signature": "33ef1d59071b4cb809e65f6c63ea4d925e2f7dc75a0703033e7e8177f9137149",
    },
    "hair-top": {
        "path": "hairTop",
        "mesh": "ftkmf_glb_wildbloom-hair-top.glb",
        "signature": "77a9c31c091976a8004ebf40f7871623675169fc43de518ac46423bb472e98df",
    },
    "hair-bottom": {
        "path": "hairBottom",
        "mesh": "ftkmf_glb_wildbloom-hair-bottom.glb",
        "signature": "ccd5b08dc89616e5470ca4195938ebc3378daeb653d2e8b2cce2f93b3c3bdc2c",
    },
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def copy(source: str, target: Path) -> None:
    source_path = ROOT / source
    if not source_path.is_file() or source_path.is_symlink():
        raise ValueError(f"ordinary source file required: {source}")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source_path, target)


def gzip_copy(source: Path, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    with source.open("rb") as incoming, target.open("wb") as raw, gzip.GzipFile(fileobj=raw, mode="wb", mtime=0) as outgoing:
        shutil.copyfileobj(incoming, outgoing)


def main() -> int:
    metadata = ARCHIVE / "metadata"
    selected = ARCHIVE / "selected"
    metadata.mkdir(parents=True, exist_ok=True)
    selected.mkdir(parents=True, exist_ok=True)

    for label, capture_id in CAPTURES.items():
        source = ROOT / "scratch/mirewarden-game/model-test-output" / f"{capture_id}.json"
        gzip_copy(source, metadata / f"{label}.json.gz")

    for target, source in METADATA.items():
        copy(source, metadata / target)

    selection = {
        "idle-body": (CAPTURES["idle-body"], 12),
        "idle-hair-top": (CAPTURES["idle-hair-top"], 12),
        "idle-hair-bottom": (CAPTURES["idle-hair-bottom"], 12),
        "death-body": (CAPTURES["death-body"], 12),
        "death-hair-top": (CAPTURES["death-hair-top"], 12),
        "death-hair-bottom": (CAPTURES["death-hair-bottom"], 12),
        "attack-body": (CAPTURES["combat-body"], 24),
        "hit-body": (CAPTURES["combat-body"], 88),
    }
    selected_rows = []
    prefix = "art-experiments/wildbloom-herbalist/live-validation-v2-canonical/selected"
    for label, (capture_id, frame) in selection.items():
        target = selected / f"{label}.png"
        copy(f"scratch/mirewarden-game/model-test-output/{capture_id}/{frame:04d}.png", target)
        selected_rows.append({"label": label, "path": f"{prefix}/{target.name}", "sha256": sha256(target)})

    prior_validation = metadata / "prior-v1-validation.json"
    prior_integrity = metadata / "prior-v1-integrity.json"
    validation = {
        "schema": "ftkmf.player-canonical-route.v1",
        "status": "canonical_native_player_route_reviewed",
        "revision": "V2 canonical",
        "displayName": "Wildbloom Herbalist",
        "sessions": {"preview": PREVIEW_SESSION, "combat": COMBAT_SESSION},
        "scope": "Exact herbalist_Female body, upper-hair and seven-bone lower-hair player route through native Party Select, overworld, ordinary combat, progression and one combat-avatar rebuild.",
        "profile": json.loads((ROOT / "art-experiments/wildbloom-herbalist/runtime-profile.json").read_text())["profiles"][0],
        "topologyGroup": "45c7a9b9fb730195",
        "renderers": [{"part": part, **route} for part, route in ROUTES.items()],
        "avatarOwners": {
            "preview": {"status": "observed_actual_native_player_preview", "observedAvatars": 1, "ownerInstanceId": -19122, "celInstanceId": -292682, "nativeMenuMembership": True, "reciprocalPreviewReference": True, "parentIsNativePedestal": True},
            "overworld": {"heroInstanceId": -235140, "celInstanceId": -238744, "leaseId": 3},
            "combatFirst": {"ownerInstanceId": 358974, "celInstanceId": -245318, "leaseId": 3},
            "combatSecond": {"ownerInstanceId": 358974, "celInstanceId": -249004, "leaseId": 3},
        },
        "captures": [
            *[{"label": f"native-preview-idle-{part}", "scope": "player-preview", "rendererPath": route["path"], "mesh": route["mesh"], "raw": f"metadata/idle-{part}.json.gz"} for part, route in ROUTES.items()],
            *[{"label": f"native-preview-death-fixture-{part}", "scope": "player-preview", "rendererPath": route["path"], "mesh": route["mesh"], "provenance": "native-state-playback", "raw": f"metadata/death-{part}.json.gz"} for part, route in ROUTES.items()],
            {"label": "native-player-attack-and-hit", "scope": "player-combat", "rendererPath": "player_Herbalist", "mesh": ROUTES["body"]["mesh"], "raw": "metadata/combat-body.json.gz", "clips": ["attack_blunt1H", "damageLight_blunt1H"]},
        ],
        "gameplay": {
            "ordinaryAttack": {"enemyHp": [72, 62], "clip": "attack_blunt1H", "source": "metadata/combat-action-journal.jsonl"},
            "ordinaryHit": {"heroHp": [999, 962], "clip": "damageLight_blunt1H", "source": "metadata/combat-action-journal.jsonl"},
            "finalReady": {"ok": True, "level": 0, "room": 2, "buttonCount": 1},
            "progression": {"afterFirstVictory": {"level": 1, "xp": 38, "gold": 46}, "strictReady": {"ok": True, "level": 0, "room": 2, "buttonCount": 1}},
        },
        "explicitDeathFixture": {"method": "Animator.Play Death on actual game-owned preview owner", "provenance": "native-state-playback", "ordinaryPlayerDeath": False, "boundary": "Motion and deformation fixture only; no ordinary lethal, corpse lifetime or cleanup claim."},
        "equipment": {"status": "native_default_outfit_observed_no_custom_apparel_declared", "customApparelApplicable": False, "nativeVisible": ["armorHerbalist(Clone)", "bootsHerbalist(Clone)", "helmHerbalist(Clone)", "backpackHerbalist(Clone)"]},
        "lifecycle": {"status": "preview_to_overworld_to_combat_and_combat_rebuild_observed", "firstCombatCelInstanceId": -245318, "secondCombatCelInstanceId": -249004, "sharedLeaseId": 3, "references": 2, "boundary": "The first combat owner disappeared and a second native combat CEL retained the exact three meshes. Final owner teardown and Unity-null resources were not measured for this route."},
        "visualReview": {"status": "reviewed_native_preview_idle_death_and_combat_samples", "observed": "Selected full-resolution frames show a coherent green petal-crowned Wildbloom Herbalist in Party Select, a readable native death pose, an ordinary hammer attack and an ordinary hit response without exploded geometry.", "boundary": "Sampled cameras only; no multiplayer, every equipment combination, corpse lifetime or final-owner cleanup claim."},
        "selectedFrames": selected_rows,
        "priorEvidence": {"v1": {"validation": "metadata/prior-v1-validation.json", "sha256": sha256(prior_validation), "integrity": "metadata/prior-v1-integrity.json", "integritySha256": sha256(prior_integrity)}},
        "limitations": [
            "Death is explicit native-state playback on a real preview owner, not ordinary lethal gameplay.",
            "The kill action is an explicit fixture used only to reach native loot and Ready progression.",
            "No custom apparel is declared; native Herbalist clothing remains game-owned.",
            "Final owner teardown, corpse lifetime, portraits and multiplayer remain outside this route's claims.",
        ],
    }
    (ARCHIVE / "validation.json").write_text(json.dumps(validation, indent=2) + "\n")
    (ARCHIVE / "README.md").write_text(
        "# Wildbloom Herbalist V2 canonical route\n\n"
        "This archive pins the exact three-mesh Herbalist Female route through native Party Select, overworld, ordinary attack and hit, loot progression, and one combat-avatar rebuild. Death is a native-state playback fixture and does not claim ordinary lethal gameplay.\n\n"
        "Run `python3 tools/ai-model-pipeline/verify_wildbloom_canonical_archive.py` to verify every retained file and evidence boundary.\n"
    )
    files = {str(path.relative_to(ARCHIVE)): sha256(path) for path in sorted(ARCHIVE.rglob("*")) if path.is_file() and path.name != "integrity.json"}
    (ARCHIVE / "integrity.json").write_text(json.dumps({"schema": "ftkmf.canonical-archive-integrity.v1", "files": files}, indent=2) + "\n")
    print(json.dumps({"archive": str(ARCHIVE), "files": len(files), "validationSha256": sha256(ARCHIVE / "validation.json"), "integritySha256": sha256(ARCHIVE / "integrity.json")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
