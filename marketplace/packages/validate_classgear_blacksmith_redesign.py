#!/usr/bin/env python3
"""Validate exact approved-redesign assets and gameplay without claiming live fit."""
import argparse
import json
from pathlib import Path

import build_classgear_blacksmith_redesign as builder
from validate_classgear_blacksmith import read_glb, require


def validate(manifest, package):
    entries, assets, _ = builder.assemble(manifest)
    require(json.loads((package / "content.json").read_text()) == {"entries": entries}, "Package differs from approved art/gameplay inputs")
    weapons = [entry for entry in entries if entry["kind"] == "weapon"]
    require(len(weapons) == 8 and all(entry["fields"]["skill"] == "vitality" for entry in weapons),
            "All eight weapons must use the native Vitality stat, not toughness/Strength")
    require(all("icon" not in entry for entry in weapons),
            "Weapon icons feed native combat buttons and must retain native white-outline sprites")
    provenance = json.loads((package / "assets.provenance.json").read_text())
    require(set(provenance["assets"]) == set(assets), "Runtime asset inventory mismatch")
    limits, skinned = {}, set()
    for entry in entries:
        for route in ("itemModels", "displayModels"):
            require(route not in entry or bool(entry[route]), "Empty native model route: " + entry["id"])
        apparel = entry.get("apparelModels", {}).get("renderers", [])
        for row in apparel:
            skinned.add(row["model"])
        budget = 15000 if apparel else 8000
        for relative in builder.base.visual_assets(entry):
            if relative.endswith(".glb"):
                limits[relative] = min(limits.get(relative, budget), budget)
    summaries = {}
    for relative, (source, digest) in assets.items():
        target = builder.checked_path(package, relative)
        require(builder.base.digest(target) == digest == provenance["assets"][relative]["sha256"], "Changed runtime bytes: " + relative)
        if target.suffix == ".png":
            require(target.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", "Invalid PNG: " + relative)
            continue
        doc, read = read_glb(target)
        require(not any("uri" in b for b in doc.get("buffers", [])), "External GLB buffers are not allowed")
        triangles, vertices = 0, 0
        for mesh in doc["meshes"]:
            for primitive in mesh["primitives"]:
                require(primitive.get("mode", 4) == 4, "Nontriangle GLB")
                attrs = primitive["attributes"]
                positions, normals, uvs = read(attrs["POSITION"]), read(attrs["NORMAL"]), read(attrs["TEXCOORD_0"])
                count = len(positions)
                require(0 < count <= 65534 and len(normals) == count == len(uvs), "Vertex/attribute mismatch")
                indices = [x[0] for x in read(primitive["indices"])]
                require(len(indices) % 3 == 0 and all(0 <= i < count for i in indices), "Invalid triangle indices")
                triangles += len(indices) // 3
                vertices += count
                if relative in skinned:
                    joints, weights = read(attrs["JOINTS_0"]), read(attrs["WEIGHTS_0"])
                    require(len(joints) == count == len(weights), "Skin attribute mismatch")
                    require(all(all(w >= 0 for w in ws) and abs(sum(ws) - 1) < 0.002 for ws in weights), "Unnormalized skin weights")
        require(0 < triangles <= limits[relative] and vertices <= 65534, "Mesh exceeds approved runtime budget: " + relative)
        if relative in skinned:
            require(bool(doc.get("skins")), "Missing native skeleton binding")
            for skin in doc["skins"]:
                require(len(read(skin["inverseBindMatrices"])) == len(skin["joints"]), "Bindpose count mismatch")
        summaries[relative] = {"triangles": triangles, "vertices": vertices}
    return {"items": len(entries), "runtimeAssets": len(assets), "models": summaries,
            "scope": "Exact gameplay/source hashes and GLB structural budgets only. Native route, grip, appearance, card framing and combat remain live gates."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--redesign-manifest", type=Path, required=True)
    parser.add_argument("--package", type=Path, default=builder.base.PACKAGE)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    report = validate(args.redesign_manifest, args.package)
    if args.report:
        args.report.write_text(json.dumps(report, indent=2) + "\n")
    print("PASS:", report["items"], "items,", report["runtimeAssets"], "assets; live gates separate")


if __name__ == "__main__":
    main()
