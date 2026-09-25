#!/usr/bin/env python3
"""Check the local Thief package inventory and referenced original assets."""

import json
import importlib.util
from pathlib import Path


PACKAGE = Path(__file__).resolve().parent / "thief"
TIERS = ("street", "burglar", "guild", "masterwork", "locksmith", "nightblade", "wayfarer")
FAMILIES = ("twins", "bow", "coat", "hood", "boots", "charm")
ARTIFACTS = ("thief_twins_skeleton_key", "thief_twins_candles_end",
             "thief_bow_unlost_road")
ARTIFACT_TAGS = dict(zip(ARTIFACTS, ("borrowedFortune", "lastLight", "looseAndLeave")))
ACTIONS = ("thief_feint", "thief_draw_out", "thief_pierce", "thief_thread_needle",
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
    assert len(entries) == len(by_id) == 54
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
            assert row["precisionAction"] in ("prepare", "pierce")
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
            assert row["precisionWeapon"] == ("paired" if "_twins_" in row["id"] else "bow")
            assert f["damagegain"] == 1
            assert f["skill"] == ("quickness" if "_twins_" in row["id"] else "awareness")
            assert f["slots"] in (2, 3, 4)
            assert f["m_NoRegularAttack"] is False
            assert row["replaceProficiencies"] is True
            assert len(row["proficiencies"]) == 1
            assert set(row["proficiencies"]) <= set(ACTIONS)
        else:
            assert "precisionWeapon" not in row
        if row["id"] in ARTIFACTS:
            assert f["rarity"] == "artifact" and f["townmarket"] is False
            assert row["thiefArtifact"] == ARTIFACT_TAGS[row["id"]]
        else:
            assert f["townmarket"] is True
            assert "thiefArtifact" not in row
    assert by_id["thief_charm_locksmith"]["modifiers"]["focusCapacity"] == 1
    assert by_id["thief_twins_guild"]["fields"]["maxlevel"] == 4
    assert all((PACKAGE / ref).is_file() for ref in refs)
    print("PASS: Thief class, 45 equipment rows, 8 actions, and %d referenced assets" % len(refs))


if __name__ == "__main__":
    main()
