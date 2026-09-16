"""Independent binary cap facing proof, including actual native posed matrices."""
import json,struct,hashlib,argparse
from pathlib import Path
import numpy as np
O=Path(__file__).resolve().parent;B=O.parent;R=B.parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--corrected',action='store_true');ap.add_argument('--reopened',action='store_true');args=ap.parse_args();direction=1 if args.corrected else -1
p=O/'mossglass_caps_v3-reopened.glb' if args.reopened else O/'mossglass_caps_v3.glb' if args.corrected else B/'mossglass.glb';raw=p.read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);blob=raw[28+size:]
def read(i):
 a=doc['accessors'][i];b=doc['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];return np.frombuffer(blob,dtype={5123:'<u2',5126:'<f4'}[a['componentType']],offset=b.get('byteOffset',0)+a.get('byteOffset',0),count=a['count']*n).reshape(-1,n).astype(float)
attrs=doc['meshes'][0]['primitives'][0]['attributes'];v=read(attrs['POSITION']);normals=read(attrs['NORMAL']);j=read(attrs['JOINTS_0']).astype(int);w=read(attrs['WEIGHTS_0']);tri=read(doc['meshes'][0]['primitives'][1]['indices']).astype(int).reshape(-1,3);ibm=read(doc['skins'][0]['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1);caps={'top':[i for i,t in enumerate(tri) if v[t,1].min()>2.039],'bottom':[i for i,t in enumerate(tri) if v[t,1].max()<.031]};assert all(len(x)==16 for x in caps.values());results=[]
for name,ids in caps.items():
 expected=np.array([0,1 if name=='top' else -1,0]);t=tri[ids];cross=np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]]);cos=cross@expected/np.linalg.norm(cross,axis=1);assert (cos*direction>0).all();results.append(dict(cap=name,primitive1TriangleIndices=ids,vertexIds=np.unique(t).tolist(),restOutwardCosineMin=float(cos.min()),restOutwardCosineMax=float(cos.max()),allInward=not args.corrected))
pose=[]
for c in ['b2ba62b0f66e4dcd8b875fe5eb8bc3cd','99de42ba1bf44ac18cfe1449e3e664b1','43b06eec57a840a4b4f301367fe83320']:
 cp=R/f'scratch/mirewarden-game/model-test-output/{c}.json';frames=json.loads(cp.read_text())['frames'];values=[]
 for f in frames:
  mats=np.array([b['localToWorld']for b in f['bones']]).reshape(-1,4,4)@ibm
  for name,ids in caps.items():
   ts=tri[ids];idsv=np.unique(ts);weights=w[idsv];assert np.allclose(weights,weights[0]);matrix=sum(mats[j[idsv[0],k]]*weights[0,k]for k in range(4));ps=(np.c_[v,np.ones(len(v))]@matrix.T)[:,:3];cross=np.cross(ps[ts[:,1]]-ps[ts[:,0]],ps[ts[:,2]]-ps[ts[:,0]]);expected=np.linalg.inv(matrix[:3,:3]).T@np.array([0,1 if name=='top'else -1,0]);co=cross@expected/(np.linalg.norm(cross,axis=1)*np.linalg.norm(expected));values.extend(co.tolist())
 assert min(x*direction for x in values)>0;pose.append(dict(captureSha256=hashlib.sha256(cp.read_bytes()).hexdigest(),capture=c,frames=len(frames),minimumOutwardCosine=min(values),maximumOutwardCosine=max(values),allCapsInward=not args.corrected))
r=dict(status='PASS_CAPS_OUTWARD_BIND_AND_ALL360_NATIVE_POSES' if args.corrected else 'CONFIRMED_EXPORTED_CAPS_INWARD_IN_BIND_AND_CAPTURED_POSES',glbSha256=hashlib.sha256(raw).hexdigest(),caps=results,nativePoses=pose,method='Independently decode GLB accessors. Compare cap triangle cross products with exterior +/-Y in rest; under recorded affine skin matrix compare with inverse-transpose exterior direction. All cap vertices share cap-local weights.',conclusion='Corrected top and bottom caps point outward in bind and all observed native poses.' if args.corrected else 'Top and bottom cap winding is inward. Native opaque backface culling is consistent with absent top surface/notch; Blender double-sided studio hid this error. Triangle/normal agreement was positive because both were inward.',limits=['Offline projection, not Unity rendered triangle-ID capture.','Does not explain extreme attack framing or invisible death body.'])
(O/('reopened-cap-audit.json' if args.reopened else 'corrected-cap-audit.json' if args.corrected else 'v1-v2-cap-diagnosis.json')).write_text(json.dumps(r,indent=2)+'\n');print(r['status'])
