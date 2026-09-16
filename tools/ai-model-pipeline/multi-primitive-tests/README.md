# Bounded multi-primitive tests

The C# project links the actual production GLB loader against small Unity boundary stand-ins. Original two-layer triangles test correct submesh index mapping, one shared skin, rejection of malformed second primitives and bad slot mappings, and the unchanged strict single-primitive path. A separate original rigid triangle validates the strict static loader, including MeshFilter-local geometry, UV conversion, and rejection of skin, joint attributes, or multiple primitives. The stand-ins do not establish native rendering, material instancing or animation behavior.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/multi-primitive-tests/make_fixtures.py scratch/multi-primitive-fixtures
dotnet run --project tools/ai-model-pipeline/multi-primitive-tests -- scratch/multi-primitive-fixtures
scratch/model-venv/bin/python -m unittest discover -s tools/ai-model-pipeline -p test_multi_primitive_glb.py
dotnet run --project tools/ai-model-pipeline/mesh-transaction-tests
```

The Python suite independently checks the binary structure and rejects malformed primitives. When the ignored local reference is available, it also reproduces the pre-change Basilight GLB hash exactly. Missing that proprietary reference skips only this local regression check.

Transaction tests link the production transaction/lease, descriptors and scrolling prefix: preflight/commit rollback, immutable descriptor snapshots, per-slot emission, native material isolation, disabled-renderer phase accumulation without allocation, negative rates, arbitrary texture properties, clone first-write rollback, independent source/clone phases, repeated-write allocation bounds, unknown-material non-adoption, and final lease cleanup.

Native Unity `.materials` behavior is deliberately avoided on the supported scroller seam. The existing native component retains its rate, private phase and texture property; only explicitly opted-in successful owners use sharedMaterials with tracked, private material sets. Runtime tests must still exercise multiple scrollers, native scheduling, source/clone lifetime orders, renderer material identities and the actual cubeA attack/death route before claiming live compatibility.

## Portable original cubeA fixture

`cube-fixture/` contains the original72-vertex, two-submesh calibration GLB, separate checker/stripe PNGs, source JSON, editable Blender scene, exact runtime profile and provenance manifest. These are two authored boxes rather than finished anatomy: slot0 tests a stationary checker and slot1 tests visible native scrolling stripes. The three native palette names/IBMs remain; only mid/top have positive weights. It does not prove all-joint deformation or completed creature quality.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/multi-primitive-tests/verify_cube_layer_fixture.py
/Applications/Blender.app/Contents/MacOS/Blender -b \
  --python tools/ai-model-pipeline/multi-primitive-tests/create_cube_layer_fixture.py -- \
  --reference scratch/cube-topology-analysis/reference-121012/reference.npz \
  --skeleton scratch/cube-topology-analysis/reference-121012/skeleton.json \
  --python "$PWD/scratch/model-venv/bin/python" --output-dir scratch/cube-fixture-rebuild
```

The portable verifier needs no native game file: it checks every original vertex against16 analytic box corners, both12-triangle groups, exact source/GLB attribute and skin-weight equality, palette names and packaged hashes. Rebuilding the editable bind armature needs the ignored native reference; no native surface guide is created or included. The generators build the two boxes and checker/stripe textures from constants.

`implementation-validation-v1.json` pins the offline candidate and tests. Its Core/content versions must be deployed as a reviewed pair; the content registration method references the new Core types even if a selected profile uses legacy fields. Frozen older content remains separate. This portable record is source/offline evidence, not native cubeA visual or lifecycle acceptance.
