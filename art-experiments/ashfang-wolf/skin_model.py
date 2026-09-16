"""Blend original body geometry to local wolfA, with explicit rigid face/paw details."""
import sys,json
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import transfer_weights,write_glb
reference=np.load(ROOT/'scratch/model-reference/wolf-a/reference.npz')
data=json.loads((OUT/'source_unskinned.json').read_text())
joints,weights=transfer_weights(np.asarray(data['positions']),reference)
names=reference['bone_names'].tolist()
for i,bone in enumerate(data.pop('rigid_overrides')):
 if bone:
  joints[i]=[names.index(bone),0,0,0];weights[i]=[1,0,0,0]
data['joints']=joints.tolist();data['weights']=weights.tolist();data['bone_names']=names
(OUT/'source_mesh.json').write_text(json.dumps(data))
write_glb(OUT/'ashfang_rigged.glb',data,reference)
