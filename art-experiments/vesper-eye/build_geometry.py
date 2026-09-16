"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['181D29','283243','424A59','999784','CCC4AB','E8DEC1','F7ECCF','0B0E16','955135','D78735','F1B649','FFE0A0']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'vesper_basecolor.png')
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
body=Surface(121031);eye=Surface(121210)
# Anatomical direction is established from the separate native eye envelope:
# its positive-Z protrusion (to+.98) faces away from the body's rear (-1.74).
# Root matrices differ slightly between renderers and are retained independently.
body.tube('Faceted obsidian orb',[(0,1.8,z) for z in [-1.30,-1.17,-.85,-.35,.10,.46,.63]],
 [.24,.56,.89,1.06,1.0,.90,.70],[.22,.55,.91,1.06,1.03,.84,.66],['Root_M']*7,
 [0,1,2,1,0,0,1,2,1,0,1,1],axis=(1,0,0),sides=12)
# Broad upper and lower ivory lid plates hug the body, rather than orbiting markers.
upper=[(-.91,2.00,.46),(-.72,2.32,.57),(-.35,2.47,.64),(0,2.42,.66),(.35,2.47,.64),(.72,2.32,.57),(.91,2.00,.46)]
body.tube('Upper ivory eyelid armor',upper,[.067,.075,.083,.087,.083,.075,.067],[.10,.13,.14,.13,.14,.13,.10],['Root_M']*7,[3,4,5,4,3,3,4,5],axis=(0,0,1),sides=8)
lower=[(-.84,1.53,.50),(-.63,1.25,.61),(-.30,1.12,.64),(0,1.10,.65),(.30,1.12,.64),(.63,1.25,.61),(.84,1.53,.50)]
body.tube('Lower ivory eyelid armor',lower,[.045,.058,.064,.064,.064,.058,.045],[.07,.095,.105,.11,.105,.095,.07],['Root_M']*7,[3,4,4,5,4,3,3,4],axis=(0,0,1),sides=8)
# Three restrained copper seams follow the orb's outer curvature; no horns or spikes.
for side in [-1,1]:
 points=[(side*.87,2.30,-.64),(side*1.025,2.01,-.38),(side*1.02,1.61,-.28),(side*.91,1.27,-.40)]
 body.tube('Copper side ridge '+str(side),points,[.022,.027,.027,.021],[.024,.025,.025,.02],['Root_M']*4,[8,9,8,8,8,9],axis=(0,0,1),sides=6)
 body.tube('Ivory temple plate '+str(side),[(side*.92,1.75,.42),(side*.91,1.92,.47)],[.09,.08],[.10,.09],['Root_M']*2,[3,4,5,4,3,4],axis=(0,0,1),sides=6)
body.tube('Copper crown seam',[(0,2.76,-.72),(0,2.86,-.31),(0,2.79,.10)],[.024,.03,.023],[.02,.023,.018],['Root_M']*3,[8,9,8,8,9,8],axis=(1,0,0),sides=6)
# Complete, separately skinned eye surface: broad amber globe, concentric iris,
# vertical pupil and small angular catchlight. All follow the actual eyeball bone.
eye.ellipsoid('Amber ocular globe',(0,1.80,.43),(.76,.69,.43),'eyeball',[8,9,10,9,8,9,10,9])
eye.ellipsoid('Golden iris',(0,1.80,.866),(.50,.53,.065),'eyeball',[9,10,11,10,9,10,11,10])
eye.ellipsoid('Vertical obsidian pupil',(0,1.80,.943),(.13,.405,.017),'eyeball',7)
eye.ellipsoid('Upper iris catchlight',(-.19,2.02,.942),(.049,.068,.014),'eyeball',11)
# Fine inset radial spokes give the eye a purposeful living focal point.
for angle in [-2.35,-1.55,-.75,.75,1.55,2.35]:
 a=np.array([.34*math.sin(angle),1.8+.38*math.cos(angle),.920]);b=np.array([.43*math.sin(angle),1.8+.46*math.cos(angle),.903])
 eye.tube('Amber iris ray',[a,b],[.009,.006],[.005,.004],['eyeball']*2,8,sides=4)
body.write('vesper_body');eye.write('vesper_eye')
manifest={'name':'Vesper Eye','native_chassis':'beholderA','art_status':'Original multipart surface; studio/live review pending','native_surface_copied':False,'assignments':[{'renderer_path':'EyeBody','reference_renderer':121031,'glb':'vesper_body.glb','texture':'vesper_basecolor.png'},{'renderer_path':'EyeBody/EyeEye','reference_renderer':121210,'glb':'vesper_eye.glb','texture':'vesper_basecolor.png'}],'native_forward':'+UnityZ, verified from native separate eye front extent and body rear extent before authoring','weight_rationale':'Original orb, ivory lid plates and copper ridges are rigid to body Root_M; entire separate globe/iris/pupil is rigid to its actual eyeball bone. Each renderer retains its own complete native two-bone palette and exact inverse binds; no native surface or weight values copied.','remaining':['Studio review','Live exact two-renderer binding','Native eye motion/idle/attack/hit/death and culling/material review']}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
