#!/usr/bin/env python3
"""Assemble the unpublished native class gear source package for Blacksmith."""
import argparse
import copy
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "marketplace/packages/classgear"
CAMPAIGN = ROOT / "art-experiments/blacksmith-forge-rodin"
RIGID_MANIFEST_PATH = CAMPAIGN / "rigid/route-ready/manifest.json"
RIGID_MANIFEST = json.loads(RIGID_MANIFEST_PATH.read_text())
RIGID = (RIGID_MANIFEST_PATH.parent / RIGID_MANIFEST["assetDirectory"]).resolve()
APPAREL = CAMPAIGN / "apparel"
ARMOR = APPAREL / "processed/armor-family-v2"
BOOTS = APPAREL / "boots"

# These files supply only native renderer/bone identities and route names. Model
# and texture bytes come from the fresh blacksmith-forge-rodin campaign above.
APPAREL_ROUTE_METADATA = ROOT / "art-experiments/blacksmith-forge/apparel/manifest.json"
APPAREL_BINDINGS = ROOT / "art-experiments/blacksmith-forge/apparel/bindings.json"
APPAREL_ROUTE_MANIFEST = json.loads(APPAREL_ROUTE_METADATA.read_text())
APPAREL_BINDING_DATA = json.loads(APPAREL_BINDINGS.read_text())
APPAREL_TIER = {
    "coalmark": "apprentice",
    "bellowsworn": "journeyman",
    "rivetwatch": "forgemaster",
    "kilnward": "anvilward",
}
LEVELS = {"coalmark": [0], "bellowsworn": [1, 2], "rivetwatch": [3], "kilnward": [4, 5, 6]}
PRICES = {
    "hammer-1h": {"coalmark": 12, "bellowsworn": 58, "rivetwatch": 178, "kilnward": 390},
    "hammer-2h": {"rivetwatch": 188, "kilnward": 370},
    "shield": {"coalmark": 10, "bellowsworn": 32, "rivetwatch": 75, "kilnward": 160},
    "helmet": {"coalmark": 16, "bellowsworn": 32, "rivetwatch": 75, "kilnward": 220},
    "trinket": {"coalmark": 10, "bellowsworn": 55, "rivetwatch": 135, "kilnward": 300},
    "necklace": {"coalmark": 12, "bellowsworn": 70, "rivetwatch": 180, "kilnward": 350},
    "armor": {"coalmark": 35, "bellowsworn": 85, "rivetwatch": 200, "kilnward": 560},
    "boots": {"coalmark": 12, "bellowsworn": 30, "rivetwatch": 80, "kilnward": 220},
}
MODIFIERS = {
    "shield": {
        "coalmark": {"armor": 1, "taunt": True},
        "bellowsworn": {"armor": 2, "resistance": 1, "taunt": True},
        "rivetwatch": {"armor": 3, "resistance": 2, "taunt": True},
        "kilnward": {"armor": 4, "resistance": 3, "taunt": True},
    },
    "helmet": {
        "coalmark": {"armor": 2},
        "bellowsworn": {"armor": 4, "resistance": 1},
        "rivetwatch": {"armor": 6, "resistance": 2},
        "kilnward": {"armor": 8, "resistance": 3},
    },
    "trinket": {
        "coalmark": {"armor": 1},
        "bellowsworn": {"armor": 2, "resistance": 1},
        "rivetwatch": {"armor": 2, "resistance": 2},
        "kilnward": {"armor": 3, "resistance": 3},
    },
    "necklace": {
        "coalmark": {"vitality": 0.02},
        "bellowsworn": {"vitality": 0.02},
        "rivetwatch": {"vitality": 0.03, "resistance": 1},
        "kilnward": {"vitality": 0.03, "resistance": 2},
    },
    "armor": {
        "coalmark": {"armor": 3},
        "bellowsworn": {"armor": 6, "resistance": 1},
        "rivetwatch": {"armor": 10, "resistance": 2},
        "kilnward": {"armor": 13, "resistance": 3},
    },
    "boots": {
        "coalmark": {"armor": 2},
        "bellowsworn": {"armor": 4, "resistance": 1},
        "rivetwatch": {"armor": 6, "resistance": 1},
        "kilnward": {"armor": 8, "resistance": 2},
    },
}
WEAPON_STATS = {
    "hammer-1h": {"coalmark": (10, 4), "bellowsworn": (17, 4), "rivetwatch": (24, 4), "kilnward": (29, 4)},
    "hammer-2h": {"rivetwatch": (30, 5), "kilnward": (33, 5)},
}
APPAREL_ITEMS = {
    "armor": {
        "template": "armorHeavy1",
        "names": {
            "coalmark": "Coalmark Forge Apron",
            "bellowsworn": "Bellowsworn Riveted Harness",
            "rivetwatch": "Rivetwatch Platecoat",
            "kilnward": "Kilnward Forgeplate",
        },
        "icon": "armor-icon.png",
        "display": "armor-display.glb",
        "display_path": "armorSplintVestDisplay",
    },
    "boots": {
        "template": "bootsHeavy3",
        "names": {
            "coalmark": "Coalmark Foundry Boots",
            "bellowsworn": "Bellowsworn Riveted Boots",
            "rivetwatch": "Rivetwatch Forge Greaves",
            "kilnward": "Kilnward Forge Greaves",
        },
        "icon": "boots-icon.png",
        "display": "boots-display.glb",
        "display_path": "bootsIronGreavesDisplay",
    },
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def levels(tier):
    return LEVELS[tier]


def common_fields(tier, family):
    band = levels(tier)
    return {
        "minlevel": band[0],
        "maxlevel": band[-1],
        "goldvalue": PRICES[family][tier],
        "rarity": "common" if band[-1] <= 2 else "rare",
        "dropable": True,
        "townmarket": True,
        "m_NightMarket": True,
        "m_DungeonMerchant": True,
        "_shopStock": 1,
        "m_CollectLoreItemUnlock": "",
        "dlc": "None",
    }


def affinity():
    return {"classId": "blacksmith", "modifiers": {"vitality": 0.01}}


def append_asset(asset_paths, source_dir, source_name):
    source = source_dir / source_name
    if not source.is_file() or source.is_symlink():
        raise ValueError("Missing or nonregular source asset: " + str(source))
    dest = "assets/" + source.name
    prior = asset_paths.get(dest)
    entry = (source, digest(source))
    if prior and prior[1] != entry[1]:
        raise ValueError("Asset basename collision: " + dest)
    asset_paths[dest] = entry
    return dest


def append_named_asset(asset_paths, source, destination_name):
    """Copy a fresh source asset under a collision-safe package basename."""
    if not source.is_file() or source.is_symlink():
        raise ValueError("Missing or nonregular source asset: " + str(source))
    dest = "assets/" + destination_name
    entry = (source, digest(source))
    prior = asset_paths.get(dest)
    if prior and prior[1] != entry[1]:
        raise ValueError("Asset basename collision: " + dest)
    asset_paths[dest] = entry
    return dest


def renderer_rows(rows, source_dir, asset_paths):
    result = []
    for row in rows:
        result.append({
            "path": row["path"],
            "model": append_asset(asset_paths, source_dir, row["model"]),
            "texture": append_asset(asset_paths, source_dir, row["texture"]),
        })
    return result


def rigid_entries(asset_paths):
    result = []
    for item in RIGID_MANIFEST["items"]:
        kind = item["kind"]
        tier = item["tier"]
        item_fields = common_fields(tier, kind)
        if kind.startswith("hammer-"):
            damage, slots = WEAPON_STATS[kind][tier]
            item_fields.update({"damage": damage, "skill": "toughness", "slots": slots, "damagegain": 1})
            entry_kind = "weapon"
        else:
            entry_kind = "item"

        entry = {
            "kind": entry_kind,
            "id": item["id"],
            "template": item["template"],
            "displayName": item["name"],
            "fields": item_fields,
            "classAffinity": affinity(),
        }
        if kind in MODIFIERS:
            entry["modifiers"] = MODIFIERS[kind][tier]
        entry["icon"] = append_asset(asset_paths, RIGID, item["icon"])
        if item.get("itemModels"):
            entry["itemModels"] = renderer_rows(item["itemModels"], RIGID, asset_paths)
        if item.get("displayModels"):
            entry["displayModels"] = renderer_rows(item["displayModels"], RIGID, asset_paths)
        result.append(entry)
    return result


def apparel_entries(asset_paths):
    result = []
    binding_data = APPAREL_BINDING_DATA["bindings"]
    apparel_routes = {}
    for route in APPAREL_ROUTE_MANIFEST["assets"]:
        if route.get("part") in ("armor", "boots") and route.get("skinset"):
            apparel_routes[(route["tier"], route["part"], route["skinset"])] = route
    for tier, art_tier in APPAREL_TIER.items():
        for part, definition in APPAREL_ITEMS.items():
            prefix = "blacksmith-" + art_tier + "-"
            item_id = "blacksmith_" + part + "_" + tier
            part_folder = ARMOR if part == "armor" else BOOTS
            palette = (ARMOR / tier / (tier + "-palette.png") if part == "armor" else
                       BOOTS / tier / ("blacksmith_boots_" + tier + ".png"))
            display = (ARMOR / "displays" / ("blacksmith-" + tier + "-armor-display.glb") if part == "armor" else
                       BOOTS / tier / "loot-display.glb")
            icon = (ARMOR / "icons" / ("blacksmith-" + tier + "-armor.png") if part == "armor" else
                    BOOTS / tier / "icon.png")
            entry = {
                "kind": "item",
                "id": item_id,
                "template": definition["template"],
                "displayName": definition["names"][tier],
                "fields": common_fields(tier, part),
                "modifiers": MODIFIERS[part][tier],
                "classAffinity": affinity(),
                "icon": append_named_asset(asset_paths, icon, prefix + part + "-icon.png"),
            }
            if not palette.is_file():
                raise ValueError("Missing tier-specific fresh apparel palette: " + str(palette))
            if part == "armor":
                female_route = apparel_routes[(art_tier, part, "blacksmith_Female")]
                male_route = apparel_routes[(art_tier, part, "blacksmith_Male")]
                if (female_route["expectedNativeMeshName"] != binding_data["female-armor"]["nativeMeshName"] or
                        male_route["expectedNativeMeshName"] != binding_data["male-armor"]["nativeMeshName"]):
                    raise ValueError("Armor mesh name disagrees with the native binding evidence")
                renderers = [
                    {
                        "path": female_route["path"],
                        "nativeMesh": female_route["expectedNativeMeshName"],
                        "model": append_named_asset(asset_paths, part_folder / tier / ("female-" + tier + ".glb"), prefix + "female-armor.glb"),
                        "texture": append_named_asset(asset_paths, palette, prefix + "armor-palette.png"),
                    },
                    {
                        "path": male_route["path"],
                        "nativeMesh": male_route["expectedNativeMeshName"],
                        "model": append_named_asset(asset_paths, part_folder / tier / ("male-" + tier + ".glb"), prefix + "male-armor.glb"),
                        "texture": append_named_asset(asset_paths, palette, prefix + "armor-palette.png"),
                    },
                ]
            else:
                boots_route = apparel_routes[(art_tier, part, "blacksmith_Female")]
                if boots_route["expectedNativeMeshName"] != binding_data["boots"]["nativeMeshName"]:
                    raise ValueError("Boot mesh name disagrees with the native binding evidence")
                boot_model = BOOTS / tier / ("blacksmith_boots_" + tier + ".glb")
                renderers = [{
                    "path": boots_route["path"],
                    "nativeMesh": boots_route["expectedNativeMeshName"],
                    "model": append_named_asset(asset_paths, boot_model, prefix + "female-boots.glb"),
                    "texture": append_named_asset(asset_paths, palette, prefix + "boots-palette.png"),
                }]
            entry["apparelModels"] = {
                "femaleBinding": "blacksmith_Female",
                "maleBinding": "blacksmith_Male",
                "renderers": renderers,
            }
            entry["displayModels"] = [{
                "path": definition["display_path"],
                "model": append_named_asset(asset_paths, display, prefix + part + "-display.glb"),
                "texture": append_named_asset(asset_paths, palette, prefix + part + "-palette.png"),
            }]
            result.append(entry)
    return result


# Selection is explicit. Candidate files appearing on disk cannot promote themselves.
SELECTION_PATH = Path(__file__).with_name("classgear_blacksmith.sources.json")
SELECTED_IDS = {"blacksmith_" + kind + "_kilnward" for kind in
                ("armor", "boots", "hammer_1h", "hammer_2h", "shield", "helmet")}
VISUAL_KEYS = {"icon", "itemModels", "displayModels", "apparelModels"}


def visual_assets(entry):
    paths = {entry["icon"]} if entry.get("icon") else set()
    for group in ("itemModels", "displayModels"):
        for row in entry.get(group, []):
            paths.update((row["model"], row["texture"]))
    for row in entry.get("apparelModels", {}).get("renderers", []):
        paths.update((row["model"], row["texture"]))
    return paths


def pinned_source(record):
    relative = Path(record["source"])
    path = ROOT / relative
    candidate_root = CAMPAIGN / "banner-faithful"
    if (relative.is_absolute() or ".." in relative.parts or path.is_symlink() or
            not path.resolve().is_relative_to(candidate_root.resolve()) or not path.is_file()):
        raise ValueError("Invalid banner candidate source: " + str(relative))
    if digest(path) != record["sha256"]:
        raise ValueError("Banner candidate hash mismatch: " + str(relative))
    return path


def apply_source_selection(entries, asset_paths, selection_path=SELECTION_PATH):
    """Select a complete reviewed outfit while preserving IDs and gameplay values.

    The optional JSON contains schema, accepted, sourceManifests (name to
    source/sha256), entries (six IDs to visual declarations), and assets
    (package-relative path to source/sha256). All sources live in banner-faithful.
    Keep accepted false until the parent has reviewed the fitted source visuals.
    """
    if not selection_path.exists():
        return None
    selection = json.loads(selection_path.read_text())
    if selection.get("schema") != "ftkmf.blacksmith-banner-selection.v1":
        raise ValueError("Unknown Blacksmith source selection schema")
    if selection.get("accepted") is not True:
        raise ValueError("Banner source selection is not visually accepted")
    if set(selection["entries"]) != SELECTED_IDS:
        raise ValueError("Banner selection must cover exactly the six Kilnward pieces")
    if set(selection["sourceManifests"]) != {"apparel", "rigid"}:
        raise ValueError("Both apparel and rigid candidate manifests must be pinned")
    for record in selection["sourceManifests"].values():
        pinned_source(record)
    by_id = {entry["id"]: entry for entry in entries}
    for item_id, visuals in selection["entries"].items():
        expected_keys = {key for key in VISUAL_KEYS if key in by_id[item_id]}
        if item_id == "blacksmith_helmet_kilnward":
            expected_keys.add("itemModels")
        if set(visuals) != expected_keys:
            raise ValueError("Incomplete or nonvisual candidate declaration: " + item_id)
        original = by_id[item_id]
        for key in expected_keys:
            if key == "icon":
                if visuals[key] != original[key]:
                    raise ValueError("Candidate must preserve package icon destination: " + item_id)
                continue
            if key == "apparelModels":
                if {k: v for k, v in visuals[key].items() if k != "renderers"} != {
                        k: v for k, v in original[key].items() if k != "renderers"}:
                    raise ValueError("Candidate must preserve native apparel bindings")
                candidate_rows, original_rows = visuals[key]["renderers"], original[key]["renderers"]
            else:
                candidate_rows, original_rows = visuals[key], original.get(key, [])
            if item_id == "blacksmith_helmet_kilnward" and key == "itemModels":
                if len(candidate_rows) != 1:
                    raise ValueError("Crown must have one equipped renderer")
                continue
            expected_rows = copy.deepcopy(original_rows)
            if item_id == "blacksmith_helmet_kilnward" and key == "displayModels":
                expected_rows[0]["path"] = "helmCrown"
            if candidate_rows != expected_rows:
                raise ValueError("Candidate must preserve audited routes and package destinations: " + item_id)
        for key in expected_keys:
            by_id[item_id][key] = copy.deepcopy(visuals[key])
    helmet = by_id["blacksmith_helmet_kilnward"]
    helmet["template"] = "helmetCrown"
    if ([r["path"] for r in helmet["itemModels"]] != ["."] or
            [r["path"] for r in helmet["displayModels"]] != ["helmCrown"]):
        raise ValueError("Banner helmet must use the verified native crown routes")
    expected_assets = set().union(*(visual_assets(by_id[item_id]) for item_id in SELECTED_IDS))
    if set(selection["assets"]) != expected_assets:
        raise ValueError("Banner selection must pin every selected visual asset exactly")
    for relative, record in selection["assets"].items():
        if not relative.startswith("assets/") or Path(relative).name != relative[len("assets/"):]:
            raise ValueError("Invalid candidate package asset destination: " + relative)
        asset_paths[relative] = (pinned_source(record), record["sha256"])
    used = set().union(*(visual_assets(entry) for entry in entries))
    for relative in list(asset_paths):
        if relative not in used:
            del asset_paths[relative]
    return selection


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, default=SELECTION_PATH)
    args = parser.parse_args()
    PACKAGE.mkdir(parents=True, exist_ok=True)
    assets_dir = PACKAGE / "assets"
    assets_dir.mkdir(exist_ok=True)
    asset_paths = {}
    entries = rigid_entries(asset_paths) + apparel_entries(asset_paths)
    baseline_entries, baseline_assets = copy.deepcopy(entries), dict(asset_paths)
    selection = apply_source_selection(entries, asset_paths, args.selection)
    if selection is not None:
        from validate_classgear_blacksmith import validate_selected_geometry
        validate_selected_geometry(selection, baseline_entries, baseline_assets)
    entries.sort(key=lambda row: (
        row["fields"]["minlevel"], row["fields"]["maxlevel"], row["id"]
    ))

    manifest = {
        "modGuid": "com.ftkmf.classgear",
        "name": "Blacksmith Forge Gear",
        "version": "0.1.0",
        "frameworkVersion": "1.0.1",
        "author": "JarlBrak",
        "description": "A 30-piece Blacksmith gear progression from Coalmark to Kilnward, with original low-poly models, unrestricted equipment, and a Blacksmith Vitality affinity.",
    }
    content = {"entries": entries}
    write_json(PACKAGE / "manifest.json", manifest)
    write_json(PACKAGE / "content.json", content)

    for relative, (source, _) in sorted(asset_paths.items()):
        destination = PACKAGE / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(source.read_bytes())

    provenance = {
        "schema": "ftkmf.native-class-gear.blacksmith.v1",
        "geometryAuthor": "Astra High",
        "runtimeAssetCount": len(asset_paths),
        "sourceManifests": {
            "rigidRouteReady": digest(RIGID_MANIFEST_PATH),
            "armorFamily": digest(ARMOR / "manifest.json"),
            "armorStaticDisplays": digest(ARMOR / "displays/manifest.json"),
            "armorIcons": digest(ARMOR / "icons/render-manifest.json"),
            "boots": digest(BOOTS / "asset-manifest.json"),
            "apparelRouteMetadata": digest(APPAREL_ROUTE_METADATA),
            "nativeApparelBindings": digest(APPAREL_BINDINGS),
        },
        "assets": {
            relative: {
                "source": source.relative_to(ROOT).as_posix(),
                "sha256": sha,
            }
            for relative, (source, sha) in sorted(asset_paths.items())
        },
        "scope": "Fresh blacksmith-forge-rodin geometry, palettes, icons and renderer route evidence. Static source checks pass. Native outfit/loot fit, item-card camera, animation, break state, save/resume, end-game balance and co-op remain live gates.",
    }
    if selection is not None:
        provenance["sourceSelection"] = selection
        provenance["sourceManifests"].update({
            "banner" + name.title(): record["sha256"]
            for name, record in selection["sourceManifests"].items()
        })
    write_json(PACKAGE / "assets.provenance.json", provenance)
    print("Built Blacksmith source package with", len(entries), "gear entries and", len(asset_paths), "runtime assets")


if __name__ == "__main__":
    main()
