"""Original parametric Belladusk Pitcher surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['244B48','35635A','4C8170','7A9D78','503D66','775782','9C789A','EAD8AC','B88547','E3AF63','1B1823','A57650']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'belladusk_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/plant-e-native-topology-analysis/reference-121530/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121530)
ns=['Root_M']+['Bone%03d'%i for i in range(2,15)]+['Head'];pts=[b.B[n].copy()for n in ns];pts[0][1]=-.035
b.tube('Continuous curving dark teal stalk',pts,[.075-i*.0014 for i in range(len(pts))],[.070-i*.0012 for i in range(len(pts))],ns,[0,1,2,1,0,1,2,1],sides=8)
# Broad original root leaves, each follows the actual root two-joint chain.
for first,last in [('Bone035','Bone036'),('Bone035(mirrored)','Bone036(mirrored)'),('Bone037','Bone038'),('Bone037(mirrored)','Bone038(mirrored)')]:
 start=b.B[first];tip=b.B[last];delta=tip-start;end=tip+delta*.58;end[1]=.055
 b.tube('Broad root blade '+last,[start,start*.4+tip*.6,tip,end],[.025,.13,.17,.005],[.024,.035,.025,.004],[first,{first:.4,last:.6},last,last],[0,1,2,3,2,1],axis=(1,0,0),sides=6)
# Plum hood is original head volume; dark inset throat and separately articulated lower lip.
b.ellipsoid('Plum pitcher hood',[0,1.22,-.04],[.285,.195,.13],'Head',[4,5,6,5,4,5,6,5,4,5])
# Original recessed funnel, continuously skinned from fixed throat to articulated lip.
def mouthskin(t):
 jaw=max(0.,-math.sin(t));return {'Head':1-jaw,'Jaw':jaw}if jaw>0 else 'Head'
front=[];back=[];N=24
for k in range(N):
 t=2*math.pi*k/N;front.append(np.array([.237*math.cos(t),1.195+.133*math.sin(t),.318]));back.append(np.array([.10*math.cos(t),1.195+.05*math.sin(t),.115]))
start=len(b.data['positions'])
for k in range(N):
 n=(k+1)%N;ta=2*math.pi*k/N;tb=2*math.pi*n/N
 # Interior inward-facing funnel, visible from the front; deliberately open rim.
 b.triangle([front[k],front[n],back[n]],[mouthskin(ta),mouthskin(tb),'Head'],10)
 b.triangle([front[k],back[n],back[k]],[mouthskin(ta),'Head','Head'],10)
 b.triangle([[0,1.195,.114],back[k],back[n]],['Head']*3,10)
b.pieces.append(dict(name='Continuous recessed mouth interior',vertex_start=start,vertex_count=len(b.data['positions'])-start,openSurface=True))
# Thick-looking original outer pitcher sleeve, continuously joining the hood to moving lip.
# Front follows the exact lip blend; the rear stays embedded in the head volume.
start=len(b.data['positions']);outer=[]
for radiusx,radiusy,z in [(.22,.16,-.045),(.268,.172,.14),(.255,.154,.313)]:
 outer.append([np.array([radiusx*math.cos(2*math.pi*k/N),1.195+radiusy*math.sin(2*math.pi*k/N),z])for k in range(N)])
def outerskin(ring,t):
 jaw=max(0.,-math.sin(t))*[0.,.45,1.][ring]
 return {'Head':1-jaw,'Jaw':jaw}if jaw>0 else 'Head'
for ring in range(2):
 for k in range(N):
  n=(k+1)%N;ta=2*math.pi*k/N;tb=2*math.pi*n/N
  c=4 if k>=N//2 else 5
  b.triangle([outer[ring][k],outer[ring][n],outer[ring+1][n]],[outerskin(ring,ta),outerskin(ring,tb),outerskin(ring+1,tb)],c)
  b.triangle([outer[ring][k],outer[ring+1][n],outer[ring+1][k]],[outerskin(ring,ta),outerskin(ring+1,tb),outerskin(ring+1,ta)],c)
for k in range(N):
 n=(k+1)%N;ta=2*math.pi*k/N;tb=2*math.pi*n/N
 b.triangle([outer[-1][k],outer[-1][n],front[n]],[mouthskin(ta),mouthskin(tb),mouthskin(tb)],5)
 b.triangle([outer[-1][k],front[n],front[k]],[mouthskin(ta),mouthskin(tb),mouthskin(ta)],5)
b.pieces.append(dict(name='Continuous purple outer lip-to-hood flesh',vertex_start=start,vertex_count=len(b.data['positions'])-start,openSurface=True))
b.ellipsoid('Amber throat inset',[0,1.19,.125],[.052,.028,.006],'Head',8)
# Lip arcs follow head and jaw independently, with generous overlapping corners.
upper=[[.245*__import__('math').cos(t),1.195+.14*__import__('math').sin(t),.32]for t in np.linspace(0,math.pi,9)]
lower=[[.245*__import__('math').cos(t),1.195+.135*__import__('math').sin(t),.32]for t in np.linspace(math.pi,2*math.pi,9)]
b.tube('Ivory upper pitcher rim',upper,[.032]*9,[.031]*9,['Head']*9,7,axis=(0,0,1),sides=6)
b.tube('Copper lower articulated pitcher rim',lower,[.035]*9,[.033]*9,[mouthskin(t)for t in np.linspace(math.pi,2*math.pi,9)],8,axis=(0,0,1),sides=6)
for x in [-.15,-.075,.075,.15]:
 y=1.195+.14*math.sqrt(1-(x/.245)**2);b.tube('Pale upper tooth '+str(x),[[x,y-.01,.334],[x,y-.073,.345]],[.022,.003],[.021,.003],['Head']*2,7,sides=5)
# Small rear hood curl uses only the two native positively weighted crown joints.
b.tube('Curled hood sepal',[b.B['Head']+[0,.07,-.10],b.B['Bone018']+[0,0,-.04],b.B['Bone019']],[.11,.075,.012],[.065,.04,.008],['Head','Bone018','Bone019'],[4,5,6,5,4,5],axis=(0,0,1),sides=6)
b.tube('Side leaf at native Leaf6',[b.B['Bone006'],b.B['Leaf6']+[.03,.03,.04],b.B['Leaf6']+[-.18,.08,.12]],[.035,.11,.005],[.025,.027,.004],['Bone006','Leaf6','Leaf6'],[0,1,2,3,2,1],axis=(0,1,0),sides=6)
b.ellipsoid('Copper vine collar',b.B['Vine1'],[.07,.05,.06],'Vine1',11)
b.ellipsoid('Head hinge calyx',b.B['Bone016'],[.11,.11,.08],'Bone016',0)
b.write('belladusk')
