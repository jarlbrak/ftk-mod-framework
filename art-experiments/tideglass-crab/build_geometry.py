"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['163A42','25616A','398B92','75B5B1','C8D8BF','E9E2C6','815743','B27C55','DFAC78','14262A','E9C56E','F5E3A2']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'tideglass_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/f'scratch/skeleton-audit/{rid}/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
 def weight(self,b):return {b:1.} if isinstance(b,str) else b
 def triangle(self,points,skin,color):
  v=np.array(points);cross=np.cross(v[1]-v[0],v[2]-v[0]);length=np.linalg.norm(cross)
  if length<1e-11:raise ValueError('Degenerate original face')
  offset=len(self.data['positions']);self.data['triangles'].append([offset,offset+1,offset+2])
  for p,w in zip(v,skin):
   w=self.weight(w);j=[self.names.index(n) for n in w];weights=list(w.values());total=sum(weights)
   self.data['positions'].append(p.tolist());self.data['normals'].append((cross/length).tolist());self.data['uvs'].append([(color+.5)/len(PALETTE),.5]);self.data['joints'].append(j+[0]*(4-len(j)));self.data['weights'].append([x/total for x in weights]+[0.]*(4-len(j)))
 def tube(self,label,points,widths,depths,skin,color,axis=(1,0,0),sides=8):
  start=len(self.data['positions']);points=np.array(points);rings=[]
  for i,p in enumerate(points):
   tangent=points[min(i+1,len(points)-1)]-points[max(0,i-1)];tangent/=np.linalg.norm(tangent);u=np.array(axis,dtype=float);u-=tangent*np.dot(u,tangent)
   if np.linalg.norm(u)<.01:u=np.cross(tangent,[0,0,1])
   u/=np.linalg.norm(u);v=np.cross(tangent,u)
   rings.append([p+u*widths[i]*math.cos(k*2*math.pi/sides)+v*depths[i]*math.sin(k*2*math.pi/sides) for k in range(sides)])
  for i in range(len(points)-1):
   for k in range(sides):
    n=(k+1)%sides;c=color[k%len(color)] if isinstance(color,list) else color
    self.triangle([rings[i][k],rings[i][n],rings[i+1][n]],[skin[i],skin[i],skin[i+1]],c)
    self.triangle([rings[i][k],rings[i+1][n],rings[i+1][k]],[skin[i],skin[i+1],skin[i+1]],c)
  for k in range(sides):
   n=(k+1)%sides;self.triangle([points[0],rings[0][n],rings[0][k]],[skin[0]]*3,color[0] if isinstance(color,list) else color);self.triangle([points[-1],rings[-1][k],rings[-1][n]],[skin[-1]]*3,color[0] if isinstance(color,list) else color)
  self.pieces.append(dict(name=label,vertex_start=start,vertex_count=len(self.data['positions'])-start))
 def ellipsoid(self,label,center,size,bone,color):
  center=np.array(center);# Ringed original low-poly volume, flat triangulated surface.
  points=[center+np.array([0,t*size[1],0]) for t in [-.98,-.7,0,.7,.98]];r=[.2,.71,1,.71,.2];self.tube(label,points,[x*size[0] for x in r],[x*size[2] for x in r],[bone]*5,color,sides=10)
 def write(self,name):
  (OUT/f'{name}.source.json').write_text(json.dumps(self.data,separators=(',',':'))+'\n');write_glb(OUT/f'{name}.glb',self.data,self.ref);v=validate(OUT/f'{name}.glb',self.ref);(OUT/f'{name}.validation.json').write_text(json.dumps(v,indent=2)+'\n');(OUT/f'{name}.pieces.json').write_text(json.dumps(self.pieces,indent=2)+'\n');return v
b=Surface(121411)
# Original broad carapace, independent of the native wizard surface.
b.tube('Broad tideglass carapace',[(0,.65,-.75),(0,.66,-.49),(0,.68,-.19),(0,.67,.13),(0,.65,.34)],[.16,.48,.57,.45,.18],[.10,.22,.29,.23,.12],['Root_M','Root_M','BackA_M','Chest_M','Head_M'],[2,3,2,1,2,3,4,5,4,3,2,3],axis=(1,0,0),sides=12)
# Original shell pattern colors the same surface, avoiding floating armor slabs.
for tri in b.data['triangles']:
 c=np.mean([b.data['positions'][i] for i in tri],axis=0)
 if c[1]>.83 and abs(c[0])<.24:
  for i in tri:b.data['uvs'][i]=[(3+.5)/len(PALETTE),.5]
b.ellipsoid('Ivory front mouthplate',(0,.63,.312),(.205,.112,.115),'Head_M',[4,5,4,3,4,5,4,5])
b.tube('Mouth slit',[(-.105,.627,.414),(0,.613,.436),(.105,.627,.414)],[.006]*3,[.011]*3,['Head_M']*3,9,axis=(0,0,1),sides=6)
for side,sign in [('R',1),('L',-1)]:
 for label in ['FrontLeg','MiddleLeg','BackLeg']:
  names=[f'{label}{i}_{side}' for i in range(1,6)];points=[b.B[n].copy() for n in names];points[-1]=points[-2]+.93*(points[-1]-points[-2]);points[-1][1]=max(points[-1][1],.025)
  b.tube('Walking leg '+label+side,points,[.055,.07,.074,.03,.007],[.05,.065,.066,.028,.007],[names[0],names[1],names[2],names[3],names[3]],[0,1,2,3,2,1],axis=(0,0,1),sides=6)
 # Native antenna chain becomes stout original eyestalk, avoiding unused terminal spans.
 names=[f'Antenna{i}_{side}' for i in range(1,5)];points=[b.B[n] for n in names]
 b.tube('Eyestalk '+side,points,[.036,.032,.027,.024],[.032,.031,.025,.024],[names[0],names[1],names[2],names[3]],[0,1,2,3,2,1],sides=6)
 c=b.B[names[-1]]
 b.ellipsoid('Dark eye housing '+side,c,(.06,.065,.06),names[-1],9)
 b.ellipsoid('Golden stalk eye '+side,c+np.array([0,.009,.050]),(.043,.046,.021),names[-1],10)
 # Claw palms follow actual claw chains, pincers remain independently articulated.
 C1=side+'Claw1';C2=side+'Claw2';C3=side+'Claw3';bottom=side+'PIncerBottom';top=side+'PIncerTop'
 b.tube('Claw arm '+side,[b.B[C1],b.B[C2],b.B[C3]],[.105,.13,.16],[.10,.13,.15],[C1,C2,C3],[0,1,2,3,2,1,2,3],axis=(1,0,0))
 b.tube('Massive claw palm '+side,[(sign*.34,.625,.91),(sign*.34,.651,1.11),(sign*.34,.66,1.27)],[.16,.205,.18],[.18,.215,.17],[C3]*3,[1,2,3,4,3,2,1,2],axis=(1,0,0))
 # Purposeful crescent shapes are original, anchored rigidly to each pincer joint.
 b.tube('Lower ivory pincer '+side,[(sign*.34,.54,1.20),(sign*.34,.45,1.44),(sign*.34,.50,1.72),(sign*.34,.65,1.86)],[.16,.139,.079,.019],[.095,.095,.062,.018],[bottom]*4,[6,7,8,5,4,5,8,7],axis=(1,0,0))
 b.tube('Upper ivory pincer '+side,[(sign*.34,.79,1.20),(sign*.34,.89,1.43),(sign*.34,.84,1.68),(sign*.34,.69,1.82)],[.15,.132,.073,.018],[.09,.088,.058,.018],[top]*4,[6,7,8,5,4,5,8,7],axis=(1,0,0))
 # A few crushing teeth belong wholly to their pincer, never span the opening.
 for i,z in enumerate([1.42,1.57]):
  b.tube('Lower crushing tooth '+side+str(i),[(sign*.34,.51,z),(sign*.34,.58,z+.025)],[.039,.014],[.041,.014],[bottom]*2,[4,5,4,5,4,5],sides=6)
  b.tube('Upper crushing tooth '+side+str(i),[(sign*.34,.84,z),(sign*.34,.77,z+.018)],[.035,.013],[.037,.013],[top]*2,[4,5,4,5,4,5],sides=6)
# Low original rear shell ridge follows the actual proximal tail joints only.
b.tube('Rear tidal ridge',[(0,.78,-.58),(0,.91,-.64),(0,1.055,-.70)],[.17,.125,.05],[.045,.044,.025],['Tail1_M','Tail1_M','Tail2_M'],[1,2,3,4,3,2],axis=(1,0,0),sides=6)
b.write('tideglass')
manifest=dict(name='Tideglass Crab',native_chassis='crabB',reference_renderer=121411,renderer_path='enCrabWizard',native_surface_copied=False,art_status='Original tidal crab; studio/live review pending',native_forward='+UnityZ from claw/pincer chains; tail extends rear -Z',rig_boundary='Exact63-joint palette and inverse binds. Six walking-leg chains, two articulated claw arms, independent upper/lower pincer surfaces; no bridge across claw opening.',live_baseline='No indexed crab live baseline located when authoring; native diagnostic pending separately',remaining=['Studio review','Native crabB baseline','Exact authored live assignment','Native attacks/hit/death/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
