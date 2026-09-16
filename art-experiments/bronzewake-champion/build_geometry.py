"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['172F39','24515A','437B7D','684733','A37143','CEA36A','E4C896','674A43','A37B64','C59E7D','292C39','101824']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'bronzewake_basecolor.png')
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
body=Surface(121272);hair=Surface(121500);armor=Surface(121522);boots=Surface(121661)
# Original form dimensions are authored in native bind space; no native vertices used.
body.ellipsoid('Angular unhelmeted head',(0,1.833,.137),(.17,.175,.19),'Head_M',[8,9,8,7,9,8,9,8,7,8])
body.tube('Neck',[(0,1.59,.06),(0,1.72,.07)],[.084,.09],[.082,.086],['Neck_M','Head_M'],8)
for side,sign in [('R',1),('L',-1)]:
 S='Shoulder_'+side;E='Elbow_'+side;W='Wrist_'+side;F='MiddleFinger1_'+side
 body.tube('Bare articulated arm '+side,[(sign*x,y,z) for x,y,z in [(.23,1.565,.032),(.39,1.565,.033),(.575,1.565,.035),(.65,1.565,.038),(.84,1.565,.044),(.93,1.565,.048)]],[.105,.11,.09,.092,.077,.06],[.105,.11,.09,.092,.077,.06],[S,S,{S:.15,E:.85},E,E,W],[8,9,8,7,8,9,9,8],axis=(0,0,1))
 body.tube('Closed weapon grip '+side,[(sign*.925,1.563,.08),(sign*1.04,1.548,.115),(sign*1.14,1.535,.14)],[.075,.079,.06],[.068,.063,.05],[W,F,F],8,axis=(0,0,1))
 body.tube('Thumb '+side,[(sign*.953,1.575,.11),(sign*1.005,1.55,.185)],[.04,.037],[.035,.03],['ThumbFinger1_'+side,'ThumbFinger2_'+side],9)
 # Matte dark brows and eyes remain readable if the retained helmet exposes them.
 body.ellipsoid('Eye slit '+side,(sign*.065,1.863,.313),(.045,.016,.010),'Head_M',11)
 body.tube('Eyebrow '+side,[(sign*.02,1.893,.301),(sign*.107,1.889,.294)],[.018,.017],[.02,.018],['Head_M']*2,10)
body.ellipsoid('Nose',(0,1.823,.325),(.034,.045,.045),'Head_M',9)
body.tube('Short beard',[(0,1.778,.287),(0,1.705,.222)],[.094,.051],[.032,.032],['Head_M']*2,10)
# Hair is compact nape and short paired braids, preserving the native helmet clearance.
hair.ellipsoid('Dark nape hair',(0,1.80,-.054),(.17,.14,.075),'Head_M',[10,11,10,10,11,10,10,11])
for side in [-1,1]:
 hair.tube('Side braid '+str(side),[(side*.172,1.88,.02),(side*.191,1.79,.02),(side*.178,1.685,.037)],[.027,.032,.022],[.03,.03,.024],['Head_M']*3,[10,1,10,11,10,1])
 hair.ellipsoid('Bronze braid clasp '+str(side),(side*.179,1.708,.039),(.033,.019,.031),'Head_M',5)
# Soft undersuit follows trunk/hips; individual lamellar plates follow local spine.
armor.tube('Teal fitted torso',[(0,1.00,.06),(0,1.14,.065),(0,1.31,.066),(0,1.49,.06),(0,1.58,.05)],[.20,.205,.245,.285,.22],[.14,.15,.17,.175,.15],['Root_M','BackA_M','BackB_M','Chest_M','Chest_M'],[0,1,1,2,1,0,1,1])
for level,(y,w,bone) in enumerate([(1.13,.188,'BackA_M'),(1.26,.22,'BackB_M'),(1.39,.248,'Chest_M'),(1.51,.235,'Chest_M')]):
 for sign in [-1,1]:
  armor.tube('Bronze lamella %s %s'%(level,sign),[(sign*.018,y,.235),(sign*w,y-.025,.204)],[.018,.018],[.061,.059],[bone]*2,[4,5,4,3,4,5],axis=(0,0,1),sides=6)
armor.ellipsoid('Sun disk breast clasp',(0,1.48,.252),(.055,.061,.022),'Chest_M',6)
armor.tube('Bronze belt',[(0,1.025,.066),(0,1.085,.066)],[.214,.212],[.153,.154],['Root_M']*2,[4,5,4,3,4,5,4,3])
for side,sign in [('R',1),('L',-1)]:
 H='Hip_'+side;K='Knee_'+side;S='Shoulder_'+side;E='Elbow_'+side
 armor.tube('Trousers '+side,[(sign*.20,1.015,.067),(sign*.203,.80,.08),(sign*.203,.56,.095),(sign*.203,.40,.081)],[.13,.125,.105,.085],[.13,.115,.10,.09],[H,H,{H:.12,K:.88},K],[0,1,0,1,2,1,0,1])
 armor.tube('Shoulder mantle '+side,[(sign*.22,1.565,.031),(sign*.31,1.568,.032),(sign*.405,1.565,.033)],[.123,.128,.109],[.12,.128,.10],[S]*3,[4,5,4,3,4,5,4,3],axis=(0,0,1))
 armor.tube('Upper arm teal band '+side,[(sign*.46,1.565,.034),(sign*.52,1.565,.034)],[.105,.101],[.103,.099],[S,S],[0,2,1,0],axis=(0,0,1))
 # Tassets follow their own hips, avoiding bridges across independently moving legs.
 armor.tube('Hip tasset '+side,[(sign*.20,.99,.211),(sign*.20,.89,.21),(sign*.20,.77,.202)],[.123,.13,.10],[.032,.035,.028],[H]*3,[3,4,5,4,3,4,5,4])
 boots.tube('Teal boot shaft '+side,[(sign*.203,.53,.093),(sign*.203,.36,.08),(sign*.203,.16,.068)],[.106,.094,.087],[.10,.098,.11],[K,K,'Ankle_'+side],[0,1,2,1,0,1,2,1])
 boots.tube('Bronze shin plate '+side,[(sign*.203,.50,.188),(sign*.203,.35,.179),(sign*.203,.20,.165)],[.082,.077,.066],[.019,.02,.017],[K,K,'Ankle_'+side],[4,5,4,3,4,5,4,3])
 boots.tube('Sabatons '+side,[(sign*.203,.085,.07),(sign*.203,.083,.19),(sign*.203,.076,.32)],[.104,.111,.091],[.081,.073,.06],['Ankle_'+side,'MiddleToe1_'+side,'MiddleToe1_'+side],[3,4,5,4,3,4,5,4],axis=(1,0,0))
for obj,name in [(body,'bronzewake_body'),(hair,'bronzewake_hair'),(armor,'bronzewake_armor'),(boots,'bronzewake_boots')]:obj.write(name)
manifest=dict(name='Bronzewake Champion',native_chassis='bossGladiator',native_surface_copied=False,art_status='Original four-part champion; studio/live review pending',native_forward='+UnityZ, established by toe chain and native facial extent',assignments=[dict(renderer_path=p,reference_renderer=r,glb=n+'.glb',texture='bronzewake_basecolor.png') for p,r,n in [('enBossGladiator',121272,'bronzewake_body'),('hairBottomBossGladiator',121500,'bronzewake_hair'),('armorBossGladiator',121522,'bronzewake_armor'),('bootsBossGladiator',121661,'bronzewake_boots')]],accessory_boundary='Native helmet and weapon/shield rigid accessories retained; studio excludes proprietary accessories; no automatic rigid replacement',remaining=['Studio review','Live native accessory intersections','Live attacks/hit/ragdoll/materials/culling'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
