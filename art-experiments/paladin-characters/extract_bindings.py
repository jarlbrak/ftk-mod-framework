#!/usr/bin/env python3
"""Extract only names and inverse bind matrices into ignored local scratch."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import UnityPy

TARGETS = {"female-body":121067,"female-hair-top":121083,"female-hair-bottom":121178,
           "female-armor":121248,"male-body":121660,"male-hair-top":121495,
           "male-hair-bottom":121274,"male-armor":121510,"boots":121113}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets",type=Path,required=True)
    parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[2]
    output=args.output.resolve()
    output.relative_to(root/"scratch")
    output.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(args.assets))
    asset=next(f for f in env.files.values() if Path(f.name).name==args.assets.name)
    records=[]
    for key,rid in TARGETS.items():
        renderer=asset.objects[rid].read()
        mesh=renderer.m_Mesh.read()
        names=[p.read().m_GameObject.read().m_Name for p in renderer.m_Bones]
        binds=np.array([[[getattr(m,f"e{r}{c}") for c in range(4)] for r in range(4)] for m in mesh.m_BindPose])
        assert len(names)==len(binds) and len(set(names))==len(names)
        document={"rendererId":rid,"nativeMeshName":mesh.m_Name,"bone_names":names,"bindposes":binds.tolist()}
        (output/(key+".json")).write_text(json.dumps(document,indent=2)+"\n")
        records.append({"key":key,"rendererId":rid,"nativeMeshName":mesh.m_Name,"boneCount":len(names)})
    report={"sourceSha256":hashlib.sha256(args.assets.read_bytes()).hexdigest(),"scope":"Names and inverse bind matrices only. No surface, texture, UV, weight, bounds or animation data is selected or written.","targets":records}
    (output/"binding-provenance.json").write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps(records,indent=2))


if __name__=="__main__":main()
