"""Offline preview render + mechanical verdict for a candidate enemy .glb.

This is the OFFLINE half of the visual-verification gate (spec #66, FR-5;
work item #67). Two stages:

  RENDER   : Blender Workbench rasterizes the candidate .glb (+ optional texture)
             to a PNG. This reuses the camera/lighting approach proven in
             03_render_preview.py. Blender runs under its OWN bundled Python, so
             the render stage is a child `blender --background --python` call.
  ANALYSIS : the SAME shared visual_gate.analyze(...) used by the in-game
             analyzer (#70) runs on the rendered PNG and emits preview_verdict.json.

An offline mechanical FAIL is the launch gate: a later work item wires the actual
launch-skip; here we just emit a clear pass/fail/inconclusive verdict and exit
non-zero on FAIL so a caller can gate on the exit code.

Run it with the venv interpreter (numpy/scipy/Pillow live there):

  /Users/tbrack/Documents/Projects/FTK/.venv-3dgen/bin/python \
    tools/ai-model-pipeline/03b_preview_metrics.py \
    --glb ai-model-gen/mudwretch_rigged.glb \
    [--texture ai-model-gen/mudwretch_basecolor.png] \
    [--baseline tools/ai-model-pipeline/baseline.json] \
    [--out-dir /tmp/ftk_preview] \
    [--render-only-png /tmp/foo.png]   # skip render, analyze an existing PNG

The analysis stage imports visual_gate from this same directory; do not duplicate
its segmentation here. The render stage is the only thing that touches Blender.
"""

import argparse
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import visual_gate  # noqa: E402  (after sys.path setup so the venv run finds it)

BLENDER = "/opt/homebrew/bin/blender"
RENDER_RES = 720

# The render-stage program. It runs inside Blender's bundled Python (has bpy),
# so it must NOT import numpy/scipy/visual_gate. It only renders one front PNG of
# the .glb. We render with EEVEE and a flat EMISSION material (not Workbench): the
# gate measures SHAPE (silhouette, components, upright proxy, scale), not color, so
# a single bright unlit emission is the right, content-agnostic choice. Emission
# is fully normal-independent, so it cannot render a face dark, and EEVEE reliably
# rasterizes glTF meshes from any exporter (the headless Workbench path silently
# renders trimesh/round-tripped glTF meshes empty on this Blender build, which
# would otherwise force a false INCONCLUSIVE on a malformed asset).
_RENDER_SRC = r'''
import bpy, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT, RES = argv[0], argv[1], int(argv[2])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
all_meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not all_meshes:
    print("RENDER ERROR: no meshes imported"); sys.exit(3)
body = max(all_meshes, key=lambda o: len(o.data.vertices))

# Drop importer helper proxies (e.g. a 42-vertex "Icosphere" the glTF importer
# materializes for an armature/empty): any mesh with fewer than 1 percent of the
# body's vertices is not part of the candidate asset and would otherwise read as
# a spurious extra component. A real baked lantern is a full mesh node, well
# above this floor, so it is kept; the gate still judges genuine extra parts.
all_names = [o.name for o in all_meshes]
body_name = body.name
helper_floor = max(1, int(0.01 * len(body.data.vertices)))
helpers = [o for o in all_meshes if o is not body and len(o.data.vertices) < helper_floor]
for o in helpers:
    bpy.data.objects.remove(o, do_unlink=True)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
tv = sum(len(o.data.vertices) for o in meshes)
print("imported meshes:", all_names, "-> kept:", [o.name for o in meshes], "verts", tv)
print("body mesh:", body_name, "verts", len(body.data.vertices))

mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
for c in body.bound_box:
    w = body.matrix_world @ Vector(c)
    mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
center = (mn + mx) / 2; size = (mx - mn); rad = max(size) / 2 or 1.0
print("body bounds size:", tuple(round(v, 3) for v in size))

# Auto-orient (validated against the known-good trellis troll): a humanoid's UP
# axis is its LONGEST bounding-box axis. Of the two remaining axes, the LARGER is
# the depth (front-to-back) the camera looks along, and the SMALLER becomes the
# on-screen width. This yields a front-on, upright silhouette for the stocky
# troll chassis after the Blender 5.x glTF import (which bakes the Y-up rotation
# into the vertices, leaving an identity object matrix).
order = sorted(range(3), key=lambda i: size[i])  # ascending; order[2] is longest
up_axis = order[2]            # longest -> screen up
look_axis = order[1]          # larger remaining -> camera looks along this (depth)
side_axis = order[0]          # smaller remaining -> on-screen width

look_dir = Vector((0, 0, 0)); look_dir[look_axis] = -1.0   # stand off along -depth
look_dir[up_axis] = 0.20      # slight elevation so the pose reads, not flat orthographic
look_dir.normalize()
print("axes: up", up_axis, "look", look_axis, "side", side_axis)

tgt = bpy.data.objects.new("tgt", None)
bpy.context.collection.objects.link(tgt); tgt.location = center

scn = bpy.context.scene
scn.render.engine = "BLENDER_EEVEE"

# One bright EMISSION material on every mesh: each visible face renders at a flat
# bright value with no lighting/normal dependence, so a dark asset texture cannot
# wash out the silhouette and a coherent body vs a shard cloud is judged purely by
# geometry. A dark world gives a near-black background for the luminance segmenter.
mat = bpy.data.materials.new("gate_emission")
mat.use_nodes = True
nt = mat.node_tree; nt.nodes.clear()
em = nt.nodes.new("ShaderNodeEmission")
em.inputs[0].default_value = (0.9, 0.9, 0.9, 1.0)   # bright
em.inputs[1].default_value = 1.0                    # strength
out_node = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(em.outputs[0], out_node.inputs[0])
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(mat)

if scn.world is None:
    scn.world = bpy.data.worlds.new("gate_world")
scn.world.use_nodes = True
bg = scn.world.node_tree.nodes.get("Background")
if bg is not None:
    bg.inputs[0].default_value = (0.0, 0.0, 0.0, 1.0)
    bg.inputs[1].default_value = 0.0

scn.render.resolution_x = RES; scn.render.resolution_y = RES
scn.render.film_transparent = False
try:
    scn.view_settings.view_transform = "Standard"
except Exception:
    pass

cam_data = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_data)
bpy.context.collection.objects.link(cam); scn.camera = cam
con = cam.constraints.new("TRACK_TO"); con.target = tgt
con.track_axis = "TRACK_NEGATIVE_Z"
# Map the TRACK_TO up_axis to the world axis we determined is "up".
con.up_axis = {0: "UP_X", 1: "UP_Y", 2: "UP_Z"}[up_axis]

cam.location = center + look_dir * (rad * 3.2)
scn.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("RENDER DONE", OUT)
'''


def render_glb(glb_path, out_png):
    """Run Blender headless to render `glb_path` to `out_png`. Raises on failure."""
    script_path = out_png + ".blender_render.py"
    with open(script_path, "w") as f:
        f.write(_RENDER_SRC)
    cmd = [
        BLENDER, "--background", "--factory-startup",
        "--python", script_path, "--",
        glb_path, out_png, str(RENDER_RES),
    ]
    print("RENDER: " + " ".join(cmd))
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    sys.stdout.write(proc.stdout.decode("utf-8", "replace"))
    if proc.returncode != 0 or not os.path.exists(out_png):
        raise RuntimeError("Blender render failed (rc=%d, png exists=%s)"
                           % (proc.returncode, os.path.exists(out_png)))
    return out_png


def main():
    ap = argparse.ArgumentParser(description="Offline preview render + mechanical verdict.")
    ap.add_argument("--glb", help="candidate .glb to render and analyze")
    ap.add_argument("--texture", default=None,
                    help="optional base-color PNG (recorded; Workbench uses embedded/vertex color)")
    ap.add_argument("--baseline", default=None,
                    help="path to baseline.json; omit for the offline default thresholds")
    ap.add_argument("--out-dir", default="/tmp/ftk_preview",
                    help="directory for the render PNG and preview_verdict.json")
    ap.add_argument("--render-only-png", default=None,
                    help="skip rendering and analyze this existing PNG instead")
    args = ap.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    verdict_path = os.path.join(args.out_dir, "preview_verdict.json")

    # RENDER stage (or reuse a supplied PNG).
    if args.render_only_png:
        png = args.render_only_png
        if not os.path.exists(png):
            print("ERROR: --render-only-png does not exist: " + png)
            return 2
    else:
        if not args.glb:
            print("ERROR: --glb is required unless --render-only-png is given")
            return 2
        png = os.path.join(args.out_dir, "preview_render.png")
        try:
            render_glb(os.path.abspath(args.glb), os.path.abspath(png))
        except Exception as e:  # render error -> INCONCLUSIVE, never a synthetic verdict
            verdict = visual_gate.analyze(
                os.path.abspath(png), args.baseline,
                capture_error="render-error: %s" % e)
            verdict["crop_image"] = None
            visual_gate.write_verdict(verdict, verdict_path)
            print("\n=== PREVIEW VERDICT (render error) ===")
            print(json.dumps(verdict, indent=2))
            return 2

    # ANALYSIS stage: the SAME shared analyze() the in-game path (#70) uses.
    verdict = visual_gate.analyze(os.path.abspath(png), args.baseline)
    # For the preview we have no separate annotated/crop pass (that is the
    # in-game analyzer #70); record the render itself as the crop reference.
    verdict["crop_image"] = os.path.abspath(png)
    verdict["annotated_image"] = os.path.abspath(png)
    if args.texture:
        verdict.setdefault("notes", {})["texture"] = os.path.abspath(args.texture)
    visual_gate.write_verdict(verdict, verdict_path)

    print("\n=== PREVIEW VERDICT -> %s ===" % verdict_path)
    print(json.dumps(verdict, indent=2))

    v = verdict["verdict"]
    if v == "pass":
        print("\nPREVIEW PASS: offline mechanical gate is GREEN; an in-game launch is warranted.")
        return 0
    if v == "fail":
        print("\nPREVIEW FAIL: offline mechanical gate BLOCKS the launch. Per-criterion reasons above.")
        return 1
    print("\nPREVIEW INCONCLUSIVE: %s (no launch decision)." % verdict["inconclusive_reason"])
    return 3


if __name__ == "__main__":
    sys.exit(main())
