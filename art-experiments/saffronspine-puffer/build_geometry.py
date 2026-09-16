"""Original parametric Saffronspine Puffer surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['B97624','D69936','EBB654','735135','F3D9A0','FFF0BF','395568','65879B','BC6550','E89771','171E20','9B792E']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'saffronspine_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/puffer-native-topology-analysis/reference-121509/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121509)
surface=[n for n in b.names if n.startswith(('Mid_','RSide_','LSide_')) or 'Puffcheek'in n]
def skin(p):
 distance=np.array([np.linalg.norm(np.array(p)-b.B[n])for n in surface]);ix=np.argsort(distance)[:4];w=1/np.maximum(distance[ix],.09)**3;w/=w.sum();return {surface[i]:float(v)for i,v in zip(ix,w)}
center=np.array([0,1.58,-.35]);radii=np.array([.635,.68,.725]);N=24;L=12
rings=[]
for j in range(1,L):
 t=math.pi*j/L;rings.append([center+radii*np.array([math.sin(t)*math.cos(2*math.pi*k/N),math.cos(t),math.sin(t)*math.sin(2*math.pi*k/N)])for k in range(N)])
def bodytri(v):
 v=[v[0],v[2],v[1]]
 avg=np.mean(v,axis=0);c=3 if avg[1]>1.99 else (4 if avg[1]<1.30 else 1 if avg[0]>0 else 0)
 if c in [0,1] and avg[2]>.10:c=2
 b.triangle(v,[skin(x)for x in v],c)
start=len(b.data['positions'])
for j in range(len(rings)-1):
 for k in range(N):
  n=(k+1)%N;bodytri([rings[j][k],rings[j+1][k],rings[j+1][n]]);bodytri([rings[j][k],rings[j+1][n],rings[j][n]])
for k in range(N):
 n=(k+1)%N;bodytri([center+[0,radii[1],0],rings[0][k],rings[0][n]]);bodytri([center-[0,radii[1],0],rings[-1][n],rings[-1][k]])
b.pieces.append(dict(name='Continuous softly weighted inflation body',vertex_start=start,vertex_count=len(b.data['positions'])-start))
# Compact lip lobes and eye discs overlap the flexible body, rather than floating away.
b.ellipsoid('Coral upper lip',[0,1.465,.410],[.135,.066,.11],'TopLip',8)
b.ellipsoid('Coral lower lip',[0,1.365,.407],[.122,.053,.10],'BottomLip',9)
b.ellipsoid('Dark mouth cavity',[0,1.414,.464],[.077,.020,.028],{'TopLip':.5,'BottomLip':.5},10)
for sign in [-1,1]:
 b.tube('Ochre seated eye socket '+str(sign),[[sign*.225,1.73,.26],[sign*.225,1.73,.46]],[.095,.070],[.10,.074],['Eyes']*2,1,axis=(1,0,0),sides=8)
 b.ellipsoid('Small dark eye '+str(sign),[sign*.225,1.73,.480],[.071,.080,.052],'Eyes',10)
 b.ellipsoid('Restrained eye glint '+str(sign),[sign*.207,1.755,.524],[.017,.023,.009],'Eyes',5)
 side='R'if sign>0 else'L';bone=side+'Fin';root=b.B[bone]
 b.tube('Fan fin '+side,[root+[-sign*.15,0,-.01],root,root+[sign*.17,-.025,-.05],root+[sign*.34,-.04,-.11]],[.12,.13,.22,.255],[.055,.045,.030,.013],[{**{n:w*.5 for n,w in list(skin(root).items())[:3]},bone:.5},bone,bone,bone],[6,7,6,7,6,7,6,7],axis=(0,1,0),sides=8)
# The broad tail fan is an authored closed volume rigid to the dedicated tail joint.
b.tube('Blue tail fan',[b.B['Tail'],[0,1.51,-1.19],[0,1.51,-1.39]],[.075,.16,.27],[.08,.25,.31],['Tail']*3,[6,7,6,7,6,7,6,7],axis=(1,0,0),sides=8)
# Short ivory nodules follow local surface weights; no rigid hierarchy connectors.
for i,(latitude,longitude)in enumerate([(0.65,k)for k in [.25,1.15,2.15,3.2,4.2,5.3]]+[(1.1,k)for k in [.4,2.5,3.7,5.5]]):
 unit=np.array([math.sin(latitude)*math.cos(longitude),math.cos(latitude),math.sin(latitude)*math.sin(longitude)]);base=center+radii*unit;tip=base+unit*.095;b.tube('Short ivory dermal nub '+str(i),[base-unit*.025,tip],[.053,.010],[.047,.010],[skin(base)]*2,5,axis=(1,0,0),sides=5)
b.write('saffronspine')
