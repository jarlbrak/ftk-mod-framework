"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['263632','415044','647361','8D9981','BDC3A2','323029','534335','796049','556538','819052','DAA252','151E1C']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'briarback_basecolor.png')
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
b=Surface(120991)
# Original massive trunk, with local spine blends and no borrowed vertex positions.
b.tube('Heavy bark torso',[(0,1.18,.09),(0,1.45,.09),(0,1.80,.12),(0,2.12,.12),(0,2.40,.10),(0,2.55,.10)],[.36,.43,.55,.64,.62,.39],[.30,.36,.41,.44,.40,.29],['Root_M','BackA_M','BackB_M','Chest_M','Chest_M','Neck_M'],[0,1,2,1,0,1,2,3,1,0,1,2],sides=12)
# Layered stone breast forms give a readable broad chest; each sits on local spine.
for sign in [-1,1]:
 b.ellipsoid('Stone pectoral '+str(sign),(sign*.28,2.13,.492),(.285,.26,.085),'Chest_M',[1,2,3,2,1,2,3,4,2,1])
 b.tube('Torso bark ridge '+str(sign),[(sign*.22,1.40,.397),(sign*.27,1.68,.483),(sign*.23,1.89,.50)],[.037,.044,.038],[.04,.047,.041],['BackA_M','BackB_M','BackB_M'],[5,6,7,6,5,6],sides=6)
# Compact head and forward muzzle follow the actual Head_M, not unused Hair_M.
b.ellipsoid('Blocky granite head',(0,2.80,.19),(.38,.43,.39),'Head_M',[1,2,3,2,1,2,3,4,2,1])
b.ellipsoid('Broad stone muzzle',(0,2.69,.542),(.27,.17,.155),'Head_M',[2,3,4,3,2,3,4,3,2,3])
b.tube('Dark mouth',[(-.19,2.62,.651),(0,2.60,.696),(.19,2.62,.651)],[.015]*3,[.024]*3,['Head_M']*3,11,axis=(0,0,1),sides=6)
b.ellipsoid('Flat bark nose',(0,2.81,.636),(.112,.071,.052),'Head_M',5)
for sign in [-1,1]:
 b.ellipsoid('Shadow eye '+str(sign),(sign*.156,2.91,.526),(.093,.054,.039),'Head_M',11)
 b.ellipsoid('Amber eye '+str(sign),(sign*.156,2.91,.565),(.048,.028,.012),'Head_M',10)
 b.tube('Heavy brow '+str(sign),[(sign*.035,3.008,.50),(sign*.29,2.998,.412)],[.056,.06],[.076,.083],['Head_M']*2,[1,2,3,2,1,2],axis=(0,0,1),sides=6)
 b.ellipsoid('Small ear '+str(sign),(sign*.36,2.88,.18),(.087,.125,.102),'Head_M',6)
# Moss cap is broad low-growing foliage, not horns/spikes.
for x,y,z,s in [(-.22,3.10,.17,.18),(0,3.18,.18,.22),(.22,3.10,.16,.18)]:
 b.ellipsoid('Moss head hummock '+str(x),(x,y,z),(s,.13,.23),'Head_M',[8,9,8,8,9,8,9,8])
for side,sign in [('R',1),('L',-1)]:
 S='Shoulder_'+side;E='Elbow_'+side;W='Wrist_'+side;H='Hip_'+side;K='Knee_'+side;A='Ankle_'+side;F='MiddleFinger1_'+side
 b.tube('Massive upper arm '+side,[b.B[S],(sign*.79,2.376,.037),b.B[E]],[.235,.23,.17],[.235,.24,.17],[S,S,{S:.1,E:.9}],[0,1,2,3,2,1,2,3],axis=(0,0,1))
 b.tube('Stone forearm '+side,[b.B[E],(sign*1.30,2.37,.032),b.B[W]],[.19,.25,.17],[.19,.24,.17],[E,E,W],[1,2,3,2,1,2,3,4],axis=(0,0,1))
 b.ellipsoid('Heavy knuckles '+side,(sign*1.78,2.348,.115),(.205,.172,.193),F,[1,2,3,2,1,2,3,4])
 b.tube('Curled broad fingers '+side,[(sign*1.82,2.35,.12),(sign*1.97,2.31,.18),(sign*2.045,2.22,.23)],[.14,.13,.105],[.145,.135,.105],[F,'MiddleFinger2_'+side,'MiddleFinger3_'+side],[1,2,3,2,1,2,3,4],axis=(0,0,1))
 b.tube('Hook thumb '+side,[b.B['ThumbFinger1_'+side],b.B['ThumbFinger2_'+side],b.B['ThumbFinger3_'+side]],[.084,.083,.063],[.084,.081,.06],['ThumbFinger1_'+side,'ThumbFinger2_'+side,'ThumbFinger3_'+side],[1,2,3,2,1,2],sides=6)
 # Asymmetric small moss mantle mounds stay with upper-arm motion.
 for k,(xx,yy,zz) in enumerate([(.57,2.59,.045),(.73,2.55,.02),(.88,2.52,.015)]):
  b.ellipsoid('Shoulder moss '+side+str(k),(sign*xx,yy,zz),(.16,.105,.22),S,[8,9,8,8,9,8,9,8])
 b.tube('Forearm bark stripe '+side,[(sign*1.12,2.43,.197),(sign*1.32,2.47,.229),(sign*1.51,2.41,.196)],[.033,.039,.031],[.035,.04,.035],[E,E,E],[5,6,7,6,5,6],axis=(0,0,1),sides=6)
 b.tube('Powerful legs '+side,[(sign*.321,1.46,.09),(sign*.321,1.15,.105),b.B[K],(sign*.321,.50,.06),b.B[A]],[.27,.25,.21,.22,.20],[.26,.25,.21,.21,.20],[H,H,{H:.12,K:.88},K,A],[0,1,2,1,0,1,2,3])
 b.ellipsoid('Stone knee '+side,(sign*.321,.78,.324),(.18,.17,.078),K,[1,2,3,4,3,2,3,2])
 b.tube('Broad rooted foot '+side,[(sign*.321,.15,.035),(sign*.321,.132,.29),(sign*.321,.115,.59)],[.24,.265,.22],[.13,.125,.105],[A,'MiddleToe1_'+side,'MiddleToe2_'+side],[5,6,7,6,5,6,7,6],axis=(1,0,0))
 b.tube('Shin root ridge '+side,[(sign*.30,.36,.248),(sign*.32,.53,.269),(sign*.31,.65,.28)],[.035,.04,.033],[.037,.043,.035],[A,K,K],[5,6,7,6,5,6],sides=6)
b.write('briarback')
manifest=dict(name='Briarback Guardian',native_chassis='yetiBoss',resource_prefab='enyeti',reference_renderer=120991,renderer_path='Yeti',native_surface_copied=False,art_status='Original forest stone guardian; studio/live review pending',native_forward='+UnityZ; native toe chain and weighted head forward envelope checked before authoring',rig_boundary='Resource-only old Yeti120991, not modern enYeti121381. Full37-joint palette retained, unusedHair_M unweighted. Original local spine and limb blends.',remaining=['Studio review','Exact resource override live assignment','Native attacks/hit/ragdoll/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
