"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['182832','293C47','415765','69828B','754A37','AD7450','D8A577','E5D0A6','E8B656','F8DDA3','101B23','B9C6BE']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'copperveil_basecolor.png')
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
b=Surface(121386)
# Original navicular thorax and broad abdomen, +Z front from native fangs/head.
b.tube('Dark thorax',[(0,.664,-.27),(0,.674,-.12),(0,.69,.08),(0,.68,.25)],[.18,.25,.27,.18],[.15,.20,.21,.15],['Root_M','BackA_M','BackB_M','Chest_M'],[0,1,2,1,0,1,2,3],axis=(1,0,0),sides=10)
b.tube('Bulbous midnight abdomen',[(0,.68,-.28),(0,.78,-.48),(0,.79,-.79),(0,.69,-1.08),(0,.57,-1.27)],[.14,.36,.46,.34,.075],[.12,.30,.39,.29,.07],['Tail1_M','Tail1_M','Tail2_M','Tail3_M','Tail3_M'],[0,1,2,1,0,1,2,3,1,0,1,2],axis=(1,0,0),sides=12)
# Paired copper chevrons lie on the original abdomen, following its local tail joints.
for k,(z,y,w,j) in enumerate([(-.49,1.04,.25,'Tail1_M'),(-.75,1.163,.31,'Tail2_M'),(-.99,.976,.23,'Tail3_M')]):
 for sign in [-1,1]:
  b.tube('Abdomen copper chevron %s %s'%(k,sign),[(sign*w,y-.11,z-.045),(sign*.025,y,z+.015)],[.028,.03],[.018,.018],[j]*2,[4,5,6,5,4,5],axis=(0,1,0),sides=6)
# Head volume is separate readable plate with clustered eyes and ivory mandibles.
b.ellipsoid('Shield head',(0,.66,.29),(.232,.18,.18),'Head_M',[0,1,2,3,2,1,2,3,1,0])
for sign in [-1,1]:
 for k,(x,y,z,s) in enumerate([(.071,.724,.435,.041),(.151,.736,.397,.029),(.193,.683,.366,.024)]):
  b.ellipsoid('Eye socket %s %s'%(sign,k),(sign*x,y,z),(s*1.28,s*1.20,s*.6),'Head_M',10)
  b.ellipsoid('Amber eye %s %s'%(sign,k),(sign*x,y+.002,z+s*.42),(s,s*.9,s*.4),'Head_M',8 if k else 9)
 for label in ['VeryFrontLeg','FrontLeg','MiddleLeg','BackLeg']:
  side='R' if sign>0 else 'L';names=[f'{label}{i}_{side}' for i in range(1,6)];points=[b.B[n].copy() for n in names]
  # Native terminal centers can sit beyond surface bounds; inset original toe tip.
  points[-1]=points[-2]+.955*(points[-1]-points[-2]);points[-1][1]=max(points[-1][1],.025)
  b.tube('Eight articulated legs '+label+side,points,[.052,.063,.077,.031,.007],[.048,.059,.070,.03,.007],[names[0],names[1],names[2],names[3],names[3]],[0,1,2,1,0,1],axis=(0,0,1),sides=6)
  # Short copper knee sleeve follows leg3 only, not a connection across arbitrary bones.
  a=points[2]*.89+points[1]*.11;c=points[2]*.92+points[3]*.08
  b.tube('Copper knee '+label+side,[a,points[2],c],[.078,.082,.075],[.072,.076,.07],[names[2]]*3,[4,5,6,5,4,5],axis=(0,0,1),sides=6)
 prefix='RFang' if sign>0 else 'LFang';end='joint5' if sign>0 else 'joint4';names=[prefix+'1',prefix+'2',prefix+'3'];points=[b.B[n] for n in names]+[b.B[names[-1]]+.90*(b.B[end]-b.B[names[-1]])]
 b.tube('Ivory articulated fang '+prefix,points,[.037,.045,.035,.007],[.034,.04,.03,.007],[names[0],names[1],names[2],names[2]],[7,11,7,6,7,11],axis=(0,0,1),sides=6)
 side='R' if sign>0 else 'L';names=[f'Antenna{i}_{side}' for i in range(1,5)];points=[b.B[n] for n in names]
 b.tube('Short palp '+side,points,[.02,.025,.018,.006],[.019,.023,.017,.006],[names[0],names[1],names[2],names[2]],[0,1,4,5,1,0],sides=6)
b.write('copperveil')
manifest=dict(name='Copperveil Weaver',native_chassis='spiderB',reference_renderer=121386,renderer_path='enSpiderB',native_surface_copied=False,art_status='Original eight-legged spider; studio/live review pending',native_forward='+UnityZ from head/fang chains; abdomen Tail chain extends -Z',rig_boundary='Exact65-joint palette and inverse binds; original local leg/tail/palp/fang geometry, no native surface',live_baseline='No indexed spider live baseline located when authoring; native diagnostic pending separately',remaining=['Studio review','Native spiderB baseline','Exact authored mesh live assignment','Native attacks/hit/death/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
