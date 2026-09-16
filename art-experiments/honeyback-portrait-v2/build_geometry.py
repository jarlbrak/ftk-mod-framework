"""Original portrait-readability variant; immutable tested v1 remains in sibling folder."""
import json,hashlib,sys,shutil
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent;V1=OUT.parent/'honeyback-bear'
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
source=V1/'honeyback.source.json';d=json.loads(source.read_text());pieces=json.loads((V1/'honeyback.pieces.json').read_text());before=np.array(d['positions']);after=before.copy();changes=[]
for piece in pieces:
 name=piece['name'];start=piece['vertex_start'];end=start+piece['vertex_count'];p=after[start:end];c=p.mean(0);scale=np.ones(3);shift=np.zeros(3)
 if name.startswith('Amber eye '):scale=np.array([1.45,1.40,1.10]);shift[0]=np.sign(c[0])*.088
 elif name.startswith('Eye shadow '):scale=np.array([1.22,1.20,1.0]);shift[0]=np.sign(c[0])*.088
 elif name.startswith('Brown brow '):shift[0]=np.sign(c[0])*.065
 elif name=='Cream upper muzzle':scale[0]=.88
 elif name=='Black nose':scale[0]=.81
 else:continue
 after[start:end]=(p-c)*scale+c+shift;changes.append(dict(piece=name,vertex_start=start,vertex_count=end-start,scale_about_centroid=scale.tolist(),translation=shift.tolist()))
d['positions']=after.tolist()
# Original source is split per triangle, so transformed faces receive exact flat normals.
for tri in d['triangles']:
 v=after[tri];n=np.cross(v[1]-v[0],v[2]-v[0]);n/=np.linalg.norm(n)
 for i in tri:d['normals'][i]=n.tolist()
ref=np.load(ROOT/'scratch/skeleton-audit/121467/reference.npz',allow_pickle=False)
if not ((after.min(0)>=ref['positions'].min(0)).all() and (after.max(0)<=ref['positions'].max(0)).all()):raise ValueError('Native bind bounds exceeded')
name='honeyback_portrait_v2';(OUT/f'{name}.source.json').write_text(json.dumps(d,separators=(',',':'))+'\n');write_glb(OUT/f'{name}.glb',d,ref);v=validate(OUT/f'{name}.glb',ref);(OUT/f'{name}.validation.json').write_text(json.dumps(v,indent=2)+'\n');(OUT/f'{name}.pieces.json').write_text(json.dumps(pieces,indent=2)+'\n');shutil.copyfile(V1/'honeyback_basecolor.png',OUT/'honeyback_basecolor.png')
changed=np.any(after!=before,axis=1)
audit=dict(source_v1_path=str(source.relative_to(ROOT)),source_v1_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),changed_vertices=int(changed.sum()),total_vertices=len(after),pieces=changes,unchanged_contract=['triangles','uvs','joints','weights','bone_names'],scope='Eyes/shadows/brows moved outward; eyes enlarged; upper muzzle and nose narrowed. Body, limbs, lower jaw, ears, palette and bindings unchanged.')
for key in audit['unchanged_contract']:assert d[key]==json.loads(source.read_text())[key]
(OUT/'geometry-change-audit.json').write_text(json.dumps(audit,indent=2)+'\n')
manifest=dict(name='Honeyback Bear portrait v2',native_chassis='bearB',reference_renderer=121467,renderer_path='enBear01',native_surface_copied=False,art_status='Original portrait variant; studio/native portrait live acceptance pending',preserved_v1='../honeyback-bear/',remaining=['Offline native PortraitCam visibility audit','Studio review','Live native portrait animation and combat appearance'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
