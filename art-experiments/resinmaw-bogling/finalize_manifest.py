"""Refresh hashes and mechanical checks after rebuilding; never grant visual/live acceptance."""
import json,hashlib,subprocess
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
manifest=json.loads((OUT/'manifest.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest['parts']=[];manifest['references']=[]
for name,rid in [('resinmaw',121344)]:
 path=ROOT/f'scratch/skeleton-audit/{rid}/reference.npz';ref=np.load(path,allow_pickle=False)
 data=json.loads((OUT/f'{name}.source.json').read_text());positions=np.asarray(data['positions'])
 if not ((positions.min(0)>=ref['positions'].min(0)).all() and (positions.max(0)<=ref['positions'].max(0)).all()):raise ValueError(name+' exceeds native bind surface bounds')
 v=json.loads((OUT/f'{name}.validation.json').read_text());b=json.loads((ROOT/f'scratch/resinmaw-roundtrip/{name}.validation.json').read_text())
 if not v['status'].startswith('PASS') or not b['status'].startswith('PASS'):raise ValueError('Export validation did not pass')
 manifest['references'].append(dict(renderer_id=rid,local_reference_path=str(path.relative_to(ROOT)),sha256=sha(path)))
 manifest['parts'].append(dict(name=name,vertices=v['vertices'],triangles=v['triangles'],palette_joints=v['bones'],max_bind_rest_error=v['max_bind_rest_error'],blender_reopen_max_bind_rest_error=b['max_bind_rest_error'],normal_agreement=v['source_normal_agreement']['positive_fraction'],bind_bounds_min=v['bounds_min'],bind_bounds_max=v['bounds_max'],blender_roundtrip_glb_sha256=sha(ROOT/f'scratch/resinmaw-roundtrip/{name}.glb')))
manifest['checks']=dict(independent_export='pass for the part',saved_blender_reopen_export='pass for the part',native_bind_bounds='the original surface contained in corresponding native rest surface bounds; not animated-envelope proof',studio_review='pending manual review after regeneration',live_binding='pending',live_animation='pending',live_material_and_culling='pending')
manifest['art_status']='Original amber slug-toad surface model; manual studio and live acceptance pending after regeneration'
manifest['assets']={p.name:dict(sha256=sha(p),bytes=p.stat().st_size) for p in sorted(OUT.iterdir()) if p.is_file() and p.name!='manifest.json'}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('Mechanical checks and hashes refreshed; review remains manual.')
