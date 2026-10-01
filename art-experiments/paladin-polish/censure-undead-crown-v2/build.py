"""Small symmetric upper-rear crown clearance correction on original Censure geometry."""
import hashlib,json
from pathlib import Path
import numpy as np
from mesh_io import decode,topology
from export_ftk_glb import write_static_glb
HERE=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
expected={'paladin-censure-helmet.glb':'e59603b1c32393b77131ed594fb7972b30b2763e80dc9f34b45fc773faa829b1','paladin-censure-helmet-display.glb':'0b22d033b0fe2407b7b456e4e671fc47ef7f7d58076c4d0c5808f32da0f5cfa3'}
def smooth(x):
 t=np.clip(x,0,1);return t*t*(3-2*t)
def displacement(p):
 x,y,z=p.T
 return .022*np.exp(-((z+.21)/.24)**2)*smooth((y+.32)/.20)*(1-smooth((y+.06)/.14))
def main():
 for name,h in expected.items():assert sha(HERE/'inputs'/name)==h
 a=decode(HERE/'inputs/paladin-censure-helmet.glb');b=decode(HERE/'inputs/paladin-censure-helmet-display.glb')
 p=a['POSITION'].astype(float);delta=displacement(p);q=p.copy();q[:,1]+=delta
 assert delta.max()<.0221 and np.count_nonzero(delta)>0
 # Preserve authored normal seams through the inverse transpose of this smooth deformation.
 grad=[]
 for i in range(3):
  offset=np.zeros_like(p);offset[:,i]=1e-5;grad.append((displacement(p+offset)-displacement(p-offset))/2e-5)
 dx,dy,dz=grad;assert np.min(1+dy)>.75
 n=a['NORMAL'].astype(float);n[:,1]/=1+dy;n[:,0]-=dx*n[:,1];n[:,2]-=dz*n[:,1];n/=np.linalg.norm(n,axis=1)[:,None]
 # The existing card is a uniform affine copy; carry the same shape correction into its framing.
 affine=np.linalg.lstsq(np.c_[p,np.ones(len(p))],b['POSITION'],rcond=None)[0]
 assert np.abs(np.c_[p,np.ones(len(p))]@affine-b['POSITION']).max()<2e-7
 assert np.array_equal(a['triangles'],b['triangles']) and np.array_equal(a['TEXCOORD_0'],b['TEXCOORD_0'])
 scale=float(np.trace(affine[:3])/3);assert np.max(np.abs(affine[:3]-np.eye(3)*scale))<1e-7
 card=b['POSITION'].astype(float);card[:,1]+=delta*scale
 records=[]
 for name,source,positions in [('paladin-censure-helmet.glb',a,q),('paladin-censure-helmet-display.glb',b,card)]:
  dest=HERE/'outputs'/name;write_static_glb(dest,dict(positions=positions.tolist(),normals=n.tolist(),uvs=source['TEXCOORD_0'].tolist(),triangles=source['triangles'].tolist()))
  final=decode(dest);f=final['triangles'];cross=np.cross(final['POSITION'][f[:,1]]-final['POSITION'][f[:,0]],final['POSITION'][f[:,2]]-final['POSITION'][f[:,0]])
  agreement=np.sum(cross*final['NORMAL'][f].mean(1),axis=1);assert np.all(agreement>0)
  assert topology(source['POSITION'],f)==topology(final['POSITION'],f)
  assert np.array_equal(final['TEXCOORD_0'],source['TEXCOORD_0']) and np.array_equal(f,source['triangles'])
  assert np.array_equal(final['POSITION'][delta==0],source['POSITION'][delta==0])
  records.append({'output':name,'sha256':sha(dest),'inputSha256':expected[name],'vertices':len(p),'triangles':len(f),'topology':topology(final['POSITION'],f),'unchangedVertices':int(np.count_nonzero(delta==0)),'maxDisplacement':float(np.max(np.abs(final['POSITION']-source['POSITION']))),'minNormalAgreement':float(agreement.min())})
 receipt={'status':'Offline candidate, native fit pending','method':'Smooth symmetric 0.022 maximum Y clearance across the upper dome and central ridge, tapering through the plume base. Upper plume, lower brow, cheeks and lower rim remain fixed. Original topology and UVs exact; authored normals transformed through deformation Jacobian. Display framing retains existing uniform transform.','reason':'Undead skull patches confirmed by native material tint in R41 and both sides in R42. V1 cleared one side but left two opposite-side patches. No native geometry inspected or copied.','outputs':records}
 (HERE/'outputs/receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(records,indent=2))
if __name__=='__main__':main()
