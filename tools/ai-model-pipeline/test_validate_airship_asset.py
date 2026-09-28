"""Original synthetic geometry exercises the offline airship audit without game data."""
import copy
import json
import struct
import tempfile
import unittest
from pathlib import Path

import numpy as np

from validate_airship_asset import audit, load_contract, matches_node, node_matrix


def plane(y=0, minx=-1, maxx=1, minz=-2, maxz=2, slope=0):
    return [[minx, y + slope * minx, minz], [maxx, y + slope * maxx, minz],
            [maxx, y + slope * maxx, maxz], [minx, y + slope * minx, maxz]]


def write_asset(folder, meshes=None, contract=None, mutate=None):
    meshes = meshes or [("deck", plane())]
    data = bytearray()
    doc = {"asset": {"version": "2.0"}, "buffers": [], "bufferViews": [], "accessors": [],
           "meshes": [], "nodes": [], "scenes": [{"nodes": list(range(len(meshes)))}], "scene": 0}

    def add(values, dtype, kind, component):
        raw = np.asarray(values, dtype=dtype).tobytes()
        doc["bufferViews"].append({"buffer": 0, "byteOffset": len(data), "byteLength": len(raw)})
        data.extend(raw)
        data.extend(b"\0" * (-len(data) % 4))
        doc["accessors"].append({"bufferView": len(doc["bufferViews"]) - 1, "componentType": component,
                                 "type": kind, "count": len(values)})
        return len(doc["accessors"]) - 1

    for i, (name, vertices) in enumerate(meshes):
        pos = add(vertices, "<f4", "VEC3", 5126)
        indices = add([0, 2, 1, 0, 3, 2], "<u2", "SCALAR", 5123)
        doc["meshes"].append({"primitives": [{"attributes": {"POSITION": pos}, "indices": indices}]})
        doc["nodes"].append({"name": name, "mesh": i})
    doc["buffers"] = [{"byteLength": len(data)}]
    if mutate:
        mutate(doc)
    js = json.dumps(doc).encode()
    js += b" " * (-len(js) % 4)
    raw = struct.pack("<III", 0x46546C67, 2, 28 + len(js) + len(data))
    raw += struct.pack("<II", len(js), 0x4E4F534A) + js
    raw += struct.pack("<II", len(data), 0x004E4942) + data
    path = folder / "test.glb"
    path.write_bytes(raw)
    default = {"schemaVersion": 1, "longAxis": "z",
               "hull": {"minX": -1, "maxX": 1, "minZ": -2, "maxZ": 2},
               "deck": {"y": 0, "minX": -1, "maxX": 1, "minZ": -2, "maxZ": 2}, "upperworks": []}
    path.with_suffix(".ship.json").write_text(json.dumps(contract or default))
    return path


class AirshipAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)

    def test_level_floor_passes_with_reproducible_report(self):
        path = write_asset(self.folder)
        result = audit(path, spacing=.25)
        self.assertEqual(result["status"], "PASS")
        self.assertEqual(result["deck"]["floor_coverage"], 1)
        self.assertEqual(result["deck"]["grid"], [8, 16])
        self.assertEqual(result, audit(path, spacing=.25))

    def test_props_do_not_masquerade_as_clear_floor(self):
        path = write_asset(self.folder, [("deck", plane()), ("crate", plane(.5, -.5, .5, -1, 1))])
        result = audit(path, spacing=.25)
        self.assertEqual(result["status"], "FAIL")
        self.assertEqual(result["deck"]["floor_coverage"], 1)
        self.assertEqual(result["deck"]["obstructed_fraction"], .25)
        self.assertEqual(result["deck"]["unobstructed_floor_coverage"], .75)

    def test_roof_alone_does_not_count_as_floor(self):
        path = write_asset(self.folder, [("roof", plane(.5)), ("keel", plane(-1, -.1, .1))])
        result = audit(path, spacing=.25)
        self.assertEqual(result["deck"]["floor_coverage"], 0)
        self.assertEqual(result["deck"]["obstructed_fraction"], 1)

    def test_hole_is_measured_even_with_valid_overall_bounds(self):
        path = write_asset(self.folder, [("left", plane(0, -1, -.25)), ("right", plane(0, .25, 1))])
        result = audit(path, spacing=.25)
        self.assertEqual(result["status"], "FAIL")
        self.assertEqual(result["deck"]["floor_coverage"], .75)

    def test_whole_named_upperwork_excluded(self):
        path = write_asset(self.folder, [("deck", plane()), ("lift", plane(1))])
        contract = json.loads(path.with_suffix(".ship.json").read_text())
        contract["upperworks"] = ["lift"]
        path.with_suffix(".ship.json").write_text(json.dumps(contract))
        result = audit(path, spacing=.25)
        self.assertEqual(result["status"], "PASS")
        self.assertEqual(result["geometry"]["retained_triangles"], 2)
        contract["upperworks"] = ["missing"]
        path.with_suffix(".ship.json").write_text(json.dumps(contract))
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            audit(path)

    def test_slight_plank_variation_is_reported_with_explicit_tolerance(self):
        path = write_asset(self.folder, [("deck", plane(slope=.015))])
        result = audit(path, spacing=.25, height_tolerance=.02)
        self.assertEqual(result["status"], "PASS")
        self.assertGreater(result["deck"]["floor_y_variation"], .02)
        self.assertEqual(audit(path, spacing=.25, height_tolerance=.001)["status"], "FAIL")

    def test_hierarchy_matrix_rotation_and_runtime_reflection(self):
        def hierarchy(doc):
            doc["nodes"][0]["translation"] = [1, 0, 0]
            matrix = np.eye(4)
            matrix[:3, 3] = [2, 3, 4]
            doc["nodes"].append({"matrix": matrix.T.ravel().tolist(), "children": [0]})
            doc["scenes"][0]["nodes"] = [1]
        c = {"schemaVersion": 1, "longAxis": "z",
             "hull": {"minX": -4, "maxX": -2, "minZ": 2, "maxZ": 6},
             "deck": {"y": 3, "minX": -4, "maxX": -2, "minZ": 2, "maxZ": 6}}
        result = audit(write_asset(self.folder, contract=c, mutate=hierarchy), spacing=.25)
        self.assertEqual(result["runtime_bounds_min"], [-4, 3, 2])
        self.assertEqual(result["status"], "PASS")
        rotate = node_matrix({"rotation": [0, np.sin(np.pi/4), 0, np.cos(np.pi/4)], "scale": [2, 1, 1]})
        np.testing.assert_allclose(rotate @ [1, 0, 0, 1], [0, 0, -2, 1], atol=1e-12)

    def test_mirrored_node_keeps_upward_floor(self):
        result = audit(write_asset(self.folder, mutate=lambda d: d["nodes"][0].update(scale=[-1, 1, 1])), spacing=.25)
        self.assertEqual(result["status"], "PASS")

    def test_interleaved_positions_respect_byte_stride(self):
        vertices = [v + [123.0] for v in plane()]
        result = audit(write_asset(self.folder, [("deck", vertices)],
                                   mutate=lambda d: d["bufferViews"][0].update(byteStride=16)), spacing=.25)
        self.assertEqual(result["runtime_bounds_min"], [-1, 0, -2])
        self.assertEqual(result["status"], "PASS")

    def test_extensions_cycles_and_bad_accessor_ranges_rejected(self):
        cases = [(lambda d: d.update(extensionsRequired=["KHR_draco_mesh_compression"]), "required glTF"),
                 (lambda d: d["nodes"][0].update(children=[0]), "cyclic"),
                 (lambda d: d["accessors"][0].update(count=1000), "bufferView"),
                 (lambda d: d["accessors"][0].update(sparse={}), "sparse"),
                 (lambda d: d["nodes"][0].update(skin=0), "Skinned")]
        for mutate, message in cases:
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                audit(write_asset(self.folder, mutate=mutate))

    def test_budget_and_sampling_budget_rejected(self):
        path = write_asset(self.folder)
        with self.assertRaisesRegex(ValueError, "triangle budget"):
            audit(path, max_triangles=1)
        with self.assertRaisesRegex(ValueError, "Sampling budget"):
            audit(path, spacing=.00001)

    def test_contract_float_overflow_and_dependency_rejected(self):
        path = write_asset(self.folder)
        sidecar = path.with_suffix(".ship.json")
        original = json.loads(sidecar.read_text())
        for value in (float("nan"), float("inf"), 1e100):
            c = copy.deepcopy(original)
            c["deck"]["y"] = value
            sidecar.write_text(json.dumps(c))
            with self.assertRaises(ValueError):
                load_contract(sidecar)
        original["combatModel"] = "missing"
        sidecar.write_text(json.dumps(original))
        with self.assertRaisesRegex(ValueError, "variant missing"):
            audit(path)

    def test_ambiguous_upperwork_name_rejected(self):
        path = write_asset(self.folder, [("deck", plane()), ("lift", plane(1)), ("lift", plane(2))])
        sidecar = path.with_suffix(".ship.json")
        c = json.loads(sidecar.read_text())
        c["upperworks"] = ["lift"]
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "ambiguous"):
            audit(path)

    def test_downward_surface_is_not_walkable_floor(self):
        vertices = plane()
        vertices.reverse()
        result = audit(write_asset(self.folder, [("underside", vertices)]), spacing=.25)
        self.assertEqual(result["deck"]["floor_coverage"], 0)
        self.assertEqual(result["status"], "FAIL")

    def test_nonfinite_vertex_and_material_rejected(self):
        vertices = plane()
        vertices[0][0] = float("nan")
        with self.assertRaisesRegex(ValueError, "Nonfinite"):
            audit(write_asset(self.folder, [("deck", vertices)]))
        with self.assertRaisesRegex(ValueError, "Nonfinite"):
            audit(write_asset(self.folder, mutate=lambda d: d.update(materials=[{"pbrMetallicRoughness": {"metallicFactor": float("inf")}}])))

    def test_rotor_contract_normalization_and_exact_node_identity(self):
        path = write_asset(self.folder)
        sidecar = path.with_suffix(".ship.json")
        c = json.loads(sidecar.read_text())
        c["rotors"] = [{"node": "deck", "pivot": [0, 0, 0], "axis": [0, 3, 0], "degreesPerSecond": -90}]
        sidecar.write_text(json.dumps(c))
        self.assertEqual(audit(path, spacing=.25)["rotors"][0]["axis"], [0, 1, 0])
        c["rotors"][0]["node"] = "deck/p0"
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            audit(path, spacing=.25)

    def test_invalid_rotor_metadata_is_rejected(self):
        path = write_asset(self.folder)
        sidecar = path.with_suffix(".ship.json")
        original = json.loads(sidecar.read_text())
        rotor = {"node": "deck", "pivot": [0, 0, 0], "axis": [1, 0, 0], "degreesPerSecond": 90}
        cases = [("axis", [0, 0, 0]), ("axis", [float("nan"), 0, 1]),
                 ("pivot", [0, 0]), ("pivot", [2, 0, 0]),
                 ("degreesPerSecond", 721), ("degreesPerSecond", True),
                 ("node", "missing"), ("node", " ")]
        for key, value in cases:
            c = copy.deepcopy(original)
            c["rotors"] = [dict(rotor, **{key: value})]
            sidecar.write_text(json.dumps(c))
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                audit(path, spacing=.25)
        for rotors in (None, {}, [False], [rotor, rotor], [rotor] * 17):
            c = copy.deepcopy(original); c["rotors"] = rotors
            sidecar.write_text(json.dumps(c))
            with self.assertRaises(ValueError):
                audit(path, spacing=.25)
        c = copy.deepcopy(original); c["rotors"] = [rotor]; c["upperworks"] = ["deck"]
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "separate from upperworks"):
            audit(path, spacing=.25)

    def test_duplicate_source_rotor_names_are_rejected(self):
        path = write_asset(self.folder, [("deck", plane()), ("rotor", plane(1)), ("rotor", plane(2))])
        sidecar = path.with_suffix(".ship.json"); c = json.loads(sidecar.read_text())
        c["rotors"] = [{"node": "rotor", "pivot": [0, 1, 0], "axis": [0, 1, 0], "degreesPerSecond": 90}]
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            audit(path, spacing=.25)

    def test_rotor_part_selector_matches_runtime_suffix_grammar(self):
        for part in ("rotor/p0", "rotor/p123", "rotor/p2#17"):
            with self.subTest(part=part):
                self.assertTrue(matches_node(part, "rotor"))
        for part in ("rotor", "rotor_port/p0", "rotor/pedestal", "rotor/p", "rotor/p0#",
                     "rotor/p0#x", "rotor/p0/p0", "rotor/p0-extra", "rotor/p\u0661"):
            with self.subTest(part=part):
                self.assertFalse(matches_node(part, "rotor"))
        self.assertFalse(matches_node("rotor/p0", "rotor/p0"))
        self.assertTrue(matches_node("rotor/p0/p2#1", "rotor/p0"))
        self.assertFalse(matches_node("rotorX/p0", "rotor."))

    def test_literal_suffix_source_and_whitespace_name_have_runtime_identity(self):
        for name in ("rotor/p0", " "):
            with self.subTest(name=name):
                path = write_asset(self.folder, [("deck", plane()), (name, plane())])
                sidecar = path.with_suffix(".ship.json"); c = json.loads(sidecar.read_text())
                c["rotors"] = [{"node": name, "pivot": [0, 0, 0], "axis": [0, 0, -4], "degreesPerSecond": 0}]
                sidecar.write_text(json.dumps(c))
                result = audit(path, spacing=.5)
                self.assertEqual(result["status"], "PASS")
                self.assertEqual(result["rotors"][0]["axis"], [0, 0, -1])
                self.assertTrue(any("full-turn clearance" in w for w in result["warnings"]))

    def test_rotor_with_source_but_no_rendered_primitive_is_rejected(self):
        path = write_asset(self.folder, [("deck", plane()), ("rotor", plane())],
                           mutate=lambda d: d["meshes"][1].update(primitives=[]))
        sidecar = path.with_suffix(".ship.json"); c = json.loads(sidecar.read_text())
        c["rotors"] = [{"node": "rotor", "pivot": [0, 0, 0], "axis": [1, 0, 0], "degreesPerSecond": 90}]
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "no rendered parts"):
            audit(path, spacing=.5)

    def test_rotor_numeric_checks_use_runtime_float_precision(self):
        path = write_asset(self.folder); sidecar = path.with_suffix(".ship.json")
        c = json.loads(sidecar.read_text())
        rotor = {"node": "deck", "pivot": [0, 0, 0], "axis": [3, 4, 0], "degreesPerSecond": 720.00002}
        c["rotors"] = [rotor]; sidecar.write_text(json.dumps(c))
        actual = load_contract(sidecar)["rotors"][0]
        self.assertEqual(actual["degreesPerSecond"], 720)
        self.assertEqual(actual["axis"], [float(np.float32(.6)), float(np.float32(.8)), 0])
        for field, value in (("degreesPerSecond", -720.00004), ("axis", [0, 0, .000001]),
                             ("axis", [0, 0, float("inf")]), ("pivot", [True, 0, 0]),
                             ("pivot", [0, 0, 1e100]), ("axis", [0, "1", 0])):
            bad = copy.deepcopy(c); bad["rotors"][0][field] = value
            sidecar.write_text(json.dumps(bad))
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                load_contract(sidecar)

    def test_rotor_pivot_checked_after_hierarchy_and_runtime_reflection(self):
        def move(doc):
            doc["nodes"][0]["translation"] = [3, 2, 4]
        c = {"schemaVersion": 1, "longAxis": "z",
             "hull": {"minX": -4, "maxX": -2, "minZ": 2, "maxZ": 6},
             "deck": {"y": 2, "minX": -4, "maxX": -2, "minZ": 2, "maxZ": 6},
             "rotors": [{"node": "deck", "pivot": [-3, 2, 4], "axis": [1, 0, 0], "degreesPerSecond": -720}]}
        path = write_asset(self.folder, contract=c, mutate=move)
        self.assertEqual(audit(path, spacing=.5)["status"], "PASS")
        c["rotors"][0]["pivot"] = [3, 2, 4]
        path.with_suffix(".ship.json").write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "pivot exceeds"):
            audit(path, spacing=.5)

    def test_sixteen_distinct_rotors_allowed_but_seventeenth_rejected(self):
        path = write_asset(self.folder, [(f"rotor{i}", plane()) for i in range(17)])
        sidecar = path.with_suffix(".ship.json"); c = json.loads(sidecar.read_text())
        c["rotors"] = [{"node": f"rotor{i}", "pivot": [0, 0, 0], "axis": [1, 0, 0],
                        "degreesPerSecond": 90 if i % 2 else -90} for i in range(16)]
        sidecar.write_text(json.dumps(c))
        self.assertEqual(len(audit(path, spacing=.5)["rotors"]), 16)
        c["rotors"].append(dict(c["rotors"][0], node="rotor16"))
        sidecar.write_text(json.dumps(c))
        with self.assertRaisesRegex(ValueError, "more than 16"):
            audit(path, spacing=.5)


if __name__ == "__main__":
    unittest.main()
