#!/usr/bin/env python3
"""Independently decode the prototype exports and compare the established binds."""
import hashlib
import json
import struct
from pathlib import Path
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=next(p for p in OUT.parents if (p/'FTKModFramework').is_dir())
BASE=ROOT/'art-experiments/thief/ranged-apparel'


def decode(path):
    raw=path.read_bytes()
    assert struct.unpack_from('<4sII',raw)==(b'glTF',2,len(raw))
    n,kind=struct.unpack_from('<II',raw,12)
    assert kind==0x4e4f534a and n%4==0
    doc=json.loads(raw[20:20+n]);size,kind=struct.unpack_from('<II',raw,20+n)
    assert kind==0x004e4942 and size%4==0 and 28+n+size==len(raw)
    binary=raw[28+n:]
    def read(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        assert v.get('buffer')==0 and 'byteStride' not in v and 'sparse' not in a
        width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
        offset=v.get('byteOffset',0)+a.get('byteOffset',0)
        assert offset%4==0
        array=np.frombuffer(binary,dtype=dtype,count=a['count']*width,offset=offset).reshape(a['count'],width)
        assert array.nbytes<=v['byteLength'] and np.isfinite(array).all()
        return array
    return doc,read


def main():
    manifest=json.loads((OUT/'manifest.json').read_text());checked=[]
    for item in manifest['assets']:
        path=OUT/item['file'];doc,read=decode(path)
        assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256']
        assert len(doc['meshes'])==1 and len(doc['meshes'][0]['primitives'])==1
        prim=doc['meshes'][0]['primitives'][0];attrs=prim['attributes']
        assert prim.get('mode',4)==4
        pos=read(attrs['POSITION']);normal=read(attrs['NORMAL']);uv=read(attrs['TEXCOORD_0'])
        tri=read(prim['indices']).reshape(-1,3)
        assert 0<len(pos)<65535 and tri.min()>=0 and tri.max()<len(pos)
        assert np.max(np.abs(np.linalg.norm(normal,axis=1)-1))<1e-4
        assert uv.min()>=0 and uv.max()<=1
        cross=np.cross(pos[tri[:,1]]-pos[tri[:,0]],pos[tri[:,2]]-pos[tri[:,0]])
        areas=np.linalg.norm(cross,axis=1)
        assert areas.min()>1e-10,(path.name,'degenerate')
        alignment=np.einsum('ij,ij->i',cross/areas[:,None],normal[tri[:,0]])
        assert alignment.min()>.99,(path.name,float(alignment.min()))
        source=json.loads(path.with_suffix('.source.json').read_text())
        for key,array in [('positions',pos),('normals',normal),('uvs',uv)]:
            assert np.allclose(array,source[key],atol=1e-6),(path.name,key)
        assert np.array_equal(tri,source['triangles'])
        piece_results=[]
        for piece in json.loads(path.with_suffix('.pieces.json').read_text()):
            a=piece['vertex_start'];b=a+piece['vertex_count'];selected=tri[(tri[:,0]>=a)&(tri[:,0]<b)]
            volume=np.einsum('ij,ij->i',pos[selected[:,0]],np.cross(pos[selected[:,1]],pos[selected[:,2]])).sum()/6
            assert volume>0,(path.name,piece['name'],float(volume))
            piece_results.append({'name':piece['name'],'outwardSignedVolume':float(volume)})
        result={'file':path.name,'sha256':item['sha256'],'vertices':len(pos),'triangles':len(tri),
                'normalAgreementMinimum':float(alignment.min()),'pieces':piece_results}
        if item['binding']:
            joints=read(attrs['JOINTS_0']);weights=read(attrs['WEIGHTS_0']);skin=doc['skins'][0]
            names=[doc['nodes'][i]['name'] for i in skin['joints']]
            assert names==item['binding']['bone_names']
            assert np.allclose(weights.sum(axis=1),1) and (weights>=0).all()
            assert set(joints[weights>0].tolist())==set(range(len(names)))
            old,oldread=decode(BASE/path.name);oldskin=old['skins'][0]
            assert names==[old['nodes'][i]['name'] for i in oldskin['joints']]
            bind=read(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
            oldbind=oldread(oldskin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
            assert np.array_equal(bind,oldbind),'Established Street bind changed'
            matrices=np.linalg.inv(bind.astype(float))@bind
            xyz1=np.column_stack([pos,np.ones(len(pos))]);deformed=np.zeros_like(xyz1)
            for slot in range(4):
                deformed+=np.einsum('nij,nj->ni',matrices[joints[:,slot]],xyz1)*weights[:,slot,None]
            error=float(np.abs(deformed[:,:3]-pos).max());assert error<1e-5
            result.update(bones=len(names),exactEstablishedBinding=True,maxBindRestError=error)
        else:assert 'skins' not in doc and 'JOINTS_0' not in attrs
        checked.append(result)
    result={'status':'PASS','scope':'Independent GLB decode; source equality; finite unit normals and nondegenerate triangles; positive closed-piece volume; UV range; exact established Street bone palette and inverse binds; normalized weights with complete positive joint use; bind-rest identity.',
            'liveGates':'Not run: native renderer/material binding, fit, face aperture, ordinary attack/hit/idle/locomotion, female and alternate race/body profiles, displays, equipment rebuild and resource lifetime. Surface validity is not art acceptance.',
            'meshes':checked}
    (OUT/'validation.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS:',len(checked),'exports; exact established Street bindings; positive exterior volumes')


if __name__=='__main__':main()
