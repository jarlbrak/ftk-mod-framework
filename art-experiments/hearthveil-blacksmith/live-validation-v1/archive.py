#!/usr/bin/env python3
"""Build the Hearthveil Blacksmith live-evidence archive.

Only authored assets, capture derivatives, and lossless metadata are archived.
The script never copies a game binary, Unity asset bundle, extracted native mesh,
or local reference data. A completed archive is immutable unless an operator
explicitly sets FTK_ARCHIVE_REBUILD=1.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents
            if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
ASSET = ROOT / "art-experiments" / "hearthveil-blacksmith"
OUT = ASSET / "live-validation-v1"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
PLAYER_CATALOG = GAME / "model-test-player-profiles.json"
ENEMY_CATALOG = GAME / "model-test-profiles.json"
PLAYER_REGISTRATION = GAME / "model-test-player-registration.json"
GAMEPLAY_SESSION_FILE = ASSET / "live-validation-inputs" / "gameplay-session-eeadf75d450540558db47a6908ae9667.json"
STAGE = ROOT / "scratch" / "hearthveil-blacksmith-stage" / "receipt.json"
DEPLOYMENT = GAME / "deployment-backups" / "hearthveil-blacksmith-20260911-225349" / "deployment.json"
NATIVE_CREATE_HELPER_DEPLOYMENT = GAME / "deployment-backups" / "native-create-character-screen-helper-20260912-072751" / "deployment.json"
PROFILE_KEY = "ftkmf_modeltest_player_hearthveil_blacksmith_female"
CLASS_ID = 113
SESSION = "eeadf75d450540558db47a6908ae9667"
PLAYER_CATALOG_SHA256 = "2cb8a092702691735ae2b2900acf4ba37f6050e559048e4f7a10ff91eba7472c"
ENEMY_CATALOG_SHA256 = "32351b3c32bcb0dc6d0e84b81a89de319537578abf7a613f8cabe8f2d99f7a37"
HERO_INSTANCE_ID = -236266
ASSETS = {
    "hearthveil-body.glb": "ddb8691011173d8de1a1e48260dd99b9b2d88f91ecabb207bc922286d439f634",
    "hearthveil-hair-top.glb": "40f55b523dd9d2a5827b6e9bdced2dfb70a69faab0a86bd894462ed3c155dbae",
    "hearthveil-hair-bottom.glb": "855233f93cd71ecf1173251723d76def70d3cf8ef33657b4f9003728ffd07973",
    "hearthveil-default-armor.glb": "d0bdb4ee5b1c4afae68d93345df7a9601fc1b628406733cec59ecea396214298",
    "hearthveil-boots.glb": "5b91bc4e437e5b238ca0b3eb8f1c1c83d21110cb4a89f86ed92bbed38e814d16",
    "hearthveil-gambeson.glb": "704d3fc7b57eb94d9b4e522f9584f51078245f7ba5da2683a0fe0ce8363da207",
    "hearthveil-palette.png": "ba99d7ab57a505286b323a7054a9376172b241fa3d51694a5f159c26d072e7fb",
}
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
AUTHORING = [
    "README.md", "manifest.json", "runtime-profile.json", "build_geometry.py",
    "verify_original_geometry.py", "render_studio.py", "build-report.json",
    "original-geometry-proof.json",
    "hearthveil-body.glb", "hearthveil-body.source.json", "hearthveil-body.pieces.json", "hearthveil-body.validation.json",
    "hearthveil-hair-top.glb", "hearthveil-hair-top.source.json", "hearthveil-hair-top.pieces.json", "hearthveil-hair-top.validation.json",
    "hearthveil-hair-bottom.glb", "hearthveil-hair-bottom.source.json", "hearthveil-hair-bottom.pieces.json", "hearthveil-hair-bottom.validation.json",
    "hearthveil-default-armor.glb", "hearthveil-default-armor.source.json", "hearthveil-default-armor.pieces.json", "hearthveil-default-armor.validation.json",
    "hearthveil-boots.glb", "hearthveil-boots.source.json", "hearthveil-boots.pieces.json", "hearthveil-boots.validation.json",
    "hearthveil-gambeson.glb", "hearthveil-gambeson.source.json", "hearthveil-gambeson.pieces.json", "hearthveil-gambeson.validation.json",
    "hearthveil-palette.png", "hearthveil-hero.png", "hearthveil-side.png",
    "hearthveil-gambeson-hero.png", "hearthveil-gambeson-side.png",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def add_source(sources: set[Path], path: Path) -> None:
    value = path.resolve()
    assert value.is_file() and not value.is_symlink(), value
    assert value.is_relative_to(ROOT), value
    assert value.suffix.lower() not in FORBIDDEN_SUFFIXES, value
    sources.add(value)


def player_profile() -> dict:
    document = read(ASSET / "runtime-profile.json")
    assert isinstance(document, dict) and document["version"] == 1
    profiles = document["profiles"]
    assert isinstance(profiles, list) and len(profiles) == 1
    return profiles[0]


def item_count(owner: dict, slot: str, item: int) -> int:
    rows = [row for row in owner["slots"] if row["slot"] == slot]
    assert len(rows) == 1
    return sum(entry["count"] for entry in rows[0]["items"] if entry["item"] == item)


def hero(owner_document: dict) -> dict:
    rows = owner_document["heroes"]
    assert len(rows) == 1 and rows[0]["heroInstanceId"] == HERO_INSTANCE_ID and rows[0]["alive"] is True
    return rows[0]


def expected_meshes(document: dict) -> dict[str, str]:
    result = {}
    for row in document["renderers"]:
        path = row.get("celRelativeRendererPath")
        if path in EXPECTED_MESHES:
            assert path not in result
            result[path] = row["mesh"]
    return result


def case_event(case_dir: Path, kind: str) -> dict:
    matches = []
    for line in (case_dir / "journal.jsonl").read_text().splitlines():
        event = json.loads(line)
        if event.get("kind") == kind:
            matches.append(event["data"])
    assert matches, (case_dir, kind)
    return matches[-1]


def state_enemy_hp(state: dict) -> int:
    matches = [row for row in state["combat"]["enemies"] if row.get("type") == "ftkmf_modeltest_cairnfire_trollb"]
    assert len(matches) == 1
    return matches[0]["hp"]


def capture_case(label: str, case_id: str, raw_id: str, action: str,
                 scope: str, renderer_path: str, mesh: str, frames: int = 120) -> dict:
    case_dir = BASE / case_id
    result_path = case_dir / "result.json"
    result = read(result_path)
    raw_path = BASE / f"{raw_id}.json"
    raw = read(raw_path)
    frame_dir = raw_path.with_suffix("")
    pngs = sorted(frame_dir.glob("*.png"))
    assert result["ok"] is True and result["status"] == "recorded_action_and_frames"
    assert result["action"] == action and result["captureScope"] == scope
    assert result["capture"]["result"] == str(raw_path.resolve()) and result["capture"]["frames"] == frames
    assert raw["ok"] is True and raw["error"] is None and raw["session"] == SESSION
    assert raw["scope"] == scope and raw["fixedStep"] is True and len(raw["frames"]) == frames == len(pngs)
    assert all(frame["ownerKind"] == scope and frame["celRelativeRendererPath"] == renderer_path
               and frame["mesh"] == mesh and frame["isVisible"] is True
               for frame in raw["frames"])
    for path in [result_path, case_dir / "journal.jsonl", case_dir / "capture-summary.json", raw_path]:
        add_source(SOURCES, path)
    for png in pngs:
        add_source(SOURCES, png)
        IMAGE_PINS[relative(png)] = sha(png)
    summary = read(case_dir / "capture-summary.json")
    assert summary["captureId"] == raw_id and summary["frameCount"] == frames
    assert summary["meshIdentityStable"] is True and summary["allScreenshotsPresent"] is True
    return {
        "label": label,
        "case": relative(result_path),
        "caseSha256": sha(result_path),
        "capture": {"source": relative(raw_path), "sha256": sha(raw_path)},
        "frameCount": frames,
        "scope": scope,
        "rendererPath": renderer_path,
        "mesh": mesh,
        "summary": {"source": relative(case_dir / "capture-summary.json"), "sha256": sha(case_dir / "capture-summary.json")},
    }


def standalone_capture(label: str, raw_id: str, scope: str, renderer_path: str,
                       mesh: str, frames: int) -> dict:
    raw_path = BASE / f"{raw_id}.json"
    raw = read(raw_path)
    frame_dir = raw_path.with_suffix("")
    pngs = sorted(frame_dir.glob("*.png"))
    assert raw["ok"] is True and raw["error"] is None and raw["session"] == SESSION
    assert raw["scope"] == scope and raw["fixedStep"] is True and len(raw["frames"]) == frames == len(pngs)
    assert all(frame["ownerKind"] == scope and frame["celRelativeRendererPath"] == renderer_path
               and frame["mesh"] == mesh and frame["isVisible"] is True
               for frame in raw["frames"])
    add_source(SOURCES, raw_path)
    for png in pngs:
        add_source(SOURCES, png)
        IMAGE_PINS[relative(png)] = sha(png)
    return {
        "label": label,
        "capture": {"source": relative(raw_path), "sha256": sha(raw_path)},
        "frameCount": frames,
        "scope": scope,
        "rendererPath": renderer_path,
        "mesh": mesh,
    }


def video_from_frames(frame_dir: Path, frame_count: int, label: str) -> dict:
    output = OUT / "video" / f"{label}.mp4"
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", str(frame_count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == frame_count
    return {
        "label": label,
        "path": str(output.relative_to(OUT)),
        "sha256": sha(output),
        "frames": frame_count,
        "width": int(probe["width"]),
        "height": int(probe["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative. Raw capture metadata and source-image hashes preserve the measured timing record.",
    }


def select_frame(label: str, raw_id: str, index: int, observation: str) -> dict:
    source = BASE / raw_id / f"{index:04d}.png"
    destination = OUT / "selected" / f"{label}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(source) == sha(destination)
    return {
        "label": label,
        "source": relative(source),
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": observation,
    }


if (OUT / "validation.json").exists() and os.environ.get("FTK_ARCHIVE_REBUILD") != "1":
    raise AssertionError("Refusing to overwrite a completed archive. Set FTK_ARCHIVE_REBUILD=1 to rebuild intentionally.")

assert sha(PLAYER_CATALOG) == PLAYER_CATALOG_SHA256
assert sha(ENEMY_CATALOG) == ENEMY_CATALOG_SHA256
assert {name: sha(ASSET / name) for name in ASSETS} == ASSETS
GAME_MODELS = GAME / "BepInEx" / "plugins" / "FTKModFramework_content" / "models"
assert {name: sha(GAME_MODELS / name) for name in ASSETS} == ASSETS

PROFILE = player_profile()
player_catalog = read(PLAYER_CATALOG)
assert isinstance(player_catalog, dict) and player_catalog["version"] == 1 and len(player_catalog["profiles"]) == 98
assert next(row for row in player_catalog["profiles"] if row["key"] == PROFILE_KEY) == PROFILE
registration = read(PLAYER_REGISTRATION)
registered = next(row for row in registration["registered"] if row["key"] == PROFILE_KEY)
assert registered["id"] == CLASS_ID and registered["skinset"] == "blacksmith_Female"
assert registered["startingArmor"] == "armorCloth1" and registered["startingArmorAppendedCount"] == 1
assert read(GAMEPLAY_SESSION_FILE)["session"] == SESSION
stage = read(STAGE)
deployment = read(DEPLOYMENT)
assert stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
assert stage["catalogKind"] == "player" and stage["candidateProfileCount"] == 98
assert stage["newAssets"] == ASSETS and stage["replacedAssets"] == {}
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["new"]["model-test-player-profiles.json"] == PLAYER_CATALOG_SHA256
assert all(deployment["new"][f"BepInEx/plugins/FTKModFramework_content/models/{name}"] == digest
           for name, digest in ASSETS.items())

EXPECTED_MESHES = {
    "playerBlacksmith": "ftkmf_glb_hearthveil-body.glb",
    "hairTop": "ftkmf_glb_hearthveil-hair-top.glb",
    "hairBottom": "ftkmf_glb_hearthveil-hair-bottom.glb",
    "armorBlacksmithF(Clone)": "ftkmf_glb_hearthveil-default-armor.glb",
    "bootsBlacksmith(Clone)": "ftkmf_glb_hearthveil-boots.glb",
    "armorGambesonF(Clone)": "ftkmf_glb_hearthveil-gambeson.glb",
}
SOURCES: set[Path] = set()
IMAGE_PINS: dict[str, str] = {}
for path in [
    Path(__file__), PLAYER_CATALOG, ENEMY_CATALOG, PLAYER_REGISTRATION, GAMEPLAY_SESSION_FILE, STAGE, DEPLOYMENT,
    NATIVE_CREATE_HELPER_DEPLOYMENT,
    ROOT / "tools/ai-model-pipeline/stage_custom_model_profile.py",
    ROOT / "tools/ai-model-pipeline/deploy_custom_model_stage.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/record_case.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/record_player_case.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/command.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/Plugin.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/NativeCreateCharacterScreen.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/test_native_create_character_screen_boundary.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/README.md",
    ROOT / "skills/ftk-custom-models/SKILL.md",
    ROOT / "docs/MODEL-PLAYER-API.md",
    ROOT / "docs/MODEL-AUTHORING.md",
]:
    add_source(SOURCES, path)
for name in AUTHORING:
    path = ASSET / name
    add_source(SOURCES, path)
    if path.suffix.lower() == ".png":
        IMAGE_PINS[relative(path)] = sha(path)

# Exact raw helper results, kept independent of convenience stdout transcripts.
RAW = {
    "equipmentBefore": "ed7ff88a55d541e9a75ecca4ae041d6d",
    "watchBefore": "e8523fc0c88a4a18b7ae26066d95955a",
    "unequip": "4e47e66e7b5d4c1d943689d8578007a7",
    "overworldDefault": "5801f54672e84a0da9c5a08d763e9d64",
    "combatDefault": "183c9fcaa174487f81bea7e39b0aa3a4",
    "watchAfterUnequip": "e2381e83fe5f4d6c8f829ab7f62c0c62",
    "readyBeforeReequip": "6704a5d35d404e3f873f769850325f67",
    "watchBeforeReequip": "343e03e1782144269770bbdb18cfa90e",
    "equip": "ed2dbfad9298457095dad628f9aea784",
    "equipmentAfter": "f24e10221c9e43449c45559782529965",
    "overworldGambeson": "86d6bd97013b4758bc766fbe59ecde7b",
    "combatGambeson": "c812ac107ba549d3aff195369e3daa81",
    "watchAfterReequip": "82d4808878044764a51a2a3a52a7ec61",
    "watchClear": "d08c0e6d2de2480a8cfc1b4a1732fae7",
    "readyAfterReequip": "545d45f0dcfd4fa095a5b7a101181320",
    "stagePass": "01afac12f07c40dc959d2d84b95e833d",
    "readyPass": "f793cf0906094e71904e6efca67043e7",
    "lootAfterKill": "7f366db18af74bd0a1e8d3defa4d4d9c",
    "collectFirst": "3a604f3badb44d4ab2812810f06705b3",
    "collectSecond": "a96539338a3641e6a1069744f6d6a1a1",
    "readyAfterLoot": "e1b33394e4404c64a3681a04dda9e5c3",
}
RAW_PATHS = {name: BASE / f"{value}.json" for name, value in RAW.items()}
for path in RAW_PATHS.values():
    add_source(SOURCES, path)
    assert read(path)["session"] == SESSION

before = hero(read(RAW_PATHS["equipmentBefore"]))
unequip = read(RAW_PATHS["unequip"])
equip = read(RAW_PATHS["equip"])
after = hero(read(RAW_PATHS["equipmentAfter"]))
assert item_count(before, "Body", 59) == 1 and item_count(before, "Backpack", 59) == 0
assert unequip["method"] == "CharacterOverworld.UnequipItem(item,true)" and unequip["item"] == 59
assert item_count(unequip["before"], "Body", 59) == 1 and item_count(unequip["after"], "Backpack", 59) == 1
assert item_count(unequip["after"], "Body", 59) == 0
assert equip["method"] == "CharacterOverworld.EquipItem(item,false)" and equip["item"] == 59
assert item_count(equip["before"], "Backpack", 59) == 1 and item_count(equip["after"], "Body", 59) == 1
assert item_count(equip["after"], "Backpack", 59) == 0
assert item_count(after, "Body", 59) == 1 and item_count(after, "Backpack", 59) == 0
assert (before["lease"]["leaseId"], unequip["after"]["lease"]["leaseId"], after["lease"]["leaseId"]) == (4, 6, 7)
assert before["celInstanceId"] != unequip["after"]["celInstanceId"] != after["celInstanceId"]
assert before["dummyCelInstanceId"] != unequip["after"]["dummyCelInstanceId"] != after["dummyCelInstanceId"]

default_overworld = read(RAW_PATHS["overworldDefault"])
default_combat = read(RAW_PATHS["combatDefault"])
gambeson_overworld = read(RAW_PATHS["overworldGambeson"])
gambeson_combat = read(RAW_PATHS["combatGambeson"])
assert expected_meshes(default_overworld) == {key: value for key, value in EXPECTED_MESHES.items() if key != "armorGambesonF(Clone)"}
assert expected_meshes(default_combat) == {key: value for key, value in EXPECTED_MESHES.items() if key != "armorGambesonF(Clone)"}
assert expected_meshes(gambeson_overworld) == {key: value for key, value in EXPECTED_MESHES.items() if key != "armorBlacksmithF(Clone)"}
assert expected_meshes(gambeson_combat) == {key: value for key, value in EXPECTED_MESHES.items() if key != "armorBlacksmithF(Clone)"}
assert all(row["resourceLease"]["leaseId"] == 7 and row["resourceLease"]["references"] == 2
           for row in gambeson_combat["renderers"] if row.get("celRelativeRendererPath") in EXPECTED_MESHES)
watch = read(RAW_PATHS["watchAfterReequip"])
watch_by_id = {entry["leaseId"]: entry for entry in watch["watches"]}
assert set(watch_by_id) == {4, 6}
assert all(row["status"] == "observed-disposed" and row["leasePresent"] is False
           and row["references"] == 0 and row["allResourcesUnityNull"] is True and len(row["resources"]) == 15
           for row in watch_by_id.values())
assert read(RAW_PATHS["watchClear"])["status"] == "watch-references-cleared"
ready_after_reequip = read(RAW_PATHS["readyAfterReequip"])
assert ready_after_reequip["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

captures = [
    capture_case("native-attack", "case-ebfc5a76e8b44749acac20556ccc89dd", "e15347d06ac34e7f8bf8dd63053af9d7",
                 "attack", "player-combat", "armorGambesonF(Clone)", EXPECTED_MESHES["armorGambesonF(Clone)"], 120),
    capture_case("native-pass", "case-7d912e3902074e59a6a412c37c37e3c1", "39b81906434c44efaeddca9267d34fa8",
                 "pass", "player-combat", "armorGambesonF(Clone)", EXPECTED_MESHES["armorGambesonF(Clone)"], 120),
    standalone_capture("default-armor-ready", "f922dde44ca348b1b82ddfd16ec7aefd", "player-combat",
                       "armorBlacksmithF(Clone)", EXPECTED_MESHES["armorBlacksmithF(Clone)"], 24),
    capture_case("victory-fixture", "case-17c3e9955fde4c869cefd68d956ba5dc", "7ce9a329e00041408401704f718c77ee",
                 "kill-fixture", "enemies", "enTroll01", "ftkmf_glb_cairnfire-trollb.glb", 120),
]
attack_before = case_event(BASE / "case-ebfc5a76e8b44749acac20556ccc89dd", "before")
attack_after = case_event(BASE / "case-ebfc5a76e8b44749acac20556ccc89dd", "after")
pass_before = case_event(BASE / "case-7d912e3902074e59a6a412c37c37e3c1", "before")
pass_after = case_event(BASE / "case-7d912e3902074e59a6a412c37c37e3c1", "after")
assert (state_enemy_hp(attack_before), state_enemy_hp(attack_after)) == (72, 62)
assert (pass_before["party"][0]["hp"], pass_after["party"][0]["hp"]) == (970, 913)
kill = read(BASE / "case-17c3e9955fde4c869cefd68d956ba5dc" / "result.json")
assert kill["killFixtureHandoff"]["status"] == "victory_pending_native_loot"
assert kill["killFixtureHandoff"]["targetHp"] == 0 and kill["killFixtureHandoff"]["targetAlive"] is False
loot_state = read(RAW_PATHS["lootAfterKill"])
assert loot_state["strictLootCollect"] == {"ok": True, "buttonCount": 1}
assert all(read(RAW_PATHS[name])["status"] == "clicked" for name in ("collectFirst", "collectSecond"))
final_ready = read(RAW_PATHS["readyAfterLoot"])
assert final_ready["strictReady"] == {"ok": True, "level": 0, "room": 3, "buttonCount": 1}

videos = [
    video_from_frames(BASE / "e15347d06ac34e7f8bf8dd63053af9d7", 120, "native-attack"),
    video_from_frames(BASE / "39b81906434c44efaeddca9267d34fa8", 120, "native-pass"),
    video_from_frames(BASE / "f922dde44ca348b1b82ddfd16ec7aefd", 24, "default-armor-ready"),
    video_from_frames(BASE / "7ce9a329e00041408401704f718c77ee", 120, "victory-fixture"),
]
selected = [
    select_frame("gambeson-attack-start", "e15347d06ac34e7f8bf8dd63053af9d7", 16,
                 "Native attack_blunt1H begins with the custom Gambeson branch visible."),
    select_frame("gambeson-attack", "e15347d06ac34e7f8bf8dd63053af9d7", 27,
                 "Custom Gambeson branch during the native one-handed Blacksmith attack."),
    select_frame("gambeson-attack-damage", "e15347d06ac34e7f8bf8dd63053af9d7", 87,
                 "Custom Gambeson branch during native damageLight_blunt1H."),
    select_frame("gambeson-pass-damage", "39b81906434c44efaeddca9267d34fa8", 60,
                 "Native enemy response during a real pass; player HP later records 970 to 913."),
    select_frame("gambeson-pass-damage-repeat", "39b81906434c44efaeddca9267d34fa8", 108,
                 "Second sampled native damageLight_blunt1H response from the pass capture."),
    select_frame("default-armor-ready", "f922dde44ca348b1b82ddfd16ec7aefd", 10,
                 "The unequipped default armor branch is selected on the real combat avatar."),
]

metadata = []
for source in sorted(SOURCES):
    if source.suffix.lower() == ".png":
        continue
    raw = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    metadata.append({
        "source": relative(source),
        "sourceSha256": sha(source),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", IMAGE_PINS)
validation = {
    "status": "live_original_player_binding_motion_equipment_cleanup_and_native_progression_observed_preview_pending",
    "scope": "Fresh isolated Hearthveil Blacksmith run. It establishes the deployed original six-mesh package on blacksmith_Female through actual default and Gambeson apparel branches, native player combat, two avatar rebuilds, old-lease disposal, and normal loot progression. It does not establish character-creation preview or final art approval.",
    "profile": PROFILE,
    "session": SESSION,
    "identity": {
        "classId": CLASS_ID,
        "heroInstanceId": HERO_INSTANCE_ID,
        "skinset": "blacksmith_Female",
        "enemy": "ftkmf_modeltest_cairnfire_trollb",
        "playerCatalogSha256": sha(PLAYER_CATALOG),
        "enemyCatalogSha256": sha(ENEMY_CATALOG),
    },
    "provenance": {
        "stage": {"source": relative(STAGE), "sha256": sha(STAGE)},
        "deployment": {"source": relative(DEPLOYMENT), "sha256": sha(DEPLOYMENT)},
        "playerRegistration": {"source": relative(PLAYER_REGISTRATION), "sha256": sha(PLAYER_REGISTRATION)},
        "gameplaySession": {"source": relative(GAMEPLAY_SESSION_FILE), "sha256": sha(GAMEPLAY_SESSION_FILE)},
        "nativeCreateHelperDeployment": {"source": relative(NATIVE_CREATE_HELPER_DEPLOYMENT), "sha256": sha(NATIVE_CREATE_HELPER_DEPLOYMENT)},
        "gameAssets": ASSETS,
    },
    "avatarOwners": {
        "overworld": {"celInstanceId": after["celInstanceId"], "customSmrs": 5, "leaseId": 7, "references": 2},
        "combat": {"celInstanceId": after["dummyCelInstanceId"], "customSmrs": 5, "leaseId": 7, "references": 2},
        "preview": {"status": "pending_native_character_creation_ui", "observedAvatars": 0},
    },
    "equipment": {
        "item": 59,
        "name": "armorCloth1",
        "nativeSequence": "Body1/Backpack0 -> Body0/Backpack1 -> Body1/Backpack0",
        "operations": {
            "unequip": {"source": relative(RAW_PATHS["unequip"]), "sha256": sha(RAW_PATHS["unequip"])},
            "equip": {"source": relative(RAW_PATHS["equip"]), "sha256": sha(RAW_PATHS["equip"])},
        },
        "defaultBranch": {
            "selected": "armorBlacksmithF(Clone)",
            "mesh": EXPECTED_MESHES["armorBlacksmithF(Clone)"],
            "sharedRequiredPaths": ["playerBlacksmith", "hairTop", "hairBottom", "bootsBlacksmith(Clone)"],
        },
        "gambesonBranch": {
            "selected": "armorGambesonF(Clone)",
            "mesh": EXPECTED_MESHES["armorGambesonF(Clone)"],
            "sharedRequiredPaths": ["playerBlacksmith", "hairTop", "hairBottom", "bootsBlacksmith(Clone)"],
        },
        "oldLeaseDisposal": [
            {"leaseId": lease_id, "resources": len(row["resources"]), "status": row["status"],
             "leasePresent": row["leasePresent"], "allResourcesUnityNull": row["allResourcesUnityNull"]}
            for lease_id, row in sorted(watch_by_id.items())
        ],
    },
    "captures": captures,
    "gameplay": {
        "ordinaryAttack": {"enemyHp": [72, 62], "clip": "attack_blunt1H frames16..39"},
        "pass": {"heroHp": [970, 913], "clip": "damageLight_blunt1H frames59..69 and108..118"},
        "victory": {"method": "KillSingle fixture", "handoff": "victory_pending_native_loot"},
        "collects": 2,
        "finalReady": final_ready["strictReady"],
    },
    "selectedFrames": selected,
    "videos": videos,
    "visualReview": {
        "status": "sampled_live_combat_reviewed_preview_pending_final_art_unapproved",
        "observed": "Selected native combat frames show a coherent original Forge guardian silhouette with stable body, hair, boots, and Gambeson meshes. The native shield, hammer, backpack, helmet, effects, and combat UI remain visibly attached by the game.",
        "notAccepted": "No native character-creation preview was available while the local Mac UI was locked. This archive therefore does not make a preview, all-camera, culling, player-death, final-owner-teardown, or final-art claim.",
    },
    "limitations": [
        "The native attack records one same-target HP change. It does not infer balance, hit chance, block, dodge, or damage source.",
        "Pass proves a native turn was submitted and sampled incoming damage. It does not establish every enemy action or player damage state.",
        "KillSingle is an explicit fixture used only to reach the native Loot handoff; it is not ordinary lethal gameplay.",
        "Old-lease disposal applies to the two watched old avatar leases and their 15 recorded resources each. It does not establish global process leak freedom.",
        "This exact skinset and item59 outfit layout do not prove another Blacksmith skinset, apparel item, weapon controller, class, or multiplayer layout.",
    ],
}
write(OUT / "validation.json", validation)
integrity = {
    "status": "PASS",
    "validation": {"path": "validation.json", "sha256": sha(OUT / "validation.json")},
    "metadata": metadata,
    "sourceImages": {"count": len(IMAGE_PINS), "pins": "source-image-pins.json"},
    "selected": selected,
    "videos": videos,
}
write(OUT / "integrity.json", integrity)
print(json.dumps({
    "status": "PASS",
    "metadata": len(metadata),
    "sourceImages": len(IMAGE_PINS),
    "selected": len(selected),
    "videos": len(videos),
    "validationSha256": sha(OUT / "validation.json"),
}, indent=2))
