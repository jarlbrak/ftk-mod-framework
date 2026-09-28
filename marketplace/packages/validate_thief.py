#!/usr/bin/env python3
"""Check the local Thief package inventory and referenced original assets."""

import json
import importlib.util
from pathlib import Path
import struct
from sync_nonweapon_icons import validate_package_icons
from sync_display_framing import validate_display_framing


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


def main():
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
        assert f["dropable"] is True and f["m_NightMarket"] is True
        assert f["m_DungeonMerchant"] is True and f["_shopStock"] == 1
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
            exposed = tier in ("street", "burglar")
            assert row["helmetHairVisibility"] == {"top": exposed, "bottom": exposed}
            assert [binding["path"] for binding in row["itemModels"]] == ["."]
            assert [binding["path"] for binding in row["displayModels"]] == ["helmKettle"]
            assert all(binding["texture"] == f"assets/thief-hood-{tier}-diffuse.png"
                       for field in ("itemModels", "displayModels") for binding in row[field])
        if row["id"].startswith("thief_charm_"):
            tier = row["id"].removeprefix("thief_charm_")
            assert "itemModels" not in row and "offHandModels" not in row
            assert row["displayModels"] == [{"path": "trinketHorn2",
                "model": f"assets/thief-charm-{tier}-display.glb",
                "texture": f"assets/thief-charm-{tier}-diffuse.png"}]
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
            assert f["townmarket"] is True
            assert "thiefArtifact" not in row
    assert by_id["thief_charm_locksmith"]["modifiers"]["focusCapacity"] == 1
    assert by_id["thief_twins_guild"]["fields"]["maxlevel"] == 4
    validate_package_icons("thief", entries)
    validate_display_framing("thief", entries)
    assert all((PACKAGE / ref).is_file() for ref in refs)
    print("PASS: Thief class, 45 equipment rows, 9 actions, and %d referenced assets" % len(refs))


if __name__ == "__main__":
    main()
