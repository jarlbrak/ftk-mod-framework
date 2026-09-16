"""Original parametric Thistlewick Hexer surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['263E30','3C5940','58734C','819363','D7C7A1','F0E2BE','A89168','A36238','D39758','24272A','504638','101A19']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'thistlewick_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/scourge-leprechaun-topology-analysis/reference-121222/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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
b=Surface(121222)
b.tube('Moss coat body',[[0,.52,.065],b.B['Root_M'],b.B['BackA_M'],b.B['BackB_M'],b.B['Chest_M'],[0,.94,.045]],[.185,.18,.14,.145,.185,.20],[.135,.14,.11,.115,.14,.13],['Root_M','Root_M','BackA_M','BackB_M','Chest_M','Chest_M'],[0,1,2,1,0,1,2,1],sides=10)
b.tube('Visible carved neck',[b.B['Chest_M'],b.B['Neck_M'],b.B['Head_M']],[.065,.072,.085],[.07,.075,.085],['Chest_M','Neck_M','Head_M'],4,sides=8)
b.ellipsoid('Pale carved imp face',[0,1.245,.09],[.16,.225,.155],'Head_M',[4,5,4,6,4,5,4,6,4,5])
b.tube('Long angular wooden nose',[[0,1.27,.205],[0,1.235,.29],[0,1.20,.354]],[.052,.047,.014],[.045,.035,.016],['Head_M']*3,5,sides=6)
for sign in [-1,1]:
 b.ellipsoid('Inset dark eye '+str(sign),[sign*.088,1.304,.209],[.041,.027,.024],'Head_M',11)
 b.tube('Carved angled brow '+str(sign),[[sign*.035,1.348,.209],[sign*.108,1.355,.198],[sign*.153,1.319,.171]],[.023,.027,.009],[.023]*3,['Head_M']*3,6,sides=5)
 b.tube('Pointed wooden ear '+str(sign),[[sign*.13,1.30,.06],[sign*.21,1.32,.03],[sign*.27,1.385,.035]],[.054,.036,.005],[.03,.022,.005],['Head_M']*3,4,sides=6)
 b.tube('Split angular collar '+str(sign),[[sign*.025,1.01,.10],[sign*.11,.94,.145],[sign*.15,.88,.11]],[.045,.051,.015],[.035]*3,['Chest_M']*3,2,sides=5)
for side in ['R','L']:
 sign=1 if side=='R'else -1
 b.tube('Blended shoulder yoke '+side,[b.B['Chest_M']+[sign*.06,.07,0],b.B['Scapula_'+side],b.B['Shoulder_'+side]],[.11,.105,.10],[.11,.10,.095],['Chest_M','Scapula_'+side,'Shoulder_'+side],[0,1,2,1,0,1,2,1],axis=(0,1,0),sides=8)
 names=['Shoulder_'+side,'Elbow_'+side,'Wrist_'+side];pts=[b.B[n]for n in names]
 b.tube('Long articulated coat sleeve '+side,pts,[.10,.072,.072],[.095,.071,.068],names,[0,1,2,1,0,1,2,1],axis=(0,1,0),sides=8)
 wrist=b.B[names[-1]];elbow=b.B[names[-2]];p=wrist*.86+elbow*.14
 b.tube('Copper sleeve cuff '+side,[p,wrist],[.082,.086],[.080,.084],[{'Elbow_'+side:.14,'Wrist_'+side:.86},'Wrist_'+side],7,axis=(0,1,0),sides=8)
 b.tube('Carved palm '+side,[wrist,b.B['MiddleFinger1_'+side]],[.048,.047],[.056,.05],['Wrist_'+side,'MiddleFinger1_'+side],4,axis=(0,1,0),sides=8)
 for finger in ['MiddleFinger','ThumbFinger']:
  ns=[finger+str(i)+'_'+side for i in [1,2,3]];points=[b.B[n]for n in ns];b.tube('Long articulated '+finger+' '+side,points,[.030,.025,.012],[.026,.023,.012],ns,[4,5,4,6,4,5],axis=(0,1,0),sides=6)
 ns=['Hip_'+side,'Knee_'+side,'Ankle_'+side];b.tube('Charcoal leg '+side,[b.B[n]for n in ns],[.078,.057,.047],[.082,.06,.049],ns,9,sides=8)
 toe=b.B['MiddleToe1_'+side];ankle=b.B['Ankle_'+side]
 b.tube('Dark pointed boot '+side,[ankle,[toe[0],.045,.12],[toe[0],.032,.25]],[.065,.075,.012],[.060,.030,.017],['Ankle_'+side,'MiddleToe1_'+side,'MiddleToe2_L' if side=='L' else 'MiddleToe1_R'],10,sides=8)
# Copper center fastening follows local trunk joints; no rigid span across moving torso.
for y,n in [(.63,'BackA_M'),(.74,'BackB_M'),(.84,'Chest_M')]:b.ellipsoid('Copper coat clasp '+n,[0,y,.195],[.026,.024,.014],n,8)
b.write('thistlewick')
