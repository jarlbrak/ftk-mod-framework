"""Original parametric Gloamfin Kraken surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['162A35','244753','346773','569099','85ACA7','C0CFB5','754934','AB7651','DED4AE','A6B39E','151D26','F1E5C2']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'gloamfin_basecolor.png')
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
b=Surface(121260)
# Single original continuous cephalopod neck/mantle, deliberately soft through all transition rings.
points=[(0,-5.35,-.45),(0,-4.65,-.5),(0,-3.7,-.6),(0,-2.75,-.78),(0,-1.75,-1.1),(0,-.8,-1.55),(0,.2,-2.),(0,1.2,-2.65),(0,2.15,-3.2),(0,3.1,-3.5),(0,3.7,-3.75)]
widths=[1.25,1.55,1.65,1.85,2.1,2.5,3.,3.05,2.7,1.8,.5];depths=[.9,1.1,1.25,1.4,1.6,1.8,2.05,2.15,1.8,1.2,.45]
weights=[{'joint1':.9,'neck':.1},{'joint1':.75,'neck':.25},{'joint1':.45,'neck':.55},{'joint1':.15,'neck':.85},{'neck':.8,'head':.2},{'neck':.5,'head':.5},{'neck':.15,'head':.85},{'head':.8,'topHead':.2},{'head':.55,'topHead':.45},{'head':.25,'topHead':.75},{'head':.1,'topHead':.9}]
b.tube('Continuous soft neck and mantle',points,widths,depths,weights,[0,1,2,3,4,3,2,1,0,1,2,3,4,3,2,1],sides=16)
# Face masses meet the neck and move on head, keeping lower jaw independently articulated.
b.ellipsoid('Upper cephalopod muzzle',(0,.18,-.15),(2.12,.80,1.08),'head',[1,2,3,4,3,2,1,2,3,4,3,2])
b.ellipsoid('Dark upper mouth',(0,-.40,.53),(1.42,.40,.36),'head',10)
b.ellipsoid('Articulated lower jaw',(0,-1.38,.35),(1.80,.52,1.12),'jaw',[0,1,2,3,4,3,2,1,0,1,2,3])
b.ellipsoid('Lower mouth cushion',(0,-.94,.48),(1.43,.14,.76),'jaw',10)
b.tube('Flexible dark throat',[(0,-.28,.24),(0,-.62,.30),(0,-.96,.34)],[1.25,1.22,1.26],[.28,.29,.27],['head',{'head':.5,'jaw':.5},'jaw'],10,sides=12)
# Restrained copper beak halves emphasize jaw opening rather than tooth spikes.
b.tube('Upper copper beak',[(0,.24,.74),(0,-.05,1.1),(0,-.60,1.29)],[.48,.37,.045],[.23,.22,.05],['head']*3,[6,7,8,7,6,7,8,7])
b.tube('Lower copper beak',[(0,-1.35,1.12),(0,-1.0,1.36),(0,-.69,1.29)],[.45,.27,.035],[.24,.17,.035],['jaw']*3,[6,7,8,7,6,7,8,7])
for sign in [-1,1]:
 b.ellipsoid('Eye mantle '+str(sign),(sign*1.65,.91,-.32),(.74,.62,.72),'head',[1,2,3,4,3,2,1,2])
 b.ellipsoid('Pale watchful eye '+str(sign),(sign*1.75,1.02,.25),(.46,.36,.16),'head',8)
 b.ellipsoid('Deep pupil '+str(sign),(sign*1.72,1.02,.397),(.16,.25,.033),'head',10)
 b.ellipsoid('Eye reflection '+str(sign),(sign*1.72-.065,1.11,.426),(.055,.07,.022),'head',11)
 # Short curved facial feelers; these are head-rigid original forms, not simulated tentacles.
 b.tube('Curved facial feeler '+str(sign),[(sign*2.0,-.15,.05),(sign*2.8,-.55,.45),(sign*3.35,-1.20,.70),(sign*3.0,-1.85,1.05),(sign*2.50,-1.65,1.25)],[.37,.35,.29,.20,.04],[.33,.32,.25,.17,.035],['head']*5,[0,1,2,3,4,3,2,1])
b.write('gloamfin')
manifest=dict(name='Gloamfin Kraken',native_chassis='resource enkrakenhead; owned endpoint fixture only',reference_renderer=121260,renderer_path='krakenHead',native_surface_copied=False,art_status='Original soft-skinned head blockout; not finished art, live skin acceptance or production adapter',native_forward='+UnityZ face from native jaw/front landmarks',rig_boundary='Exact joint1/neck/head/topHead/jaw palette and IBMs. Continuous mantle has graded two-bone rings; jaw separate. Short facial feelers rigidly follow head, not tentacle simulation.',remaining=['Studio blockout review','Owned endpoint deformation and transition review','Future separate reviewed fixture asset variant','Other controller scenarios and production adapter'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
