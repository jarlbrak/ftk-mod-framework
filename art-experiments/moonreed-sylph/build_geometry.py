"""Original parametric Moonreed Sylph surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['192F32','2C5355','46706C','74708E','A6A0B5','D9D9C6','F0E9D3','956447','BB8860','464761','192428','BED0C9']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'moonreed_basecolor.png')
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
b=Surface(121395)
# Anatomical up+Y, face+Z; exact binding landmarks, no native surface sampling.
sp=['Root_M','RigSpine2','RigSpine3','RigSpine4','RigSpine5','RigRibcage'];b.tube('Continuous teal tunic',[b.B[n]for n in sp],[.085,.081,.068,.065,.077,.089],[.062,.056,.048,.047,.052,.052],sp,[0,1,2,1,0,1,2,1],axis=(1,0,0),sides=10)
b.tube('Soft neck transition',[b.B['RigRibcage'],b.B['RigNeck'],[0,1.055,0]],[.053,.031,.038],[.043,.029,.035],['RigRibcage','RigNeck',{'RigNeck':.5,'RigHead':.5}],5,sides=8)
b.ellipsoid('Ivory mask face',(0,1.093,.02),(.068,.093,.061),'RigHead',[5,6,5,6,5,6,5,6])
for sign,side in [(-1,'L'),(1,'R')]:
 b.ellipsoid(side+' dark almond eye',(sign*.030,1.112,.077),(.019,.009,.009),'RigHead',10)
 b.ellipsoid(side+' copper temple',(sign*.061,1.118,.012),(.018,.049,.031),'RigHead',7)
 names=['Rig'+side+'Leg1','Rig'+side+'Leg2','Rig'+side+'LegAnkle'];pts=[b.B[n]for n in names];b.tube(side+' fitted lavender leg',pts,[.049,.031,.023],[.05,.032,.025],names,[3,4,3,9,3,4,3,9],sides=8)
 toes=['Rig'+side+'LegAnkle','Rig'+side+'LegToes1','Rig'+side+'LegToes2'];pts=[b.B[n]for n in toes]+[b.B[toes[-1]]+[0,-.014,.06]];b.tube(side+' reed boot',pts,[.026,.028,.027,.016],[.027,.025,.019,.01],toes+[toes[-1]],[0,1,2,1,0,1,2,1],sides=8)
 arm=['Rig'+side+'Arm1','Rig'+side+'Arm2','Rig'+side+'ArmPalm'];pts=[b.B[n]for n in arm];b.tube(side+' teal sleeve',pts,[.04,.032,.021],[.039,.028,.022],arm,[0,1,2,1,0,1,2,1],axis=(0,1,0),sides=8)
 b.ellipsoid(side+' shoulder attachment',b.B[arm[0]],(.036,.04,.04),{'RigRibcage':.5,arm[0]:.5},1)
 b.tube(side+' ivory palm',[b.B[arm[-1]],b.B['Rig'+side+'ArmIndex1']],[.019,.024],[.018,.019],[arm[-1]]*2,5,axis=(0,1,0),sides=8)
 for finger,count in [('Thumb',2),('Index',3),('Rest',3)]:
  ns=['Rig'+side+'Arm'+finger+str(i)for i in range(1,count+1)];pts=[b.B[n]for n in ns]+[b.B[ns[-1]]+[sign*.012,0,.007 if finger=='Thumb'else 0]];b.tube(side+' '+finger+' fingers',pts,[.008]*(len(pts)-1)+[.004],[.008]*(len(pts)-1)+[.004],ns+[ns[-1]],5,axis=(0,1,0),sides=5)
 for part in ['Up','Down']:
  ns=[('Rig'+side+part+'Wing'+(str(i)if side=='R'and part=='Down'else f'{i:02}'))for i in [1,2,3]];c=[b.B[n]for n in ns];tip=c[-1]+[sign*(.18 if part=='Up'else .145),.17 if part=='Up'else -.13,0];points=c+[tip];widths=[.013,.095,.115,.006]if part=='Up'else[.012,.077,.085,.006]
  b.tube(side+' '+part+' opaque wing',points,widths,[.006]*3+[.003],ns+[ns[-1]],[5,11,5,4,5,11,5,4],axis=(0,1,0),sides=8)
  b.ellipsoid(side+' '+part+' wing root collar',c[0]+[0,0,.023],(.028,.024,.04),{'RigRibcage':.6,ns[0]:.4},1)
  b.tube(side+' '+part+' indigo leading vein',[x+[0,0,.008]for x in points],[.006,.005,.004,.002],[.004]*4,ns+[ns[-1]],9,axis=(0,1,0),sides=5)
  for i in [1,2]:
   p=c[i]+[0,0,.009];b.tube(side+' '+part+' copper cross vein'+str(i),[p+[0,-widths[i]*.7,0],p+[0,widths[i]*.7,0]],[.003,.003],[.003,.003],[ns[i]]*2,7,axis=(1,0,0),sides=4)
# Original segmented copper crest follows the complete native hair chain.
hair=['RigHair'+str(i)for i in range(1,11)];c=[b.B[n]for n in hair];b.tube('Copper flowing reed crest',c,[.048,.041,.035,.032,.03,.025,.021,.017,.013,.009],[.04,.038,.032,.028,.026,.022,.018,.015,.011,.009],hair,[7,8,7,8,7,8,7,8],sides=8)
b.write('moonreed')
manifest=dict(name='Moonreed Sylph',native_chassis='fairyA',reference_renderer=121395,renderer_path='enFairy01',native_surface_copied=False,art_status='Original fairy surfaces, studio review pending',native_forward='Mesh+Y up, anatomical face+Z; native binding transforms unchanged',remaining=['Studio and native captured pose review','Original live body/portrait/material/death/cleanup acceptance'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
