"""Original parametric Basilight Cockatrice surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['173C37','24574C','397B68','65A08A','99B79D','BA8638','E6B452','F0E6C4','D0C9AF','733F43','A65C55','101B20']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'basilight_basecolor.png')
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
b=Surface(121328)
# Continuous original torso rings follow the four native trunk pivots.
b.tube('Jade scaled torso',[[0,2.52,-1.15],b.B['Root_M'],b.B['BackA_M'],b.B['BackB_M'],b.B['Chest_M']],[.36,.72,.89,.79,.48],[.40,.67,.75,.72,.48],['Root_M','Root_M','BackA_M','BackB_M','Chest_M'],[0,1,2,3,2,1,0,1],sides=12)
b.tube('Blended upright neck',[b.B['Chest_M'],(b.B['Chest_M']+b.B['Neck_M'])/2,b.B['Neck_M'],(b.B['Neck_M']+b.B['Head_M'])/2,b.B['Head_M']],[.48,.43,.36,.31,.34],[.46,.42,.35,.30,.32],['Chest_M',{'Chest_M':.5,'Neck_M':.5},'Neck_M',{'Neck_M':.5,'Head_M':.5},'Head_M'],[1,2,3,4,3,2,1,2],sides=10)
b.ellipsoid('Dragon bird head',[0,3.68,1.95],[.40,.39,.46],'Head_M',[0,1,2,3,2,1,0,1])
b.tube('Ivory hooked upper beak',[[0,3.72,2.21],[0,3.69,2.57],[0,3.51,2.74],[0,3.43,2.72]],[.26,.19,.045,.013],[.19,.14,.06,.017],['Head_M']*4,[7,8,7,8,7,8,7,8],sides=10)
b.tube('Articulated lower beak',[b.B['Jaw_M'],[0,3.48,2.41],[0,3.43,2.64]],[.22,.17,.02],[.105,.07,.025],['Jaw_M',{'Jaw_M':.35,'JawEnd_M':.65},'JawEnd_M'],8,sides=8)
b.tube('Dark mouth throat',[b.B['Jaw_M']+[0,.01,-.12],[0,3.57,2.30],[0,3.49,2.52]],[.19,.16,.07],[.10,.027,.016],[{'Head_M':.6,'Jaw_M':.4},{'Head_M':.3,'Jaw_M':.7},'Jaw_M'],11,sides=8)
for sign in [-1,1]:
 b.ellipsoid('Amber eye '+str(sign),[sign*.348,3.79,2.125],[.072,.10,.14],'Head_M',6)
 b.ellipsoid('Dark slit pupil '+str(sign),[sign*.409,3.797,2.161],[.018,.058,.045],'Head_M',11)
 b.tube('Ivory brow '+str(sign),[[sign*.29,3.89,2.24],[sign*.40,3.90,2.08],[sign*.36,3.88,1.92]],[.043,.055,.021],[.045,.045,.022],['Head_M']*3,8,sides=5)
# Three deliberate amber crest vanes, rigid to head, not detached spikes.
for q in range(3):
 z=1.62+q*.25;b.tube('Amber crest vane '+str(q),[[0,3.89,z],[0,4.22+(2-q)*.18,z-.08],[0,4.25+(2-q)*.18,z-.28]],[.13,.065,.016],[.19,.13,.02],['Head_M']*3,[5,6,5,6,5,6],sides=6)
for side,sign in [('R',1),('L',-1)]:
 arm=['Scapula_'+side,'Shoulder_'+side,'Elbow_'+side,'Wrist_'+side,'IndexFinger1_'+side,'IndexFinger2_'+side]
 pts=[b.B[n] for n in arm]
 # Narrow leading edge and broad original membrane loft follow every direct arm segment.
 b.tube(side+' articulated jade wing',pts,[.36,.48,.55,.51,.38,.045],[.21,.16,.12,.09,.055,.025],arm,[0,1,2,3,2,1,0,1],axis=(0,0,1),sides=8)
 b.ellipsoid(side+' shoulder overlap',b.B[arm[1]],(.32,.23,.42),{arm[0]:.35,arm[1]:.65},1)
 ns=[side+'_Wing',('R_Wing_End' if side=='R' else 'L_WingEnd')];pp=[b.B[n]for n in ns]
 b.tube(side+' inner feather vane',[pp[0],(pp[0]+pp[1])/2,pp[1]],[.36,.41,.04],[.10,.06,.018],[ns[0],{ns[0]:.5,ns[1]:.5},ns[1]],[1,2,9,10,9,2,1,2],axis=(1,0,0),sides=8)
 # Discrete overlapping flight feathers follow distal arm bones, never an invented skeleton-spanning strut.
 for k,n in enumerate(arm[2:]):
  start=b.B[n]+[0,-.015,-.015];end=start+[sign*.20,-.03,-(.87-.13*k)]
  b.tube(side+' red tipped flight feather '+str(k),[start,start*.45+end*.55,end],[.21,.25,.014],[.067,.048,.013],[n]*3,[1,2,9,10,9,2,1,2],axis=(1,0,0),sides=8)
 leg=['Hip_'+side,'Knee_'+side,'Ankle_'+side,'MiddleToe1_'+side,'MiddleToe2_'+side]
 b.tube(side+' haunch and scaled shin',[b.B[n]for n in leg],[.36,.27,.12,.115,.04],[.37,.25,.13,.11,.035],leg,[0,1,2,1,0,1,2,1],sides=8)
 for q in [-1,0,1]:
  a=b.B[leg[-2]]+[q*.075,.025,0];c=b.B[leg[-1]]+[q*.17,.015,.08];b.tube(side+' hooked talon '+str(q),[a,(a+c)/2,c],[.058,.05,.013],[.045,.035,.013],[leg[-2],{leg[-2]:.5,leg[-1]:.5},leg[-1]],8,sides=6)
 gn=[side+'_Gobble_01',side+'_Gobble_02',side+'_Gobble_End'];b.tube(side+' muted red throat feathers',[b.B[n]for n in gn],[.10,.15,.025],[.09,.11,.035],gn,[9,10,9,10,9,10],sides=6)
tail=['Root_M','Tail1_M','Tail2_M','Tail3_M','Tail4_M','Tail5_M','Tail5_M 1','Tail6_M','Tail7_M','Tail8_M']
b.tube('Long articulated jade dragon tail',[b.B[n]for n in tail]+[[0,2.49,-6.18]],[.42,.39,.33,.29,.25,.21,.17,.13,.10,.07,.017],[.44,.36,.30,.26,.22,.18,.14,.11,.09,.065,.015],tail+['Tail8_M'],[0,1,2,3,2,1,0,1],sides=8)
for i,n in enumerate(tail[2:]):
 c=b.B[n];seat=[.30,.26,.22,.18,.14,.11,.09,.065][i]*.8;b.tube('Tail amber dorsal scale '+str(i),[c+[0,seat,0],c+[0,seat+.19-i*.012,-.12],c+[0,seat,-.31]],[.09,.03,.02],[.14,.08,.02],[n]*3,5,sides=5)
b.write('basilight')
manifest=dict(name='Basilight Cockatrice',native_chassis='cockatriceC',reference_renderer=121484,reference_binding_representative=121328,renderer_path='enChicken',native_surface_copied=False,art_status='Original bird dragon surfaces; studio review pending',native_forward='Up+Y/front+Z; native head/jaw and source EncounterCam toward+Z',remaining=['Studio/nativepose review','Original live body/portrait/death/cleanup validation'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
