#!/usr/bin/env python3
"""Read back original GLBs and verify exact binding-only contracts."""
import hashlib
import json
from pathlib import Path
import struct
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]


def main():
    manifest=json.loads((OUT/"manifest.json").read_text())
    assert hashlib.sha256((OUT/"build_geometry.py").read_bytes()).hexdigest()==manifest["generatorSha256"]
    assert hashlib.sha256((ROOT/manifest["primitiveLibrary"]).read_bytes()).hexdigest()==manifest["primitiveLibrarySha256"]
    for name,sha in manifest["files"].items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==sha,name
    results=[]
    for item in manifest["assets"]:
        key=item["key"];raw=(OUT/(key+".glb")).read_bytes()
        assert struct.unpack_from("<III",raw)==(0x46546c67,2,len(raw))
        jlen,jtype=struct.unpack_from("<II",raw,12);assert jtype==0x4e4f534a
        gltf=json.loads(raw[20:20+jlen]);blen,btype=struct.unpack_from("<II",raw,20+jlen)
        binary=raw[28+jlen:];assert btype==0x004e4942 and len(binary)==blen
        def read(index):
            a=gltf["accessors"][index];v=gltf["bufferViews"][a["bufferView"]]
            count={"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4,"MAT4":16}[a["type"]]
            return np.frombuffer(binary,dtype={5126:"<f4",5123:"<u2"}[a["componentType"]],offset=v.get("byteOffset",0)+a.get("byteOffset",0),count=a["count"]*count).reshape(-1,count)
        primitive=gltf["meshes"][0]["primitives"][0];attrs=primitive["attributes"]
        pos=read(attrs["POSITION"]);normal=read(attrs["NORMAL"]);uv=read(attrs["TEXCOORD_0"]);tri=read(primitive["indices"]).reshape(-1,3)
        source=json.loads((OUT/(key+".source.json")).read_text())
        for field,array in [("positions",pos),("normals",normal),("uvs",uv),("triangles",tri)]:assert np.allclose(array,source[field],atol=1e-6),(key,field)
        assert np.isfinite(pos).all() and len(pos)<65535 and tri.max()<len(pos)
        assert np.allclose(np.linalg.norm(normal,axis=1),1,atol=1e-5)
        a,b,c=[pos[tri[:,i]] for i in range(3)];cross=np.cross(b-a,c-a)
        assert (np.einsum("ij,ij->i",cross,normal[tri[:,0]])>1e-10).all(),key
        assert ((uv>=0)&(uv<=1)).all()
        if item["part"]!="helmet":
            path=ROOT/"scratch/paladin-bindings"/(item["bindingInput"]+".json")
            assert hashlib.sha256(path.read_bytes()).hexdigest()==item["bindingSha256"]
            binding=json.loads(path.read_text());skin=gltf["skins"][0]
            names=[gltf["nodes"][i]["name"] for i in skin["joints"]]
            assert names==binding["bone_names"]
            matrices=read(skin["inverseBindMatrices"]).reshape(-1,4,4).transpose(0,2,1)
            assert np.allclose(matrices,binding["bindposes"],atol=1e-6)
            joints=read(attrs["JOINTS_0"]);weights=read(attrs["WEIGHTS_0"])
            assert np.allclose(weights,source["weights"],atol=1e-6) and np.array_equal(joints,source["joints"])
            assert (weights>=0).all() and np.allclose(weights.sum(axis=1),1,atol=1e-6)
            assert (joints < len(names)).all(),(key,"joint outside binding palette")
            unused={name for index,name in enumerate(names) if index not in set(joints[weights>0].tolist())}
            # Novice leaves hands exposed while retaining the exact native binding palette.
            expected_unused=({"Wrist_R","Wrist_L","ThumbFinger1_R","ThumbFinger1_L"}&set(names)
                             if item["part"]=="armor" and item["outfit"]=="novice" else set())
            assert unused==expected_unused,(key,"unexpected unweighted bones",unused)
        else:assert "skins" not in gltf
        results.append({"key":key,"status":"PASS","triangles":len(tri),"vertices":len(pos),"exactBinding":item["part"]!="helmet"})
    (OUT/"validation.json").write_text(json.dumps({"status":"PASS","scope":"Offline binary, original geometry and exact binding metadata only. Native assembly, placement, motion, appearance and lifetime remain unverified.","assets":results},indent=2)+"\n")
    assert len(results)==24
    assert len(manifest["equipmentSets"])==6
    print("PASS: 18 skinned apparel assets and six rigid helmets across six equipment sets")


if __name__=="__main__":main()
