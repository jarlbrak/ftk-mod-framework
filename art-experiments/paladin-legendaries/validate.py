#!/usr/bin/env python3
"""Independently decode emitted GLBs and verify original per-piece geometry."""
import hashlib
import json
from pathlib import Path
import struct
import numpy as np

ROOT = Path(__file__).resolve().parent


def main(compare_manifest=None):
    manifest = json.loads((ROOT / "manifest.json").read_text())
    assert hashlib.sha256((ROOT / manifest["generator"]).read_bytes()).hexdigest() == manifest["generatorSha256"]
    for name, digest in manifest["dependencies"].items():
        assert hashlib.sha256((ROOT.parents[1]/name).read_bytes()).hexdigest() == digest, name
    for name, digest in manifest["files"].items():
        assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest() == digest, name
    results=[]
    for item in [{"key": Path(name).stem} for name in manifest["files"] if name.endswith(".glb")]:
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
            from collections import Counter
            edges=Counter()
            for triangle in triangles[start:end]:
                points=[tuple(round(float(v),6) for v in pos[index]) for index in triangle]
                for i in range(3):edges[tuple(sorted((points[i],points[(i+1)%3])))]+=1
            assert all(count==2 for count in edges.values()), (key,piece['name'],'open or nonmanifold edge')
            volumes.append(volume)
        results.append({"key":key,"vertices":len(pos),"triangles":len(triangles),"closedPiecePositiveVolumes":len(volumes),"minimumTriangleNormalDot":float(agreement.min()),"status":"PASS"})
    assert len(results)==11
    # Fragment delivery must cover exactly the source pieces once and preserve every triangle.
    for item in manifest['assets']:
        if not item['fragments']: continue
        original=json.loads((ROOT/(item['key']+'.source.json')).read_text())
        def triangle_multiset(data):
            from collections import Counter
            return Counter(tuple(tuple(round(float(v),6) for v in data['positions'][i]) for i in tri) for tri in data['triangles'])
        from collections import Counter
        union=Counter()
        for fragment in item['fragments']:
            union.update(triangle_multiset(json.loads((ROOT/(fragment['key']+'.source.json')).read_text())))
        assert union==triangle_multiset(original), item['key']
    from PIL import Image
    package=ROOT.parents[1]/'marketplace/packages/paladin'
    provenance=json.loads((package.parent/'paladin-assets.provenance.json').read_text())['files']
    for name,sha in manifest['files'].items():
        if name.endswith('.glb') or name.endswith(('-icon.png','-palette.png')):
            assert hashlib.sha256((package/'assets'/name).read_bytes()).hexdigest()==sha
            assert provenance['assets/'+name]['sha256']==sha
        if name.endswith('-icon.png'):
            image=Image.open(ROOT/name)
            assert image.mode=='RGBA' and image.size==(256,256)
            alpha=image.getchannel('A'); bounds=alpha.getbbox()
            assert bounds and bounds[0]>0 and bounds[1]>0 and bounds[2]<256 and bounds[3]<256, (name,bounds)
    report={"status":"PASS","scope":"Independent static binary/source equality, finite arrays, indices, UVs, normal length, winding and positive piece volume. Not native placement, binding, gameplay or visual acceptance.","assets":results}
    (ROOT/"validation.json").write_text(json.dumps(report,indent=2)+"\n")
    if compare_manifest:
        previous=json.loads(Path(compare_manifest).read_text())
        differences=[name for name,sha in manifest['files'].items() if previous['files'].get(name)!=sha]
        assert not differences, differences
        assert previous['generatorSha256']==manifest['generatorSha256']
        reproduction={'status':'PASS','generatorSha256':manifest['generatorSha256'],'validatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'comparedFiles':len(manifest['files']),'differences':differences,'scope':'Consecutive full rebuilds yield byte-identical geometry, source/piece maps, icons and palette. Live behavior remains separate.'}
        (ROOT/'reproducibility.json').write_text(json.dumps(reproduction,indent=2)+'\n')
    print("PASS: 11 original static GLBs and all source/manifests")


if __name__=="__main__":
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compare-manifest',type=Path)
    main(parser.parse_args().compare_manifest)
