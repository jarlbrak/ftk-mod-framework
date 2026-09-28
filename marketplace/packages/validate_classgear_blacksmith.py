#!/usr/bin/env python3
"""Validate Blacksmith's unpublished Native Class Gear source package."""
import argparse
import copy
import hashlib
import math
import struct
import build_classgear_blacksmith as builder
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT / "marketplace/packages/classgear"
CAMPAIGN = ROOT / "art-experiments/blacksmith-forge-rodin"
RIGID_MANIFEST_PATH = CAMPAIGN / "rigid/route-ready/manifest.json"
RIGID = json.loads(RIGID_MANIFEST_PATH.read_text())
RIGID_ASSETS = (RIGID_MANIFEST_PATH.parent / RIGID["assetDirectory"]).resolve()
APPAREL = CAMPAIGN / "apparel"
ARMOR_MANIFEST_PATH = APPAREL / "processed/armor-family-v2/manifest.json"
ARMOR_DISPLAYS_PATH = APPAREL / "processed/armor-family-v2/displays/manifest.json"
ARMOR_ICONS_PATH = APPAREL / "processed/armor-family-v2/icons/render-manifest.json"
BOOTS_MANIFEST_PATH = APPAREL / "boots/asset-manifest.json"
BOOTS_CAVITY_AUDIT_PATH = APPAREL / "boots/cavity-audit.json"
BOOTS_CAVITY_AUDIT = json.loads(BOOTS_CAVITY_AUDIT_PATH.read_text())
ARMOR_MANIFEST = json.loads(ARMOR_MANIFEST_PATH.read_text())
ARMOR_DISPLAYS = json.loads(ARMOR_DISPLAYS_PATH.read_text())
ARMOR_ICONS = json.loads(ARMOR_ICONS_PATH.read_text())
BOOTS_MANIFEST = json.loads(BOOTS_MANIFEST_PATH.read_text())
APPAREL_ROUTE_METADATA = ROOT / "art-experiments/blacksmith-forge/apparel/manifest.json"
APPAREL_BINDINGS_PATH = ROOT / "art-experiments/blacksmith-forge/apparel/bindings.json"
EXPECTED_TIERS = {
    "coalmark": (0, 0),
    "bellowsworn": (1, 2),
    "rivetwatch": (3, 3),
    "kilnward": (4, 6),
}
APPAREL_TIER = {
    "coalmark": "apprentice",
    "bellowsworn": "journeyman",
    "rivetwatch": "forgemaster",
    "kilnward": "anvilward",
}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_glb(path):
    raw = path.read_bytes()
    require(struct.unpack_from("<III", raw) == (0x46546c67, 2, len(raw)), "invalid GLB header")
    size, kind = struct.unpack_from("<II", raw, 12)
    require(kind == 0x4e4f534a, "missing GLB JSON")
    doc = json.loads(raw[20:20 + size])
    binary_size, kind = struct.unpack_from("<II", raw, 20 + size)
    require(kind == 0x004e4942 and 28 + size + binary_size == len(raw), "invalid GLB binary chunk")
    binary = raw[28 + size:]

    def accessor(index):
        a = doc["accessors"][index]
        view = doc["bufferViews"][a["bufferView"]]
        require(view.get("buffer", 0) == 0 and "sparse" not in a, "unsupported GLB accessor")
        component, component_size = {5121: ("B", 1), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}[a["componentType"]]
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
        stride = view.get("byteStride", width * component_size)
        offset = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        require(a.get("byteOffset", 0) + (a["count"] - 1) * stride + width * component_size <= view["byteLength"],
                "GLB accessor exceeds buffer view")
        values = [struct.unpack_from("<" + component * width, binary, offset + i * stride) for i in range(a["count"])]
        require(all(math.isfinite(v) for row in values for v in row), "nonfinite GLB accessor")
        return values

    return doc, accessor


def validate_selected_geometry(selection, baseline_entries, baseline_assets):
    """Read candidate bytes; old campaign receipts cannot establish new geometry validity."""
    native_by_destination = {}
    binding_data = json.loads(APPAREL_BINDINGS_PATH.read_text())["bindings"]
    apparel_assets = set().union(*(builder.visual_assets(selection["entries"][item_id])
                                  for item_id in ("blacksmith_armor_kilnward", "blacksmith_boots_kilnward")))
    by_id = {entry["id"]: entry for entry in baseline_entries}
    for item_id in ("blacksmith_armor_kilnward", "blacksmith_boots_kilnward"):
        original = by_id[item_id]["apparelModels"]
        chosen = selection["entries"][item_id]["apparelModels"]
        require(chosen["femaleBinding"] == original["femaleBinding"] and
                chosen["maleBinding"] == original["maleBinding"], "selected native binding changed")
        require(len(chosen["renderers"]) == len(original["renderers"]), "selected apparel renderer count changed")
        for row, native in zip(chosen["renderers"], original["renderers"]):
            require((row["path"], row["nativeMesh"]) == (native["path"], native["nativeMesh"]),
                    "selected apparel route changed")
            binding_key = ("boots" if item_id == "blacksmith_boots_kilnward" else
                           "female-armor" if "F(Clone)" in native["path"] else "male-armor")
            native_by_destination[row["model"]] = binding_data[binding_key]
    for relative, record in selection["assets"].items():
        source = builder.pinned_source(record)
        if source.suffix != ".glb":
            continue
        doc, read = read_glb(source)
        triangle_count = 0
        for mesh in doc["meshes"]:
            for primitive in mesh["primitives"]:
                require(primitive.get("mode", 4) == 4, "selected GLB is not triangles")
                attrs = primitive["attributes"]
                positions = read(attrs["POSITION"])
                normals = read(attrs["NORMAL"])
                require(len(read(attrs["TEXCOORD_0"])) == len(positions), "selected UV count mismatch")
                indices = [row[0] for row in read(primitive["indices"])]
                require(0 < len(positions) <= 65534 and len(normals) == len(positions), "selected vertex budget/normal mismatch")
                require(len(indices) > 0 and len(indices) % 3 == 0 and max(indices) < len(positions), "invalid selected triangles")
                triangle_count += len(indices) // 3
                for i in range(0, len(indices), 3):
                    a, b, c = [positions[k] for k in indices[i:i + 3]]
                    u, v = [b[k] - a[k] for k in range(3)], [c[k] - a[k] for k in range(3)]
                    cross = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
                    require(sum(x*x for x in cross) > 1e-24, "degenerate selected triangle")
                    average = [sum(normals[j][k] for j in indices[i:i + 3]) for k in range(3)]
                    require(sum(cross[k] * average[k] for k in range(3)) > 0,
                            "selected triangle winding opposes its normals")
                if relative in native_by_destination:
                    joints, weights = read(attrs["JOINTS_0"]), read(attrs["WEIGHTS_0"])
                    require(len(joints) == len(weights) == len(positions), "skin attribute count mismatch")
                    require(all(min(w) >= 0 and abs(sum(w)-1) < 1e-5 for w in weights), "invalid selected skin weights")
                    require(len(doc.get("skins", [])) == 1, "selected apparel must have one skin")
                    skin = doc["skins"][0]
                    require(all(0 <= j < len(skin["joints"]) for row in joints for j in row), "selected skin joint out of range")
        require(0 < triangle_count <= (15000 if relative in apparel_assets else 8000),
                "selected triangle budget exceeded: " + relative)
        if relative in native_by_destination:
            native = native_by_destination[relative]
            # Native row-major matrices become column-major GLB MAT4 accessors.
            native_binds = dict(zip(native["bone_names"],
                                    [tuple(matrix[row][column] for column in range(4) for row in range(4))
                                     for matrix in native["bindposes"]]))
            skin = doc["skins"][0]
            names = [doc["nodes"][j]["name"] for j in skin["joints"]]
            matrices = read(skin["inverseBindMatrices"])
            require(len(names) == len(set(names)) == len(matrices), "selected skin names or bind count mismatch")
            for name, matrix in zip(names, matrices):
                require(name in native_binds and all(abs(a-b) <= 1e-6 for a, b in zip(matrix, native_binds[name])),
                        "selected inverse bind differs from verified native binding: " + name)
        else:
            require(not doc.get("skins"), "rigid/display candidate unexpectedly has a skin")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, default=builder.SELECTION_PATH)
    args = parser.parse_args()
    baseline_assets = {}
    expected_entries = builder.rigid_entries(baseline_assets) + builder.apparel_entries(baseline_assets)
    baseline_entries = copy.deepcopy(expected_entries)
    selected_assets = dict(baseline_assets)
    selection = builder.apply_source_selection(expected_entries, selected_assets, args.selection)
    selected_ids = builder.SELECTED_IDS if selection is not None else set()
    manifest = json.loads((PACKAGE / "manifest.json").read_text())
    content = json.loads((PACKAGE / "content.json").read_text())
    receipt = json.loads((PACKAGE / "assets.provenance.json").read_text())
    route_metadata = json.loads(APPAREL_ROUTE_METADATA.read_text())
    bindings = json.loads(APPAREL_BINDINGS_PATH.read_text())["bindings"]
    receipt_assets = receipt["assets"]
    entries = content["entries"]
    by_id = {entry["id"]: entry for entry in entries}
    require(by_id == {entry["id"]: entry for entry in expected_entries},
            "package content differs from the deterministic selected definitions")
    require(receipt.get("sourceSelection") == selection, "package source selection differs from explicit selection")
    require(set(receipt_assets) == set(selected_assets), "selected asset inventory mismatch")
    for relative, (source, source_hash) in selected_assets.items():
        require(receipt_assets[relative] == {"source": source.relative_to(ROOT).as_posix(), "sha256": source_hash},
                "selected source provenance mismatch: " + relative)
    if selection is not None:
        validate_selected_geometry(selection, baseline_entries, baseline_assets)

    require(manifest["modGuid"] == "com.ftkmf.classgear", "unexpected stable package identity")
    require(manifest["version"] == "0.1.0", "unexpected source package version")
    require(len(entries) == 30 and len(by_id) == 30, "Blacksmith package must contain 30 unique items")
    rigid_by_id = {item["id"]: item for item in RIGID["items"]}
    rigid_ids = set(rigid_by_id)
    apparel_ids = {"blacksmith_" + slot + "_" + tier for slot in ("armor", "boots") for tier in EXPECTED_TIERS}
    require(set(by_id) == rigid_ids | apparel_ids, "package IDs differ from the authored inventory")
    counts = {band: 0 for band in EXPECTED_TIERS.values()}
    for entry in entries:
        fields = entry["fields"]
        band = (fields["minlevel"], fields["maxlevel"])
        require(band in counts, "unsupported item level band: " + str(band))
        counts[band] += 1
        require(entry["kind"] in ("item", "weapon"), "Blacksmith package adds only equipment")
        require(fields["dropable"] and fields["townmarket"] and fields["m_NightMarket"] and fields["m_DungeonMerchant"],
                "every item must use shared drop and shop pools: " + entry["id"])
        require(fields["_shopStock"] == 1 and fields["m_CollectLoreItemUnlock"] == "" and fields["dlc"] == "None",
                "unexpected lore, DLC or shop gating: " + entry["id"])
        require(entry["classAffinity"] == {"classId": "blacksmith", "modifiers": {"vitality": 0.01}},
                "every item must grant exactly one Blacksmith Vitality point while equipped")
        require(0 <= fields["goldvalue"] <= 600 and fields["rarity"] in ("common", "rare"),
                "out-of-band value or rarity: " + entry["id"])
        require("class" not in entry and "class" not in fields, "equipment must remain wearable by every class")
        require("icon" in entry, "missing inventory icon: " + entry["id"])
        if entry["template"] == "shieldblacksmith":
            require(entry["modifiers"].get("taunt") is True, "Blacksmith shield must retain native Taunt")
        if entry["kind"] == "weapon":
            require(fields["skill"] == "toughness" and fields["damagegain"] == 1,
                    "hammer must preserve Blacksmith's native Toughness progression")
        expected_damage = {
            "blacksmith_hammer_1h_coalmark": 10,
            "blacksmith_hammer_1h_bellowsworn": 17,
            "blacksmith_hammer_1h_rivetwatch": 24,
            "blacksmith_hammer_1h_kilnward": 29,
            "blacksmith_hammer_2h_rivetwatch": 30,
            "blacksmith_hammer_2h_kilnward": 33,
        }
        if entry["id"] in expected_damage:
            require(fields["damage"] == expected_damage[entry["id"]],
                    "weapon damage differs from the reviewed balance ledger")
        if entry["id"].startswith("blacksmith_hammer_1h_"):
            require(entry["template"] == "bluntSmithHammer" and fields["slots"] == 4,
                    "one-handed hammer template or sockets changed")
            require([model["path"] for model in entry["itemModels"]] == [".", "Break", "Break/Break"],
                    "one-handed equipped fragment route changed")
        if entry["id"].startswith("blacksmith_hammer_2h_"):
            require(entry["template"] == "bluntWarHammer" and fields["slots"] == 5,
                    "two-handed maul template or sockets changed")
            require([model["path"] for model in entry["itemModels"]] == [".", "break", "break/break2", "break/break1"],
                    "two-handed equipped fragment route changed")
        for group in ("itemModels", "displayModels"):
            for model in entry.get(group, []):
                for key in ("model", "texture"):
                    require((PACKAGE / model[key]).is_file(), "missing " + key + " asset for " + entry["id"])
        require((PACKAGE / entry["icon"]).is_file(), "missing icon file for " + entry["id"])
        if entry["id"] in rigid_ids and entry["id"] not in selected_ids:
            source = rigid_by_id[entry["id"]]
            require(entry["displayName"] == source["name"] and entry["template"] == source["template"],
                    "rigid source identity changed: " + entry["id"])
            for group in ("itemModels", "displayModels"):
                expected = [
                    {"path": row["path"], "model": "assets/" + row["model"], "texture": "assets/" + row["texture"]}
                    for row in source.get(group, [])
                ]
                require(entry.get(group, []) == expected, "rigid route differs from fresh source: " + entry["id"])
                for row in source.get(group, []):
                    for key in ("model", "texture"):
                        record = receipt_assets["assets/" + row[key]]
                        source_path = (RIGID_ASSETS / row[key]).relative_to(ROOT).as_posix()
                        require(record["source"] == source_path,
                                "rigid package asset differs from its fresh route source: " + entry["id"])
            require(entry["icon"] == "assets/" + source["icon"],
                    "rigid icon differs from fresh source: " + entry["id"])
            require(receipt_assets[entry["icon"]]["source"] ==
                    (RIGID_ASSETS / source["icon"]).relative_to(ROOT).as_posix(),
                    "rigid icon source path changed: " + entry["id"])

    require(counts == {(0, 0): 7, (1, 2): 7, (3, 3): 8, (4, 6): 8}, "unexpected level-band inventory: " + str(counts))
    require(len(rigid_ids) == 22 and len(apparel_ids) == 8, "model inventory changed")

    apparel_assets = {row["key"]: row for row in route_metadata["assets"]}
    armor_rows = {(row["tier"], row["sex"]): row for row in ARMOR_MANIFEST["items"]}
    require(ARMOR_MANIFEST["priorGeometryImported"] is False and
            ARMOR_MANIFEST["nativeSurfaceDataUsed"] is False,
            "armor campaign must use fresh geometry without native surface input")
    require(len(armor_rows) == 8 and
            {(tier, sex) for tier in EXPECTED_TIERS for sex in ("female", "male")} == set(armor_rows),
            "armor source manifest must contain all four tiers for both skinsets")
    require(all(row["triangles"] <= 15000 and row["vertices"] <= 65534 and
                row["exactInverseBinds"] and row["allTrianglesNondegenerateAndNormalAligned"]
                for row in armor_rows.values()), "armor source geometry or binding checks changed")
    require(len(ARMOR_DISPLAYS["artifacts"]) == 4 and len(ARMOR_ICONS["artifacts"]) == 4,
            "armor display and icon sources must cover all four tiers")
    boot_audits = {row["tier"]: row for row in BOOTS_CAVITY_AUDIT}
    require(set(boot_audits) == set(EXPECTED_TIERS), "boot source audit must cover all tiers")
    require(all(row["triangles"] <= 15000 and row["vertices"] <= 65534 and
                all(cavity["interiorFootPathClear"] for cavity in row["cavityChecks"])
                for row in boot_audits.values()), "boot source geometry or cavity checks changed")
    require(BOOTS_MANIFEST["authoringModel"] == "gpt-6-astra" and
            len([row for row in BOOTS_MANIFEST["files"] if row["path"].endswith(".glb")]) == 8,
            "boot source files or authoring provenance changed")
    armor_display_rows = {row["itemId"]: row for row in ARMOR_DISPLAYS["artifacts"]}
    armor_icon_rows = {row["tier"]: row for row in ARMOR_ICONS["artifacts"]}
    for tier, art_tier in APPAREL_TIER.items():
        for slot in ("armor", "boots"):
            entry = by_id["blacksmith_" + slot + "_" + tier]
            if entry["id"] in selected_ids:
                continue
            require(entry["kind"] == "item" and entry["apparelModels"]["femaleBinding"] == "blacksmith_Female" and
                    entry["apparelModels"]["maleBinding"] == "blacksmith_Male", "wrong native apparel binding")
            renderers = entry["apparelModels"]["renderers"]
            if slot == "armor":
                expected = [
                    ("armorBlacksmithF(Clone)", "armorBlacksmith", "female-armor"),
                    ("armorBlacksmithM(Clone)", "armorBlacksmithM", "male-armor"),
                ]
            else:
                expected = [("bootsBlacksmith(Clone)", "bootsBlacksmith", "boots")]
            require(len(renderers) == len(expected), "unexpected apparel renderer count")
            for renderer, (path, mesh, binding_key) in zip(renderers, expected):
                require(renderer["path"] == path and renderer["nativeMesh"] == mesh,
                        "native apparel renderer route or mesh mismatch")
                require(binding_key in bindings, "missing audited native apparel binding")
                require((PACKAGE / renderer["model"]).is_file(), "missing skinned apparel model")
                sex = "female" if binding_key == "female-armor" else "male"
                if slot == "armor":
                    source_row = armor_rows[(tier, sex)]
                    source = (ARMOR_MANIFEST_PATH.parent / tier / source_row["file"]).relative_to(ROOT).as_posix()
                    require(receipt_assets[renderer["model"]]["source"] == source,
                            "armor renderer model differs from its fresh gendered source")
                else:
                    source = (APPAREL / "boots" / tier / ("blacksmith_boots_" + tier + ".glb")).relative_to(ROOT).as_posix()
                    require(receipt_assets[renderer["model"]]["source"] == source,
                            "boot renderer model differs from its fresh pair source")
            display_path = "armorSplintVestDisplay" if slot == "armor" else "bootsIronGreavesDisplay"
            require([row["path"] for row in entry["displayModels"]] == [display_path],
                    "apparel loot display route changed")
            display_source = ((ARMOR_DISPLAYS_PATH.parent / armor_display_rows[entry["id"]]["file"])
                              if slot == "armor" else APPAREL / "boots" / tier / "loot-display.glb")
            require(receipt_assets[entry["displayModels"][0]["model"]]["source"] ==
                    display_source.relative_to(ROOT).as_posix(), "apparel display differs from fresh static source")
            palette_source = ((ARMOR_MANIFEST_PATH.parent / tier / (tier + "-palette.png"))
                              if slot == "armor" else APPAREL / "boots" / tier / ("blacksmith_boots_" + tier + ".png"))
            for renderer in renderers:
                require(receipt_assets[renderer["texture"]]["source"] == palette_source.relative_to(ROOT).as_posix(),
                        "apparel renderer palette differs from fresh source")
            require(receipt_assets[entry["displayModels"][0]["texture"]]["source"] ==
                    palette_source.relative_to(ROOT).as_posix(), "apparel display palette differs from fresh source")
            require(entry["icon"].endswith("blacksmith-" + art_tier + "-" + slot + "-icon.png"),
                    "apparel icon progression mapping changed")
            icon_source = ((ARMOR_ICONS_PATH.parent / armor_icon_rows[tier]["icon"])
                           if slot == "armor" else APPAREL / "boots" / tier / "icon.png")
            require(receipt_assets[entry["icon"]]["source"] == icon_source.relative_to(ROOT).as_posix(),
                    "apparel item icon differs from fresh source")
            for renderer, skinset in zip(renderers, ("blacksmith_Female", "blacksmith_Male")):
                route = apparel_assets["blacksmith-" + art_tier + "-" + skinset.split("_")[1].lower() + "-" + slot]
                require(route["path"] == renderer["path"] and
                        route["expectedNativeMeshName"] == renderer["nativeMesh"],
                        "apparel renderer differs from native route metadata")

    files = receipt_assets
    require(receipt["geometryAuthor"] == "Astra High", "models must be authored by Astra High")
    require(len(files) == receipt["runtimeAssetCount"], "provenance inventory count mismatch")
    for relative, record in files.items():
        path = PACKAGE / relative
        require(path.is_file() and sha(path) == record["sha256"], "asset provenance mismatch: " + relative)
        require(relative.startswith("assets/") and not path.is_symlink(), "invalid runtime asset path")
        source_path = (ROOT / record["source"]).resolve()
        require(source_path.is_relative_to(ROOT.resolve()) and source_path.is_file() and not source_path.is_symlink(),
                "missing, nonregular, or out-of-repository fresh source: " + relative)
        require(sha(source_path) == record["sha256"], "packaged byte differs from fresh source: " + relative)
    runtime_paths = [path for path in (PACKAGE / "assets").rglob("*") if path.is_file()]
    require(all(not path.is_symlink() for path in runtime_paths), "runtime asset directory contains a symlink")
    runtime_inventory = {path.relative_to(PACKAGE).as_posix() for path in runtime_paths}
    require(runtime_inventory == set(files), "runtime assets differ from the provenance inventory")
    expected_source_hashes = {
        "rigidRouteReady": sha(RIGID_MANIFEST_PATH),
        "armorFamily": sha(ARMOR_MANIFEST_PATH),
        "armorStaticDisplays": sha(ARMOR_DISPLAYS_PATH),
        "armorIcons": sha(ARMOR_ICONS_PATH),
        "boots": sha(BOOTS_MANIFEST_PATH),
        "apparelRouteMetadata": sha(APPAREL_ROUTE_METADATA),
        "nativeApparelBindings": sha(APPAREL_BINDINGS_PATH),
    }
    if selection is not None:
        expected_source_hashes.update({"banner" + name.title(): record["sha256"]
                                       for name, record in selection["sourceManifests"].items()})
    require(receipt["sourceManifests"] == expected_source_hashes, "source manifest provenance changed")
    require(receipt["geometryAuthor"] == "Astra High", "models must be authored by Astra High")
    glb_count = sum(path.endswith(".glb") for path in files)
    png_count = sum(path.endswith(".png") for path in files)
    require(glb_count == (81 if selection is not None else 80) and png_count == 60, "runtime model or texture inventory changed")

    print("PASS: 30 unrestricted Blacksmith equipment entries, " +
          str(receipt["runtimeAssetCount"]) + " hashed runtime assets, four item bands")


if __name__ == "__main__":
    main()
