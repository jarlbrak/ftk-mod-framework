"""Original parametric Vesper Eye surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['1C3838','38645A','648777','503C31','836047','806139','BE985B','E5D8B5','141D20','632B3D','B15A6B','E9BF62']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'verdigrin_basecolor.png')
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
b=Surface(121192)
def box(label,center,size,bone,color):
 start=len(b.data['positions']);c=np.array(center);s=np.array(size)/2
 vertices=[c+s*np.array(v)for v in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 faces=[(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]
 for k,(a,c,d,e) in enumerate(faces):
  col=color[k%len(color)] if isinstance(color,list)else color;b.triangle([vertices[a],vertices[c],vertices[d]],[bone]*3,col);b.triangle([vertices[a],vertices[d],vertices[e]],[bone]*3,col)
 b.pieces.append(dict(name=label,vertex_start=start,vertex_count=len(b.data['positions'])-start))
# Separate base and hollow lower chest, not a solid calibration box.
box('Root base plinth',(0,.13,0),(1.64,.22,1.12),'Root_M',[3,5,3,3,5,6])
box('Inner mouth floor',(0,.27,.0),(1.43,.11,1.01),'mid',9)
box('Front wood wall',(0,.54,.51),(1.52,.51,.13),'mid',[3,4,3,3,3,4])
box('Back wood wall',(0,.52,-.46),(1.52,.47,.12),'mid',3)
for sign in [-1,1]:
 box('Teal side wall '+str(sign),(sign*.71,.55,.025),(.13,.56,1.03),'mid',[0,1,0,1,0,2])
 box('Front corner brass '+str(sign),(sign*.685,.55,.59),(.10,.56,.07),'mid',6)
 box('Plinth brass foot '+str(sign),(sign*.63,.067,.40),(.24,.10,.25),'Root_M',5)
 # Broad painted panels on front shell with recessed wood gaps.
 box('Front teal panel '+str(sign),(sign*.325,.55,.584),(.48,.32,.045),'mid',[0,1,0,1,0,2])
box('Lower mouth brass rim',(0,.837,.50),(1.57,.085,.17),'mid',6)
for sign in [-1,1]:box('Side mouth brass rim '+str(sign),(sign*.73,.837,.04),(.12,.085,.93),'mid',6)
# Lid is upright in exact native bind pose and carries no mesh bridge to mid.
box('Hinged wood lid',(0,1.405,-.55),(1.55,1.01,.15),'lidHinge',[3,4,3,3,3,4])
box('Lid inner mouth',(0,1.40,-.461),(1.35,.80,.04),'lidHinge',9)
for sign in [-1,1]:
 box('Lid side brass '+str(sign),(sign*.731,1.42,-.441),(.105,1.035,.09),'lidHinge',6)
 box('Lid face teal inlay '+str(sign),(sign*.36,1.435,-.43),(.56,.67,.04),'lidHinge',1)
box('Lid top brass',(0,1.909,-.475),(1.56,.087,.15),'lidHinge',6)
box('Lid hinge beam',(0,.927,-.53),(1.57,.095,.23),'lidHinge',5)
# Expressive eyes on the inner lid; all fixed to actual lidHinge, not unused lidTop.
for sign in [-1,1]:
 b.ellipsoid('Dark lid eye '+str(sign),(sign*.30,1.49,-.386),(.17,.095,.037),'lidHinge',8)
 b.ellipsoid('Amber lid eye '+str(sign),(sign*.30,1.49,-.35),(.095,.052,.015),'lidHinge',11)
 b.tube('Brass brow '+str(sign),[(sign*.13,1.63,-.385),(sign*.46,1.61,-.385)],[.02,.021],[.032,.035],['lidHinge']*2,6,axis=(0,0,1),sides=6)
# Purposeful irregular ivory teeth occupy native front rim; each rigid to its jaw.
for k,x in enumerate([-.62,-.40,-.19,.06,.29,.52]):
 height=[.20,.25,.18,.24,.19,.22][k]
 b.tube('Lower tooth '+str(k),[(x,.858,.48),(x*.96,.858+height,.43)],[.069,.008],[.064,.008],['mid']*2,[6,7,7,6,7,7],sides=6)
 b.tube('Upper tooth '+str(k),[(x,1.855,-.39),(x*.97,1.79,-.17)],[.066,.007],[.061,.007],['lidHinge']*2,[6,7,7,6,7,7],sides=6)
# Articulated original tongue follows native vertical rest chain, not posed forward.
points=[(0,.72,.05),(0,1.03,.055),(.015,1.33,.07),(.015,1.63,.06),(0,1.87,.04)]
b.tube('Wine colored tongue',points,[.12,.13,.115,.093,.038],[.075,.075,.064,.050,.025],['tongueBase','tongue1','tongue2','tongue3','tongueTip'],[9,10,10,9,9,10,10,9])
b.tube('Tongue central crease',[(0,.91,.123),(0,1.15,.131),(.014,1.40,.13),(.014,1.62,.113)],[.008]*4,[.008]*4,['tongueBase','tongue1','tongue2','tongue3'],9,sides=6)
box('Front brass lock',(0,.56,.634),(.18,.21,.063),'mid',6)
box('Dark keyhole',(0,.58,.672),(.045,.087,.012),'mid',8)
b.write('verdigrin')
manifest=dict(name='Verdigrin Coffer',native_chassis='mimicA',reference_renderer=121192,renderer_path='mimic01',native_surface_copied=False,art_status='Original hinged mimic; studio/live review pending',native_forward='+UnityZ from mid/front surface and rear upright lid envelope',rig_boundary='Exact9-joint palette/IBMs. Lid follows weighted lidHinge, unusedlidTopunweighted; lowerchestmid/baseRoot; tonguefollowsnativeverticalrestchain. No meshbridge acrosshinge.',live_baseline='No indexed mimic baseline found when authoring; pending separately',remaining=['Studio review','Native mimicA baseline','Exact authored assignment','Native lid/tongue attacks/hit/death/material/culling/cleanup'])
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
