"""Original parametric Lunacrest Clam surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['1B2F54','314B76','526E95','8CA8BC','D2DDD5','F0EAD6','8E7097','B898B5','A68551','D6BA7B','1A263A','F7F3DE']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'lunacrest_basecolor.png')
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
b=Surface(121306)
def face(points,bone,color,normal):
 points=list(points)
 if np.dot(np.cross(np.array(points[1])-points[0],np.array(points[2])-points[0]),normal)<0:points[1],points[2]=points[2],points[1]
 b.triangle(points,[bone]*3,color)
# Original lower concave shell, with distinct inner/outer surfaces and closed rim.
start=len(b.data['positions']);count=20;angles=np.arange(count)*2*math.pi/count;inner=[];outer=[]
for r,y in [(.28,.265),(.57,.32),(.82,.425),(1.,.56)]:
 inner.append([np.array([.93*r*math.cos(a),y+.035*math.sin(a),-.14+.79*r*math.sin(a)])for a in angles]);outer.append([np.array([.93*r*math.cos(a),y-(.14 if r<1 else .07)+.035*math.sin(a),-.14+.79*r*math.sin(a)])for a in angles])
for k in range(count):
 n=(k+1)%count
 face([(0,.25,-.14),inner[0][k],inner[0][n]],'Root_M',4,[0,1,0]);face([(0,.075,-.14),outer[0][k],outer[0][n]],'Root_M',0,[0,-1,0])
 for i in range(3):
  face([inner[i][k],inner[i+1][k],inner[i+1][n]],'Root_M',4+(k%3==0),[0,1,0]);face([inner[i][k],inner[i+1][n],inner[i][n]],'Root_M',4,[0,1,0])
  face([outer[i][k],outer[i+1][k],outer[i+1][n]],'Root_M',k%3,[0,-1,0]);face([outer[i][k],outer[i+1][n],outer[i][n]],'Root_M',k%3,[0,-1,0])
 expected=[math.cos(angles[k]),0,math.sin(angles[k])];face([inner[-1][k],outer[-1][k],outer[-1][n]],'Root_M',9,expected);face([inner[-1][k],outer[-1][n],inner[-1][n]],'Root_M',9,expected)
b.pieces.append(dict(name='Concave lower lapis shell',vertex_start=start,vertex_count=len(b.data['positions'])-start))
# Upper scallop shell is upright in native rest pose. Geometry follows ClamShell_Joint only.
def fanpoint(r,a,front):
 ripple=1+.022*math.cos(a*8);return np.array([.94*r*math.sin(a),.69+1.68*r*math.cos(a)*ripple,-1.055-(.11 if front else .26)*math.sin(math.pi*r)-(.0 if front else .09)])
start=len(b.data['positions']);angles=np.linspace(-1.38,1.38,17);front=[[fanpoint(r,a,True)for a in angles]for r in [.26,.52,.77,1.]];back=[[fanpoint(r,a,False)for a in angles]for r in [.26,.52,.77,1.]]
for k in range(len(angles)-1):
 face([(0,.69,-1.055),front[0][k],front[0][k+1]],'ClamShell_Joint',4,[0,0,1]);face([(0,.69,-1.145),back[0][k],back[0][k+1]],'ClamShell_Joint',1,[0,0,-1])
 for i in range(3):
  face([front[i][k],front[i+1][k],front[i+1][k+1]],'ClamShell_Joint',5 if k%4 in [0,1] else 4,[0,0,1]);face([front[i][k],front[i+1][k+1],front[i][k+1]],'ClamShell_Joint',4,[0,0,1]);face([back[i][k],back[i+1][k],back[i+1][k+1]],'ClamShell_Joint',k%3,[0,0,-1]);face([back[i][k],back[i+1][k+1],back[i][k+1]],'ClamShell_Joint',k%3,[0,0,-1])
 expected=np.array([math.sin(angles[k]),math.cos(angles[k]),0]);face([front[-1][k],back[-1][k],back[-1][k+1]],'ClamShell_Joint',9,expected);face([front[-1][k],back[-1][k+1],front[-1][k+1]],'ClamShell_Joint',9,expected)
for k,sign in [(0,-1),(-1,1)]:
 F=[np.array([0,.69,-1.055])]+[r[k]for r in front];B=[np.array([0,.69,-1.145])]+[r[k]for r in back]
 for i in range(4):face([F[i],B[i],B[i+1]],'ClamShell_Joint',8,[sign,-.4,0]);face([F[i],B[i+1],F[i+1]],'ClamShell_Joint',8,[sign,-.4,0])
b.pieces.append(dict(name='Hinged scalloped upper shell',vertex_start=start,vertex_count=len(b.data['positions'])-start))
# Five purposeful inner ribs echo scallop anatomy, each entirely attached to shell hinge.
for a in [-1.05,-.52,0,.52,1.05]:
 points=[fanpoint(r,a,True)+np.array([0,0,.018])for r in [.22,.48,.74,.96]]
 b.tube('Pearl shell rib '+str(a),points,[.025,.025,.021,.016],[.018,.018,.016,.013],['ClamShell_Joint']*4,[4,5,11,5,4,5],axis=(0,0,1),sides=6)
# Independent original hinge surfaces overlap at the real shell pivot.
b.tube('Rooted rear hinge support',[(0,.45,-.75),(0,.53,-.91),(0,.6345,-1.0157)],[.29,.27,.23],[.12,.15,.145],['Root_M']*3,[0,1,2,8,1,2,8,0],axis=(1,0,0))
b.ellipsoid('Root hinge knuckle',(0,.6345,-1.0157),(.28,.14,.15),'Root_M',[8,9,8,1,9,8,9,1])
b.ellipsoid('Upper shell hinge collar',(0,.6645,-1.0457),(.205,.13,.135),'ClamShell_Joint',[8,9,4,8,9,4,9,8])
# Original soft mantle follows actual tongue chain; minor-influence terminal joints do not gain invented full influence.
b.tube('Lilac mantle',[(0,.58,.015),(0,.90,.025),(0,1.17,.034),(0,1.48,.047),(0,1.67,.05)],[.25,.23,.20,.15,.065],[.11,.105,.09,.074,.045],['Tongue_1','Tongue_1','Tongue_2','Tongue_3','Tongue_3'],[6,7,7,6,6,7,7,6])
b.ellipsoid('Moon pearl',(0,1.59,.14),(.185,.185,.16),'Tongue_3',[3,4,5,11,5,4,5,11,5,4])
# A tiny original crest on pearl is a surface detail, not a new skeleton controller.
b.ellipsoid('Pearl glint',(-.048,1.65,.287),(.028,.037,.014),'Tongue_3',11)
b.write('lunacrest')
manifest=dict(name='Lunacrest Clam',native_chassis='clamA',reference_renderer=121306,renderer_path='enClam',native_surface_copied=False,art_status='Original hinged clam; studio/live review pending',native_forward='+UnityZ opening, native upper shell upright at rear Z~-1.016; tongue rises Y in bind pose',rig_boundary='Exact7-joint palette/IBMs. LowerbowlRoot_M, uppershellClamShell_Joint, mantle/pearlTongue1-3. Minor-influence native joint4/joint6 remain in palette but unweighted by this original surface. Independent overlapping hinge surfaces; no cross-part bridge.',remaining=['Studio review','Exact authored assignment','Native shell/tongue attacks/hit/death/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
