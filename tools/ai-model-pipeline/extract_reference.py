#!/usr/bin/env python3
"""Extract a local, non-redistributable Unity skin reference. Never writes game files."""
import argparse
import json
from pathlib import Path
import numpy as np
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler


def decode_reference(assets, renderer_id, env=None):
    if env is None:
        env = UnityPy.load(str(assets))
    source = next(f for f in env.files.values() if Path(f.name).name == assets.name)
    obj = source.objects[renderer_id]
    if obj.type.name != 'SkinnedMeshRenderer':
        raise ValueError(f'Path ID {renderer_id} is not a SkinnedMeshRenderer in {assets.name}')
    renderer = obj.read()
    mesh = renderer.m_Mesh.read()
    handler = MeshHandler(mesh)
    handler.process()
    transforms = [p.read() for p in renderer.m_Bones]
    names = [t.m_GameObject.read().m_Name for t in transforms]
    # Path IDs are only unique within an asset file. Resolve external pointers first.
    def bone_key(pointer):
        if not pointer: return None
        resolved = pointer.deref()
        return (resolved.assets_file.name, resolved.path_id)
    ids = [bone_key(p) for p in renderer.m_Bones]
    parents = []
    for transform in transforms:
        key = bone_key(transform.m_Father)
        parents.append(ids.index(key) if key in ids else -1)
    bindposes = np.array([[[getattr(m, f'e{r}{c}') for c in range(4)] for r in range(4)] for m in mesh.m_BindPose])
    positions = np.asarray(handler.m_Vertices)
    weights = np.asarray(handler.m_BoneWeights, dtype=float)
    # UnityPy 1.25.3 compressed-mesh fourth weight uses 1 - integer_sum,
    # instead of 1 - integer_sum/31. Reconstruct that residual explicitly.
    invalid_fourth = weights[:, 3] < 0
    weights[invalid_fourth, 3] = 1 - weights[invalid_fourth, :3].sum(axis=1)
    if (weights < -1e-6).any() or not np.allclose(weights.sum(axis=1), 1, atol=1e-6):
        raise ValueError('Decoded skin weights are not normalized')
    weights = np.maximum(weights, 0)
    weights /= weights.sum(axis=1, keepdims=True)
    if len(names) != len(bindposes) or len(set(names)) != len(names):
        raise ValueError('Bone names/bindposes must be unique and pair 1:1')
    rest = np.linalg.inv(bindposes)
    metadata = dict(mesh_name=mesh.m_Name, renderer_path_id=renderer_id,
                    coordinate_space='Unity mesh-local: X right, Y up, Z forward; no conversion',
                    uv_space='Native Unity bottom-origin V; flip V in a copy before using as exporter source',
                    bone_names=names, bone_parents=parents, rest_matrices=rest.tolist(),
                    bone_positions=rest[:, :3, 3].tolist(), vertex_count=len(positions),
                    bounds_min=positions.min(0).tolist(), bounds_max=positions.max(0).tolist(),
                    copyright='Extracted game reference: local use only, never distribute or commit')
    arrays = dict(
        positions=positions,
        normals=np.asarray(handler.m_Normals),
        uvs=np.asarray(handler.m_UV0)[:, :2],
        triangles=np.concatenate(handler.get_triangles()),
        joints=np.asarray(handler.m_BoneIndices),
        weights=weights,
        bindposes=bindposes,
        bone_names=np.asarray(names),
    )
    return metadata, arrays


def extract(assets, renderer_id, output, env=None):
    metadata, arrays = decode_reference(assets, renderer_id, env)
    output.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(output/'reference.npz', **arrays)
    (output/'skeleton.json').write_text(json.dumps(metadata, indent=2)+'\n')
    print(json.dumps(metadata, indent=2))

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--assets', type=Path, required=True)
    p.add_argument('--renderer-id', type=int, default=121152, help='Local FTK cave troll SMR path ID; inspect your build if different')
    p.add_argument('--output', type=Path, required=True, help='Use a gitignored scratch directory')
    a = p.parse_args()
    extract(a.assets, a.renderer_id, a.output)
