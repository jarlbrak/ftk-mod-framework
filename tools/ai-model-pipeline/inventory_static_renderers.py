#!/usr/bin/env python3
"""Inventory exact direct-enemy MeshRenderer children without reading mesh data.

The regular skeleton inventory intentionally covers only SkinnedMeshRenderer
objects.  This companion records the small set of rigid MeshRenderer children
that are anchored below an exact mapped native enemy root.  It captures only
identity, hierarchy, component, mesh-pointer, and material-slot metadata; it
does not decode native vertices, normals, UVs, weights, textures, or animation
samples.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import UnityPy
from UnityPy.classes import PPtr


Json = dict[str, Any]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def resolve_path(root: Path, value: Path) -> Path:
    return value.resolve() if value.is_absolute() else (root / value).resolve()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def mapping_rows(mapping: Json) -> dict[tuple[str, int], list[str]]:
    rows = mapping.get("enemies")
    if not isinstance(rows, list):
        raise ValueError("mapping has no enemy rows")
    result: dict[tuple[str, int], list[str]] = {}
    seen: set[str] = set()
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("mapping enemy row must be an object")
        enemy = row.get("enemy_id")
        prefab = row.get("prefab_name")
        cel = row.get("cel_path_id")
        if (not isinstance(enemy, str) or not enemy or enemy in seen or
                not isinstance(prefab, str) or not prefab or
                not isinstance(cel, int) or cel <= 0):
            raise ValueError("mapping has an incomplete direct-enemy root identity")
        seen.add(enemy)
        result.setdefault((prefab, cel), []).append(enemy)
    return {key: sorted(value) for key, value in result.items()}


def inventory(assets: Path, mapping: Json) -> Json:
    """Read rigid child metadata from the exact mapping source asset.

    Every emitted row is rooted at both the mapped prefab name and the mapped
    CharacterEventListener path ID.  This prevents a visually similar rigid
    child from another prefab being accepted by a direct-enemy profile.
    """
    assets = assets.resolve()
    source_path = Path(mapping.get("source_file", "")).resolve()
    expected_hash = mapping.get("source_sha256")
    if not source_path.is_file() or not isinstance(expected_hash, str) or len(expected_hash) != 64:
        raise ValueError("mapping lacks a readable pinned source asset")
    if assets != source_path:
        raise ValueError("static inventory assets must be the exact mapping source asset")
    actual_hash = sha256(assets)
    if actual_hash != expected_hash:
        raise ValueError("mapping source asset hash has changed; regenerate the mapping first")
    anchors = mapping_rows(mapping)

    environment = UnityPy.load(str(assets))
    source = next((item for item in environment.files.values()
                   if Path(item.name).name == assets.name), None)
    if source is None:
        raise ValueError(f"Unity environment lacks selected source asset: {assets.name}")
    cache: dict[tuple[str, int], Json] = {}

    def identity(obj: Any) -> tuple[str, int]:
        return (obj.assets_file.name, obj.path_id)

    def tree(obj: Any) -> Json:
        if obj is None:
            return {}
        key = identity(obj)
        if key not in cache:
            cache[key] = obj.read_typetree(check_read=False)
        return cache[key]

    def resolve(owner: Any, pointer: Json | None) -> Any:
        if not pointer or not pointer.get("m_PathID"):
            return None
        return PPtr(m_FileID=pointer.get("m_FileID", 0), m_PathID=pointer["m_PathID"],
                    assetsfile=owner.assets_file).deref()

    def ref(owner: Any, field: str) -> Any:
        return resolve(owner, tree(owner).get(field))

    def name(obj: Any) -> str | None:
        value = tree(obj).get("m_Name") if obj else None
        return value if isinstance(value, str) and value else None

    def components(game_object: Any) -> list[Any]:
        return [resolve(game_object, item.get("component", item))
                for item in tree(game_object).get("m_Component", [])]

    def ancestors(game_object: Any) -> list[Any]:
        result: list[Any] = []
        seen: set[tuple[str, int]] = set()
        while game_object and identity(game_object) not in seen:
            seen.add(identity(game_object))
            result.append(game_object)
            transform = next((component for component in components(game_object)
                              if component and component.type.name in ("Transform", "RectTransform")), None)
            parent = ref(transform, "m_Father") if transform else None
            game_object = ref(parent, "m_GameObject") if parent else None
        return result

    def listener_ids(chain: list[Any]) -> set[int]:
        found: set[int] = set()
        for game_object in chain:
            for component in components(game_object):
                if not component or component.type.name != "MonoBehaviour":
                    continue
                try:
                    script = ref(component, "m_Script")
                    if tree(script).get("m_ClassName") == "CharacterEventListener":
                        found.add(component.path_id)
                except Exception:
                    # An unrelated malformed MonoBehaviour must not turn into a
                    # claimed CharacterEventListener.
                    continue
        return found

    rows: list[Json] = []
    mesh_renderer_count = 0
    for obj in source.objects.values():
        if obj.type.name != "MeshRenderer":
            continue
        mesh_renderer_count += 1
        row: Json = {"rendererId": obj.path_id, "rendererKind": "MeshRenderer", "errors": []}
        try:
            renderer_tree = tree(obj)
            game_object = ref(obj, "m_GameObject")
            chain = ancestors(game_object)
            names = [name(item) for item in chain]
            listeners = listener_ids(chain)
            matching_anchors: list[tuple[int, str, int, list[str]]] = []
            for index, prefab in enumerate(names):
                if not prefab:
                    continue
                for (candidate_prefab, cel), enemy_ids in anchors.items():
                    if candidate_prefab == prefab and cel in listeners:
                        matching_anchors.append((index, prefab, cel, enemy_ids))
            if not matching_anchors:
                continue
            # A child can encounter only one selected native root.  Multiple
            # compatible direct rows may intentionally share that root.
            nearest_index = min(item[0] for item in matching_anchors)
            selected = [item for item in matching_anchors if item[0] == nearest_index]
            if len({(item[1], item[2]) for item in selected}) != 1:
                row["errors"].append("ambiguous mapped native root ancestry")
                continue
            _, prefab, cel, enemy_ids = selected[0]
            path_parts = list(reversed(names[:nearest_index]))
            if (not path_parts or any(not part or part in (".", "..") or "/" in part or "\\" in part
                                      for part in path_parts)):
                row["errors"].append("unrepresentable rigid renderer path")
                continue
            co_located = components(game_object)
            filters = [component for component in co_located if component and component.type.name == "MeshFilter"]
            skinned = [component for component in co_located if component and component.type.name == "SkinnedMeshRenderer"]
            mesh = ref(filters[0], "m_Mesh") if len(filters) == 1 else None
            material_pointers = renderer_tree.get("m_Materials", [])
            material_ids = [pointer.get("m_PathID") for pointer in material_pointers if isinstance(pointer, dict)]
            row.update({
                "nativeEnemyCandidates": enemy_ids,
                "nativePrefab": prefab,
                "celPathId": cel,
                "rendererName": name(game_object),
                "rendererPath": "/".join(path_parts),
                "ancestorNames": names,
                "meshFilterCount": len(filters),
                "meshFilterIds": [component.path_id for component in filters],
                "coLocatedSkinnedMeshRendererCount": len(skinned),
                "coLocatedSkinnedMeshRendererIds": [component.path_id for component in skinned],
                "nativeMesh": ({"pathId": mesh.path_id, "assetFile": Path(mesh.assets_file.name).name,
                                "name": name(mesh)} if mesh else None),
                "nativeMaterialSlotCount": len(material_pointers),
                "nativeNonNullMaterialSlotCount": sum(isinstance(value, int) and value > 0 for value in material_ids),
                "nativeMaterialPathIds": material_ids,
            })
            row["strictStaticEligible"] = (
                row["meshFilterCount"] == 1 and row["coLocatedSkinnedMeshRendererCount"] == 0 and
                isinstance(row["nativeMesh"], dict) and row["nativeMesh"].get("pathId", 0) > 0 and
                row["nativeMaterialSlotCount"] == 1 and row["nativeNonNullMaterialSlotCount"] == 1
            )
        except Exception as error:
            row["errors"].append(f"{type(error).__name__}: {error}")
        rows.append(row)
    rows.sort(key=lambda row: (str(row.get("nativeEnemyCandidates", [""])[0]),
                               str(row.get("rendererPath", "")), int(row["rendererId"])))
    return {
        "schemaVersion": 1,
        "scope": "Read-only direct-enemy rigid MeshRenderer inventory. It proves only serialized root/path/component/slot identity, never an original mesh, runtime binding, motion, culling, material response, or art acceptance.",
        "source": {"path": str(assets), "sha256": actual_hash},
        "mapping": {"sourceAssetSha256": expected_hash},
        "summary": {
            "meshRendererCount": mesh_renderer_count,
            "directEnemyAnchoredMeshRenderers": len(rows),
            "strictStaticEligible": sum(row.get("strictStaticEligible") is True for row in rows),
            "rowsWithErrors": sum(bool(row.get("errors")) for row in rows),
        },
        "renderers": rows,
        "limitations": [
            "Only MeshRenderer objects serialized directly in the selected mapping source asset are enumerated.",
            "A strict-eligible row is a route prerequisite, not an approval to reuse native geometry or a live acceptance result.",
            "External renderers, bundles, multiple material slots, and co-located SkinnedMeshRenderer targets remain outside this rigid one-filter workflow.",
        ],
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    mapping_path = resolve_path(root, args.mapping)
    output = resolve_path(root, args.output)
    if output.exists() and not args.overwrite:
        raise SystemExit(f"refusing to overwrite static renderer inventory: {output}")
    result = inventory(resolve_path(root, args.assets), read_object(mapping_path))
    result["mapping"].update({"path": relative(root, mapping_path), "sha256": sha256(mapping_path)})
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result["summary"], indent=2))


if __name__ == "__main__":
    main()
