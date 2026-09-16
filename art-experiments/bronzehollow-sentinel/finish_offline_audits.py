"""Verify original/Blender roundtrip equivalence and exact recorded native bone order."""
import json,hashlib
from pathlib import Path
import numpy as np
O=Path(__file__).resolve().parent;R=O.parent.parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
a=json.loads((O/'bronzehollow.source.json').read_text());b=json.loads((O/'bronzehollow-reopened.source.json').read_text());checks={}
assert a['bone_names']==b['bone_names']
for k,tol in [('positions',1e-6),('normals',1e-4),('uvs',1e-6),('joints',0),('weights',0),('triangles',0)]:
 x=np.array(a[k]);y=np.array(b[k]);assert x.shape==y.shape;kmax=float(abs(x-y).max());assert kmax<=tol;checks[k]={'maximumAbsoluteDifference':kmax,'tolerance':tol}
assert sha(O/'bronzehollow_basecolor.png')==sha(O/'bronzehollow-reopened.png')
frames=[]
for cid in ['cc28a1e0350e48a3bc927d1894bea810','79e0edfed0c049eebd4788919e9770d6','3c32bd1578be482c87d3353aabdd5938']:
 p=R/'scratch/mirewarden-game/model-test-output'/f'{cid}.json';f=json.loads(p.read_text());assert len(f['frames'])==120
 assert all([b['name'] for b in x['bones']]==a['bone_names'] for x in f['frames'])
 frames.append({'path':str(p.relative_to(R)),'sha256':sha(p),'frames':120,'allOrdered36BonesMatch':True})
r={'status':'PASS_OFFLINE_ROUNDTRIP_AND_CAPTURE_BINDING','primaryGlbSha256':sha(O/'bronzehollow.glb'),'blenderReopenedGlbSha256':sha(O/'bronzehollow-reopened.glb'),'checks':checks,'captures':frames,'vertices':len(a['positions']),'triangles':len(a['triangles']),'scope':'Original geometry and reopened Blender exporter agree within stated float tolerances. Native capture bone names checked for all360frames. No live original appearance acceptance.'}
(O/'roundtrip-and-capture-audit.json').write_text(json.dumps(r,indent=2)+'\n')
