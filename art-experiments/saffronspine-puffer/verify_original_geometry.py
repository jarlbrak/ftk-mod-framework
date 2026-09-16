"""Prove source geometry/palette regeneration accesses binding metadata only."""
import json,hashlib,sys
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent;target=ROOT/'scratch/saffronspine-original-proof';target.mkdir(exist_ok=True)
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'));import export_ftk_glb,validate_glb
original_load=np.load;original_write=export_ftk_glb.write_glb;original_validate=validate_glb.validate;access=[]
class BindingOnly:
 def __init__(self,reference):self.reference=reference
 def __getitem__(self,key):
  access.append(key)
  if key not in ['bone_names','bindposes']:raise AssertionError('Generator attempted native surface access: '+key)
  return self.reference[key]
try:
 np.load=lambda *a,**k:BindingOnly(original_load(*a,**k))
 # Validation/writing itself reads native comparison metadata; exclude it from the authoring access proof.
 export_ftk_glb.write_glb=lambda *a,**k:None;validate_glb.validate=lambda *a,**k:{}
 code=(OUT/'build_geometry.py').read_text().replace('OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent',f'OUT=Path({str(target)!r});ROOT=Path({str(ROOT)!r})')
 exec(compile(code,str(OUT/'build_geometry.py'),'exec'),{'__file__':str(OUT/'build_geometry.py'),'__name__':'__original_proof__'})
finally:np.load=original_load;export_ftk_glb.write_glb=original_write;validate_glb.validate=original_validate
files=[]
for name in ['saffronspine.source.json','saffronspine_basecolor.png','saffronspine.pieces.json']:
 a=hashlib.sha256((OUT/name).read_bytes()).hexdigest();b=hashlib.sha256((target/name).read_bytes()).hexdigest();assert a==b;files.append(dict(file=name,sha256=a,reproduced_identical=True))
result=dict(status='PASS',method='Rerun exact authoring generator with output redirected and native reference reads restricted to bone_names/bindposes. Runtime export/validator stubbed only for this source-generation access proof; independent full export audit is separate.',binding_keys_accessed=sorted(set(access)),files=files,generator_sha256=hashlib.sha256((OUT/'build_geometry.py').read_bytes()).hexdigest(),scope='Original authoring provenance proof, not a replacement for export validation or live deformation checks.')
(OUT/'original-geometry-proof.json').write_text(json.dumps(result,indent=2)+'\n');print(result['status'])
