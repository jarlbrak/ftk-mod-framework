"""Original parametric Tamarind Trickster surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['50352C','75503A','926C49','B18B5A','D5BA83','E7D1A0','392927','AD7439','C69B54','6B4533','241D1A','151516']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'tamarind_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/skeleton-audit/121301/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121301)
ns=['Root_M','BackA_M','BackB_M','Chest_M','Neck_M']
b.tube('Continuous brown torso',[b.B[n]for n in ns],[.20,.22,.23,.235,.145],[.16,.185,.18,.17,.12],ns,[0,1,2,1,0,1,4,4,4,1],sides=10)
b.ellipsoid('Rounded monkey skull',[0,1.215,.09],[.215,.185,.18],'Head_M',1)
b.ellipsoid('Cream upper muzzle',[0,1.15,.245],[.147,.10,.122],'Head_M',4)
b.tube('Articulated lower jaw',[b.B['Jaw'],b.B['joint2']],[.13,.095],[.07,.055],['Jaw','joint2'],3,axis=(1,0,0),sides=8)
b.ellipsoid('Dark nose',[0,1.17,.351],[.078,.039,.033],'Head_M',10)
for sg in [-1,1]:
 b.ellipsoid('Dark seated eye '+str(sg),[sg*.126,1.269,.236],[.044,.046,.026],'Head_M',11)
 b.ellipsoid('Small eye glint '+str(sg),[sg*.119,1.283,.259],[.010,.012,.005],'Head_M',5)
 b.ellipsoid('Rounded ear '+str(sg),[sg*.208,1.223,.065],[.061,.083,.047],'Head_M',2)
 b.ellipsoid('Ear inset '+str(sg),[sg*.219,1.228,.101],[.036,.049,.014],'Head_M',3)
 b.tube('Swept brow '+str(sg),[[sg*.035,1.318,.204],[sg*.11,1.325,.229],[sg*.179,1.285,.185]],[.025,.027,.018],[.027,.027,.018],['Head_M']*3,0,sides=6)
ns=['Tail1','Tail2','Tail3','Tail4','joint7'];pts=[b.B[n].copy()for n in ns];pts[-1][2]+=.03
b.tube('Articulated tapered tail',pts,[.083,.071,.055,.037,.022],[.083,.071,.055,.037,.022],ns,[0,1,2,1,0,1,2,1],sides=8)
for side in ['R','L']:
 sg=1 if side=='R'else -1
 ns=['Chest_M','Scapula_'+side,'Shoulder_'+side];b.tube('Soft shoulder '+side,[b.B[ns[0]]+[sg*.12,.11,0],b.B[ns[1]],b.B[ns[2]]],[.145,.132,.115],[.13,.12,.11],ns,1,axis=(0,1,0))
 ns=['Shoulder_'+side,'Elbow_'+side,'Wrist_'+side];b.tube('Long forelimb '+side,[b.B[n]for n in ns],[.118,.094,.06],[.11,.09,.057],ns,[0,1,2,1,0,1,2,1],axis=(0,1,0))
 ns=['Wrist_'+side,'MiddleFinger1_'+side,'MiddleFinger2_'+side];pts=[b.B[n]for n in ns]+[b.B['MiddleFinger3_'+side]*.995];b.tube('Broad hand fingers '+side,pts,[.062,.075,.050,.020],[.06,.055,.035,.018],ns+[ns[-1]],6,axis=(0,1,0))
 ns=['Wrist_'+side,'ThumbFinger1_'+side];b.tube('Articulated thumb '+side,[b.B[n]for n in ns]+[b.B['ThumbFinger2_'+side]],[.045,.034,.015],[.04,.032,.013],ns+[ns[-1]],6,axis=(0,1,0),sides=6)
 ns=['Hip_'+side,'Knee_'+side,'Ankle_'+side];b.tube('Haunch and shin '+side,[b.B[n]for n in ns],[.10,.08,.055],[.12,.078,.052],ns,1)
 ns=['Ankle_'+side,'MiddleToe1_'+side];pts=[b.B[n].copy()for n in ns]+[b.B['MiddleToe2_'+side].copy()];pts[1][1]=.045;pts[2][1]=.037;b.tube('Long gripping foot '+side,pts,[.055,.079,.055],[.052,.04,.032],ns+[ns[-1]],6)
b.write('tamarind')
