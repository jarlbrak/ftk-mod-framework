"""Original Mirewarden geometry fitted to the locally extracted FTK troll bind pose.

Blender X right, Z up, -Y front. Only authored geometry is exported.
Run Blender --background --python this_file.py from any directory.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix

OUT=Path(__file__).resolve().parent
ROOT=OUT.parent.parent
REF=json.loads((ROOT/'scratch/model-reference/skeleton.json').read_text())
BONES=dict(zip(REF['bone_names'],[Vector((p[0],-p[2],p[1])) for p in REF['bone_positions']]))
random.seed(419)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
palette=[]
mats=[]
def linear(v): return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def colors(name,hexcolor,variations=5):
    base=tuple(int(hexcolor[i:i+2],16)/255 for i in (0,2,4))
    ids=[]
    for i in range(variations):
        factor=1+(i-(variations-1)/2)*.055
        srgb=tuple(min(1,c*factor) for c in base)
        rgb=tuple(linear(c) for c in srgb)
        mat=bpy.data.materials.new(name+' '+str(i))
        mat.diffuse_color=(*rgb,1)
        mat.use_nodes=True
        p=mat.node_tree.nodes.get('Principled BSDF')
        p.inputs['Base Color'].default_value=(*rgb,1)
        p.inputs['Roughness'].default_value=.9
        ids.append(len(mats)); mats.append(mat); palette.append(srgb)
    return ids
STONE=colors('Sandstone','A29B82')
LIGHT=colors('Weathered edges','B9B099')
DARK=colors('Deep stone','555949')
MOSS=colors('Moss','69703C')
MOSS_L=colors('Moss tips','84854A')
ROOTS=colors('Roots','61503A')
EYES=colors('Amber','F4BB54',1)
SOCKET=colors('Recess','282D25',1)
ASSET=[]

def finish(ob,name,bone,family):
    ob.name=name
    ob['ftk_bone']=bone
    for m in mats: ob.data.materials.append(m)
    for p in ob.data.polygons:
        p.material_index=random.choice(family)
        p.use_smooth=False
    ASSET.append(ob)
    return ob

def rock(name,loc,scale,bone='Chest_M',family=STONE,detail=2,rot=(0,0,0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=detail,radius=1,location=loc)
    ob=bpy.context.object
    # Restrained variation retains large, purposeful planes.
    for v in ob.data.vertices:
        v.co*=random.uniform(.94,1.06)
    ob.scale=scale
    ob.rotation_euler=tuple(math.radians(x) for x in rot)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if detail==2:
        mod=ob.modifiers.new('Broad facets','DECIMATE');mod.ratio=.43
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(ob,name,bone,family)

def ringmesh(name,rings,bone,family,sides=10):
    verts=[]
    for j,(z,w,d,x,y) in enumerate(rings):
        for i in range(sides):
            a=2*math.pi*i/sides
            verts.append((x+math.cos(a)*w,y+math.sin(a)*d,z))
    faces=[tuple(reversed(range(sides)))]
    for j in range(len(rings)-1):
        for i in range(sides):
            faces.append((j*sides+i,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,(j+1)*sides+i))
    faces.append(tuple(range((len(rings)-1)*sides,len(rings)*sides)))
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    return finish(ob,name,bone,family)

def linkrock(name,a,b,width,depth,bone,family=STONE):
    a,b=Vector(a),Vector(b)
    ob=rock(name,(a+b)/2,(width,depth,(b-a).length*.60),bone,family)
    ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return ob

def tube(name,points,radius,bone,family=ROOTS,sides=6):
    verts=[]
    for i,p in enumerate(points):
        p=Vector(p)
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        tangent.normalize()
        u=tangent.cross(Vector((0,0,1)))
        if u.length<.01:u=tangent.cross(Vector((0,1,0)))
        u.normalize();v=tangent.cross(u)
        for k in range(sides):
            a=k*2*math.pi/sides
            verts.append(p+radius*(math.cos(a)*u+math.sin(a)*v))
    faces=[]
    for i in range(len(points)-1):
        for k in range(sides):faces.append((i*sides+k,i*sides+(k+1)%sides,(i+1)*sides+(k+1)%sides,(i+1)*sides+k))
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    return finish(ob,name,bone,family)

# Torso built in bind pose, with broad sculpted shapes and overlapping joints.
rock('Pelvic foundation',(0,-.08,1.35),(.49,.35,.33),'Root_M',DARK)
rock('Lower abdomen',(0,-.07,1.61),(.43,.34,.32),'BackA_M')
rock('Rib core',(0,-.04,1.96),(.61,.39,.45),'BackB_M',DARK)
rock('Upper back hunch',(0,.13,2.43),(.69,.44,.59),'Chest_M')
rock('Chest central keystone',(0,-.30,2.24),(.49,.24,.46),'Chest_M',LIGHT,rot=(-12,0,0))
for s in [-1,1]:
    rock('Pectoral carved plane',(s*.35,-.32,2.29),(.37,.23,.32),'Chest_M',LIGHT,rot=(0,s*28,s*-8))
    rock('Oblique abdominal plate',(s*.22,-.26,1.91),(.32,.16,.25),'BackB_M',STONE,rot=(0,s*-18,0))
    rock('Lower belly plane',(s*.16,-.26,1.65),(.25,.15,.22),'BackA_M',STONE)

# Head: brow hood, dark recessed face, carved cheeks and jaw.
rock('Head inner core',(0,-.12,2.91),(.35,.29,.43),'Head_M',DARK)
rock('High broken crown',(-.04,.005,3.21),(.34,.31,.31),'Head_M',LIGHT,rot=(0,-12,0))
for s in [-1,1]:
    rock('Stone hood',(s*.255,-.15,3.02),(.20,.31,.37),'Head_M',STONE,rot=(0,s*26,0))
    rock('Deep eye recess',(s*.125,-.389,2.97),(.132,.044,.085),'Head_M',SOCKET)
    rock('Amber eye',(s*.121,-.428,2.973),(.047,.028,.037),'Head_M',EYES)
    rock('Carved brow',(s*.13,-.385,3.07),(.19,.13,.11),'Head_M',LIGHT,rot=(0,s*15,0))
    rock('Cheekbone',(s*.185,-.36,2.87),(.12,.10,.16),'Head_M',STONE,rot=(0,s*-20,0))
    rock('Jaw corner',(s*.11,-.34,2.73),(.14,.115,.115),'Head_M',LIGHT)
rock('Nose bridge',(0,-.422,2.91),(.079,.085,.15),'Head_M',LIGHT)
rock('Nose tip',(0,-.447,2.847),(.10,.075,.058),'Head_M',STONE)
rock('Mouth cavity',(0,-.406,2.785),(.125,.025,.043),'Head_M',SOCKET)
rock('Lower lip',(0,-.415,2.737),(.12,.06,.04),'Head_M',STONE)
rock('Chin',(0,-.346,2.675),(.17,.125,.093),'Head_M',STONE)

# Limbs use the actual FTK T-pose joints; each stone follows its intended bone.
for s,suffix in [(1,'R'),(-1,'L')]:
    shoulder,elbow,wrist=[BONES[x+'_'+suffix] for x in ['Shoulder','Elbow','Wrist']]
    hip,knee,ankle=[BONES[x+'_'+suffix] for x in ['Hip','Knee','Ankle']]
    rock('Shoulder mass '+suffix,shoulder,(.32,.33,.36),'Shoulder_'+suffix,STONE)
    rock('Shoulder ridge '+suffix,shoulder+Vector((s*-.10,.02,.24)),(.33,.35,.28),'Shoulder_'+suffix,LIGHT,rot=(0,s*20,0))
    linkrock('Upper arm '+suffix,shoulder,elbow,.235,.265,'Shoulder_'+suffix)
    rock('Elbow core '+suffix,elbow,(.18,.22,.23),'Elbow_'+suffix,DARK)
    linkrock('Heavy forearm '+suffix,elbow+Vector((s*.08,0,0)),wrist,.30,.29,'Elbow_'+suffix,STONE)
    rock('Forearm upper ridge '+suffix,(elbow+wrist)/2+Vector((0,.04,.16)),(.28,.21,.18),'Elbow_'+suffix,LIGHT)
    palm=wrist+Vector((s*.13,-.03,0))
    rock('Palm '+suffix,palm,(.245,.24,.16),'Wrist_'+suffix)
    # Three broad fingers follow the native shared finger chain; thumb its own chain.
    for j in [-1,0,1]:
        y=j*.13
        f1=BONES['MiddleFinger1_'+suffix]+Vector((0,y,0))
        f2=BONES['MiddleFinger2_'+suffix]+Vector((0,y,0))
        f3=BONES['MiddleFinger3_'+suffix]+Vector((0,y,0))
        linkrock('Finger proximal '+suffix+str(j),f1,f2,.075,.077,'MiddleFinger1_'+suffix)
        linkrock('Finger tip '+suffix+str(j),f2,f3,.068,.069,'MiddleFinger2_'+suffix,LIGHT)
    for a,b in [('ThumbFinger1','ThumbFinger2'),('ThumbFinger2','ThumbFinger3')]:
        linkrock('Thumb '+suffix+a,BONES[a+'_'+suffix],BONES[b+'_'+suffix],.084,.079,a+'_'+suffix,LIGHT)
    linkrock('Thigh '+suffix,hip,knee,.255,.28,'Hip_'+suffix)
    rock('Thigh face '+suffix,(hip+knee)/2+Vector((s*.03,-.17,0)),(.24,.16,.31),'Hip_'+suffix,LIGHT)
    rock('Knee joint '+suffix,knee,(.215,.25,.22),'Knee_'+suffix,DARK)
    rock('Knee brow '+suffix,knee+Vector((0,-.20,.04)),(.23,.17,.17),'Knee_'+suffix)
    linkrock('Shin '+suffix,knee,ankle,.25,.255,'Knee_'+suffix)
    rock('Ankle block '+suffix,ankle,(.24,.27,.24),'Ankle_'+suffix)
    rock('Foot '+suffix,(ankle.x,-.22,.17),(.31,.43,.21),'Ankle_'+suffix,STONE)
    for j in [-1,0,1]:
        rock('Toe '+suffix+str(j),(ankle.x+j*.16,-.52,.11),(.10,.19,.11),'MiddleToe1_'+suffix,LIGHT,detail=1)
    tube('Root around upper arm '+suffix,[shoulder+Vector((s*.21,-.18,.17)),shoulder+Vector((s*.30,-.27,.01)),elbow+Vector((s*-.13,-.25,-.14))],.035,'Shoulder_'+suffix)
    tube('Root hip '+suffix,[(s*.22,-.36,1.42),(s*.43,-.26,1.27),(s*.52,-.07,1.07)],.037,'Hip_'+suffix)

# Organic moss mantle: an irregular draped surface, thick enough to read from the side.
def moss_panel(name,x0,x1,y,z,width,height,bone):
    n=9; verts=[]
    for row in range(4):
        for i in range(n):
            t=i/(n-1); x=x0+(x1-x0)*t
            drop=height*(row/3)
            zig=(.10 if i%2 else -.035)*(row/3)**2
            verts.append((x,y-.075*math.sin(t*math.pi)-.035*row,z-drop+zig+.08*math.sin(t*math.pi)))
    faces=[]
    for row in range(3):
        for i in range(n-1):
            a=row*n+i; faces.extend([(a,a+1,a+n),(a+1,a+n+1,a+n)])
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    finish(ob,name,bone,MOSS)
    mod=ob.modifiers.new('Moss thickness','SOLIDIFY');mod.thickness=.025
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=mod.name)
    return ob
moss_panel('Moss ragged shawl',-.74,-.28,-.265,2.97,.5,.62,'Chest_M')
moss_panel('Moss back drape',-.60,.17,.40,2.89,.5,.65,'Chest_M')
rock('Moss shoulder crown',(-.47,.0,2.99),(.34,.32,.15),'Chest_M',MOSS_L)
rock('Moss ankle',(.33,-.31,.30),(.24,.16,.065),'Ankle_R',MOSS)
for i in range(5):
    x=-.43+i*.075
    tube('Reed stem '+str(i),[(x,.08,2.98),(x-.04,.075,3.18),(x-.065,.09,3.33+(i%3)*.065)],.009,'Chest_M',MOSS_L,sides=4)
    rock('Reed head '+str(i),(x-.065,.09,3.34+(i%3)*.065),(.018,.018,.065),'Chest_M',ROOTS,detail=1)
# Chest waymark stays readable in a small camera view, palette-dark shallow inlay.
tube('Waymark diamond',[(-.09,-.522,2.25),(0,-.543,2.35),(.09,-.522,2.25),(0,-.532,2.15),(-.09,-.522,2.25)],.016,'Chest_M',DARK,sides=4)
tube('Waymark stem',[(0,-.533,2.15),(0,-.525,2.05)],.014,'Chest_M',DARK,sides=4)

# Palette albedo. 8x8 cells, each solid to avoid mip bleeding; UVs sample cell centers.
size=256; pixels=[]
for y in range(size):
    for x in range(size):
        idx=(y//32)*8+x//32
        c=palette[min(idx,len(palette)-1)]
        pixels.extend((*c,1))
tex=bpy.data.images.new('Mirewarden palette',width=size,height=size,alpha=True)
tex.pixels=pixels
tex.filepath_raw=str(OUT/'mirewarden_basecolor.png');tex.file_format='PNG';tex.save()

# Export flattened per-face vertices for stable flat normals and palette UVs.
positions=[];normals=[];uvs=[];triangles=[];bone_names=[]
for ob in ASSET:
    if ob['ftk_bone'] not in BONES: raise ValueError(ob['ftk_bone'])
    ob.data.calc_loop_triangles()
    transform=ob.matrix_world
    normal_matrix=transform.to_3x3().inverted().transposed()
    for tri in ob.data.loop_triangles:
        face=ob.data.polygons[tri.polygon_index]
        normal=(normal_matrix@face.normal).normalized()
        idx=face.material_index
        # RuntimeGltfMeshLoader flips V on read; encode top-origin V here.
        uv=((idx%8+.5)/8,1-(idx//8+.5)/8)
        start=len(positions)
        for vi in tri.vertices:
            p=transform@ob.data.vertices[vi].co
            positions.append([p.x,p.z,-p.y]);normals.append([normal.x,normal.z,-normal.y])
            uvs.append(uv);bone_names.append(ob['ftk_bone'])
        # (x,z,-y) is a rotation (det +1). Preserve source winding, matching
        # the vanilla mesh's positive cross-product/normal agreement.
        triangles.append([start,start+1,start+2])
(OUT/'source_mesh.json').write_text(json.dumps(dict(positions=positions,normals=normals,uvs=uvs,triangles=triangles,vertex_bone_names=bone_names)))
(OUT/'metrics.json').write_text(json.dumps({'triangles':len(triangles),'vertices':len(positions),'objects':len(ASSET),'bone_names':sorted(set(bone_names))},indent=2))

collection=bpy.data.collections.new('Original Mirewarden asset')
bpy.context.scene.collection.children.link(collection)
for ob in ASSET:
    for c in list(ob.users_collection):c.objects.unlink(ob)
    collection.objects.link(ob)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'mirewarden-bind.blend'))

# Preview pose only; exported coordinates above remain in the exact T bind pose.
for ob in ASSET:
    bone=ob['ftk_bone']
    if bone.endswith(('_L','_R')) and any(word in bone for word in ['Shoulder','Elbow','Wrist','Finger']):
        suffix=bone[-1];s=1 if suffix=='R' else -1
        pivot=BONES['Shoulder_'+suffix]
        ob.matrix_world=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(s*66),4,'Y')@Matrix.Translation(-pivot)@ob.matrix_world

# Neutral render stage.
bpy.ops.mesh.primitive_plane_add(size=200)
bpy.context.object.name='Stage';bpy.context.object.location.z=-.06
stage=bpy.data.materials.new('Stage');stage.diffuse_color=(.16,.18,.17,1)
bpy.context.object.data.materials.append(stage)
def aim(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,col in [('Key',(-4,-5,7),700,5,(1,.91,.79)),('Fill',(4,-3,5),450,4,(.80,.89,1)),('Rim',(0,4,6),800,3,(1,.94,.79))]:
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.size=size;o.data.color=col;aim(o,(0,0,1.7))
bpy.ops.object.camera_add(location=(4.8,-8,4.0));camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=4.05;aim(camera,(0,0,1.68))
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.color=(.18,.18,.18);scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'mirewarden-preview.blend'))
for name,loc in [('hero',(4.8,-8,4)),('front',(0,-9,3.6)),('back',(-4,8,4))]:
    camera.location=loc;aim(camera,(0,0,1.68));scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
print('MODEL_COMPLETE',len(triangles),len(positions))
