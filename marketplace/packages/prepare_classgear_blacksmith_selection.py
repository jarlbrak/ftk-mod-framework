#!/usr/bin/env python3
"""Prepare an unaccepted Blacksmith banner selection from explicit candidate manifests.

Both manifests provide assetDirectory and items with id plus package-shaped visual
fields (icon, itemModels/displayModels, or apparelModels). Asset references are
relative to assetDirectory. Native routes identify matching package destinations.
The result pins current bytes but does not establish visual or live acceptance.
"""
import argparse
import copy
import json
from pathlib import Path

import build_classgear_blacksmith as builder
from validate_classgear_blacksmith import require, validate_selected_geometry


DEFAULT_OUTPUT = builder.CAMPAIGN / "banner-faithful/source-selection.candidate.json"


def pin(path):
    path = path.resolve()
    record = {"source": path.relative_to(builder.ROOT).as_posix(), "sha256": builder.digest(path)}
    builder.pinned_source(record)
    return record


def prepare(apparel_path, rigid_path):
    manifests = {"apparel": apparel_path.resolve(), "rigid": rigid_path.resolve()}
    selection = {
        "schema": "ftkmf.blacksmith-banner-selection.v1",
        "accepted": False,
        "sourceManifests": {name: pin(path) for name, path in manifests.items()},
        "entries": {},
        "assets": {},
    }
    baseline_assets = {}
    baseline_entries = builder.rigid_entries(baseline_assets) + builder.apparel_entries(baseline_assets)
    baseline_by_id = {entry["id"]: entry for entry in baseline_entries}
    sources = {}
    declared_hashes = {}
    for kind, manifest_path in manifests.items():
        manifest = json.loads(manifest_path.read_text())
        asset_root = manifest_path.parent / manifest["assetDirectory"]
        items = manifest["items"]
        for record in manifest["files"]:
            declared_path = manifest_path.parent / record["path"]
            require(not Path(record["path"]).is_absolute() and not declared_path.is_symlink(),
                    "invalid manifest file path")
            pinned = pin(declared_path)
            require(pinned["sha256"] == record["sha256"], "candidate manifest file hash is stale: " + record["path"])
            declared_hashes[pinned["source"]] = record["sha256"]
        expected_ids = ({"blacksmith_armor_kilnward", "blacksmith_boots_kilnward"}
                        if kind == "apparel" else builder.SELECTED_IDS - {
                            "blacksmith_armor_kilnward", "blacksmith_boots_kilnward"})
        require(len(items) == len(expected_ids) and {item["id"] for item in items} == expected_ids,
                kind + " manifest must contain exactly its complete Kilnward candidate set")
        for item in items:
            sources[item["id"]] = (item, asset_root)

    def add_asset(destination, asset_root, relative):
        source = asset_root / relative
        require(not Path(relative).is_absolute() and not source.is_symlink(), "invalid candidate asset path")
        record = pin(source)
        require(declared_hashes.get(record["source"]) == record["sha256"],
                "selected source is absent from its manifest inventory: " + record["source"])
        prior = selection["assets"].get(destination)
        require(prior is None or prior == record, "candidate sources disagree for shared destination: " + destination)
        selection["assets"][destination] = record

    for item_id in sorted(builder.SELECTED_IDS):
        candidate, asset_root = sources[item_id]
        base = baseline_by_id[item_id]
        visuals = {key: copy.deepcopy(base[key]) for key in sorted(builder.VISUAL_KEYS) if key in base}
        if item_id == "blacksmith_helmet_kilnward":
            require(candidate["template"] == "helmetCrown", "helmet candidate must use the verified crown template")
            visuals["displayModels"][0]["path"] = "helmCrown"
            visuals["itemModels"] = [{
                "path": ".",
                "model": "assets/blacksmith-helmet-kilnward.glb",
                "texture": visuals["displayModels"][0]["texture"],
            }]
        elif "template" in candidate:
            require(candidate["template"] == base["template"], "candidate changes a gameplay template")
        add_asset(visuals["icon"], asset_root, candidate["icon"])
        for group in ("itemModels", "displayModels", "apparelModels"):
            if group not in visuals:
                require(group not in candidate, "candidate adds an unaudited renderer group")
                continue
            if group == "apparelModels":
                chosen = candidate[group]
                require({key: value for key, value in chosen.items() if key != "renderers"} ==
                        {key: value for key, value in visuals[group].items() if key != "renderers"},
                        "candidate changes native apparel binding")
                candidate_rows, expected_rows = chosen["renderers"], visuals[group]["renderers"]
            else:
                candidate_rows, expected_rows = candidate[group], visuals[group]
            by_path = {row["path"]: row for row in candidate_rows}
            require(len(by_path) == len(candidate_rows) == len(expected_rows) and
                    set(by_path) == {row["path"] for row in expected_rows},
                    "candidate route inventory differs: " + item_id + " " + group)
            for expected in expected_rows:
                row = by_path[expected["path"]]
                require({key: value for key, value in row.items() if key not in ("model", "texture")} ==
                        {key: value for key, value in expected.items() if key not in ("model", "texture")},
                        "candidate route metadata differs: " + item_id)
                for key in ("model", "texture"):
                    add_asset(expected[key], asset_root, row[key])
        selection["entries"][item_id] = visuals

    selected_entries = copy.deepcopy(baseline_entries)
    for entry in selected_entries:
        if entry["id"] in selection["entries"]:
            entry.update(selection["entries"][entry["id"]])
    inventory = set().union(*(builder.visual_assets(entry) for entry in selected_entries))
    require(len(inventory) == 141 and sum(name.endswith(".glb") for name in inventory) == 81 and
            sum(name.endswith(".png") for name in inventory) == 60,
            "candidate must produce exactly 81 models and 60 PNG assets")
    validate_selected_geometry(selection, baseline_entries, baseline_assets)
    # Fail if a model agent changed the manifest or an asset while preparing this candidate.
    for record in list(selection["sourceManifests"].values()) + list(selection["assets"].values()):
        builder.pinned_source(record)
    return selection


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apparel-manifest", type=Path, required=True)
    parser.add_argument("--rigid-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    output = args.output.resolve()
    require(output != builder.SELECTION_PATH.resolve(), "candidate generator cannot write the active selection")
    require(not output.exists() or json.loads(output.read_text()).get("accepted") is not True,
            "candidate generator cannot overwrite an accepted selection")
    selection = prepare(args.apparel_manifest, args.rigid_manifest)
    output.parent.mkdir(parents=True, exist_ok=True)
    builder.write_json(output, selection)
    print("Prepared unaccepted six-piece selection; complete package inventory would be 141 assets:", output)


if __name__ == "__main__":
    main()
