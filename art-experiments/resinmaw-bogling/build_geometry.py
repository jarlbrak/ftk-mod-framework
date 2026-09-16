"""Original parametric Resinmaw Bogling surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['392B23','69412A','97592A','BF7C32','DCA84B','E9CB78','465949','788C59','E6D8B2','B7B698','201F20','F4E5B8']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'resinmaw_basecolor.png')
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
b=Surface(121344)
# Original continuous amber trunk. Native positions are binding landmarks only.
b.tube('Amber trunk',[(0,.28,-.20),(0,.62,-.19),(0,1.05,-.16),(0,1.45,-.14),(0,1.77,-.12)],[.40,.67,.79,.74,.52],[.40,.56,.67,.57,.39],['Hips','Hips','Hips','Hips',{'Hips':.8,'UpperJaw':.2}],[1,2,3,4,3,2,1,2,3,4,3,2],sides=12)
# Broad tapering tail, with blended joints at section boundaries.
b.tube('Resin slug tail',[(0,.9,-.42),(0,.86,-.83),(0,.65,-1.23),(0,.51,-1.68),(0,.46,-2.06),(0,.40,-2.51),(0,.39,-2.68)],[.61,.60,.50,.39,.30,.16,.035],[.58,.51,.42,.35,.27,.14,.035],[{'Hips':.4,'Tail_1':.6},'Tail_1',{'Tail_1':.4,'Tail_2':.6},'Tail_2',{'Tail_2':.3,'Tail_3':.7},'Tail_3','Tail_3'],[1,2,3,4,3,2,1,2,3,4,3,2],sides=12)
for side,sign in [('R',1),('L',-1)]:
 hip=b.B[side+'Hip'];knee=b.B[side+'Knee'];foot=b.B[side+'Foot']
 b.tube(side+' folded haunch',[hip+[-.04*sign,.09,0],hip*.5+knee*.5,knee,knee*.4+foot*.6,foot],[.34,.30,.25,.21,.22],[.34,.31,.25,.20,.20],[side+'Hip',{side+'Hip':.5,side+'Knee':.5},side+'Knee',{side+'Knee':.4,side+'Foot':.6},side+'Foot'],[1,2,3,4,3,2,1,2])
 b.ellipsoid(side+' broad paddle foot',(sign*.74,.14,.25),(.37,.10,.32),side+'Foot',[0,1,2,3,2,1,2,1])
# Upper and lower mouth are distinct surfaces on native independent jaw bones.
b.ellipsoid('Jaw pivot throat',(0,1.81,-.18),(.46,.38,.40),'UpperJaw',[1,2,3,4,3,2,1,2])
b.ellipsoid('Upper amber muzzle',(0,2.34,.03),(.79,.50,.66),'UpperJaw',[2,3,4,5,4,3,2,3,4,5,4,3])
b.ellipsoid('Upper dark mouth interior',(0,2.12,.55),(.60,.20,.23),'UpperJaw',[0,10,0,10,0,10,0,10])
b.ellipsoid('Lower amber jaw',(0,1.40,.48),(.67,.24,.48),'LowerJaw',[1,2,3,4,3,2,1,2,3,4,3,2])
b.ellipsoid('Lower mouth cushion',(0,1.60,.50),(.54,.085,.34),'LowerJaw',[0,1,0,1,0,1,0,1])
# Native articulated fang chains. No invented full influence on terminal joints.
for side in ['R','L']:
 for level,end in [('Upper','joint25' if side=='R' else 'joint31'),('Lower','joint28' if side=='R' else 'joint34')]:
  first=side+level+'_1';second=side+level+'_2';a=b.B[first];c=b.B[second];d=b.B[end]
  b.tube(side+' '+level+' ivory tusk',[a,a*.45+c*.55,c,c*.4+d*.6,d*.98+c*.02],[.13,.12,.105,.065,.016],[.13,.12,.105,.065,.016],[first,{first:.4,second:.6},second,second,second],[8,11,8,9,8,11,9,8])
# Head joint carries the original expressive eye brow mass; front is Unity +Z.
b.ellipsoid('Moss brow crest',(0,2.85,-.02),(.59,.42,.45),'Head',[6,7,6,7,6,7,6,7])
for sign in [-1,1]:
 b.ellipsoid('Eye rim '+str(sign),(sign*.34,2.94,.29),(.27,.28,.18),'Head',[1,2,3,4,3,2,1,2])
 b.ellipsoid('Golden eye '+str(sign),(sign*.34,2.96,.432),(.19,.20,.07),'Head',5)
 b.ellipsoid('Vertical pupil '+str(sign),(sign*.34,2.97,.492),(.054,.135,.02),'Head',10)
 b.ellipsoid('Eye gleam '+str(sign),(sign*.34-.035,3.015,.512),(.026,.037,.012),'Head',11)
b.write('resinmaw')
manifest=dict(name='Resinmaw Bogling',native_chassis='acidBlobA',reference_renderer=121344,renderer_path='enAcidMonster',native_surface_copied=False,art_status='Original amber slug-toad; studio and live review pending',native_forward='+UnityZ from feet/fang endpoints and rear tail',rig_boundary='Exact32-joint palette and IBMs. Main body, two legs, tail, two jaws, four articulated tusks, and head mass follow native weighted anatomy. Terminal joints retained without invented full-weight geometry.',remaining=['Studio review','Captured native pose checks','Authored live acceptance','Native brown chunk and puddle effects retained'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
