"""Original parametric Cinderbloom surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['202B2A','35433C','53604B','91503D','BA6845','D99159','F1BD71','30232B','642C29','E88B37','FFE0A0','89916B']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'cinderbloom_basecolor.png')
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
body=Surface(120953);B=body.B
chain=['Bone%03d'%i for i in range(2,15)]+['Head'];points=[];skins=[]
for a,b in zip(chain[:-1],chain[1:]):
 for t in [0,.5]:points.append(B[a]*(1-t)+B[b]*t);skins.append({a:1-t,b:t} if t else a)
points.append(B['Head']);skins.append('Head');widths=np.linspace(.125,.077,len(points));body.tube('Continuous curved bark stem',points,widths,widths*.83,skins,[0,1,1,2,1,0,0,1],sides=10)
# Root rosette stays on the body renderer, whose native envelope includes the ground.
for a,b in [('Bone035','Bone036'),('Bone035(mirrored)','Bone036(mirrored)'),('Bone037','Bone038'),('Bone037(mirrored)','Bone038(mirrored)')]:
 p=B[a];q=B[b];tip=q+(q-p)*.35;tip[1]=.028
 body.tube('Broad root leaf '+b,[p,(p+q)/2+np.array([0,.07,0]),q,tip],[.06,.18,.13,.025],[.028,.047,.035,.012],[a,{a:.5,b:.5},b,b],[1,2,11,2,1,0,0,1],axis=(0,0,1))
body.ellipsoid('Root bulb',(0,.075,.055),(.16,.14,.15),'Root_M',[0,1,2,1,0,0,1,1])
# Two broad mantles follow the real petal chains with authored interpolation.
for side,suffix in [(1,''),(-1,'(mirrored)')]:
 bones=['Bone%03d'%i+suffix for i in range(18,24)];ps=[B[n].copy() for n in bones]
 for k,p in enumerate(ps):p[0]+=side*.015;p[1]-=.018
 body.tube('Copper articulated mantle '+str(side),ps,[.11,.19,.23,.23,.19,.055],[.04,.062,.065,.06,.046,.02],bones,[4,5,6,5,4,3,3,4],axis=(1,0,0),sides=8)
 # A gold vein lays close to the top face and follows the same actual bones.
 vein=[p+np.array([0,.053,0]) for p in ps]
 body.tube('Petal gold vein '+str(side),vein,[.018]*5+[.009],[.013]*6,bones,6,sides=6)
# Face version2: native plant mouth is +UnityZ. Only crown/face/jaw surfaces
# are rebuilt toward that side; fitted paired petal chains and all other parts stay unchanged.
body.tube('Broad crown petal',[(0,1.20,-.15),(0,1.35,-.08),(0,1.43,.14),(0,1.39,.37),(0,1.32,.52)],[.13,.21,.23,.19,.05],[.04,.04,.035,.03,.015],['Head']*5,[3,4,5,6,5,4,3,3],axis=(1,0,0))
body.tube('Lower jaw cup',[(0,1.075,.07),(0,.965,.17),(0,.975,.36),(0,1.04,.55)],[.16,.30,.32,.18],[.05,.065,.05,.025],['Jaw']*4,[3,4,5,5,4,3,3,4],axis=(1,0,0))
body.ellipsoid('Dark lantern throat',(0,1.17,.22),(.265,.17,.21),'Head',7)
body.ellipsoid('Ember throat center',(0,1.17,.424),(.095,.10,.025),'Head',9)
body.ellipsoid('Hot throat heart',(0,1.19,.449),(.033,.048,.009),'Head',10)
# Short inset dentition is subordinate to the broad petals.
for x in [-.22,-.11,0,.11,.22]:
 body.tube('Upper ivory tooth',[(x,1.30,.465),(x*.97,1.205,.49)],[.027,.007],[.027,.006],['Head']*2,10,sides=5)
for x in [-.18,-.06,.06,.18]:
 body.tube('Lower ivory tooth',[(x,1.035,.475),(x*.95,1.098,.49)],[.022,.006],[.022,.006],['Jaw']*2,10,sides=5)
# Original leaves fit the second renderer's separate native mid-stem envelope.
leaves=Surface(121072);L=leaves.B
for n,sign in [('Leaf5',1),('Leaf6',-1)]:
 base=L[n];tip=np.array([.225 if sign>0 else -.275,.68 if sign>0 else .44,.005 if sign>0 else .245]);mid=(base+tip)/2+np.array([0,.035,0])
 leaves.tube('Broad stem leaf '+n,[base,mid,tip],[.035,.108,.016],[.018,.035,.01],[n]*3,[1,2,11,2,1,0,0,1],axis=(0,0,1))
 # Copper central venation, not a separate decorative spike.
 leaves.tube('Leaf vein '+n,[base+np.array([0,.027,0]),mid+np.array([0,.027,0]),tip],[.013,.012,.004],[.01,.01,.004],[n]*3,5,sides=6)
body.write('cinderbloom_body');leaves.write('cinderbloom_leaves')
report={'name':'Cinderbloom','art_status':'Original surface model; studio and live review pending','native_chassis':'plantA','assignments':[{'renderer_path':'enPlant01','reference_renderer':120953,'glb':'cinderbloom_body.glb','texture':'cinderbloom_basecolor.png'},{'renderer_path':'enPlant01Leaves','reference_renderer':121072,'glb':'cinderbloom_leaves.glb','texture':'cinderbloom_basecolor.png'}],'native_surface_copied':False,'weight_rationale':'Stem rings interpolate adjacent native chain joints; paired mantle rings follow corresponding petal-chain joints; head details and jaw cup rigid to native Head/Jaw; root and separate stem leaves use their actual native root/leaf bones. No nearest-native-surface transfer.','remaining':['Studio appearance review','Live idle/attack/hit/death and all requested renderer identities','Full native animation envelope/culling review']}
previous=json.loads((OUT/'manifest.json').read_text()) if (OUT/'manifest.json').exists() else {}
report.update({k:v for k,v in previous.items() if k.startswith('live_')})
report['art_version']=2
report['orientation']='Face and jaw point +UnityZ; v1 erroneously faced -UnityZ. Full rig and rear petal-chain fitting unchanged.'
(OUT/'manifest.json').write_text(json.dumps(report,indent=2)+'\n')
