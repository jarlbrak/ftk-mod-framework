"""Original parametric Duskquill Raven surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['19202A','263040','39425A','535377','6D668A','8A829F','DED9BC','F0E9CE','AB7131','E0A64D','0D131B','687993']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'duskquill_basecolor.png')
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
b=Surface(120960)
b.ellipsoid('Charcoal breast',(0,1.274,.12),(.229,.165,.384),'Root_M',[0,1,2,1,0,1,2,1])
b.tube('Blended shoulder and neck',[b.B[n]for n in ['Spine_1','Spine_2','Neck','Head']],[.191,.158,.095,.103],[.148,.136,.095,.1],['Spine_1','Spine_2','Neck','Head'],[0,1,2,1,0,1,2,1],sides=10)
b.ellipsoid('Faceted raven head',(0,1.332,.829),(.134,.137,.169),'Head',[0,1,2,1,0,1,2,1])
b.tube('Ivory tapered beak',[(0,1.322,.957),(0,1.317,1.06),(0,1.292,1.273)],[.071,.067,.004],[.045,.046,.005],['Head']*3,[6,7,6,7,6,7,6,7],sides=8)
b.tube('Dark beak seam',[(0,1.295,1.002),(0,1.281,1.234)],[.065,.009],[.004,.002],['Head']*2,10,sides=4)
for side,sign in [('Left',-1),('Right',1)]:
 b.ellipsoid(side+' amber eye',(sign*.121,1.373,.858),(.021,.028,.034),'Head',9)
 b.ellipsoid(side+' black eye pupil',(sign*.137,1.374,.866),(.009,.019,.018),'Head',10)
 arm=['Arm_'+side+'_1','Arm_'+side+'_2','Hand_'+side];pts=[b.B[n]for n in arm];b.tube(side+' broad inner wing',[pts[0],(pts[0]+pts[1])/2,pts[1],(pts[1]+pts[2])/2,pts[2]],[.173,.202,.231,.19,.15],[.062,.059,.055,.042,.029],[arm[0],{arm[0]:.5,arm[1]:.5},arm[1],{arm[1]:.5,arm[2]:.5},arm[2]],[0,1,2,3,2,1,0,1],axis=(0,0,1),sides=8)
 b.ellipsoid(side+' wrist feather attachment',pts[2],(.066,.044,.125),arm[2],2)
 b.ellipsoid(side+' overlapping shoulder',pts[0]+[0,0,-.018],(.107,.071,.168),{'Spine_2':.5,arm[0]:.5},1)
 for group in [0,1,2]:
  ns=[('Wing'+side+'_5' if n==5 else 'Wing_'+side+'_'+str(n))for n in range(1+group*4,5+group*4)];c=[b.B[n]for n in ns];tip=c[-1]+[sign*(.10 if group<2 else .05),0,-.12];pts=c+[tip];width=[.12,.156,.14,.106,.006] if group<2 else [.15,.17,.165,.13,.008];b.tube(side+' primary feather fan '+str(group),pts,width,[.028,.023,.018,.013,.003],ns+[ns[-1]],[1,2,3,2,1,2,3,2],axis=(0,0,1),sides=8)
  b.tube(side+' violet quill '+str(group),[p+[0,.025,0]for p in pts],[.014,.012,.009,.006,.002],[.006,.005,.004,.003,.002],ns+[ns[-1]],4,axis=(0,0,1),sides=4)
 # Feet remain body-rigid because this exact skeleton has no leg palette.
 ankle=[sign*.10,1.10,.07];foot=[sign*.11,.931,.14];b.tube(side+' tucked talon leg',[ankle,foot],[.024,.018],[.024,.018],['Root_M']*2,0,sides=6)
 for q in [-1,0,1]:b.tube(side+' talon '+str(q),[foot,[sign*.11+q*.027,.90,.20],[sign*.11+q*.035,.94,.227]],[.011,.009,.003],[.011,.009,.003],['Root_M']*3,6,sides=5)
# Five independently modeled feathers, blended along same native tail axis.
for q in [-2,-1,0,1,2]:
 pts=[b.B['Tail_1']+[q*.032,0,0],b.B['Tail_2']+[q*.065,0,.035],[q*.098,1.211,-.874+abs(q)*.024]];b.tube('Violet tail feather '+str(q),pts,[.061,.076,.006],[.026,.022,.004],['Tail_1','Tail_2','Tail_2'],[1,2,3,4,3,2,1,2],sides=8)
b.tube('Body tail attachment',[[0,1.265,-.17],b.B['Tail_1']],[.133,.126],[.09,.062],['Root_M','Tail_1'],1,sides=8)
b.write('duskquill')
manifest=dict(name='Duskquill Raven',native_chassis='crowC',reference_renderer=120964,reference_binding_representative=120960,renderer_path='enCrow',native_surface_copied=False,art_status='Original bird surfaces, studio review pending',native_forward='Up+Y/front+Z, source camera/anatomy checked',remaining=['Studio/nativepose review','Original live body/portrait/death/cleanup validation'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
