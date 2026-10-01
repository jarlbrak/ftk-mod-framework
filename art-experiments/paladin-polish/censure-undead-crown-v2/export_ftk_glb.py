#!/usr/bin/env python3
"""Write FTK's mesh-local, bone-name keyed GLB contract, not a general glTF export."""
import argparse
import json
import struct
from pathlib import Path
import numpy as np


def transfer_weights(positions, reference):
    """Closest surface barycentric skin transfer, then keep strongest four influences."""
    import trimesh
    mesh = trimesh.Trimesh(reference['positions'], reference['triangles'], process=False)
    closest, distances, face_ids = trimesh.proximity.closest_point(mesh, positions)
    bary = trimesh.triangles.points_to_barycentric(mesh.triangles[face_ids], closest)
    bary = np.clip(bary, 0, 1)
    bary /= bary.sum(axis=1, keepdims=True)
    corners = mesh.faces[face_ids]
    dense = np.zeros((len(positions), len(reference['bone_names'])))
    for c in range(3):
        for w in range(4):
            np.add.at(dense, (np.arange(len(positions)), reference['joints'][corners[:, c], w]),
                      reference['weights'][corners[:, c], w] * bary[:, c])
    joints = np.argsort(-dense, axis=1, kind='stable')[:, :4]
    weights = np.take_along_axis(dense, joints, axis=1)
    weights /= weights.sum(axis=1, keepdims=True)
    print(f'Surface transfer distance: median={np.median(distances):.4f}, max={distances.max():.4f}')
    return joints, weights


def write_glb(path, data, reference, rigid_bone=None):
    positions = np.asarray(data['positions'], dtype='<f4')
    normals = np.asarray(data['normals'], dtype='<f4')
    # Input uses top-origin V; runtime flips V when assigning Unity Mesh.uv.
    uvs = np.asarray(data['uvs'], dtype='<f4')
    groups = data.get('primitives')
    if groups is not None:
        if not isinstance(groups, list) or not 2 <= len(groups) <= 4:
            raise ValueError('Native slot mode requires2..4 primitive triangle groups')
        parts = [np.asarray(p['triangles'], dtype=np.int64).reshape(-1) for p in groups]
        if any(len(p)==0 or len(p)%3 for p in parts): raise ValueError('Every primitive requires nonempty triangles')
        triangles = np.concatenate(parts)
    else:
        triangles = np.asarray(data['triangles'], dtype=np.int64).reshape(-1)
    names = reference['bone_names'].tolist()
    if rigid_bone:
        joints = np.zeros((len(positions), 4), dtype=int)
        joints[:, 0] = names.index(rigid_bone)
        weights = np.zeros((len(positions), 4)); weights[:, 0] = 1
    elif 'vertex_bone_names' in data:
        joints = np.zeros((len(positions), 4), dtype=int)
        joints[:, 0] = [names.index(n) for n in data['vertex_bone_names']]
        weights = np.zeros((len(positions), 4)); weights[:, 0] = 1
    elif 'joints' in data and 'weights' in data:
        joints = np.asarray(data['joints'], dtype=int)
        weights = np.asarray(data['weights'], dtype=float)
        if 'bone_names' in data:
            mapping = np.asarray([names.index(n) for n in data['bone_names']])
            joints = mapping[joints]
    else:
        joints, weights = transfer_weights(positions, reference)
    count = len(positions)
    if not 0 < count < 65535:
        raise ValueError('FTK requires 1..65534 vertices')
    for array, shape in [(positions,(count,3)), (normals,(count,3)), (uvs,(count,2)),
                         (joints,(count,4)), (weights,(count,4))]:
        if array.shape != shape or not np.isfinite(array).all():
            raise ValueError(f'Invalid array shape or nonfinite values: {array.shape}, expected {shape}')
    if not len(triangles) or len(triangles)%3 or triangles.min()<0 or triangles.max()>=count:
        raise ValueError('Invalid triangle indices')
    if joints.min()<0 or joints.max()>=len(names) or (weights<0).any() or (weights.sum(1)<=0).any():
        raise ValueError('Invalid skin influences')
    weights /= weights.sum(1, keepdims=True)
    bindposes = np.asarray(reference['bindposes'], dtype='<f4')
    if bindposes.shape != (len(names),4,4) or not np.isfinite(bindposes).all():
        raise ValueError('Invalid bind poses')
    root = {'asset': {'version':'2.0', 'generator':'FTK model pipeline (Unity mesh-local contract)'},
            'buffers': [], 'bufferViews': [], 'accessors': [],
            'nodes': [{'name':n} for n in names], 'meshes': [], 'skins': []}
    binary = bytearray()
    def accessor(array, component, kind, n, bounds=False):
        while len(binary)%4: binary.append(0)
        raw = array.tobytes()
        view = len(root['bufferViews'])
        root['bufferViews'].append({'buffer':0,'byteOffset':len(binary),'byteLength':len(raw)})
        binary.extend(raw)
        a = {'bufferView':view,'byteOffset':0,'componentType':component,'count':n,'type':kind}
        if bounds: a.update(min=array.min(0).tolist(), max=array.max(0).tolist())
        root['accessors'].append(a)
        return len(root['accessors'])-1
    attrs = {'POSITION':accessor(positions,5126,'VEC3',count,True),
             'NORMAL':accessor(normals,5126,'VEC3',count),
             'TEXCOORD_0':accessor(uvs,5126,'VEC2',count),
             'JOINTS_0':accessor(joints.astype('<u2'),5123,'VEC4',count),
             'WEIGHTS_0':accessor(weights.astype('<f4'),5126,'VEC4',count)}
    if groups is None:
        indices = accessor(triangles.astype('<u2'),5123,'SCALAR',len(triangles))
    else:
        part_indices = [accessor(p.astype('<u2'),5123,'SCALAR',len(p)) for p in parts]
    # Each Unity matrix is serialized column-major; transpose each row-major numpy matrix.
    ibm = accessor(bindposes.transpose(0,2,1).copy(),5126,'MAT4',len(names))
    if groups is None:
        root['meshes'] = [{'primitives':[{'attributes':attrs,'indices':indices,'mode':4}]}]
    else:
        root['materials'] = [{'name':f'FTKSlot{i}'} for i in range(len(groups))]
        root['meshes'] = [{'primitives':[{'attributes':attrs,'indices':ix,'mode':4,'material':i}
                                         for i,ix in enumerate(part_indices)]}]
    root['skins'] = [{'joints':list(range(len(names))),'inverseBindMatrices':ibm}]
    root['nodes'].append({'name':'FTKMesh','mesh':0,'skin':0})
    root['scenes'] = [{'nodes':[len(names)]}]; root['scene'] = 0
    root['buffers'] = [{'byteLength':len(binary)}]
    encoded = json.dumps(root,separators=(',',':')).encode()
    encoded += b' '*((-len(encoded))%4)
    binary.extend(b'\0'*((-len(binary))%4))
    payload = (struct.pack('<III',0x46546c67,2,12+8+len(encoded)+8+len(binary)) +
               struct.pack('<II',len(encoded),0x4e4f534a)+encoded +
               struct.pack('<II',len(binary),0x004e4942)+binary)
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes(payload)
    print(f'Exported {path}: {count} vertices, {len(triangles)//3} triangles, {len(names)} bones')


def write_static_glb(path, data):
    """Write the strict rigid GLB contract used by an exact MeshRenderer assignment.

    Static geometry is authored directly in its selected MeshFilter transform's
    local space. It deliberately contains one triangle primitive and omits
    skins, joint attributes and weights entirely.
    """
    positions = np.asarray(data['positions'], dtype='<f4')
    normals = None if data.get('normals') is None else np.asarray(data['normals'], dtype='<f4')
    uvs = None if data.get('uvs') is None else np.asarray(data['uvs'], dtype='<f4')
    triangles = np.asarray(data['triangles'], dtype=np.int64).reshape(-1)
    count = len(positions)
    if not 0 < count < 65535:
        raise ValueError('FTK static GLB requires 1..65534 vertices')
    if positions.shape != (count, 3) or not np.isfinite(positions).all():
        raise ValueError('Invalid static positions')
    if normals is not None and (normals.shape != (count, 3) or not np.isfinite(normals).all()):
        raise ValueError('Invalid static normals')
    if uvs is not None and (uvs.shape != (count, 2) or not np.isfinite(uvs).all()):
        raise ValueError('Invalid static UVs')
    if not len(triangles) or len(triangles) % 3 or triangles.min() < 0 or triangles.max() >= count:
        raise ValueError('Invalid static triangle indices')

    root = {
        'asset': {'version': '2.0', 'generator': 'FTK model pipeline (Unity static MeshFilter contract)'},
        'buffers': [], 'bufferViews': [], 'accessors': [], 'nodes': [{'name': 'FTKStaticMesh', 'mesh': 0}],
        'meshes': [], 'scenes': [{'nodes': [0]}], 'scene': 0,
    }
    binary = bytearray()

    def accessor(array, component, kind, items, bounds=False):
        while len(binary) % 4:
            binary.append(0)
        raw = array.tobytes()
        view = len(root['bufferViews'])
        root['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(raw)})
        binary.extend(raw)
        value = {'bufferView': view, 'byteOffset': 0, 'componentType': component, 'count': items, 'type': kind}
        if bounds:
            value.update(min=array.min(0).tolist(), max=array.max(0).tolist())
        root['accessors'].append(value)
        return len(root['accessors']) - 1

    attributes = {'POSITION': accessor(positions, 5126, 'VEC3', count, True)}
    if normals is not None:
        attributes['NORMAL'] = accessor(normals, 5126, 'VEC3', count)
    if uvs is not None:
        attributes['TEXCOORD_0'] = accessor(uvs, 5126, 'VEC2', count)
    indices = accessor(triangles.astype('<u2'), 5123, 'SCALAR', len(triangles))
    root['meshes'] = [{'primitives': [{'attributes': attributes, 'indices': indices, 'mode': 4}]}]
    root['buffers'] = [{'byteLength': len(binary)}]
    encoded = json.dumps(root, separators=(',', ':')).encode()
    encoded += b' ' * ((-len(encoded)) % 4)
    binary.extend(b'\0' * ((-len(binary)) % 4))
    payload = (struct.pack('<III', 0x46546c67, 2, 12 + 8 + len(encoded) + 8 + len(binary)) +
               struct.pack('<II', len(encoded), 0x4e4f534a) + encoded +
               struct.pack('<II', len(binary), 0x004e4942) + binary)
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    print(f'Exported {path}: {count} vertices, {len(triangles)//3} triangles, static MeshFilter local space')

if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,required=True,help='JSON or NPZ with Unity-space positions, normals, uvs, triangles; optional joints+weights')
    p.add_argument('--reference',type=Path,help='Required for a skinned GLB; omitted for --static.')
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--rigid-bone',help='Optional single-bone diagnostic export; does not deform')
    p.add_argument('--static',action='store_true',help='Write one unskinned MeshFilter-local triangle primitive.')
    a=p.parse_args()
    data=json.loads(a.source.read_text()) if a.source.suffix=='.json' else dict(np.load(a.source,allow_pickle=False))
    if a.static:
        if a.rigid_bone:
            p.error('--rigid-bone is a skinned diagnostic and cannot be used with --static')
        write_static_glb(a.output,data)
    else:
        if a.reference is None:
            p.error('--reference is required unless --static is supplied')
        write_glb(a.output,data,np.load(a.reference,allow_pickle=False),a.rigid_bone)
