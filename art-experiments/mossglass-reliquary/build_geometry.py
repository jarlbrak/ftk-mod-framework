"""Original analytic Mossglass surfaces. Native source supplies palette/IBMs and bounds only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['1A2924','EEE2BD','BCAD82','DBA746','F2C461','80512B','172D25','2F5C46','477A59','659875','91B294','B0C5A1']
for slot in [0,1]:
 im=Image.new('RGB',(384,128))
 for x in range(384):
  base=tuple(int(PALETTE[x//32][k:k+2],16) for k in (0,2,4))
  for y in range(128):
   stripe=slot==1 and (y+int(7*math.sin(x*.075)))%64<4
   im.putpixel((x,y),tuple(min(255,int(c*(1.17 if stripe else 1))) for c in base))
 im.save(OUT/f'mossglass.slot{slot}.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/cube-topology-analysis/reference-121012/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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
b=Surface(121012)
slot0=[];slot1=[]
def skin(y):
 if y<=1:return {'Root_M':1-y,'jellyCubeMid':y} if y>0 else {'Root_M':1}
 t=min(1,(y-1)/1.1);return {'jellyCubeMid':1-t,'jellyCubeTop':t}
def mark(slot,before):slot.extend(b.data['triangles'][before:])
# Rounded-square rings, open front portal. All coordinates are authored constants.
ys=[.03,.22,.48,.85,1.22,1.58,1.86,2.04];rads=[.59,.84,.94,.97,.95,.87,.69,.37];rings=[]
for y,r in zip(ys,rads):
 rings.append([[r*math.copysign(abs(math.cos(k*math.tau/16))**.48,math.cos(k*math.tau/16)),y,r*math.copysign(abs(math.sin(k*math.tau/16))**.48,math.sin(k*math.tau/16))] for k in range(16)])
start=len(b.data['positions']);before=len(b.data['triangles'])
for i in range(7):
 for k in range(16):
  if i in [2,3,4] and k in [2,3,4,5]:continue
  n=(k+1)%16;c=[7,8,8,9,9,8,7,6,7,8,9,10,9,8,7,6][k]
  b.triangle([rings[i][k],rings[i+1][n],rings[i][n]],[skin(ys[i]),skin(ys[i+1]),skin(ys[i])],c)
  b.triangle([rings[i][k],rings[i+1][k],rings[i+1][n]],[skin(ys[i]),skin(ys[i+1]),skin(ys[i+1])],c)
for k in range(16):
 n=(k+1)%16;b.triangle([[0,.03,0],rings[0][n],rings[0][k]],[skin(.03)]*3,6);b.triangle([[0,2.07,0],rings[-1][k],rings[-1][n]],[skin(2.04)]*3,9)
b.pieces.append(dict(name='Blended open-front jade shell',vertex_start=start,vertex_count=len(b.data['positions'])-start));mark(slot1,before)
# Ivory mouth rim and ribs are independent locally blended structures, with static UVs.
before=len(b.data['triangles'])
for sign in [-1,1]:
 b.tube('Ivory portal jamb '+str(sign),[[sign*.66,.43,.82],[sign*.61,.8,.88],[sign*.55,1.19,.88],[sign*.38,1.57,.80]],[.09,.07,.075,.095],[.075]*4,[skin(y) for y in [.43,.8,1.19,1.57]],[1,2,1,2,1,2],sides=6)
 b.tube('Upper angled ivory lintel '+str(sign),[[sign*.04,1.58,.91],[sign*.27,1.65,.88],[sign*.50,1.55,.84]],[.075,.1,.08],[.08]*3,[skin(y) for y in [1.58,1.65,1.55]],1,sides=6)
 for i in range(3):
  y=.55+i*.22;b.tube('Inner seed cup rib '+str((sign,i)),[[sign*float(np.interp(y,[.43,.8,1.19,1.57],[.66,.61,.55,.38])),y,float(np.interp(y,[.43,.8,1.19,1.57],[.82,.88,.88,.80]))],[sign*.34,y-.09,.75],[sign*.13,y-.12,.70]],[.052,.045,.033],[.045]*3,[skin(y)]*3,2,sides=6)
b.ellipsoid('Dark cavity back',[0,1.0,.10],[.63,.60,.29],skin(1.),0)
b.ellipsoid('Faceted amber seed',[0,1.0,.50],[.24,.38,.22],skin(1.),[3,4,3,5,3,4,3,5,3,4])
b.tube('Ivory lower sill',[[-.59,.43,.78],[0,.35,.90],[.59,.43,.78]],[.09]*3,[.075]*3,[skin(.43),skin(.35),skin(.43)],1,sides=6)
mark(slot0,before)
b.data['primitives']=[{'triangles':slot0},{'triangles':slot1}]
# Shell UV changes vertically across each face so native offset has a visible moving vein pattern.
for t in slot1:
 for vi in t:b.data['uvs'][vi][1]=1-(b.data['positions'][vi][1]*.65%1)
b.write('mossglass')
# AUTHORING_COMPLETE: below this marker only independent native-bounds diagnostics.
positions=np.array(b.data['positions']);assert np.all(positions.min(0)>=b.ref['positions'].min(0)) and np.all(positions.max(0)<=b.ref['positions'].max(0))
(OUT/'binding-audit.json').write_text(json.dumps(dict(names=b.names,vertexCount=len(positions),triangleCount=len(b.data['triangles']),primitiveTriangles=[len(slot0),len(slot1)],softVertices=int((np.count_nonzero(np.array(b.data['weights'])>0,axis=1)>1).sum()),boundsMin=positions.min(0).tolist(),boundsMax=positions.max(0).tolist(),nativeBoundsMin=b.ref['positions'].min(0).tolist(),nativeBoundsMax=b.ref['positions'].max(0).tolist(),nativeBoundsContained=True,geometry='Original analytic rings/tubes/ellipsoids; no native surface copied'),indent=2)+'\n')
