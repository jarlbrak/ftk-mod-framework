"""Original Ashfang wolf, fitted to wolfA's bind pose. Blender background script."""
import bpy, json, math, random
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
ROOT=OUT.parent.parent
REF=json.loads((ROOT/'scratch/model-reference/wolf-a/skeleton.json').read_text())
B={n:Vector((p[0],-p[2],p[1])) for n,p in zip(REF['bone_names'],REF['bone_positions'])}
random.seed(622)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
ASSET=[];palette=[];materials=[]
def family(name,h,count=4):
 rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)];ids=[]
 for k in range(count):
  srgb=[min(1,v*(.90+k*.06)) for v in rgb];lin=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in srgb]
  m=bpy.data.materials.new(name+str(k));m.diffuse_color=(*lin,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*lin,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9
  ids.append(len(materials));materials.append(m);palette.append(srgb)
 return ids
COAT=family('Charcoal blue','404D59');LIGHT=family('Silver fur','879497');PALE=family('Ivory fur','B9BAA8');DARK=family('Nose and recess','1A2429');RUST=family('Autumn ruff','A76A43');EYE=family('Amber eyes','F2C36C',1);TOOTH=family('Ivory teeth','DDD2AF',1)
def finish(ob,name,col,rigid=None):
 ob.name=name;ob['rigid_bone']=rigid or ''
 for m in materials:ob.data.materials.append(m)
 for p in ob.data.polygons:p.material_index=random.choice(col);p.use_smooth=False
 ASSET.append(ob);return ob

def ellipsoid(name,p,scale,col=COAT,rigid=None,detail=2):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=detail,radius=1,location=p);o=bpy.context.object;o.scale=scale
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,col,rigid)

def segment(name,a,b,r1,r2,col=COAT,rigid=None,sides=8):
 a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
 if u.length<.01:u=axis.cross(Vector((1,0,0)))
 u.normalize();v=axis.cross(u);verts=[]
 for p,r in [(a,r1),(b,r2)]:
  for j in range(sides):
   angle=2*math.pi*j/sides;verts.append(p+r*(math.cos(angle)*u+math.sin(angle)*v))
 faces=[tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
 for j in range(sides):faces.append((j,(j+1)%sides,(j+1)%sides+sides,j+sides))
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);return finish(o,name,col,rigid)

def spike(name,base,tip,r,col=LIGHT,rigid=None):return segment(name,base,tip,r,.005,col,rigid,5)

# Continuous silhouette from overlapping original volumes; organic skin transfer below.
ellipsoid('Lean ribcage',(0,-.10,1.12),(.36,.69,.39))
ellipsoid('Shoulder mass',(0,-.44,1.16),(.42,.34,.44),LIGHT)
ellipsoid('Tucked abdomen',(0,.34,1.04),(.27,.34,.27))
ellipsoid('Haunch core',(0,.60,1.15),(.35,.33,.33))
segment('Rising neck',(0,-.45,1.22),(0,-.90,1.49),.32,.26,LIGHT)
ellipsoid('Angular skull',(0,-1.05,1.47),(.29,.33,.29),LIGHT,'Head_M')
ellipsoid('Cheek mask',(0,-1.24,1.32),(.30,.25,.19),PALE,'Head_M')
ellipsoid('Upper muzzle',(0,-1.48,1.29),(.20,.31,.135),PALE,'Head_M')
ellipsoid('Nose',(0,-1.755,1.315),(.145,.075,.085),DARK,'Head_M',1)
ellipsoid('Dark mouth slit',(0,-1.46,1.168),(.173,.25,.045),DARK,'Jaw_M')
ellipsoid('Lower jaw',(0,-1.46,1.11),(.16,.26,.075),PALE,'Jaw_M')
for s in [-1,1]:
 # Ears are tall planes with inset darker triangles.
 spike('Pointed ear',(s*.205,-.91,1.64),(s*.30,-.84,1.97),.13,COAT,'Head_M')
 spike('Ear inner plane',(s*.205,-.952,1.70),(s*.283,-.886,1.91),.07,RUST,'Head_M')
 ellipsoid('Eye recess',(s*.257,-1.24,1.48),(.048,.096,.073),DARK,'Head_M',1)
 ellipsoid('Amber eye',(s*.286,-1.256,1.49),(.023,.055,.031),EYE,'Head_M',1)
 segment('Severe brow',(s*.26,-1.14,1.56),(s*.21,-1.36,1.53),.065,.045,LIGHT,'Head_M',5)
 spike('Upper fang',(s*.14,-1.52,1.22),(s*.13,-1.55,1.08),.039,TOOTH,'Head_M')
 # Layered ruff points sweep back, leaving the face readable.
 for j in range(5):
  base=(s*(.25+j*.023),-.65+j*.15,1.42-j*.035)
  tip=(s*(.42+j*.012),-.37+j*.18,1.25-j*.05)
  spike('Autumn mane',base,tip,.14,RUST)
 for j in range(3):spike('Cheek fur',(s*.24,-1.03+j*.05,1.28),(s*.37,-.80+j*.065,1.17-j*.035),.095,PALE,'Head_M')
 for label,hip,knee,ankle,ball,thigh,shin in [('Front','frontHip','frontKnee','frontAnkle','frontBall',.155,.085),('Rear','backHip','backKnee','backAnkle','backBall',.21,.085)]:
  suffix='R' if s==1 else 'L';h,k,a,b=[B[n+'_'+suffix] for n in [hip,knee,ankle,ball]]
  ellipsoid(label+' upper joint',h,(thigh,thigh,thigh*1.5),COAT)
  segment(label+' thigh',h,k,thigh,.095,COAT)
  ellipsoid(label+' elbow',k,(.105,.105,.115),LIGHT)
  segment(label+' shin',k,a,shin,.055,LIGHT)
  paw=(a.x,b.y+.06,.08)
  segment(label+' wrist',a,paw,.075,.09,PALE)
  ellipsoid(label+' paw',paw,(.135,.19,.095),PALE,ankle+'_'+suffix)
  for t in [-1,0,1]:
   toe=(paw[0]+t*.072,paw[1]-.12,.063)
   ellipsoid(label+' toe',toe,(.047,.084,.05),PALE,ball+'_'+suffix,1)
   spike(label+' claw',(toe[0],toe[1]-.04,.06),(toe[0],toe[1]-.13,.038),.023,DARK,ball+'_'+suffix)
# Segmented tapered bushy tail follows all four native joints.
for i in range(3):
 a=B['Tail'+str(i)+'_M'];b=B['Tail'+str(i+1)+'_M']
 segment('Tail coat '+str(i),a,b,[.15,.19,.14][i],[.19,.14,.025][i],COAT if i<2 else LIGHT)
for j in range(4):spike('Back ridge',(0,-.3+j*.27,1.48),(0,-.1+j*.30,1.40),.085,COAT)
# Silver breast bib.
ellipsoid('Breast bib',(0,-.67,1.045),(.24,.10,.28),PALE)

pixels=[]
for y in range(256):
 for x in range(256):pixels.extend((*palette[min((y//32)*8+x//32,len(palette)-1)],1))
tex=bpy.data.images.new('Ashfang palette',width=256,height=256,alpha=True);tex.pixels=pixels;tex.filepath_raw=str(OUT/'ashfang_basecolor.png');tex.file_format='PNG';tex.save()
positions=[];normals=[];uvs=[];triangles=[];overrides=[]
bpy.context.view_layer.update()
for ob in ASSET:
 ob.data.calc_loop_triangles();m=ob.matrix_world;nmat=m.to_3x3().inverted().transposed()
 for tri in ob.data.loop_triangles:
  face=ob.data.polygons[tri.polygon_index];n=(nmat@face.normal).normalized();idx=face.material_index;start=len(positions)
  for vi in tri.vertices:
   p=m@ob.data.vertices[vi].co;positions.append([p.x,p.z,-p.y]);normals.append([n.x,n.z,-n.y]);uvs.append([(idx%8+.5)/8,1-(idx//8+.5)/8]);overrides.append(ob['rigid_bone'] or None)
  triangles.append([start,start+1,start+2])
(OUT/'source_unskinned.json').write_text(json.dumps(dict(positions=positions,normals=normals,uvs=uvs,triangles=triangles,rigid_overrides=overrides)))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ashfang-bind.blend'))
# Studio view is bind pose, not animation evidence.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.065));stage=bpy.data.materials.new('Stage');stage.diffuse_color=(.14,.15,.16,1);bpy.context.object.data.materials.append(stage)
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((3,-4,6),800,4),((-4,-2,4),600,4),((0,4,5),900,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.size=size;aim(o,(0,0,1))
bpy.ops.object.camera_add(location=(4,-5,3));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=4.4;aim(cam,(0,0,.9));scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world.color=(.15,.15,.15);scene.view_settings.view_transform='AgX'
for name,loc in [('hero',(4,-5,3)),('side',(5,0,2.7))]:
 cam.location=loc;aim(cam,(0,0,.9));scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('ASHFANG',len(positions),len(triangles))
