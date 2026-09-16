"""Original parametric Reefstrider surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['235E62','347B78','5C9990','87B4A0','D2D5AB','F1E4BB','28465A','BC6855','DF9271','9BBAAF','163033','161C24']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'reefstrider_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/fish-a01-native-topology-analysis/reference-121695/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121695)
ns=['Root_M','BackA_M','BackB_M','Chest_M','Neck_M'];pts=[b.B[n]for n in ns]
b.tube('Continuous sea-green torso',pts,[.23,.27,.28,.29,.19],[.22,.24,.24,.23,.18],ns,[0,1,2,1,0,1,2,1],sides=10)
b.ellipsoid('Cream rounded belly',[0,1.39,.285],[.205,.31,.072],{'BackA_M':.3,'BackB_M':.4,'Chest_M':.3},4)
b.tube('Broad fish neck',[b.B['Chest_M'],b.B['Neck_M'],b.B['Head_M']],[.19,.19,.22],[.17,.19,.20],['Chest_M','Neck_M','Head_M'],1,sides=10)
b.ellipsoid('Rounded fish head',[0,2.10,.28],[.34,.275,.33],'Head_M',[0,1,2,1,0,1,2,1,0,1])
b.ellipsoid('Cream lower fish muzzle',[0,1.965,.475],[.225,.11,.195],'Nose_M',4)
b.tube('Coral lower mouth rim',[[-.20,1.99,.596],[0,1.97,.677],[.20,1.99,.596]],[.027,.036,.027],[.027,.03,.027],['Nose_M']*3,7,axis=(0,1,0),sides=6)
b.ellipsoid('Dark mouth inset',[0,2.01,.605],[.158,.030,.022],{'Head_M':.5,'Nose_M':.5},11)
for sign in [-1,1]:
 b.ellipsoid('Lateral dark eye '+str(sign),[sign*.222,2.165,.510],[.065,.072,.040],'Head_M',11)
 b.ellipsoid('Eye glint '+str(sign),[sign*.210,2.187,.545],[.016,.018,.008],'Head_M',5)
 b.tube('Deep-blue gill cheek '+str(sign),[[sign*.25,2.20,.18],[sign*.32,2.10,.18],[sign*.265,2.015,.18]],[.045,.05,.03],[.024,.025,.015],['Head_M']*3,6,axis=(0,0,1),sides=6)
# Short broad crown fin uses actual Hair_M, with a blended base rather than tall spikes.
b.tube('Coral crown fin',[[0,2.29,.19],[0,2.35,.37],[0,2.405,.60]],[.035,.045,.008],[.12,.10,.018],[{'Head_M':.7,'Hair_M':.3},'Hair_M','Hair_M'],7,axis=(1,0,0),sides=6)
for side in ['R','L']:
 sign=1 if side=='R'else -1
 b.tube('Blended shoulder '+side,[b.B['Chest_M']+[sign*.13,.05,0],b.B['Scapula_'+side],b.B['Shoulder_'+side]],[.15,.145,.14],[.15,.13,.12],['Chest_M','Scapula_'+side,'Shoulder_'+side],1,axis=(0,1,0),sides=8)
 ns=['Shoulder_'+side,'Elbow_'+side,'Wrist_'+side];b.tube('Pectoral arm '+side,[b.B[n]for n in ns],[.14,.11,.085],[.12,.10,.075],ns,[0,1,2,1,0,1,2,1],axis=(0,1,0),sides=8)
 ns=['Wrist_'+side]+['MiddleFinger'+str(i)+'_'+side for i in [1,2,3]];b.tube('Broad articulated flipper hand '+side,[b.B[n]for n in ns],[.08,.19,.16,.025],[.07,.05,.035,.014],ns,[1,2,7,2,1,2,7,2],axis=(0,1,0),sides=8)
 ns=['Hip_'+side,'Knee_'+side,'Ankle_'+side];b.tube('Sea-green leg '+side,[b.B[n]for n in ns],[.15,.11,.09],[.16,.115,.09],ns,[0,1,2,1,0,1,2,1],sides=8)
 ns=['Ankle_'+side,'MiddleToe1_'+side,'MiddleToe2_'+side];pts=[b.B[n].copy()for n in ns];pts[1][1]=.055;pts[2][1]=.045;b.tube('Wide webbed foot '+side,pts,[.095,.17,.19],[.07,.045,.028],ns,[0,1,2,7,2,1],axis=(1,0,0),sides=6)
b.write('reefstrider')
