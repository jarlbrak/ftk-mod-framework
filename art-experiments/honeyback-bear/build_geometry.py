"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['292624','3E332C','594334','79573C','AA7C48','CAA267','D6BD90','E7D7B4','181D1D','654537','B2834C','E1B363']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'honeyback_basecolor.png')
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
b=Surface(121467)
# Original continuous torso loft follows successive native spine segments.
b.tube('Heavy dark-fur trunk',[(0,1.61,-.66),(0,1.62,-.36),(0,1.64,.16),(0,1.66,.72),(0,1.65,1.22),(0,1.62,1.54)],[.34,.59,.65,.66,.55,.35],[.36,.58,.69,.69,.62,.39],['Root_M','Root_M','BackA_M','BackB_M','Chest_M','Neck_M'],[0,1,2,1,0,1,2,3,1,0,1,2],axis=(1,0,0),sides=12)
b.tube('Thick neck',[(0,1.63,1.30),(0,1.59,1.60),(0,1.52,1.94)],[.42,.43,.36],[.42,.44,.36],['Chest_M','Neck_M','Head_M'],[1,2,3,2,1,2,3,2],axis=(1,0,0),sides=10)
# Golden shoulder marking colors the existing original trunk faces, avoiding a floating overlay.
for tri in b.data['triangles']:
 center=np.mean([b.data['positions'][i] for i in tri],axis=0)
 if center[1]>2.06 and .25<center[2]<1.26:
  color=5 if center[1]>2.24 else 4
  for i in tri:b.data['uvs'][i]=[(color+.5)/len(PALETTE),.5]
b.ellipsoid('Broad bear skull',(0,1.55,2.06),(.465,.44,.47),'Head_M',[1,2,3,2,1,2,3,4,2,1])
b.tube('Cream upper muzzle',[(0,1.49,2.27),(0,1.39,2.57),(0,1.34,2.76)],[.30,.285,.235],[.22,.19,.13],['Head_M']*3,[5,6,7,6,5,6,7,6],axis=(1,0,0))
b.ellipsoid('Black nose',(0,1.385,2.787),(.21,.11,.07),'Head_M',8)
# Real jaw bone carries a separate lower jaw; no invented link to JawEnd.
b.tube('Articulated lower muzzle',[(0,1.265,2.17),(0,1.16,2.43),(0,1.14,2.70)],[.25,.23,.17],[.12,.115,.085],['Jaw_M']*3,[2,3,5,6,5,3,2,3],axis=(1,0,0))
for sign in [-1,1]:
 b.ellipsoid('Eye shadow '+str(sign),(sign*.281,1.697,2.396),(.09,.073,.046),'Head_M',8)
 b.ellipsoid('Amber eye '+str(sign),(sign*.282,1.704,2.44),(.036,.032,.014),'Head_M',11)
 b.tube('Brown brow '+str(sign),[(sign*.19,1.80,2.37),(sign*.36,1.773,2.29)],[.033,.044],[.05,.06],['Head_M']*2,[1,2,3,2,1,2],axis=(0,0,1),sides=6)
 side='R' if sign>0 else 'L';ear='Ear_'+side
 b.ellipsoid('Round ear '+side,(sign*.447,1.826,2.264),(.14,.18,.102),ear,[0,1,2,3,2,1,2,1])
 b.ellipsoid('Ear inner '+side,(sign*.45,1.848,2.351),(.081,.107,.021),ear,4)
 b.ellipsoid('Russet cheek '+side,(sign*.363,1.426,2.199),(.145,.21,.186),'Head_M',[2,3,4,3,2,3,4,3])
 for front in ['front','back']:
  H=front+'Hip_'+side;K=front+'Knee_'+side;A=front+'Ankle_'+side;B=front+'Ball_'+side;T=front+'Toe_'+side
  points=[b.B[H],b.B[K],b.B[A],b.B[B]]
  b.tube('Powerful '+front+' leg '+side,points,[.255,.22,.175,.13],[.27,.22,.17,.12],[H,{H:.12,K:.88},A,B],[0,1,2,1,0,1,2,3])
  # Broad paws stay low with a small original toe extension, not floating joint markers.
  c=b.B[B].copy();z=c[2];c[1]=.145
  b.tube('Broad '+front+' paw '+side,[(c[0],.155,z-.12),(c[0],.139,z+.02),(c[0],.125,z+.20)],[.235,.25,.22],[.12,.115,.095],[A,B,T],[0,1,2,3,2,1,2,3],axis=(1,0,0))
  for k in [-1,0,1]:
   b.tube('Pale '+front+' claw '+side+str(k),[(c[0]+k*.135,.116,z+.15),(c[0]+k*.135,.088,z+.29)],[.032,.009],[.028,.008],[T]*2,[6,7,6,5,6,7],axis=(1,0,0),sides=6)
 # Rounded hip/shoulder masses join broad torso to legs and retain organic movement.
 for name,bone,center,size in [('haunch','backHip_'+side,(sign*.47,1.51,-.09),(.29,.52,.40)),('shoulder','frontHip_'+side,(sign*.43,1.58,1.23),(.30,.43,.33))]:
  b.ellipsoid(name+side,center,size,bone,[0,1,2,3,2,1,2,3])
b.ellipsoid('Short bear tail',(0,1.59,-.759),(.145,.143,.123),'Tail0_M',[0,1,2,1,0,1,2,1])
b.write('honeyback')
manifest=dict(name='Honeyback Bear',native_chassis='bearB',reference_renderer=121467,renderer_path='enBear01',native_surface_copied=False,art_status='Original quadruped bear; studio/live review pending',native_forward='+UnityZ from head/jaw/toe positions; tail at rear -Z',rig_boundary='Exact38-joint palette and inverse binds; original spine/limb blends and independently articulated jaw',live_baseline='No indexed bear live baseline located at authoring; native diagnostic pending separately',remaining=['Studio review','Native bearB baseline','Exact authored live assignment','Native attacks/hit/death/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
