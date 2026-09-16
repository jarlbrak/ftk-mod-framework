"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['122D3C','245565','467B8A','769CA8','B3D6D8','E3F0E8','63C6D0','D4FFFF','704434','AD7650','762F41','AB4B5B']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'rimecrown_basecolor.png')
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
hat=Surface(121404);head=Surface(121414);base=Surface(121556);scarf=Surface(121638);body=Surface(121696)
# Independent ice boulders use the native surface's actual dominant joint.
# There is deliberately no Base/BottomBall/Chest connector or shared triangle.
base.tube('Faceted glacier pedestal',[(0,.075,.02),(0,.24,.02),(0,.69,.02),(0,1.07,.02),(0,1.22,.02)],[.70,.91,.85,.61,.37],[.59,.76,.73,.55,.34],['Root_M']*5,[2,3,4,3,2,3,4,5,3,2,3,4],sides=12)
# Original dark strata emphasize mass without thin skeleton struts.
for side in [-1,1]:
 base.tube('Glacier seam '+str(side),[(side*.23,.29,.702),(side*.32,.58,.711),(side*.26,.87,.598)],[.022,.026,.02],[.025,.024,.02],['Root_M']*3,1)
body.ellipsoid('Separate chest ice',(0,1.47,-.06),(.54,.43,.46),'ChestBall',[2,3,4,3,2,3,4,5,3,2])
body.ellipsoid('Heart inset',(0,1.47,.385),(.10,.17,.035),'ChestBall',0)
body.ellipsoid('Amber heart seal',(0,1.47,.423),(.055,.115,.018),'ChestBall',9)
for side,sign in [('R',1),('L',-1)]:
 A=side+'_Arm';E=side+'_Elbow';H=side+'_Hand'
 body.tube('Upper ice arm '+side,[body.B[A],(sign*.70,1.60,.03),body.B[E]],[.115,.126,.087],[.12,.13,.085],[A,A,{A:.15,E:.85}],[1,2,3,2,1,2,4,3],axis=(0,0,1))
 body.tube('Forearm ice '+side,[body.B[E],(sign*1.18,1.56,.069),body.B[H]],[.102,.115,.098],[.098,.11,.093],[E,E,H],[2,3,4,3,2,3,4,3],axis=(0,0,1))
 body.ellipsoid('Broad crystalline palm '+side,(sign*1.46,1.55,.10),(.18,.115,.14),H,[1,2,3,4,3,2,3,4])
 for label,names,points in [('outer',[side+'_Pinky_1',side+'_Pinky_2'],[(sign*1.60,1.55,.015),(sign*1.74,1.49,-.115),(sign*1.84,1.44,-.20)]),('middle',[side+'_Mid']*2,[(sign*1.62,1.53,.11),(sign*1.75,1.46,.14),(sign*1.84,1.40,.17)]),('thumb',[side+'_Thumb_1',side+'_Thumb_2'],[(sign*1.47,1.54,.205),(sign*1.52,1.46,.30),(sign*1.63,1.35,.38)])]:
  body.tube('Ice talon '+side+label,points,[.056,.047,.025],[.056,.045,.025],[names[0],names[1],names[1]],[2,4,5,3,2,4],sides=6)
head.ellipsoid('Stern glacier head',(0,2.25,.22),(.49,.35,.48),'HeadBall',[2,3,4,3,2,3,4,5,3,2])
for sign in [-1,1]:
 head.tube('Dark eye socket '+str(sign),[(sign*.055,2.29,.671),(sign*.30,2.31,.595)],[.04,.038],[.05,.044],['HeadBall']*2,0,axis=(0,0,1),sides=6)
 head.tube('Cyan eye '+str(sign),[(sign*.09,2.297,.707),(sign*.265,2.309,.654)],[.011,.011],[.017,.014],['HeadBall']*2,7,axis=(0,0,1),sides=6)
 head.tube('Carved brow '+str(sign),[(sign*.015,2.37,.65),(sign*.31,2.42,.55)],[.045,.044],[.066,.067],['HeadBall']*2,[3,4,5,4,3,4],axis=(0,0,1),sides=6)
head.tube('Stern carved mouth',[(-.16,2.10,.615),(0,2.075,.657),(.16,2.10,.615)],[.014]*3,[.018]*3,['HeadBall']*3,0,axis=(0,0,1),sides=6)
head.tube('Chiseled nose',[(0,2.30,.66),(0,2.17,.79)],[.044,.05],[.044,.049],['HeadBall']*2,[3,4,5,3,4,5],sides=6)
# Crown is one native Hat piece, intentionally free from HeadBall geometry.
hat.tube('Ice crown circlet',[(0,2.54,.24),(0,2.65,.25),(0,2.72,.27)],[.52,.54,.49],[.47,.48,.43],['Hat']*3,[0,1,2,1,0,1,2,3,1,0,1,2],sides=12)
for x,y,z,w in [(-.40,3.09,.20,.12),(0,3.40,.28,.16),(.40,3.09,.20,.12)]:
 hat.tube('Crown crystal '+str(x),[(x,2.67,z),(x,2.88,z),(x,y,z)],[w,w*.8,.023],[.16,.13,.02],['Hat']*3,[2,4,5,3,2,4],sides=6)
hat.ellipsoid('Crown amber seal',(0,2.66,.715),(.075,.064,.022),'Hat',9)
# Original cloth wraps chest; separate short front/back tails blend only native scarf chains.
scarf.tube('Burgundy neck wrap',[(0,1.83,.01),(0,1.94,.01),(0,2.00,.015)],[.425,.43,.37],[.395,.40,.36],['ChestBall']*3,[10,11,10,10,11,10,10,11],sides=12)
scarf.tube('Front scarf tail',[(0,1.78,.445),(0,1.52,.53),(.04,1.30,.59),(.065,1.06,.66)],[.13,.14,.13,.12],[.035,.034,.031,.026],[{'ChestBall':.35,'F_Scarf_1':.65},'F_Scarf_1','F_Scarf_2','F_Scarf_2'],[10,11,11,10,10,11,10,10])
scarf.tube('Back scarf tail',[(0,1.91,-.37),(0,1.74,-.56),(.025,1.55,-.82)],[.115,.13,.115],[.031,.03,.026],['B_Scarf_1',{'B_Scarf_1':.25,'B_Scarf_2':.75},'B_Scarf_2'],[10,11,10,10,11,10,10,11])
for obj,name in [(hat,'rimecrown_hat'),(head,'rimecrown_head'),(base,'rimecrown_base'),(scarf,'rimecrown_scarf'),(body,'rimecrown_body')]:obj.write(name)
manifest=dict(name='Rimecrown Sentinel',native_chassis='snowmanB',native_surface_copied=False,art_status='Original five-part frost construct; studio/live review pending',native_forward='+UnityZ; front scarf and head surface protrusion verified before authoring',assignments=[dict(renderer_path='SnowMan_Geo/'+p,reference_renderer=r,glb=n+'.glb',texture='rimecrown_basecolor.png') for p,r,n in [('enSnowmanHat',121404,'rimecrown_hat'),('enSnowmanHead',121414,'rimecrown_head'),('enSnowmanBase',121556,'rimecrown_base'),('enSnowmanScarf',121638,'rimecrown_scarf'),('enSnowmanmiddleBody',121696,'rimecrown_body')]],weight_boundary='Base Root_M; chest ChestBall; head HeadBall; crown Hat. No artificial connector across independent body sections; arms and scarf use local weighted native chains.',remaining=['Studio review','Live snowmanB exact five-renderer assignment','Native attacks/hit/death and culling/material review','snowmanA authored-model controller coverage'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
