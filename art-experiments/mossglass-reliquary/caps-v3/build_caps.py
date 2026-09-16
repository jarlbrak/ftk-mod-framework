"""Original mesh correction: reverse only 32 inward cap triangles and their 96 normals."""
import json,sys,hashlib
from pathlib import Path
import numpy as np
O=Path(__file__).resolve().parent;B=O.parent;R=B.parent.parent;sys.path.insert(0,str(R/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
d=json.loads((B/'mossglass.source.json').read_text());old=json.loads(json.dumps(d));p=np.array(d['positions']);caps=[]
for t in d['primitives'][1]['triangles']:
 if p[t,1].min()>2.039 or p[t,1].max()<.031:caps.append(tuple(t))
assert len(caps)==32;capset=set(caps);vertices={i for t in caps for i in t};assert len(vertices)==96
for key in ['triangles']:
 d[key]=[[t[0],t[2],t[1]] if tuple(t)in capset else t for t in d[key]]
for q in d['primitives']:q['triangles']=[[t[0],t[2],t[1]]if tuple(t)in capset else t for t in q['triangles']]
for i in vertices:d['normals'][i]=[-x for x in d['normals'][i]]
for key in ['positions','uvs','joints','weights','bone_names']:assert d[key]==old[key]
for t in d['primitives'][1]['triangles']:
 if set(t)<=vertices:
  q=p[t];cross=np.cross(q[1]-q[0],q[2]-q[0]);assert cross[1]*(1 if q[:,1].min()>2.039 else -1)>0
(O/'mossglass_caps_v3.source.json').write_text(json.dumps(d,separators=(',',':'))+'\n');ref=np.load(R/'scratch/cube-topology-analysis/reference-121012/reference.npz');write_glb(O/'mossglass_caps_v3.glb',d,ref);v=validate(O/'mossglass_caps_v3.glb',ref);(O/'validation.json').write_text(json.dumps(v,indent=2)+'\n');r=dict(status='PASS_CAP_ORIENTATION_AND_EXACT_CHANGE_SCOPE',priorSourceSha256=hashlib.sha256((B/'mossglass.source.json').read_bytes()).hexdigest(),newGlbSha256=hashlib.sha256((O/'mossglass_caps_v3.glb').read_bytes()).hexdigest(),reversedTriangleCount=32,reversedNormalCount=96,unchanged=['all positions','all UVs','all weights/joints','full palette','reference IBMs','other820triangles','other2460normals','primitive assignment'],scope='Cap culling correction only; native attack framing and death visibility unresolved.')
(O/'change-audit.json').write_text(json.dumps(r,indent=2)+'\n')
