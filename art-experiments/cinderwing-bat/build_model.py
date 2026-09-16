"""Original Cinderwing bat, fitted to batA's bind pose. Blender background script."""
import bpy, json, math, random
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parent
ROOT=OUT.parent.parent
REF=json.loads((ROOT/'scratch/model-reference/bat-a/skeleton.json').read_text())
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
COAT=family('Soot fur','343139');LIGHT=family('Ash fur','615A60');PALE=family('Bone mask','CCBFA2');DARK=family('Deep recess','171721');RUST=family('Copper membrane','B75D3C');EYE=family('Ember eyes','F3AF47',1);TOOTH=family('Fangs','E1D0A7',1)
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

# Original body volumes and mask, aligned to the native horizontal flight bind pose.
ellipsoid('Soot chest',(0,.065,.02),(.165,.29,.14),COAT)
ellipsoid('Tapered abdomen',(0,-.22,.015),(.11,.15,.10),COAT)
ellipsoid('Raised collar',(0,.17,.08),(.19,.125,.14),LIGHT,'Spine_2')
ellipsoid('Angular head',(0,.32,.06),(.155,.155,.125),COAT,'Head')
ellipsoid('Bone muzzle',(0,.43,.025),(.105,.10,.085),PALE,'Head')
ellipsoid('Nose',(0,.51,.055),(.059,.034,.045),DARK,'Head',1)
for s in [-1,1]:
 ellipsoid('Ember eye',(s*.115,.401,.105),(.025,.04,.027),EYE,'Head',1)
 segment('Bone brow',(s*.055,.405,.142),(s*.14,.36,.128),.035,.022,PALE,'Head',5)
 spike('Tall bat ear',(s*.11,.24,.14),(s*.19,.245,.41),.10,COAT,'Head')
 spike('Copper inner ear',(s*.105,.274,.17),(s*.17,.282,.355),.048,RUST,'Head')
 spike('Ivory fang',(s*.065,.458,-.005),(s*.062,.49,-.10),.027,TOOTH,'Head')
 side='Left' if s==1 else 'Right'
 # Original arm and finger ribs follow binding landmarks, never native geometry.
 for a,b,r1,r2 in [('Arm_'+side+'_1','Arm_'+side+'_2',.045,.033),('Arm_'+side+'_2','Hand_'+side,.032,.027)]:
  segment('Wing forearm '+a,B[a],B[b],r1,r2,COAT)
 segment('Thumb',B['Finger_'+side+'_1'],B['Finger_'+side+'_end'],.022,.006,PALE)
 for ray in [[1,2,3,4],[5,6,7,8],[9,10,11,12]]:
  names=[('Wing'+side+'_5' if i==5 else 'Wing_'+side+'_'+str(i)) for i in ray]
  for i in range(3):segment('Copper wing rib',B[names[i]],B[names[i+1]],.022-i*.004,.018-i*.004,PALE)
 # A scalloped, solid membrane with original panel vertices. Thickness prevents backface loss.
 outline=[(.16,.16),(.353,.11),(.81,.332),(1.622,.027),(1.29,-.034),(1.429,-.171),(1.05,-.135),(.89,-.437),(.56,-.28),(.192,-.555),(.10,-.33)]
 center=Vector((s*.64,-.02,-.01));points=[Vector((s*x,y,-.01)) for x,y in outline]
 if s<0:points.reverse()
 # Ensure the upper skin faces +Z. Insert midpoints to provide blended deformation samples.
 from mathutils.geometry import tessellate_polygon
 def tri_refine(a,b,c,depth):
  if depth:
   ab=(a+b)/2;bc=(b+c)/2;ca=(c+a)/2
   return tri_refine(a,ab,ca,depth-1)+tri_refine(ab,b,bc,depth-1)+tri_refine(ca,bc,c,depth-1)+tri_refine(ab,bc,ca,depth-1)
  return [(a,b,c)]
 verts=[];faces=[]
 for tri in tessellate_polygon([points]):
  a,b,c=[points[v] if isinstance(v,int) else v for v in tri]
  if (b-a).cross(c-a).z<0:b,c=c,b
  for aa,bb,cc in tri_refine(a,b,c,2):
   for height,reverse in [(.009,False),(-.009,True)]:
    start=len(verts);verts.extend([v+Vector((0,0,height)) for v in [aa,bb,cc]])
    faces.append((start+2,start+1,start) if reverse else (start,start+1,start+2))
 for i,a in enumerate(points):
  b=points[(i+1)%len(points)];start=len(verts)
  verts.extend([a+Vector((0,0,.009)),a-Vector((0,0,.009)),b-Vector((0,0,.009)),b+Vector((0,0,.009))]);faces.append((start,start+1,start+2,start+3))
 me=bpy.data.meshes.new('Copper wing membrane');me.from_pydata(verts,[],faces);me.update();o=bpy.data.objects.new('Copper wing membrane '+side,me);bpy.context.collection.objects.link(o);finish(o,o.name,RUST)
 for a,b in [('Leg_'+side+'_1','Leg_'+side+'_2'),('Leg_'+side+'_2','Foot_'+side),('Foot_'+side,'Foot_'+side+'_end')]:segment('Rear leg',B[a],B[b],.028,.017,COAT)
 for j in [-1,0,1]:
  p=B['Foot_'+side+'_end']+Vector((j*.02,0,0));spike('Hook claw',p,p+Vector((0,-.06,.015)),.012,PALE,'Foot_'+side)
for a,b in [('Tail_1','Tail_2'),('Tail_2','Tail_end')]:segment('Tapered tail',B[a],B[b],.024,.01,COAT)
pixels=[]
for y in range(256):
 for x in range(256):pixels.extend((*palette[min((y//32)*8+x//32,len(palette)-1)],1))
tex=bpy.data.images.new('Cinderwing palette',width=256,height=256,alpha=True);tex.pixels=pixels;tex.filepath_raw=str(OUT/'cinderwing_basecolor.png');tex.file_format='PNG';tex.save()
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
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'cinderwing-bind.blend'))
# Studio view is bind pose, not animation evidence.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.20));stage=bpy.data.materials.new('Stage');stage.diffuse_color=(.14,.15,.16,1);bpy.context.object.data.materials.append(stage)
def aim(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for loc,power,size in [((3,-4,6),800,4),((-4,-2,4),600,4),((0,4,5),900,3)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.size=size;aim(o,(0,0,0))
bpy.ops.object.camera_add(location=(4,-5,3));cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=3.9;aim(cam,(0,0,0));scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.world.color=(.15,.15,.15);scene.view_settings.view_transform='AgX'
for name,loc in [('hero',(2.8,4.2,4.5)),('top',(0,0,6))]:
 cam.location=loc;aim(cam,(0,0,0));scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('CINDERWING',len(positions),len(triangles))
