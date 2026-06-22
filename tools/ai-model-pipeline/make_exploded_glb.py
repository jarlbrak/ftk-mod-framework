"""Build a deliberately SHATTERED .glb from a coherent asset, for gate testing.

This is a test-fixture generator for the visual gate (#67 / #70), not a pipeline
stage. It scatters the body mesh's triangles outward in clusters so the rendered
silhouette tears into several separated islands. Feeding the result through the
offline preview (03b_preview_metrics.py) must yield `connected: fail`, proving the
gate catches a shatter (the negative control for the good-asset PASS).

Two details make the scatter render correctly through the offline harness:
  - It scatters in CLUSTERS (a coarse grid hash of triangle centroids) by a large
    offset (20-40 percent of the body span), so the islands are separated in
    SCREEN space by gaps wider than the gate's small morphological close (a tiny
    scatter just bridges back into one blob and would fail on shape, not on
    connectivity).
  - It applies a -90deg X rotation before export. trimesh's glTF export writes
    vertices in a frame the Blender glTF importer reads as Z-up, which the offline
    render harness's auto-orient then frames off-screen (a black frame). The -90deg
    X pre-rotation cancels that so the harness frames the cloud the same way it
    frames the real asset. (This is a fixture-export quirk, not a gate behavior.)

Run with the venv interpreter (trimesh lives there):

  /Users/tbrack/Documents/Projects/FTK/.venv-3dgen/bin/python \
    tools/ai-model-pipeline/make_exploded_glb.py \
    --src ai-model-gen/mudwretch_rigged.glb --out /tmp/v70_exploded.glb
"""

import argparse

import numpy as np
import trimesh


def explode(src_glb, out_glb, grid=4, offset_lo=0.20, offset_hi=0.40, seed=11):
    """Write a shard-cloud .glb derived from the largest mesh in `src_glb`."""
    scene = trimesh.load(src_glb, force="scene")
    meshes = [g for g in scene.geometry.values()
              if isinstance(g, trimesh.Trimesh) and len(g.faces)]
    if not meshes:
        raise RuntimeError("no mesh geometry in " + src_glb)
    body = max(meshes, key=lambda m: len(m.vertices))
    faces, verts = body.faces, body.vertices
    extent = np.ptp(body.bounds, axis=0)
    span = float(extent.max())

    rng = np.random.default_rng(seed)
    tcent = verts[faces].mean(axis=1)
    cell = ((tcent - body.bounds[0]) / (extent + 1e-9) * grid).astype(int)
    cell = np.clip(cell, 0, grid - 1)
    cluster = cell[:, 0] * grid * grid + cell[:, 1] * grid + cell[:, 2]

    offsets = {}
    for cid in np.unique(cluster):
        d = rng.normal(size=3)
        d /= (np.linalg.norm(d) + 1e-9)
        offsets[cid] = d * (offset_lo + (offset_hi - offset_lo) * rng.random()) * span

    new_v, new_f = [], []
    for fi, tri in enumerate(faces):
        off = offsets[cluster[fi]]
        base = len(new_v)
        for p in verts[tri]:
            new_v.append(p + off)
        new_f.append([base, base + 1, base + 2])

    shatter = trimesh.Trimesh(vertices=np.array(new_v), faces=np.array(new_f),
                              process=False)
    shatter.apply_transform(
        trimesh.transformations.rotation_matrix(np.radians(-90), [1, 0, 0]))
    shatter.export(out_glb)
    return len(offsets), len(shatter.faces)


def main():
    ap = argparse.ArgumentParser(description="Make a shattered .glb gate fixture.")
    ap.add_argument("--src", default="ai-model-gen/mudwretch_rigged.glb",
                    help="coherent source .glb")
    ap.add_argument("--out", default="/tmp/v70_exploded.glb",
                    help="output shattered .glb")
    args = ap.parse_args()
    nc, nf = explode(args.src, args.out)
    print("wrote %s: %d clusters, %d faces" % (args.out, nc, nf))


if __name__ == "__main__":
    main()
