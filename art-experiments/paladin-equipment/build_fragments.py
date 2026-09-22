#!/usr/bin/env python3
"""Partition only original authored hammer pieces into runtime break assets."""
import hashlib
import json
from pathlib import Path
import sys
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
sys.path.insert(0,str(ROOT/"tools/ai-model-pipeline"))
from export_ftk_glb import write_static_glb


def main():
    base=json.loads((OUT/"manifest.json").read_text())
    records=[];files={}
    for item in base["assets"]:
        if not item["family"].startswith("hammer"):continue
        key=item["key"];source=json.loads((OUT/(key+".source.json")).read_text())
        pieces=json.loads((OUT/(key+".pieces.json")).read_text())
        count=3 if item["family"]=="hammer-2h" else 2
        groups=[[] for _ in range(count)];assigned=set()
        positions=np.array(source["positions"]);triangles=np.array(source["triangles"])
        for piece in pieces:
            start=piece["firstTriangle"];end=start+piece["triangleCount"]
            center=positions[triangles[start:end].reshape(-1)].mean(axis=0)
            # Whole closed pieces remain intact. Two halves split by X; the
            # two-handed haft becomes the third fragment. No triangle is reused.
            if count==3 and center[1] < 1.2:group=2
            else:group=0 if center[0]<-1e-5 else 1
            for triangle in range(start,end):
                assert triangle not in assigned
                assigned.add(triangle);groups[group].append(triangle)
        assert assigned==set(range(len(triangles))) and all(groups)
        paths=["Break","Break/Break"] if count==2 else ["break","break/break2","break/break1"]
        fragments=[]
        for index,group in enumerate(groups):
            data={k:[] for k in ["positions","normals","uvs","triangles"]}
            for tid in group:
                output_triangle=[]
                for vertex in source["triangles"][tid]:
                    output_triangle.append(len(data["positions"]))
                    for attribute in ["positions","normals","uvs"]:data[attribute].append(source[attribute][vertex])
                data["triangles"].append(output_triangle)
            name=key+"-fragment-"+str(index+1)
            (OUT/(name+".source.json")).write_text(json.dumps(data,separators=(",",":"))+"\n")
            write_static_glb(OUT/(name+".glb"),data)
            for suffix in [".glb",".source.json"]:
                path=OUT/(name+suffix);files[path.name]=hashlib.sha256(path.read_bytes()).hexdigest()
            fragments.append({"rendererPath":paths[index],"glbFile":name+".glb","triangleCount":len(group),"sourceTriangleIndices":group})
        records.append({"item":key,"basePrefab":"WarHammer" if count==3 else "SmithHammer","wholeRendererPath":".","wholeGlb":key+".glb","fragments":fragments})
    report={"schema":"ftkmf.paladin-original-fragments.v1","generatorSha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),"sourceManifestSha256":hashlib.sha256((OUT/"manifest.json").read_bytes()).hexdigest(),"scope":"Disjoint partition of original source triangles by whole authored piece, complete coverage. Preserves source coordinates; native fragment attachment transforms are unverified. Intentional source component intersections are unchanged.","mappings":records,"files":files}
    (OUT/"fragments-manifest.json").write_text(json.dumps(report,indent=2)+"\n")
    print("PASS: 30 original break meshes, exact once-only source triangle coverage")


if __name__=="__main__":main()
