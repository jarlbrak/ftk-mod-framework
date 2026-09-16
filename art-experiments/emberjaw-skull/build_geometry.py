"""Original parametric Emberjaw surfaces; native data supplies binding landmarks only."""
import json,math,sys,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate
PALETTE=['171D25','293442','495261','AFAA96','D1C6AC','E8DBC1','F7E9CA','0D1017','8F3C24','EF852A','FFD171','756655']
image=Image.new('RGB',(384,32))
for i,h in enumerate(PALETTE):image.paste(tuple(int(h[k:k+2],16) for k in (0,2,4)),(i*32,0,(i+1)*32,32))
image.save(OUT/'emberjaw_basecolor.png')
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
top=Surface(121577);bottom=Surface(121483)
# Original angular cranium: volume and mask plates, not a trace of native triangles.
top.ellipsoid('Obsidian cranium',(0,2.15,-.08),(.66,.70,.63),'Root_M',[0,1,2,1,0,0,1,2])
top.ellipsoid('Ivory forehead',(0,2.24,.43),(.57,.43,.255),'Root_M',[3,4,5,4,3,4,5,5])
# Pair of purposeful backward-curving horns, fitted below the native crown bound.
for side in [-1,1]:
 top.tube('Curved obsidian horn '+str(side),[(side*.44,2.53,-.19),(side*.66,2.73,-.32),(side*.73,2.98,-.40),(side*.60,3.095,-.44)],[.19,.145,.082,.014],[.17,.13,.068,.013],['Root_M']*4,[0,1,2,1,0,0,1,1],axis=(0,0,1),sides=8)
 # A warm mineral seam around each horn base instead of decorative spikes.
 top.tube('Horn mineral collar '+str(side),[(side*.44,2.53,-.19),(side*.49,2.58,-.22)],[.196,.181],[.176,.164],['Root_M']*2,[8,9,8,8,8,9,8,8],axis=(0,0,1),sides=8)
# Low-poly socket rings: original polygon contours, each rim weighted to native eye landmarks.
for side,label in [(-1,'L'),(1,'R')]:
 center=np.array([side*.365,1.79,.734]);outline=[(-.22,.06),(-.12,.205),(.15,.18),(.255,.045),(.21,-.13),(-.08,-.20)]
 # Choose rim joints by spatial quadrant; Root and eye root remain in the full palette.
 bones=[f'{label}_Eye_4',f'{label}_Eye_5',f'{label}_Eye_1',f'{label}_Eye_2',f'{label}_Eye_3',f'{label}_Eye_4']
 ring=[center+np.array([side*x,y,0]) for x,y in outline];ring.append(ring[0]);bones.append(bones[0])
 # Bone-colored brow/cheek rim with dark inset cavity behind it.
 top.tube('Ivory socket rim '+label,ring,[.073]*7,[.047]*7,bones,[4,5,6,5,4,3,3,4],axis=(0,0,1),sides=8)
 top.ellipsoid('Deep eye socket '+label,(side*.385,1.80,.721),(.23,.19,.055),f'{label}_Eye',7)
 top.ellipsoid('Ember iris '+label,(side*.385,1.795,.78),(.068,.093,.018),f'{label}_Eye',9)
 top.ellipsoid('Hot pupil '+label,(side*.385,1.798,.8),(.019,.071,.011),f'{label}_Eye',10)
 top.tube('Cheek pillar '+label,[(side*.57,1.69,.60),(side*.60,1.51,.68),(side*.44,1.40,.86)],[.12,.11,.115],[.13,.085,.07],['Root_M']*3,[3,4,5,4,3,3,4,4],axis=(0,0,1))
# Central nasal bridge and triangular dark cavity.
top.tube('Nasal bridge',[(0,2.05,.68),(0,1.82,.80),(0,1.57,.91)],[.115,.087,.10],[.09,.075,.038],['Root_M']*3,[4,5,6,5,4,3,4,5],axis=(1,0,0))
top.triangle([[-.12,1.45,.922],[.12,1.45,.922],[0,1.66,.925]],['Upper_Lip']*3,7)
top.tube('Upper dental arch',[(-.46,1.42,.83),(-.25,1.36,.94),(0,1.355,.98),(.25,1.36,.94),(.46,1.42,.83)],[.075]*5,[.075]*5,['L_UpperLip','L_UpperLip','M_UpperLip','R_UpperLip','R_UpperLip'],[3,4,5,4,3,3,4,4],axis=(0,1,0))
for x in [-.38,-.255,-.13,0,.13,.255,.38]:
 bone='L_UpperLip' if x<-.09 else 'R_UpperLip' if x>.09 else 'M_UpperLip';z=.95-.20*abs(x);tip=1.085 if abs(x)>.3 else 1.15
 top.tube('Upper tooth '+str(x),[(x,1.36,z),(x,tip,z+.025)],[.055,.034],[.049,.028],[bone]*2,[4,5,6,5,4,4,5,5],sides=5)
# Deliberate angular fissure crosses the forehead. Tiny raised inlays sit on the original mask.
for a,b in [((-.25,2.54,.60),(-.13,2.38,.687)),((-.13,2.38,.687),(-.19,2.26,.71)),((-.19,2.26,.71),(-.075,2.10,.69)),((-.13,2.38,.687),(.035,2.38,.713))]:
 top.tube('Dark forehead fissure',[a,b],[.023,.018],[.012,.011],['Root_M']*2,7,sides=4)
 top.tube('Ember in crack',[np.array(a)+[0,0,.012],np.array(b)+[0,0,.012]],[.008,.006],[.006,.005],['Root_M']*2,8,sides=4)
# Separate hinged jaw in its own renderer, with continuous chin and authored bone blends.
pts=[(-.56,1.43,.19),(-.57,1.16,.43),(-.49,.94,.66),(-.28,.80,.80),(0,.72,.827),(.28,.80,.80),(.49,.94,.66),(.57,1.16,.43),(.56,1.43,.19)]
skin=['Jaw_Bone',{'Jaw_Bone':.65,'L_LowerLip':.35},'L_LowerLip',{'L_LowerLip':.65,'M_LowerLip':.35},'M_LowerLip',{'R_LowerLip':.65,'M_LowerLip':.35},'R_LowerLip',{'Jaw_Bone':.65,'R_LowerLip':.35},'Jaw_Bone']
bottom.tube('Angular ivory mandible',pts,[.10,.115,.115,.125,.14,.125,.115,.115,.10],[.09,.085,.07,.075,.085,.075,.07,.085,.09],skin,[3,4,5,4,3,3,4,5],axis=(0,1,0),sides=8)
bottom.tube('Obsidian chin inset',[(-.32,.74,.81),(0,.65,.85),(.32,.74,.81)],[.054,.065,.054],[.041,.042,.041],['L_LowerLip','M_LowerLip','R_LowerLip'],[0,1,2,1,0,0,1,1],axis=(0,1,0),sides=6)
for x in [-.36,-.235,-.115,.115,.235,.36]:
 y=.89+.20*abs(x);z=.83-.18*abs(x);bone='L_LowerLip' if x<-.1 else 'R_LowerLip' if x>.1 else 'M_LowerLip'
 bottom.tube('Lower tooth '+str(x),[(x,y,z),(x,y+.16,z+.015)],[.049,.029],[.045,.027],[bone]*2,[4,5,6,5,4,4,5,5],sides=5)
# Three small warm fissures in the chin tie the separate part into the crown palette.
for x in [-.12,0,.12]:bottom.tube('Chin ember seam',[(x,.635+.2*abs(x),.895),(x+.035,.71+.2*abs(x),.905)],[.01,.008],[.005,.005],['M_LowerLip']*2,8,sides=4)
top.write('emberjaw_top');bottom.write('emberjaw_bottom')
manifest={'name':'Emberjaw','native_chassis':'skullA','art_status':'Original multipart surface; studio/live review pending','native_surface_copied':False,'assignments':[{'renderer_path':'ChaosSkullBottom','reference_renderer':121483,'glb':'emberjaw_bottom.glb','texture':'emberjaw_basecolor.png'},{'renderer_path':'ChaosSkullTop','reference_renderer':121577,'glb':'emberjaw_top.glb','texture':'emberjaw_basecolor.png'}],'weight_rationale':'Cranium/horns rigid to Root_M; original socket rim vertices use native eye landmark joints, cavity/iris their eye root; upper arch/teeth use upper lip bones; jaw sides interpolate Jaw_Bone and corresponding lower lip, chin uses M_LowerLip. No nearest-surface weight transfer.','remaining':['Studio review','Live exact two-renderer binding','Native idle/attack/hit/death and animated culling/material review']}
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
