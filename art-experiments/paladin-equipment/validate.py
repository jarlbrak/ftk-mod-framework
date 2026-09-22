#!/usr/bin/env python3
"""Independently decode emitted GLBs and verify original per-piece geometry."""
import hashlib
import json
from pathlib import Path
import struct
import numpy as np

ROOT = Path(__file__).resolve().parent


def main():
    manifest = json.loads((ROOT / "manifest.json").read_text())
    assert hashlib.sha256((ROOT / manifest["generator"]).read_bytes()).hexdigest() == manifest["generatorSha256"]
    for name, digest in manifest["files"].items():
        assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest() == digest, name
    results=[]
    for item in manifest["assets"]:
        key=item["key"]
        raw=(ROOT/(key+".glb")).read_bytes()
        magic,version,length=struct.unpack_from("<III",raw)
        assert (magic,version,length)==(0x46546c67,2,len(raw))
        jlen,jkind=struct.unpack_from("<II",raw,12)
        assert jkind==0x4e4f534a
        document=json.loads(raw[20:20+jlen])
        blen,bkind=struct.unpack_from("<II",raw,20+jlen)
        assert bkind==0x004e4942
        binary=raw[28+jlen:]
        assert len(binary)==blen
        assert "skins" not in document
        assert len(document["meshes"])==1
        primitive=document["meshes"][0]["primitives"][0]
        assert set(primitive["attributes"])=={"POSITION","NORMAL","TEXCOORD_0"}
        def read(index):
            acc=document["accessors"][index]
            view=document["bufferViews"][acc["bufferView"]]
            dtype={5126:"<f4",5123:"<u2"}[acc["componentType"]]
            components={"SCALAR":1,"VEC2":2,"VEC3":3}[acc["type"]]
            offset=view.get("byteOffset",0)+acc.get("byteOffset",0)
            return np.frombuffer(binary,dtype=dtype,count=acc["count"]*components,offset=offset).reshape(-1,components)
        pos=read(primitive["attributes"]["POSITION"])
        normals=read(primitive["attributes"]["NORMAL"])
        uv=read(primitive["attributes"]["TEXCOORD_0"])
        triangles=read(primitive["indices"]).reshape(-1,3)
        source=json.loads((ROOT/(key+".source.json")).read_text())
        for field,actual in [("positions",pos),("normals",normals),("uvs",uv),("triangles",triangles)]:
            assert np.allclose(actual,source[field],atol=1e-7), (key,field)
        assert 0<len(pos)<65535 and np.isfinite(pos).all()
        assert triangles.max()<len(pos)
        assert np.allclose(np.linalg.norm(normals,axis=1),1,atol=1e-5)
        assert ((uv>=0)&(uv<=1)).all()
        a,b,c=(pos[triangles[:,i]] for i in range(3))
        cross=np.cross(b-a,c-a)
        assert (np.linalg.norm(cross,axis=1)>1e-10).all(),key
        agreement=np.einsum("ij,ij->i",cross,normals[triangles[:,0]])
        assert (agreement>0).all(),key
        pieces=json.loads((ROOT/(key+".pieces.json")).read_text())
        volumes=[]
        for piece in pieces:
            start=piece["firstTriangle"]
            end=start+piece["triangleCount"]
            volume=float(np.einsum("ij,ij->i",a[start:end],np.cross(b[start:end],c[start:end])).sum()/6)
            assert volume>0,(key,piece["name"],volume)
            volumes.append(volume)
        results.append({"key":key,"vertices":len(pos),"triangles":len(triangles),"closedPiecePositiveVolumes":len(volumes),"minimumTriangleNormalDot":float(agreement.min()),"status":"PASS"})
    assert len(results)==18
    report={"status":"PASS","scope":"Independent static binary/source equality, finite arrays, indices, UVs, normal length, winding and positive piece volume. Not native placement, binding, gameplay or visual acceptance.","assets":results}
    (ROOT/"validation.json").write_text(json.dumps(report,indent=2)+"\n")
    print("PASS: 18 original static GLBs and all source/manifests")


if __name__=="__main__": main()
