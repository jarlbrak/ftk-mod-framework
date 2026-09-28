#!/usr/bin/env python3
"""Build the approved 32-item local prototype from an explicit Rodin art ledger."""
import argparse
import copy
import json
from pathlib import Path

import build_classgear_blacksmith as base

TIERS = tuple(base.LEVELS)
KINDS = ("hammer-1h", "hammer-2h", "shield", "helmet", "trinket", "necklace", "armor", "boots")


def gameplay(item):
    tier, kind = item["tier"], item["kind"]
    index = TIERS.index(tier)
    fields = base.common_fields(tier, kind) if kind != "hammer-2h" or index > 1 else {
        **base.common_fields(tier, "hammer-1h"), "goldvalue": (14, 65)[index]}
    modifiers = copy.deepcopy(base.MODIFIERS.get(kind, {}).get(tier, {}))
    if kind == "necklace" and tier == "bellowsworn":
        modifiers["resistance"] = 1
    stat = "vitality" if kind in ("hammer-1h", "hammer-2h", "helmet", "necklace") else {
        "shield": "armor", "armor": "armor", "boots": "speed", "trinket": "resistance"}[kind]
    entry = {
        "kind": "weapon" if kind.startswith("hammer-") else "item",
        "id": item["id"], "template": item["template"], "displayName": item["name"],
        "fields": fields,
        "classAffinity": {"classId": "blacksmith", "modifiers": {stat: 0.01 if stat in ("vitality", "speed") else 1}},
    }
    if kind == "trinket":
        entry["displayName"] = tier.capitalize() + " Fire Kit"
    if modifiers:
        entry["modifiers"] = modifiers
    if kind.startswith("hammer-"):
        damage = (10, 17, 24, 29)[index] if kind == "hammer-1h" else (14, 22, 30, 33)[index]
        fields.update(damage=damage, slots=4 if kind == "hammer-1h" else 5, skill="vitality", damagegain=1)
        entry["blacksmithGear"] = ({"setHammerArmor": (2, 3, 4, 5)[index]} if kind == "hammer-1h" else
                                   {"overhandArmorPenalty": (2, 3, 4, 6)[index]})
    elif kind == "trinket" and index:
        entry["blacksmithGear"] = {"temperArmor": (0, 3, 4, 5)[index]}
    return entry


def checked_path(root, relative):
    rel = Path(relative)
    path = root / rel
    if rel.is_absolute() or ".." in rel.parts or path.is_symlink() or not path.resolve().is_relative_to(root.resolve()) or not path.is_file():
        raise ValueError("Invalid art source: " + str(relative))
    return path


def assemble(manifest_path):
    manifest_path = manifest_path.resolve()
    if not manifest_path.is_relative_to(base.CAMPAIGN.resolve()):
        raise ValueError("Art ledger must be inside the original Blacksmith campaign")
    art = json.loads(manifest_path.read_text())
    asset_root = (manifest_path.parent / art["assetDirectory"]).resolve()
    if not asset_root.is_relative_to(base.CAMPAIGN.resolve()):
        raise ValueError("Asset directory escapes campaign")
    expected = {(tier, kind) for tier in TIERS for kind in KINDS}
    if len(art["items"]) != 32 or {(i["tier"], i["kind"]) for i in art["items"]} != expected:
        raise ValueError("Art ledger must contain exactly eight identities in each of four tiers")
    assets, entries = {}, []
    for item in art["items"]:
        expected_id = "blacksmith_" + item["kind"].replace("-", "_") + "_" + item["tier"]
        if item["id"] != expected_id or not item.get("rodinGenerations") or not all(isinstance(x, str) and x.strip() for x in item["rodinGenerations"]):
            raise ValueError("Missing exact item identity or Rodin source lineage: " + item["id"])
        entry = gameplay(item)
        def asset(relative):
            source = checked_path(asset_root, relative)
            sha = base.digest(source)
            record = art["files"].get(relative)
            expected_sha = record.get("sha256") if isinstance(record, dict) else record
            if sha != expected_sha:
                raise ValueError("Unpinned or changed art asset: " + relative)
            if source.suffix not in (".png", ".glb"):
                raise ValueError("Unsupported runtime asset: " + relative)
            return base.append_named_asset(assets, source, sha[:16] + "-" + source.name)
        # Native combat uses the weapon item icon for its basic attack button.
        # Preserve the template white-outline sprite for every weapon.
        if entry["kind"] != "weapon":
            entry["icon"] = asset(item["icon"])
        for group in ("itemModels", "displayModels", "apparelModels"):
            if group not in item:
                continue
            value = copy.deepcopy(item[group])
            rows = value["renderers"] if group == "apparelModels" else value
            if not rows:
                continue
            for row in rows:
                row["model"], row["texture"] = asset(row["model"]), asset(row["texture"])
            entry[group] = value
        if not entry.get("displayModels") or (item["kind"] not in ("trinket", "necklace") and not (entry.get("itemModels") or entry.get("apparelModels"))):
            raise ValueError("Missing required native visual route: " + item["id"])
        entries.append(entry)
    entries.sort(key=lambda e: (e["fields"]["minlevel"], e["id"]))
    return entries, assets, art


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--redesign-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=base.PACKAGE)
    args = parser.parse_args()
    entries, assets, art = assemble(args.redesign_manifest)
    package = args.output.resolve()
    if not package.is_relative_to(base.ROOT):
        raise ValueError("Local prototype output must remain in workspace")
    package.mkdir(parents=True, exist_ok=True)
    # This is a local development package. Public compatibility requires a framework release.
    manifest = {"modGuid": "com.ftkmf.classgear", "name": "Blacksmith Forge Gear", "version": "0.2.0",
                "frameworkVersion": "1.0.2", "author": "JarlBrak",
                "description": "32 original Blacksmith items across four tiers. Choose shield strikes, exposed maul attacks, and limited armor reinforcement. Requires the unreleased Blacksmith framework capabilities."}
    base.write_json(package / "manifest.json", manifest)
    base.write_json(package / "content.json", {"entries": entries})
    for relative, (source, sha) in assets.items():
        target = package / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    base.write_json(package / "assets.provenance.json", {
        "schema": "ftkmf.native-class-gear.blacksmith.v2", "geometryAuthor": "Astra High",
        "artManifest": {"source": str(args.redesign_manifest.resolve().relative_to(base.ROOT)), "sha256": base.digest(args.redesign_manifest)},
        "runtimeAssetCount": len(assets),
        "rodinGenerations": {i["id"]: i["rodinGenerations"] for i in art["items"]},
        "assets": {rel: {"source": str(src.relative_to(base.ROOT)), "sha256": sha} for rel, (src, sha) in sorted(assets.items())},
        "scope": "Unpublished development candidate. Asset lineage and gameplay declarations do not establish visual acceptance, live balance, or public framework compatibility."})
    print("Built 32-item Blacksmith prototype with", len(assets), "runtime assets")


if __name__ == "__main__":
    main()
