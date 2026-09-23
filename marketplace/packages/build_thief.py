"""Regenerate the Thief development package from its reviewed equipment ledger.

This is a source helper for the local development package, not a game runtime tool.
"""

import json
from pathlib import Path


HERE = Path(__file__).resolve().parent / "thief"
ASSETS = HERE / "assets"
TIERS = ("street", "burglar", "guild", "masterwork", "locksmith", "nightblade", "wayfarer")
SKILLS = (
    "m_SteadFast", "m_PartyHeal", "m_Sneak", "m_Ambush", "m_Flee",
    "m_DoorBash", "m_TrapDisarm", "m_TrapProceed", "m_CounterAttack",
    "m_EnergyBoost", "m_Refocus", "m_FindHerb", "m_Entertain",
    "m_Encourage", "m_Inspire", "m_Justice", "m_Distract", "m_CalledShot",
    "m_Discipline", "m_SupportRange", "m_Taunt", "m_MimicWhisper",
    "m_FindTreasure", "m_Glory", "m_BlackHole",
)
RANGES = ((0, 1), (1, 2), (2, 3), (3, 4), (4, 6), (4, 6), (4, 6))
RARITIES = ("common", "common", "uncommon", "uncommon", "rare", "rare", "rare")

PAIRS = (
    ("Street Twins", 10, 14), ("Windowfangs", 15, 45),
    ("Guild Twin Daggers", 20, 100), ("Velvet Fangs", 25, 200),
    ("Locksmith's Picks", 28, 390), ("Nightglass Twins", 30, 430),
    ("Trailbreakers", 29, 410),
)
BOWS = (
    ("Rooftop Bow", 10, 16), ("Alley Recurve", 14, 40),
    ("Guild Shortbow", 18, 85), ("Gloamwood Bow", 23, 180),
    ("Latchspring", 29, 390), ("Blackthorn", 32, 440),
    ("Farstep", 30, 420),
)
COATS = (
    ("Patched Jack", 1, 0, {}, 8),
    ("Burglar's Jack", 2, 1, {}, 26),
    ("Guild Leather", 3, 1, {}, 62),
    ("Masterwork Jack", 4, 2, {"vitality": .01}, 140),
    ("Locksmith's Coat", 4, 4, {"vitality": .02}, 300),
    ("Nightblade Jack", 5, 2, {"speed": .01}, 300),
    ("Wayfarer's Coat", 4, 3, {"awareness": .02}, 300),
)
HOODS = (
    ("Street Neckerchief", 0, 0, {"talent": .01}, 6),
    ("Burglar's Hood", 1, 0, {"talent": .01}, 18),
    ("Guild Hood", 1, 1, {"awareness": .01}, 44),
    ("Masterwork Cowl", 2, 1, {"talent": .02}, 100),
    ("Locksmith's Hood", 1, 3, {"talent": .03}, 220),
    ("Nightblade Cowl", 2, 1, {"speed": .02}, 220),
    ("Wayfarer's Hood", 1, 2, {"awareness": .03}, 220),
)
BOOTS = (
    ("Softstep Shoes", 0, 0, {"speed": .01}, 6),
    ("Burglar's Boots", 0, 1, {"speed": .01}, 18),
    ("Guild Treads", 1, 1, {"speed": .01}, 44),
    ("Masterwork Treads", 1, 2, {"speed": .02}, 100),
    ("Locksmith's Steps", 1, 2, {"speed": .02, "talent": .01}, 220),
    ("Nightblade Steps", 2, 1, {"speed": .03}, 220),
    ("Wayfarer's Treads", 1, 2, {"speed": .02, "awareness": .01}, 220),
)
CHARMS = (
    ("Bent Copper", {"talent": .01}, 8),
    ("Brass Pick", {"talent": .02}, 24),
    ("Guild Token", {"awareness": .02, "talent": .01}, 55),
    ("Silver Rook", {"speed": .01, "awareness": .02}, 130),
    ("Master Keyring", {"talent": .03, "focusCapacity": 1}, 290),
    ("Snuffed Wick", {"speed": .02, "talent": .02}, 290),
    ("Trail Compass", {"awareness": .03, "speed": .01}, 290),
)


def file(name):
    assert (ASSETS / name).is_file(), name
    return "assets/" + name


def model(path, name, palette):
    return {"path": path, "model": file(name), "texture": file(palette)}


def fields(index, gold, *, artifact=False):
    low, high = (4, 6) if artifact else RANGES[index]
    return {
        "minlevel": low, "maxlevel": high, "goldvalue": gold,
        "rarity": "artifact" if artifact else RARITIES[index],
        "dropable": True, "townmarket": not artifact,
        "m_NightMarket": True, "m_DungeonMerchant": True,
        "_shopStock": 1, "m_CollectLoreItemUnlock": "", "dlc": "None",
    }


def pair(index, *, artifact=None):
    tier = TIERS[index] if artifact is None else artifact
    name, damage, gold = PAIRS[index] if artifact is None else {
        "skeleton-key": ("The Skeleton Key", 27, 650),
        "candles-end": ("Candle's End", 28, 750),
    }[artifact]
    prefix = "thief-street-twins" if tier == "street" else "thief-twins-" + tier
    palette = ("thief-street-palette.png" if tier == "street" else
               "thief-artifact-palette.png" if artifact else
               "thief-twins-" + tier + "-palette.png")
    entry = {
        "kind": "weapon", "id": "thief_twins_" + tier.replace("-", "_"),
        "template": "dualKnife", "displayName": name,
        "precisionWeapon": "paired",
        "fields": {"damage": damage, "skill": "quickness", "slots": 2 if index < 3 and not artifact else 3,
                   "m_NoRegularAttack": False,
                   "damagegain": 1, **fields(index, gold, artifact=artifact)},
        "modifiers": {},
        "itemModels": [model(".", prefix + ".glb", palette),
                       model("Break", prefix + "-fragment-1.glb", palette),
                       model("Break/Break", prefix + "-fragment-2.glb", palette)],
        "offHandModels": [model(".", prefix + ("-offhand.glb" if artifact else ".glb"), palette)],
        "icon": file(prefix + "-icon.png"),
        "displayModels": [model("dualKnife", prefix + "-display.glb", palette),
                          model("offHandWeapon", prefix + "-display-offhand.glb", palette)],
    }
    if tier == "street":
        entry["fields"]["maxlevel"] = 1
    elif tier == "guild":
        entry["fields"]["maxlevel"] = 4
    entry["replaceProficiencies"] = True
    entry["proficiencies"] = [(
        "thief_pierce_wayfarer" if tier == "wayfarer" else "thief_pierce"
    ) if tier in ("burglar", "guild", "masterwork", "wayfarer", "skeleton-key") else (
        "thief_feint_locksmith" if tier == "locksmith" else "thief_feint"
    )]
    if artifact:
        entry["thiefArtifact"] = {"skeleton-key": "borrowedFortune",
                                  "candles-end": "lastLight"}[artifact]
    return entry


def bow(index, *, artifact=False):
    tier = "unlost-road" if artifact else TIERS[index]
    name, damage, gold = ("The Unlost Road", 29, 700) if artifact else BOWS[index]
    prefix = "thief-bow-" + tier
    palette = "thief-artifact-palette.png" if artifact else "thief-bow-palette.png"
    entry = {
        "kind": "weapon", "id": "thief_bow_" + tier.replace("-", "_"),
        "template": "bowShort", "displayName": name,
        "precisionWeapon": "bow",
        "fields": {"damage": damage, "skill": "awareness", "slots": 4,
                   "m_NoRegularAttack": False,
                   "damagegain": 1, **fields(index, gold, artifact=artifact)},
        "modifiers": {},
        "itemModels": [model(".", prefix + (".glb" if artifact else "-body.glb"), palette),
                       model("shortbowString", prefix + "-string.glb", palette),
                       model("Break", prefix + "-fragment-1.glb", palette),
                       model("Break/Break", prefix + "-fragment-2.glb", palette)],
        "icon": file(prefix + "-icon.png"),
        "displayModels": [model("shortbow", prefix + ("-display.glb" if artifact else "-display-body.glb"), palette),
                          model("shortbow/shortbowString", prefix + "-display-string.glb", palette)],
    }
    entry["replaceProficiencies"] = True
    entry["proficiencies"] = [(
        "thief_thread_needle_wayfarer" if tier == "wayfarer" else "thief_thread_needle"
    ) if tier in ("burglar", "guild", "masterwork", "wayfarer", "unlost-road") else (
        "thief_draw_out_locksmith" if tier == "locksmith" else "thief_draw_out"
    )]
    if artifact:
        entry["thiefArtifact"] = "looseAndLeave"
    return entry


def action(action_id, name, description, mode, multiplier, icon):
    return {
        "kind": "proficiency", "id": action_id, "template": "musicArmorDown",
        "displayName": name, "description": description,
        "precisionAction": mode, "icon": file(icon),
        "fields": {
            "m_DmgMultiplier": multiplier,
            "m_FullSlots": mode == "pierce",
            "m_Target": "None", "m_RepeatCount": 0,
            "m_ChanceToAffect": 0,
            **({"m_IgnoresArmor": True} if mode == "pierce" else {}),
        },
    }


def actions():
    return [
        action("thief_feint", "Feint", "Deal 60% damage. A damaging hit prepares your next precision attack if you are a Thief.",
               "prepare", .6, "thief-street-twins-icon.png"),
        action("thief_draw_out", "Draw Out", "Deal 60% damage. A damaging hit prepares your next precision attack if you are a Thief.",
               "prepare", .6, "thief-bow-street-icon.png"),
        action("thief_pierce", "Pierce", "Deal 75% damage. A perfect result ignores armor.",
               "pierce", .75, "thief-twins-guild-icon.png"),
        action("thief_thread_needle", "Thread the Needle", "Deal 75% damage. A perfect result ignores armor.",
               "pierce", .75, "thief-bow-guild-icon.png"),
        action("thief_feint_locksmith", "Feint", "Deal 80% damage. A damaging hit prepares your next precision attack if you are a Thief.",
               "prepare", .8, "thief-twins-locksmith-icon.png"),
        action("thief_draw_out_locksmith", "Draw Out", "Deal 80% damage. A damaging hit prepares your next precision attack if you are a Thief.",
               "prepare", .8, "thief-bow-locksmith-icon.png"),
        action("thief_pierce_wayfarer", "Pierce", "Deal 85% damage. A perfect result ignores armor.",
               "pierce", .85, "thief-twins-wayfarer-icon.png"),
        action("thief_thread_needle_wayfarer", "Thread the Needle", "Deal 85% damage. A perfect result ignores armor.",
               "pierce", .85, "thief-bow-wayfarer-icon.png"),
    ]


def apparel(index, family, data):
    tier = TIERS[index]
    name, armor, resistance, extra, gold = data
    prefix = "thief-" + family + "-" + tier
    palette = "thief-apparel-palette.png"
    template, display = {
        "coat": ("armorHeavy1", "armorSplintVestDisplay"),
        "hood": ("helmetHeavy1", "helmKettle"),
        "boots": ("bootsHeavy3", "bootsIronGreavesDisplay"),
    }[family]
    entry = {
        "kind": "item", "id": "thief_" + family + "_" + tier,
        "template": template, "displayName": name, "fields": fields(index, gold),
        "modifiers": {"armor": armor, "resistance": resistance, **extra},
        "icon": file(prefix + "-icon.png"),
        "displayModels": [model(display, prefix + "-display.glb", palette)],
    }
    if family == "hood":
        entry["itemModels"] = [model(".", prefix + ".glb", palette)]
    else:
        renderers = ([{"path": "armorBlacksmithF(Clone)", "nativeMesh": "armorBlacksmith",
                       "model": file(prefix + "-female.glb"), "texture": file(palette)},
                      {"path": "armorBlacksmithM(Clone)", "nativeMesh": "armorBlacksmithM",
                       "model": file(prefix + "-male.glb"), "texture": file(palette)}]
                     if family == "coat" else
                     [{"path": "bootsBlacksmith(Clone)", "nativeMesh": "bootsBlacksmith",
                       "model": file(prefix + ".glb"), "texture": file(palette)}])
        entry["apparelModels"] = {"femaleBinding": "blacksmith_Female",
                                   "maleBinding": "blacksmith_Male", "renderers": renderers}
    return entry


def charm(index):
    tier = TIERS[index]
    name, modifiers, gold = CHARMS[index]
    prefix = "thief-charm-" + tier
    return {
        "kind": "item", "id": "thief_charm_" + tier,
        "template": "trinketDefense1", "displayName": name,
        "fields": fields(index, gold), "modifiers": modifiers,
        "icon": file(prefix + "-icon.png"),
        "displayModels": [model("trinketHorn2", prefix + "-display.glb",
                                "thief-apparel-palette.png")],
    }


def generate():
    source = json.loads((HERE / "content.json").read_text())
    hero = source["entries"][0]
    hero["opportunist"] = True
    hero["icon"] = file("thief-slip-away-icon.png")
    hero["fields"]["skills"] = {skill: skill in ("m_Sneak", "m_Ambush", "m_TrapDisarm")
                                for skill in SKILLS}
    hero["fields"]["startitems"] = ["thief_coat_street", "thief_hood_street",
                                      "thief_boots_street", "conLockpicks"]
    hero["fields"]["startinggold"] = 3
    entries = [hero]
    for i in range(7):
        entries += [pair(i), bow(i), apparel(i, "coat", COATS[i]),
                    apparel(i, "hood", HOODS[i]), apparel(i, "boots", BOOTS[i]), charm(i)]
    entries += [pair(4, artifact="skeleton-key"), pair(5, artifact="candles-end"),
                bow(6, artifact=True)] + actions()
    assert len(entries) == 54
    assert len({entry["id"] for entry in entries}) == len(entries)
    return {"entries": entries}


def build():
    (HERE / "content.json").write_text(json.dumps(generate(), indent=2) + "\n")


if __name__ == "__main__":
    build()
