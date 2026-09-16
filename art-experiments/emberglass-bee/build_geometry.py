"""Original parametric Emberglass Bee surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['211E1A','483324','795132','B77A2D','D79D3E','E7BD65','282C2A','6F827D','E6E7D4','B6CFD1','171A1B','F1EBD6']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'emberglass_basecolor.png')
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
b=Surface(121062)
# Mesh local up is +Z and anatomical front -Y for this exact source bind frame.
b.ellipsoid('Dark brown thorax',(0,-.135,.431),(.119,.125,.128),'Root_M',[0,1,2,1,0,1,2,1])
b.ellipsoid('Small insect head',(0,-.331,.39),(.118,.088,.101),'RigHead',[0,1,2,1,0,1,2,1])
b.tube('Blended neck attachment',[(0,-.20,.427),(0,-.255,.405),(0,-.30,.39)],[.086,.078,.084],[.085,.075,.082],['Root_M',{'Root_M':.5,'RigHead':.5},'RigHead'],1)
# Continuous segmented abdomen. Alternating ring bands are authored face colors.
tail=['RigTail1','RigTail2','RigTail3','RigTail4'];cent=[b.B[n]for n in tail];points=[];skins=[];widths=[];depths=[]
for i in range(3):
 for t in [0,.35,.65]:
  points.append(cent[i]*(1-t)+cent[i+1]*t);skins.append({tail[i]:1-t,tail[i+1]:t} if t else tail[i]);widths.append([.137,.146,.092][i]*(1-.15*t));depths.append([.115,.121,.08][i]*(1-.15*t))
points += [cent[3],cent[3]+[0,-.05,-.035]];skins+=['RigTail4','RigTail4'];widths += [.039,.008];depths += [.036,.008]
start=len(b.data['positions']);b.tube('Amber segmented abdomen',points,widths,depths,skins,3,sides=12)
for i in range(len(points)-1):
 color=0 if i%3==2 else (4 if i%3==1 else 3)
 for v in range(start+i*72,start+(i+1)*72):b.data['uvs'][v]=[(color+.5)/12,.5]
for side,sign in [('R',1),('L',-1)]:
 for limb in ['Front','Mid','Back']:
  names=['Rig'+side+limb+'Arm'+str(i)for i in [1,2,3]];a,c,d=[b.B[n]for n in names];end=d+[sign*.024,.025,-.11]
  b.tube(side+' '+limb+' jointed leg',[a,(a+c)/2,c,(c+d)/2,d,end],[.019,.018,.016,.015,.014,.006],[.018,.017,.015,.014,.013,.006],[names[0],{names[0]:.5,names[1]:.5},names[1],{names[1]:.5,names[2]:.5},names[2],names[2]],[0,1,2,1,0,1],sides=6)
 b.ellipsoid('Black compound eye '+side,(sign*.102,-.365,.411),(.043,.030,.055),'RigHead',[10,6,10,0,10,6,10,0])
 mouth='Rig'+side+'Mouth';a=b.B[mouth];b.tube(side+' small mandible',[a,(sign*.065,-.428,.302),(sign*.02,-.457,.315)],[.015,.013,.003],[.014,.012,.003],[mouth]*3,[0,1,2,1,0,1],sides=6)
 feelers=['Rig'+side+'Feeler'+str(i)for i in [1,2,3,4]];pts=[b.B[n]for n in feelers]+[b.B[feelers[-1]]+[0,-.09,-.045]]
 b.tube(side+' graceful antenna',pts,[.009,.01,.009,.008,.003],[.009,.01,.009,.008,.003],feelers+[feelers[-1]],[0,1,2,1,0,1],sides=6)
 wing=['Rig'+side+'wing'+str(i)for i in [1,2,3,4,5]];c=[b.B[n]for n in wing];tip=c[-1]+[sign*.10,.040,.025];pts=c+[tip];width=[.018,.052,.078,.09,.07,.005]
 b.ellipsoid(side+' brown wing socket',c[0] + [0,0,-.018],(.041,.046,.051),{'Root_M':.5,wing[0]:.5},[0,1,2,1,0,1])
 b.tube(side+' opaque pale wing',pts,width,[.004,.005,.005,.005,.004,.002],wing+[wing[-1]],[8,9,8,9],axis=(0,1,0),sides=4)
 b.tube(side+' wing leading vein',[x+[0,0,.008]for x in pts],[.006,.005,.004,.004,.003,.002],[.004]*6,wing+[wing[-1]],7,axis=(0,1,0),sides=4)
 for k in [1,2,3,4]:
  for edge in [-1,1]:
   a=c[k]+[0,0,.009];end=c[k]+[0,width[k]*edge*.85,.009];b.tube(side+' wing cross vein '+str(k)+' '+str(edge),[a,end],[.003,.002],[.002,.002],[wing[k]]*2,7,axis=(1,0,0),sides=4)
b.write('emberglass')
manifest=dict(name='Emberglass Bee',native_chassis='beeA',reference_renderer=121062,renderer_path='Monster Bee',native_surface_copied=False,art_status='Original insect surface; studio and live review pending',native_forward='Exact bind frame: anatomical front -meshY, up +meshZ; renderer/native skeleton transforms establish game orientation',rig_boundary='Exact47-joint palette/IBMs. Native zero-weight RigNeck and FeelerMain joints retained. Six legs, two wings, antennae and mandibles keep native chains; not transferable to mosquito bind profile.',remaining=['Studio review','Native captured pose fitting','Original live body/portrait/material/ragdoll/cleanup acceptance'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
