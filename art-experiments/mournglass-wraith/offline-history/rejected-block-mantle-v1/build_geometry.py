"""Original parametric Mournglass Wraith surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['233A45','355562','477A81','739795','C8C9AD','E8DCC0','192A37','807257','B4A577','39495E','15252E','111B22']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'mournglass_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/skeleton-audit/121008/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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
  skin=list(skin)
  for ii in range(1,len(skin)-1):
   if isinstance(skin[ii],str) and isinstance(skin[ii-1],str) and skin[ii]!=skin[ii-1]:skin[ii]={skin[ii]:.85,skin[ii-1]:.15}
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

b=Surface(121008)
ns=['Root_M','BackA_M','BackB_M','Chest_M','Neck_M']
b.tube('Continuous dark mantle torso',[b.B[n]for n in ns],[.22,.25,.265,.28,.15],[.115,.145,.15,.145,.12],ns,[0,1,2,1,0,1,0,1,2,1],sides=10)
b.tube('Attached hood neck',[b.B['Chest_M'],b.B['Neck_M'],b.B['Head_M']],[.16,.16,.18],[.12,.12,.125],['Chest_M','Neck_M','Head_M'],0,sides=10)
b.ellipsoid('Faceted cowl',[0,1.855,.058],[.232,.205,.165],'Head_M',0)
b.ellipsoid('Tall ivory funerary mask',[0,1.856,.188],[.135,.17,.070],'Head_M',4)
for sg in [-1,1]:
 b.tube('Dark angular eye slit '+str(sg),[[sg*.024,1.925,.251],[sg*.068,1.915,.257],[sg*.109,1.935,.223]],[.012,.018,.010],[.008,.009,.008],['Head_M']*3,11,axis=(0,1,0),sides=6)
 b.tube('Mask cheek carving '+str(sg),[[sg*.096,1.876,.247],[sg*.079,1.80,.245],[sg*.045,1.748,.224]],[.009,.011,.007],[.007,.008,.006],['Head_M']*3,7,axis=(0,0,1),sides=6)
 b.tube('Hood rim '+str(sg),[[sg*.175,1.742,.15],[sg*.215,1.83,.13],[sg*.175,1.999,.08],[sg*.05,2.048,.06]],[.027,.032,.026,.015],[.025,.027,.022,.012],['Head_M']*4,2,sides=6)
# Original split mantle lobes use actual hip/knee chains, without inventing feet.
for side in ['R','L']:
 sg=1 if side=='R'else -1
 b.tube('Split hanging mantle '+side,[b.B['Root_M']+[sg*.09,.06,0],b.B['Hip_'+side],b.B['Knee_'+side]+[sg*.035,.19,0]],[.16,.175,.055],[.11,.12,.028],['Root_M','Hip_'+side,'Knee_'+side],[0,1,2,1,0,1,2,1],sides=8)
 ns=['Chest_M','Scapula_'+side,'Shoulder_'+side];b.tube('Continuous shoulder drape '+side,[b.B[ns[0]]+[sg*.10,.12,0],b.B[ns[1]],b.B[ns[2]]],[.15,.135,.115],[.12,.11,.10],ns,1,axis=(0,1,0))
 ns=['Shoulder_'+side,'Elbow_'+side,'Wrist_'+side];b.tube('Long tapered sleeve '+side,[b.B[n]for n in ns],[.115,.102,.060],[.095,.075,.052],ns,[0,1,2,1,0,1,2,1],axis=(0,1,0))
 ns=['Wrist_'+side,'MiddleFinger1_'+side,'MiddleFinger2_'+side];b.tube('Ivory long fingers '+side,[b.B[n]for n in ns],[.052,.056,.037],[.047,.036,.019],ns,4,axis=(0,1,0))
 ns=['Wrist_'+side,'ThumbFinger1_'+side,'ThumbFinger2_'+side,'ThumbFinger3_'+side];b.tube('Hooked thumb '+side,[b.B[n]for n in ns],[.031,.025,.022,.012],[.029,.024,.020,.01],ns,5,axis=(0,1,0),sides=6)
b.write('mournglass')
