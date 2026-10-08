#!/usr/bin/env python3
"""Check the local Thief package inventory and referenced original assets."""

import json
import importlib.util
import math
from pathlib import Path
import struct
import hashlib


PACKAGE = Path(__file__).resolve().parent / "thief"
TIERS = ("street", "burglar", "guild", "masterwork", "locksmith", "nightblade", "wayfarer")
# Published 1.0.0 bow IDs are retained as save-compatible pistol identities.
FAMILIES = ("twins", "bow", "coat", "hood", "boots", "charm")
ARTIFACTS = ("thief_twins_skeleton_key", "thief_twins_candles_end",
             "thief_bow_unlost_road")
ARTIFACT_TAGS = dict(zip(ARTIFACTS, ("borrowedFortune", "lastLight", "looseAndLeave")))
ACTIONS = ("thief_pistol_fire", "thief_feint", "thief_draw_out", "thief_pierce", "thief_thread_needle",
           "thief_feint_locksmith", "thief_draw_out_locksmith",
           "thief_pierce_wayfarer", "thief_thread_needle_wayfarer")
EXCHANGE_ONLY = {"thief_" + family + "_" + tier for family in FAMILIES
                 for tier in ("locksmith", "nightblade", "wayfarer")}
HEAD_FITS = json.loads((PACKAGE.parent / "thief_head_fits.json").read_text())
HEAD_NAMES = ("human-female", "human-male", "undead", "cat", "demon", "fish", "goblin", "possum")
NATIVE_SKINSETS = ("treasureHunter_Female", "treasureHunter_Male", "treasureHunter_Undead",
                   "treasureHunter_Cat", "treasureHunter_Demon", "treasureHunter_Fish",
                   "treasureHunter_Goblin")


def check_worn_import(ledger):
    # Installed packages validate portable identities without private source files.
    if "wornCoatBootImport" not in ledger:
        return
    imported = ledger["wornCoatBootImport"]
    assert imported["schema"] == "ftkmf.thief.worn-coats-boots-import.v1"
    assert imported["originalAuthored"] is True and imported["nativeGeometryUsed"] is False
    assert imported["sourceLedger"] == "assets/thief-worn-coats-boots.provenance.json"
    data = (PACKAGE / imported["sourceLedger"]).read_bytes()
    assert hashlib.sha256(data).hexdigest() == imported["sourceLedgerSha256"]
    source = json.loads(data)
    assert source["schema"] == "ftkmf.thief.worn-coats-boots.v1"
    assert source["originalAuthored"] is True and source["nativeGeometryUsed"] is False
    assert source["planSha256"] == imported["planSha256"]
    assert source["scope"] == imported["scope"]
    assert source["remainingGates"] == imported["remainingGates"] and source["remainingGates"]
    expected = {"assets/thief-coat-%s-%s.glb" % (tier, sex)
                for tier in TIERS for sex in ("female", "male")}
    expected.update("assets/thief-boots-%s.glb" % tier for tier in TIERS)
    assert len(source["assets"]) == 21
    assert {row["path"] for row in source["assets"]} == expected
    inventory = {row["path"]: row for row in ledger["assets"]}
    assert len(inventory) == len(ledger["assets"])
    for value in (imported["planSha256"], imported["previousAdvancedLedgerSha256"],
                  imported["contentSha256"]):
        assert len(value) == 64 and all(c in "0123456789abcdef" for c in value)
    for row in source["assets"]:
        current = (PACKAGE / row["path"]).read_bytes()
        assert hashlib.sha256(current).hexdigest() == row["sha256"] == inventory[row["path"]]["sha256"]
        assert len(current) == row["bytes"] == inventory[row["path"]]["bytes"]
        assert row["scope"]
        for pin in (row["originalSource"], row["sourceFreeze"], row["evidence"]):
            path = pin["path"]
            assert path and "\\" not in path and ":" not in path
            assert not Path(path).is_absolute() and ".." not in Path(path).parts
            assert len(pin["sha256"]) == 64 and all(c in "0123456789abcdef" for c in pin["sha256"])
            assert isinstance(pin["bytes"], int) and pin["bytes"] > 0
        assert row["originalSource"]["sha256"] == row["sha256"]
        assert row["originalSource"]["bytes"] == row["bytes"]
        assert len(row["previousSha256"]) == 64 and all(c in "0123456789abcdef" for c in row["previousSha256"])


def check_coat_display_import(ledger):
    if "faithfulCoatDisplayImport" not in ledger:
        return
    imported = ledger["faithfulCoatDisplayImport"]
    assert imported["schema"] == "ftkmf.thief.faithful-coat-displays-import.v1"
    assert imported["sourceLedger"] == "assets/thief-coat-displays.provenance.json"
    data = (PACKAGE / imported["sourceLedger"]).read_bytes()
    assert hashlib.sha256(data).hexdigest() == imported["sourceLedgerSha256"]
    source = json.loads(data)
    assert source["schema"] == "ftkmf.thief.faithful-coat-displays.v1"
    for key in ("planSha256", "scope", "nativeCardApproval", "remainingGates"):
        assert source[key] == imported[key]
    assert source["remainingGates"]
    assert source["originalAuthored"] is imported["originalAuthored"] is True
    assert source["nativeGeometryUsed"] is imported["nativeGeometryUsed"] is False
    expected = {"assets/thief-coat-%s-display.glb" % tier for tier in TIERS}
    assert len(source["assets"]) == 7 and {row["path"] for row in source["assets"]} == expected
    inventory = {row["path"]: row for row in ledger["assets"]}
    assert len(inventory) == len(ledger["assets"])
    for row in source["assets"]:
        data = (PACKAGE / row["path"]).read_bytes()
        assert hashlib.sha256(data).hexdigest() == row["sha256"] == inventory[row["path"]]["sha256"]
        assert len(data) == row["bytes"] == inventory[row["path"]]["bytes"]
        for pin in (row["originalSource"], row["originalWornSource"], row["sourceFreeze"], row["evidence"]):
            path = pin["path"]
            assert path and "\\" not in path and ":" not in path
            assert not Path(path).is_absolute() and ".." not in Path(path).parts
            assert len(pin["sha256"]) == 64 and all(c in "0123456789abcdef" for c in pin["sha256"])
            assert isinstance(pin["bytes"], int) and pin["bytes"] > 0
        assert row["originalSource"]["sha256"] == row["sha256"]
        assert row["originalSource"]["bytes"] == row["bytes"]


def check_head_fits():
    assert set(HEAD_FITS) == set(TIERS)
    for tier in TIERS:
        source = HEAD_FITS[tier]
        assert set(source) == {"displayName", "profiles"}
        assert set(source["profiles"]) == set(HEAD_NAMES)
        for name, fit in source["profiles"].items():
            assert set(fit) in ({"nativeSkinset", "model"}, {"customRace", "model", "faceOcclusion"},
                                {"nativeSkinset", "model", "faceOcclusion"})
            if name == "possum":
                assert fit.get("customRace") == {"modGuid": "com.ftkmf.possum", "key": "possum"}
            else:
                assert fit.get("nativeSkinset") == NATIVE_SKINSETS[HEAD_NAMES.index(name)]
            expected_model = (f"assets/thief-hood-{tier}.glb" if name == "human-female" else
                              f"assets/thief-bandana-{tier}-{name}.glb")
            assert fit["model"] == expected_model
            assert (PACKAGE / expected_model).is_file(), expected_model
            face = fit.get("faceOcclusion")
            assert (face is None) == (name in ("human-female", "undead"))
            if face is None:
                continue
            assert set(face) == {"bodyPath", "planes", "upperHair", "lowerHair"}
            assert isinstance(face["bodyPath"], str) and face["bodyPath"]
            assert face["upperHair"] in ("preserve", "clipStrictHead")
            assert face["lowerHair"] in ("preserve", "clipStrictHead", "hideRenderer")
            assert isinstance(face["planes"], list) and 4 <= len(face["planes"]) <= 16
            for plane in face["planes"]:
                assert set(plane) == {"normal", "distance"}
                normal = plane["normal"]
                assert isinstance(normal, list) and len(normal) == 3
                assert all(type(v) in (float, int) and math.isfinite(v) and abs(v) <= 1000 for v in normal)
                assert type(plane["distance"]) in (float, int) and math.isfinite(plane["distance"])
                assert abs(plane["distance"]) <= 1000
                assert abs(sum(v * v for v in normal) - 1) <= .002001


def main():
    check_head_fits()
    manifest = json.loads((PACKAGE / "manifest.json").read_text())
    assert manifest["modGuid"] == "com.ftkmf.thief"
    content = json.loads((PACKAGE / "content.json").read_text())
    spec = importlib.util.spec_from_file_location("build_thief", PACKAGE.parent / "build_thief.py")
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    assert content == generator.generate(), "content.json has drifted from build_thief.py"
    entries = content["entries"]
    by_id = {row["id"]: row for row in entries}
    assert len(entries) == len(by_id) == 55
    assert {row["kind"] for row in entries} == {"class", "weapon", "item", "proficiency"}
    expected = {"thief"} | {"thief_" + family + "_" + tier
                                  for family in FAMILIES for tier in TIERS} | set(ARTIFACTS) | set(ACTIONS)
    assert set(by_id) == expected
    attack_actions = [row for row in entries if row["kind"] == "proficiency"]
    assert len(attack_actions) == len(ACTIONS)
    assert {row["id"] for row in attack_actions} == set(ACTIONS)
    for row in attack_actions:
        # The template has a two-check override. Zero selects the equipped weapon's checks.
        assert row["fields"]["m_SlotOverride"] == 0, row["id"]
        assert "\n" not in row["description"] and len(row["description"]) <= 25, row["id"]
        if row["precisionAction"] == "pierce":
            # Native adds "Ignore Armor" and wraps it with "Perfect (chance) =".
            assert row["description"] == "", row["id"]
        elif row["precisionAction"] == "prepare":
            assert row["description"] in ("60% dmg; hit prepares.", "80% dmg; hit prepares."), row["id"]
        native_icon = ("gunPierce" if row["precisionAction"] == "pierce" else "gunFire") if row["fields"]["m_GunShot"] else ("piercingattack" if row["precisionAction"] == "pierce" else "bladeDamage")
        assert row["nativeBattleButton"] == native_icon and "icon" not in row, row["id"]
    hero = by_id["thief"]
    assert hero["opportunist"] is True
    assert {skill for skill, enabled in hero["fields"]["skills"].items() if enabled} == {
        "m_Sneak", "m_Ambush", "m_TrapDisarm"}
    assert hero["fields"]["skills"]["m_MimicWhisper"] is False
    assert hero["fields"]["skills"]["m_FindTreasure"] is False
    assert hero["fields"]["startweapon"] == "thief_twins_street"
    assert hero["fields"]["startinggold"] == 3
    assert hero["fields"]["startitems"] == [
        "thief_coat_street", "thief_hood_street", "thief_boots_street", "conLockpicks"]
    # Weapon and capability icons also feed native combat action buttons.
    for row in entries:
        if row["kind"] in {"class", "weapon", "proficiency"}:
            assert "icon" not in row, row["id"]

    refs = set()

    def visit(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in {"model", "texture", "icon"} and isinstance(child, str):
                    asset = Path(child)
                    assert not asset.is_absolute() and ".." not in asset.parts
                    assert asset.parts[0] == "assets"
                    assert (PACKAGE / asset).is_file(), child
                    refs.add(child)
                else:
                    visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(entries)
    for row in entries[1:]:
        if row["kind"] == "proficiency":
            assert row["precisionAction"] in ("prepare", "pierce", "shot")
            assert row["fields"]["m_Target"] == "None"
            assert row["fields"]["m_RepeatCount"] == 0
            continue
        f = row["fields"]
        assert row["id"].startswith("thief_")
        assert f["minlevel"] <= f["maxlevel"]
        exchange_only = row["id"] in EXCHANGE_ONLY
        assert f["dropable"] is (not exchange_only)
        assert f["m_NightMarket"] is (not exchange_only)
        assert f["m_DungeonMerchant"] is (not exchange_only)
        assert f["_shopStock"] == (0 if exchange_only else 1)
        assert f["m_CollectLoreItemUnlock"] == "" and f["dlc"] == "None"
        assert "displayModels" in row
        assert ("icon" in row) == (row["kind"] == "item")
        assert "modifiers" in row
        if row["kind"] == "weapon":
            assert row["precisionWeapon"] == ("paired" if "_twins_" in row["id"] else "pistol")
            assert f["damagegain"] == 1
            assert f["skill"] == ("quickness" if "_twins_" in row["id"] else "talent")
            assert f["slots"] in (2, 3, 4)
            assert f["m_NoRegularAttack"] == (row["precisionWeapon"] == "pistol")
            if row["precisionWeapon"] == "pistol":
                assert f["m_ObjectSlot"] == "twoHands"
                assert row["proficiencies"][0] == "thief_pistol_fire"
                # CreateWeapon returns the detached Weapon child as the equipped root.
                assert [binding["path"] for binding in row["itemModels"]] == ["."]
                assert [binding["path"] for binding in row["displayModels"]] == ["gunDragon"]
            assert row["replaceProficiencies"] is True
            assert len(row["proficiencies"]) == (2 if row["precisionWeapon"] == "pistol" else 1)
            assert set(row["proficiencies"]) <= set(ACTIONS)
        else:
            assert "precisionWeapon" not in row
        if row["id"].startswith("thief_hood_"):
            tier = row["id"].removeprefix("thief_hood_")
            assert row["displayName"] == HEAD_FITS[tier]["displayName"]
            assert row["helmetHairVisibility"] == {"top": True, "bottom": True}
            assert [binding["path"] for binding in row["itemModels"]] == ["."]
            assert [binding["path"] for binding in row["displayModels"]] == ["helmKettle"]
            assert all(binding["texture"] == f"assets/thief-hood-{tier}-diffuse.png"
                       for field in ("itemModels", "displayModels") for binding in row[field])
            assert len(row["headProfiles"]) == 8
            for profile, fit in zip(row["headProfiles"], HEAD_FITS[tier]["profiles"].values()):
                expected = {key: fit[key] for key in ("nativeSkinset", "customRace") if key in fit}
                expected["model"] = {"path": ".", "model": fit["model"],
                                     "texture": f"assets/thief-hood-{tier}-diffuse.png", "matte": True}
                if "faceOcclusion" in fit:
                    expected["faceOcclusion"] = fit["faceOcclusion"]
                assert profile == expected
        if row["id"].startswith("thief_charm_"):
            tier = row["id"].removeprefix("thief_charm_")
            assert "itemModels" not in row and "offHandModels" not in row
            display = {"path": "trinketHorn2",
                "model": f"assets/thief-charm-{tier}-display.glb",
                "texture": f"assets/thief-charm-{tier}-diffuse.png"}
            if tier == "street":
                display["matte"] = True
            assert row["displayModels"] == [display]
        if row["id"].startswith("thief_twins_") and row["id"] not in ARTIFACTS:
            tier = row["id"].removeprefix("thief_twins_")
            prefix = "assets/thief-street-twins" if tier == "street" else f"assets/thief-twins-{tier}"
            routes = {"itemModels": [(".", ""), ("Break", "-fragment-1"), ("Break/Break", "-fragment-2")],
                      "offHandModels": [(".", "")],
                      "displayModels": [("dualKnife", "-display"), ("offHandWeapon", "-display-offhand")]}
            for field, bindings in routes.items():
                assert row[field] == [{"path": path, "model": prefix + suffix + ".glb",
                    "texture": prefix + "-advanced-shaded.png"} for path, suffix in bindings]
        if row["id"] in ARTIFACTS[:2]:
            tier = row["id"].removeprefix("thief_twins_").replace("_", "-")
            prefix = f"assets/thief-twins-{tier}"
            expected_routes = {
                "itemModels": [(".", ""), ("Break", "-fragment-1"), ("Break/Break", "-fragment-2")],
                "offHandModels": [(".", "-offhand")],
                "displayModels": [("dualKnife", "-display"), ("offHandWeapon", "-display-offhand")],
            }
            for field, routes in expected_routes.items():
                assert row[field] == [{"path": path, "model": prefix + suffix + ".glb",
                    "texture": prefix + "-diffuse.png"} for path, suffix in routes]
        if row["id"] in ARTIFACTS:
            assert f["rarity"] == "artifact" and f["townmarket"] is False
            assert row["thiefArtifact"] == ARTIFACT_TAGS[row["id"]]
        else:
            assert f["townmarket"] is (not exchange_only)
            assert "thiefArtifact" not in row
    assert by_id["thief_charm_locksmith"]["modifiers"]["focusCapacity"] == 1
    assert by_id["thief_twins_guild"]["fields"]["maxlevel"] == 4
    armor = [row for row in entries if "thiefArmor" in row]
    arms = [row for row in entries if "thiefArmament" in row]
    assert len(armor) == 9 and len(arms) == 6
    assert {row["thiefArmor"]["family"] for row in armor} == {"locksmith", "nightblade", "wayfarer"}
    for family in ("locksmith", "nightblade", "wayfarer"):
        assert {row["thiefArmor"]["slot"] for row in armor if row["thiefArmor"]["family"] == family} == {"head", "body", "feet"}
    assert all("thiefArmor" not in by_id[key] and "thiefArmament" not in by_id[key] for key in ARTIFACTS)
    offers = hero["townExchange"]["offers"]
    assert len(offers) == 18 and {row["item"] for row in offers} == EXCHANGE_ONLY
    assert by_id["thief_charm_guild"]["displayName"] == "Guild Insignia"
    # The imported source asset ledger remains independent of generated declarations.
    import hashlib
    ledger = json.loads((PACKAGE / "assets/advanced-import.provenance.json").read_text())
    for asset in ledger["assets"]:
        assert hashlib.sha256((PACKAGE / asset["path"]).read_bytes()).hexdigest() == asset["sha256"], asset["path"]
    check_worn_import(ledger)
    check_coat_display_import(ledger)
    assert all((PACKAGE / ref).is_file() for ref in refs)
    print("PASS: Thief class, 45 equipment rows, 9 actions, and %d referenced assets" % len(refs))


if __name__ == "__main__":
    main()
