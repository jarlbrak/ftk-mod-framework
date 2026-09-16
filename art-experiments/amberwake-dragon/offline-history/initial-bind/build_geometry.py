"""Original parametric Amberwake Dragon surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['292C32','41434A','66616A','965D37','BA7439','D79543','EDB966','F3DCA4','EEE9D7','654232','20232A','10151C']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'amberwake_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/skeleton-audit/121525/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121525)
# Original slate torso, warm ventral facets, and articulated neck/tail.
ns=['Tail0_M','Root_M','BackA_M','BackB_M','Chest_M','Neck_M','NeckPart1_M','NeckPart2_M','Head_M']
b.tube('Continuous slate body and neck',[b.B[n] for n in ns],[.40,.74,.87,.85,.76,.58,.46,.43,.47],[.40,.57,.60,.59,.57,.44,.38,.36,.39],ns,[0,1,2,1,0,1,3,4,4,3],sides=10)
b.ellipsoid('Broad dragon skull',[0,2.29,2.81],[.63,.47,.83],'Head_M',1)
b.ellipsoid('Copper armored muzzle',[0,2.08,3.42],[.55,.27,.59],'Head_M',4)
b.tube('Articulated ivory lower jaw',[b.B['Jaw_M'],b.B['JawEnd_M']+[0,.12,-.1]],[.49,.38],[.17,.13],['Jaw_M','JawEnd_M'],7)
for sg in [-1,1]:
 b.ellipsoid('Deep eye socket '+str(sg),[sg*.52,2.46,3.01],[.085,.18,.24],'Head_M',10)
 b.ellipsoid('Amber eye '+str(sg),[sg*.592,2.48,3.03],[.037,.105,.13],'Head_M',6)
 b.ellipsoid('Dark slit pupil '+str(sg),[sg*.623,2.49,3.05],[.009,.082,.033],'Head_M',11)
 b.tube('Swept ivory horn '+str(sg),[[sg*.43,2.60,2.42],[sg*.63,3.02,2.11],[sg*.77,3.34,1.59]],[.19,.12,.012],[.17,.10,.012],['Head_M']*3,8,sides=6)
 b.ellipsoid('Nostril '+str(sg),[sg*.28,2.27,3.91],[.09,.052,.033],'Head_M',11)
ns=['Root_M','Tail0_M','Tail1_M','Tail2_M','Tail3_M','Tail4_M','Tail5_M','Tail6_M']
b.tube('Long armored tail',[b.B[n] for n in ns],[.47,.43,.36,.28,.23,.17,.12,.025],[.41,.35,.28,.22,.18,.14,.10,.025],ns,[0,1,2,1,0,1,3,4],sides=8)
for name in ['Root_M','BackA_M','BackB_M','Chest_M','Neck_M','NeckPart1_M','Tail1_M','Tail2_M','Tail3_M','Tail4_M']:
 c=b.B[name];b.tube('Amber dorsal crest '+name,[c+[0,.33,0],c+[0,.67,-.08],c+[0,.74,-.35]],[.15,.10,.01],[.22,.16,.01],[name]*3,5,sides=4)
for side in ['R','L']:
 sg=1 if side=='R' else -1
 ns=['Scapula_'+side,'frontHip_'+side,'frontKnee_'+side,'frontAnkle_'+side,'frontBall_'+side]
 b.tube('Front shoulder and leg '+side,[b.B[n] for n in ns],[.43,.38,.25,.19,.21],[.38,.33,.23,.18,.17],ns,[0,1,2,1,0,1,3,3],axis=(1,0,0))
 ns=['Rump_'+side,'backHip_'+side,'backKnee_'+side,'backAnkle_'+side,'backBall_'+side]
 b.tube('Rear haunch and hock '+side,[b.B[n] for n in ns],[.47,.43,.27,.19,.21],[.43,.38,.25,.18,.17],ns,[0,1,2,1,0,1,3,3])
 for limb in ['front','back']:
  n=limb+'Ball_'+side;c=b.B[n].copy();c[1]=max(.13,c[1])
  for off in [-.17,0,.17]:
   pts=[c+[off,.02,0],c+[off,.0,.30],c+[off,-.05,.49]]
   b.tube(limb+' ivory claw '+side+str(off),pts,[.105,.07,.014],[.075,.05,.012],[n]*3,8,sides=6)
 # Preserve authored asymmetry of actual left/right binding landmarks.
 ns=[side+'_Wing_'+str(i) for i in [1,2,3]]
 b.tube('Wing leading spar '+side,[b.B[n] for n in ns],[.29,.20,.17],[.25,.18,.15],ns,0,axis=(0,1,0))
 ends=['joint6','joint6 1','joint6 2'] if side=='R' else ['joint7','joint8','joint9']
 chains=[]
 for k,end in enumerate(ends,1):
  names=[side+'_WingFlap'+str(k)+'_1',side+'_WingFlap'+str(k)+'_2',end];chains.append(names)
  b.tube('Wing finger '+side+str(k),[b.B[n] for n in names],[.15,.10,.025],[.13,.08,.022],names,1,axis=(0,1,0),sides=6)
 def panel(label,pts,skins,color):
  start=len(b.data['positions']);pts=np.array(pts);normal=np.cross(pts[1]-pts[0],pts[2]-pts[0]);normal/=np.linalg.norm(normal)
  front=pts+normal*.025;back=pts-normal*.025
  for ids in [(0,1,2),(0,2,3)]:
   b.triangle([front[i] for i in ids],[skins[i] for i in ids],color)
   rev=ids[::-1];b.triangle([back[i] for i in rev],[skins[i] for i in rev],color)
  for i in range(4):
   j=(i+1)%4
   b.triangle([front[i],back[i],back[j]],[skins[i],skins[i],skins[j]],9)
   b.triangle([front[i],back[j],front[j]],[skins[i],skins[j],skins[j]],9)
  b.pieces.append(dict(name=label,vertex_start=start,vertex_count=len(b.data['positions'])-start))
 # A closed thin panel between corresponding original finger landmarks.
 for k in [0,1]:
  a,c=chains[k],chains[k+1]
  for row in [0,1]:
   names=[a[row],a[row+1],c[row+1],c[row]];panel('Amber wing membrane '+side+str(k)+str(row),[b.B[n] for n in names],names,5 if row==0 else 6)
 names=[side+'_Wing_1',side+'_Wing_2',chains[2][1],chains[2][2]]
 panel('Inner wing membrane '+side,[b.B[n] for n in names],names,4)
b.write('amberwake')
