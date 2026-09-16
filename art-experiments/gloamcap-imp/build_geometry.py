"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['372941','573851','825367','B3747A','573E31','8C6247','BF946C','E3C59A','23242D','CBC9AA','E7D994','5E704E']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'gloamcap_basecolor.png')
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
b=Surface(121117)
# Small fungus trickster: original volumes fitted to resource Imp, no native surface.
b.tube('Ochre fungal trunk',[(0,.53,.06),(0,.66,.062),(0,.82,.07),(0,.98,.035),(0,1.075,.044)],[.105,.12,.15,.16,.083],[.087,.098,.105,.105,.073],['Root_M','BackA_M','BackB_M','Chest_M','Neck_M'],[4,5,6,5,4,5,6,7])
b.ellipsoid('Angular ochre face',(0,1.19,.076),(.155,.19,.137),'Head_M',[5,6,7,6,5,6,7,6,5,6])
b.tube('Mushroom cap gills',[(0,1.345,.085),(0,1.385,.085)],[.32,.335],[.196,.20],['Head_M']*2,[6,7,9,7,6,7,9,7,6,7,9,7],sides=12)
b.tube('Broad plum mushroom cap',[(0,1.38,.085),(0,1.42,.082),(0,1.49,.078),(0,1.54,.075)],[.34,.31,.20,.063],[.207,.196,.142,.061],['Head_M']*4,[0,1,2,1,0,1,2,3,1,0,1,2],sides=12)
# A few broad pale cap freckles, each rigid to head; no hair-bone motion invented.
for i,(x,y,z,s) in enumerate([(-.205,1.431,.168,.03),(.11,1.485,.161,.035),(-.055,1.519,.108,.027),(.255,1.424,.068,.025)]):
 b.ellipsoid('Cap freckle '+str(i),(x,y,z),(s,.01,s*.7),'Head_M',9)
for sign in [-1,1]:
 b.ellipsoid('Deep eye socket '+str(sign),(sign*.066,1.221,.201),(.053,.049,.023),'Head_M',8)
 b.ellipsoid('Pale eye '+str(sign),(sign*.064,1.226,.224),(.025,.03,.008),'Head_M',10)
 b.tube('Expressive eyebrow '+str(sign),[(sign*.021,1.289,.183),(sign*.11,1.272,.177)],[.014,.016],[.02,.022],['Head_M']*2,4,axis=(0,0,1),sides=6)
b.tube('Long tapered nose',[(0,1.214,.193),(0,1.183,.293)],[.027,.024],[.026,.024],['Head_M']*2,[5,6,7,6,5,6],sides=6)
b.tube('Crooked smile',[(-.072,1.118,.191),(0,1.104,.209),(.084,1.136,.183)],[.006]*3,[.012]*3,['Head_M']*3,8,axis=(0,0,1),sides=6)
# Russet fungal collar and short hanging fronds are attached locally to chest/spine.
b.tube('Plum collar',[(0,.94,.044),(0,1.015,.04)],[.177,.145],[.118,.10],['Chest_M']*2,[0,1,2,1,0,1,2,3])
for sign in [-1,1]:
 b.tube('Front collar lobe '+str(sign),[(sign*.085,.973,.135),(sign*.11,.86,.161),(sign*.08,.79,.163)],[.047,.048,.02],[.022,.023,.012],['Chest_M','Chest_M','BackB_M'],[1,2,3,2,1,2],sides=6)
for side,sign in [('R',1),('L',-1)]:
 S='Shoulder_'+side;E='Elbow_'+side;W='Wrist_'+side;F='MiddleFinger1_'+side;H='Hip_'+side;K='Knee_'+side;A='Ankle_'+side
 b.ellipsoid('Shoulder root '+side,(sign*.176,.997,.016),(.074,.074,.075),{'Chest_M':.3,S:.7},[4,5,6,5,4,5,6,7])
 b.tube('Flexible fungal arm '+side,[b.B[S],(sign*.34,.997,.012),b.B[E],(sign*.60,.997,.011),b.B[W]],[.07,.065,.048,.054,.038],[.067,.065,.048,.054,.038],[S,S,{S:.1,E:.9},E,W],[4,5,6,5,4,5,6,7],axis=(0,0,1))
 b.ellipsoid('Knuckle palm '+side,(sign*.836,.987,.05),(.087,.06,.072),F,[4,5,6,7,6,5,6,5])
 b.tube('Curved long digits '+side,[(sign*.855,.993,.053),(sign*.94,.975,.075),(sign*1.00,.925,.097)],[.055,.05,.028],[.047,.043,.026],[F,'MiddleFinger2_'+side,'MiddleFinger3_'+side],[4,5,6,5,4,5],axis=(0,0,1),sides=6)
 b.tube('Hook thumb '+side,[b.B['ThumbFinger1_'+side],b.B['ThumbFinger2_'+side],b.B['ThumbFinger3_'+side]],[.029,.028,.021],[.026,.026,.019],['ThumbFinger1_'+side,'ThumbFinger2_'+side,'ThumbFinger3_'+side],[4,5,6,5,4,5],sides=6)
 b.tube('Short root leg '+side,[b.B[H],(sign*.123,.46,.067),b.B[K],b.B[A]],[.077,.071,.055,.05],[.075,.071,.054,.051],[H,H,{H:.12,K:.88},A],[4,5,6,5,4,5,6,7])
 b.tube('Pointed plum shoe '+side,[(sign*.123,.073,.032),(sign*.123,.067,.15),(sign*.123,.06,.26)],[.068,.071,.047],[.06,.051,.028],[A,'MiddleToe1_'+side,'MiddleToe2_'+side],[0,1,2,1,0,1,2,3],axis=(1,0,0))
 b.tube('Ankle moss cuff '+side,[(sign*.123,.16,.04),(sign*.123,.19,.04)],[.06,.058],[.06,.058],[A]*2,11)
b.write('gloamcap')
manifest=dict(name='Gloamcap Trickster',native_chassis='impA',resource_prefab='enbaseyimp',reference_renderer=121117,renderer_path='enBaseyImp',native_surface_copied=False,art_status='Original fungal creature; studio/live review pending',native_forward='+UnityZ verified from toe chain and native weighted face envelope',rig_boundary='Full37-joint resource Imp palette retained. Mushroom cap follows Head_M, not unused Hair_M; trunk and limbs blend locally.',remaining=['Studio review','Exact resource Imp live assignment','Native attacks/hit/ragdoll/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
