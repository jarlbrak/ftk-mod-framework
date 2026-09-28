#!/usr/bin/env python3
"""Check the local Thief package inventory and referenced original assets."""

import json
import importlib.util
from pathlib import Path
import struct


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
    assert hero["icon"] == "assets/thief-slip-away-action.png"
    action_icons = {
        "thief_pistol_fire": "fire", "thief_feint": "feint",
        "thief_draw_out": "bait-shot", "thief_pierce": "pierce",
        "thief_thread_needle": "deadeye", "thief_feint_locksmith": "feint",
        "thief_draw_out_locksmith": "bait-shot", "thief_pierce_wayfarer": "pierce",
        "thief_thread_needle_wayfarer": "deadeye",
    }
    for action_id, name in action_icons.items():
        path = "assets/thief-" + name + "-action.png"
        assert by_id[action_id]["icon"] == path
        data = (PACKAGE / path).read_bytes()
        assert data[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", data[16:24]) == (256, 256)

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
        assert "icon" in row and "displayModels" in row
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
            # Neck and nape geometry cannot cover the kettle template's hidden crown.
            assert row["helmetHairVisibility"] == {"top": True, "bottom": True}
        if row["id"] in ARTIFACTS:
            assert f["rarity"] == "artifact" and f["townmarket"] is False
            assert row["thiefArtifact"] == ARTIFACT_TAGS[row["id"]]
        else:
            assert f["townmarket"] is True
            assert "thiefArtifact" not in row
    assert by_id["thief_charm_locksmith"]["modifiers"]["focusCapacity"] == 1
    assert by_id["thief_twins_guild"]["fields"]["maxlevel"] == 4
    assert all((PACKAGE / ref).is_file() for ref in refs)
    print("PASS: Thief class, 45 equipment rows, 9 actions, and %d referenced assets" % len(refs))


if __name__ == "__main__":
    main()
