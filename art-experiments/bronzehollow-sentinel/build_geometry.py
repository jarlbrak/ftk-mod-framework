"""Original articulated Bronzehollow Sentinel surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['22282D','353E42','505B5C','745039','A47543','CDAD70','DEE1CA','57756E','172023','BD8053','91A092','12181C']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'bronzehollow_basecolor.png')
class Surface:
 def __init__(self,rid):
  self.rid=rid;self.ref=np.load(ROOT/'scratch/skeleton-audit/121217/reference.npz',allow_pickle=False);self.names=self.ref['bone_names'].tolist();self.centers=np.linalg.inv(self.ref['bindposes'])[:,:3,3];self.B=dict(zip(self.names,self.centers));self.data={k:[] for k in ['positions','normals','uvs','triangles','joints','weights']};self.data['bone_names']=self.names;self.pieces=[]
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

b=Surface(121217)
# Every plate is original parametric geometry. Native reference access: binding landmarks only.
# Articulated dark stone core with separated bronze rib plates, not a continuous rubber torso.
for n,width,depth in [('Root_M',.20,.12),('BackA_M',.12,.10),('BackB_M',.14,.105),('Chest_M',.22,.12)]:
 c=b.B[n];b.ellipsoid('Faceted vertebral core '+n,c,[width,.085,depth],n,[0,1,2,1,0,1,0,1,2,1])
# Rib arches attached to distinct native spine bones. Open central gaps preserve skeletal silhouette.
for n,y,width in [('BackA_M',1.12,.19),('BackB_M',1.24,.24),('Chest_M',1.38,.29),('Chest_M',1.49,.27)]:
 for sg in [-1,1]:
  pts=[[sg*.035,y,.23],[sg*width*.70,y+.012,.21],[sg*width,y+.03,.10],[sg*width*.80,y+.045,-.075],[sg*.045,y+.04,-.10]]
  b.tube('Bronze rib arch '+n+str(y)+str(sg),pts,[.028]*5,[.037]*5,[n]*5,[3,4,5,4,3,4],axis=(0,1,0),sides=6)
b.tube('Central bronze sternum',[[0,1.29,.223],[0,1.39,.252],[0,1.51,.24]],[.036,.05,.04],[.019,.024,.02],['Chest_M']*3,4,sides=6)
b.ellipsoid('Sternum jade seal',[0,1.43,.276],[.037,.052,.015],'Chest_M',7)
# Deliberately compact head core: native rigid horned helmet stays on top, unchanged.
b.ellipsoid('Stone core beneath retained native helmet',b.B['Head_M']+[0,-.025,-.015],[.105,.085,.09],'Head_M',0)
b.tube('Bronze neck spindle',[b.B['Neck_M']+[0,-.03,0],b.B['Head_M']+[0,-.06,0]],[.065,.06],[.065,.055],['Neck_M']*2,4,sides=8)
for side,sg in [('R',1),('L',-1)]:
 def segment(label,a,c,r1,r2,col,offset=(0,0,0),sides=8):
  aa=b.B[a];cc=b.B[c];delta=cc-aa
  b.tube(label+' '+side,[aa+delta*.12+offset,aa+delta*.45+offset,aa+delta*.87+offset],[r1,r1*1.10,r2],[r1*.82,r1*.88,r2*.82],[a]*3,col,sides=sides)
 b.ellipsoid('Layered stone shoulder '+side,b.B['Shoulder_'+side]+[sg*.02,.015,0],[.145,.125,.145],'Shoulder_'+side,[0,1,2,1,0,1,2,1,0,1])
 b.tube('Bronze shoulder ridge '+side,[b.B['Shoulder_'+side]+[sg*x,.105,0] for x in [-.08,.04,.14]],[.033,.042,.027],[.13,.14,.10],['Shoulder_'+side]*3,4,axis=(0,1,0),sides=6)
 segment('Dark stone upper arm','Shoulder_'+side,'Elbow_'+side,.082,.060,[0,1,2,1,0,1,2,1])
 segment('Bronze bracer','Elbow_'+side,'Wrist_'+side,.094,.067,[3,4,5,4,3,4,3,4])
 b.ellipsoid('Dark elbow hinge '+side,b.B['Elbow_'+side],[.074,.074,.074],'Elbow_'+side,0)
 b.ellipsoid('Stone wrist '+side,b.B['Wrist_'+side],[.06,.055,.055],'Wrist_'+side,1)
 segment('Stone palm','Wrist_'+side,'MiddleFinger1_'+side,.054,.050,1)
 for prefix in ['MiddleFinger','ThumbFinger']:
  for ii in [1,2]:segment('Articulated bronze '+prefix+str(ii),prefix+str(ii)+'_'+side,prefix+str(ii+1)+'_'+side,.024,.021,4,sides=6)
  n=prefix+'3_'+side;c=b.B[n];b.ellipsoid('Finger tip '+n,c,[.035,.025,.027],n,5)
 b.ellipsoid('Stone hip socket '+side,b.B['Hip_'+side]+[0,-.025,0],[.11,.125,.125],'Hip_'+side,1)
 segment('Broad dark thigh','Hip_'+side,'Knee_'+side,.107,.077,[0,1,2,1,0,1,2,1])
 segment('Bronze shin greave','Knee_'+side,'Ankle_'+side,.099,.070,[3,4,5,4,3,4,3,4])
 b.ellipsoid('Stone knee hinge '+side,b.B['Knee_'+side],[.085,.08,.08],'Knee_'+side,0)
 b.ellipsoid('Bronze knee shield '+side,b.B['Knee_'+side]+[0,.015,.075],[.088,.105,.037],'Knee_'+side,4)
 b.tube('Stone instep '+side,[b.B['Ankle_'+side],b.B['MiddleToe1_'+side]],[.081,.087],[.070,.060],['Ankle_'+side]*2,1,sides=8)
 b.tube('Broad stone foot '+side,[b.B['MiddleToe1_'+side],b.B['MiddleToe2_'+side]],[.088,.085],[.032,.032],['MiddleToe1_'+side]*2,[0,1,2,1,0,1,2,1])
 b.ellipsoid('Bronze toe cap '+side,b.B['MiddleToe2_'+side]+[0,.005,.01],[.09,.045,.057],'MiddleToe2_'+side,4)
 # Short hip tassets avoid the native shield and do not bridge hip articulation.
 b.ellipsoid('Bronze hip guard '+side,b.B['Hip_'+side]+[sg*.08,.015,.06],[.10,.13,.055],'Hip_'+side,4)
b.write('bronzehollow')
