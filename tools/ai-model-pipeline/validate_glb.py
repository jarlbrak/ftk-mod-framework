#!/usr/bin/env python3
"""Check the binary contract independently and verify skin bind/rest identity."""
import argparse
import json
import struct
from pathlib import Path
import numpy as np


def validate(path, reference):
    raw=path.read_bytes()
    magic,version,length=struct.unpack_from('<III',raw)
    assert (magic,version,length)==(0x46546c67,2,len(raw)), 'GLB header mismatch'
    n,kind=struct.unpack_from('<II',raw,12)
    assert kind==0x4e4f534a and n%4==0
    doc=json.loads(raw[20:20+n])
    size,kind=struct.unpack_from('<II',raw,20+n)
    assert kind==0x004e4942 and size%4==0 and 28+n+size==len(raw)
    binary=raw[28+n:]
    def read(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        assert 'byteStride' not in v and v.get('buffer')==0 and 'sparse' not in a
        assert not a.get('normalized',False)
        width={'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16,'SCALAR':1}[a['type']]
        dtype={5123:'<u2',5126:'<f4',5125:'<u4'}[a['componentType']]
        offset=v.get('byteOffset',0)+a.get('byteOffset',0)
        assert offset%4==0
        result=np.frombuffer(binary,dtype=dtype,count=a['count']*width,offset=offset).reshape(a['count'],width)
        assert result.nbytes<=v['byteLength'] and np.isfinite(result).all()
        return result
    assert len(doc['meshes'])==len(doc['skins'])==1
    primitives=doc['meshes'][0]['primitives'];assert 1<=len(primitives)<=4
    p=primitives[0];attrs=p['attributes']
    if len(primitives)>1:
        assert len(doc['materials'])==len(primitives)
        assert len(set(q['indices'] for q in primitives))==len(primitives)
        for i,q in enumerate(primitives):
            assert q['attributes']==attrs and q.get('material')==i and q.get('mode',4)==4 and 'targets' not in q
            assert doc['accessors'][q['indices']]['componentType']==5123
    assert all(q.get('mode',4)==4 for q in primitives)
    for name,component,kind in [('POSITION',5126,'VEC3'),('NORMAL',5126,'VEC3'),
                                 ('TEXCOORD_0',5126,'VEC2'),('JOINTS_0',5123,'VEC4'),
                                 ('WEIGHTS_0',5126,'VEC4')]:
        a=doc['accessors'][attrs[name]]
        assert (a['componentType'],a['type'])==(component,kind), f'Invalid {name} accessor type'
    index_accessor=doc['accessors'][p['indices']]
    assert index_accessor['type']=='SCALAR' and index_accessor['componentType'] in (5123,5125)
    pos=read(attrs['POSITION']);w=read(attrs['WEIGHTS_0']);j=read(attrs['JOINTS_0'])
    assert 0<len(pos)<65535 and w.shape==j.shape==(len(pos),4)
    assert np.all(w>=0) and np.allclose(w.sum(1),1,atol=1e-6)
    assert read(attrs['NORMAL']).shape==(len(pos),3)
    assert read(attrs['TEXCOORD_0']).shape==(len(pos),2)
    submeshes=[read(q['indices']).ravel() for q in primitives]
    for indices in submeshes:
        assert len(indices)>0 and len(indices)%3==0 and indices.max()<len(pos)
    ix=np.concatenate(submeshes)
    def normal_agreement(vertices, normals, faces):
        tri=np.asarray(faces).reshape(-1,3)
        points=vertices[tri]
        cross=np.cross(points[:,1]-points[:,0],points[:,2]-points[:,0])
        average=normals[tri].mean(axis=1)
        signed=np.einsum('ij,ij->i',cross,average)
        informative=np.abs(signed)>1e-10
        assert informative.any(), 'No nondegenerate triangles with meaningful normals'
        values=signed[informative]
        return dict(positive_fraction=float(np.mean(values>0)),
                    median_signed_dot=float(np.median(values)),
                    informative_triangles=int(informative.sum()))
    native_orientation=normal_agreement(reference['positions'],reference['normals'],reference['triangles'])
    source_orientation=normal_agreement(pos,read(attrs['NORMAL']),ix)
    assert np.sign(source_orientation['median_signed_dot'])==np.sign(native_orientation['median_signed_dot']), (
        f'Triangle winding opposes native reference normals: source={source_orientation}, reference={native_orientation}')
    skin=doc['skins'][0]
    names=[doc['nodes'][i]['name'] for i in skin['joints']]
    assert len(set(names))==len(names) and j.max()<len(names)
    matrix_accessor=doc['accessors'][skin['inverseBindMatrices']]
    assert (matrix_accessor['componentType'],matrix_accessor['type'],matrix_accessor['count'])==(5126,'MAT4',len(names))
    ibm=read(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
    original_names=reference['bone_names'].tolist()
    mapped=np.array([reference['bindposes'][original_names.index(name)] for name in names])
    assert np.allclose(ibm,mapped,atol=1e-6), 'Bind matrices changed or transposed'
    rest=np.linalg.inv(mapped)
    matrices=rest@ibm
    xyz1=np.column_stack([pos,np.ones(len(pos))])
    deformed=np.zeros_like(xyz1)
    for slot in range(4):
        deformed+=np.einsum('nij,nj->ni',matrices[j[:,slot]],xyz1)*w[:,slot,None]
    error=float(np.abs(deformed[:,:3]-pos).max())
    assert error<1e-5, f'Rest skin deform error {error}'
    report=dict(file=str(path),vertices=len(pos),triangles=len(ix)//3,bones=len(names),
                primitives=len(primitives),triangles_per_primitive=[len(indices)//3 for indices in submeshes],
                bounds_min=pos.min(0).tolist(),bounds_max=pos.max(0).tolist(),max_bind_rest_error=error,
                source_normal_agreement=source_orientation,reference_normal_agreement=native_orientation,
                status='PASS (binary and skin contract; live animation still requires in-game check)')
    print(json.dumps(report,indent=2))
    return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('glb',type=Path)
    p.add_argument('--reference',type=Path,required=True)
    a=p.parse_args();validate(a.glb,np.load(a.reference,allow_pickle=False))
