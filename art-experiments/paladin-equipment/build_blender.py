#!/usr/bin/env python3
"""Author new Paladin meshes using documented Classic WoW visual references."""
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Vector

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
from export_ftk_glb import write_static_glb

COLORS = ["233344", "66829B", "C7D7DD", "936735", "E2B458", "294C85", "702C37", "261F2A", "83C9CA", "EBE1B8"]
NAMES = ["novice", "oathkeeper", "highward", "mercy", "censure", "verdict"]
MATS = []
pieces = []


def material(index):
    return MATS[index]


def finish(obj, name, color, bevel=0):
    obj.name = name
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.materials.append(material(color))
    if bevel:
        mod = obj.modifiers.new("Authored edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    pieces.append((obj, color))
    return obj


def box(name, loc, scale, color, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, color, bevel)


def cylinder(name, loc, radius, depth, color, vertices=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    return finish(bpy.context.object, name, color, .008)


def gem(name, loc, radius, color):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=loc)
    return finish(bpy.context.object, name, color)


def plate(name, outline, depth, y, color):
    # Counterclockwise outlines are authored in X/Z, with a front face toward -Y.
    count = len(outline)
    verts = [(x, y - depth / 2, z) for x, z in outline] + [(x, y + depth / 2, z) for x, z in outline]
    faces = [tuple(range(count)), tuple(range(2 * count - 1, count - 1, -1))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, name, color, .015)


def sigil(z, y, size, variant):
    # A split diamond with a suspended bead is this original order's emblem.
    points = [(0, z-size), (size*.62, z), (0, z+size), (-size*.62, z)]
    plate("Split diamond relief", points, .025, y, 4)
    box("Diamond inset", (0, y-.025, z), (size*.24, .03, size*1.12), 8 if variant == 3 else 7, .006)
    gem("Oath bead", (0, y-.05, z-size*.65), size*.15, 9)


def hammer(two_handed, tier):
    if tier == 0:
        novice_hammer(two_handed)
        return
    if tier in (3, 5):
        famous_endgame_hammer(two_handed, tier)
        return
    length = 1.5 if two_handed else .88
    width = (.74 if two_handed else .50) + min(tier, 3)*.065
    height = .32 + min(tier, 3)*.035
    accent = 5 if tier < 3 else 6
    cylinder("Oak haft", (0, 0, length*.42), .045 if two_handed else .038, length*.93, 0)
    for i in range(9 if two_handed else 6):
        cylinder("Leather grip band %02d" % i, (0, 0, .06+i*.043), .054 if two_handed else .047, .028, accent)
    cylinder("Octagonal pommel", (0, 0, -.018), .087, .075, 4, 8)
    gem("Pommel seal", (0, 0, -.075), .056, 8 if tier == 3 else 9)
    cylinder("Lower head collar", (0, 0, length-.16), .073, .11, 4)
    box("Forged head", (0, 0, length), (width, .27, height), 2 if tier < 3 else 7, .045)
    for side in [-1, 1]:
        box("Striking cap", (side*width*.5, 0, length), (.105, .33, height*1.15), 3 if tier == 0 else 4, .023)
        box("Steel strike face", (side*(width*.5+.058), 0, length), (.022, .255, height*.79), 2, .008)
    box("Center binding", (0, 0, length), (.13, .292, height*1.15), 4, .015)
    sigil(length, -.173, height*.48, tier)
    if tier >= 1:
        for side in [-1, 1]:
            box("Head inlay", (side*width*.29, -.146, length), (.085, .016, height*.62), accent, .008)
    if tier >= 2:
        for side in [-1, 1]:
            plate("Crown fin", [(side*.08,length+height*.42),(side*.24,length+height*.48),(side*.19,length+height*.87)], .13, 0, 4)
    if tier == 3:
        gem("Mercy lantern", (0, 0, length+height*.72), .105, 8)
        for side in [-1, 1]:
            plate("Mercy wing", [(side*.12,length-.02),(side*.31,length-.18),(side*.19,length-.32)], .065, -.02, 4)
    if tier == 4:
        for side in [-1, 1]:
            plate("Censure tooth", [(side*(width*.5-.06),length-height*.4),(side*(width*.5+.09),length-height*.4),(side*(width*.5+.12),length-height*.96)], .20, 0, 2)
    if tier == 5:
        for side in [-1, 1]:
            box("Verdict counterweight", (side*width*.36, 0, length), (.10,.36,height*1.38), 4, .02)
        cylinder("Verdict crown", (0,0,length+height*.78), .075,.17,4,6)


def spike(name, start, end, radius, color):
    delta=Vector(end)-Vector(start)
    bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=radius, radius2=0,
                                   depth=delta.length, location=(Vector(start)+Vector(end))*.5)
    obj=bpy.context.object
    obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler()
    finish(obj,name,color)


def famous_endgame_hammer(two_handed, tier):
    length=1.5 if two_handed else .88
    scale=1 if two_handed else .77
    cylinder("Dark wrapped haft",(0,0,length*.43),.046,length*.95,7)
    for i in range(9 if two_handed else 6):
        cylinder("Grip binding",(0,0,.055+i*.045),.051,.017,1 if tier==3 else 3)
    cylinder("Flared pommel",(0,0,-.025),.086,.067,1 if tier==3 else 4,6)
    if tier==5:
        # Classic Sulfuras: red barrel, heavy gold bands and radial gold spikes.
        box("Crimson molten head",(0,0,length),(.68*scale,.28*scale,.30*scale),6,.045)
        for side in [-1,1]:
            box("Gold raised end band",(side*.24*scale,0,length),(.047*scale,.32*scale,.36*scale),4,.017)
            spike("Axial gold spike",(side*.33*scale,0,length),(side*.49*scale,0,length),.105*scale,4)
            for up in [-1,1]:
                spike("Radial gold spike",(side*.24*scale,0,length+up*.13*scale),
                      (side*.34*scale,0,length+up*.29*scale),.080*scale,4)
        gem("Gold face stud",(0,-.155*scale,length),.082*scale,4)
        cylinder("Sulfuras broad neck",(0,0,length-.25*scale),.09*scale,.20*scale,6,8)
        for side in [-1,1]:
            spike("Pommel spur",(side*.05,0,-.025),(side*.14,0,-.025),.045,4)
        spike("Pommel point",(0,0,-.04),(0,0,-.17),.067,4)
    else:
        # Might of Menethil: hooked opposing shells and exposed blue wells.
        cylinder("Steel neck",(0,0,length-.16*scale),.074*scale,.25*scale,1,8)
        for side in [-1,1]:
            outline=[(side*x*scale,length+z*scale) for x,z in
                     [(.035,-.12),(.13,-.16),(.30,-.13),(.40,-.03),(.41,.16),
                      (.32,.27),(.33,.10),(.28,.02),(.14,.01),(.08,.09)]]
            plate("Hooked steel shell",outline,.20*scale,0,1)
            gem("Exposed azure well",(side*.225*scale,-.070*scale,length+.075*scale),.107*scale,8)
            plate("Silver hook edge",[(side*x*scale,length+z*scale) for x,z in
                  [(.32,.27),(.41,.16),(.40,-.03),(.37,.04),(.37,.15)]],.025,-.11*scale,2)
        spike("Central spear",(0,0,length-.06*scale),(0,0,length+.34*scale),.068*scale,2)
        spike("Dark pommel point",(0,0,-.045),(0,0,-.14),.062,1)


def novice_hammer(two_handed):
    # Preserve the tested grip and head stations; reduce mass around them.
    length = 1.5 if two_handed else .88
    width = .54 if two_handed else .36
    height = .22 if two_handed else .19
    depth = .18 if two_handed else .16
    cylinder("Ash haft", (0, 0, length*.42), .040 if two_handed else .034, length*.93, 3)
    for i in range(9 if two_handed else 6):
        cylinder("Crimson leather wrap %02d" % i, (0, 0, .06+i*.043), .047 if two_handed else .041, .029, 6)
    cylinder("Plain brass pommel", (0, 0, -.018), .055, .048, 4, 8)
    cylinder("Forged neck", (0, 0, length-.12), .049, .095, 3)
    if two_handed:
        # Verigan's Fist: faceted waisted barrel with paired gold end bands.
        rings = [(-.29,.115),(-.255,.147),(-.205,.151),(-.17,.139),
                 (0,.113),(.17,.139),(.205,.151),(.255,.147),(.29,.115)]
        vertices = [(x, math.cos(i*math.tau/8)*r, length+math.sin(i*math.tau/8)*r)
                    for x,r in rings for i in range(8)]
        faces = [tuple(range(7,-1,-1)),tuple(range(64,72))]
        faces += [(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i)
                  for j in range(len(rings)-1) for i in range(8)]
        mesh=bpy.data.meshes.new("Waisted silver barrel")
        mesh.from_pydata(vertices,[],faces); mesh.update()
        obj=bpy.data.objects.new("Waisted silver barrel",mesh)
        bpy.context.collection.objects.link(obj)
        finish(obj,"Waisted silver barrel",2)
        for side in [-1,1]:
            for station in [.181,.219]:
                band=cylinder("Paired gold barrel band",(side*station,0,length),.153,.014,4,8)
                band.rotation_euler[1]=math.pi/2
        plate("Gold central lozenge",[(-.057,length),(0,length+.068),(.057,length),(0,length-.068)],.012,-.119,4)
    else:
        # Ironfoe: framed rectangular steel head, recessed field and gold rune.
        box("Ironfoe steel frame",(0,0,length),(.39,.185,.235),1,.021)
        box("Recessed bronze field",(0,-.096,length),(.328,.012,.174),3,.010)
        box("Inner dark field",(0,-.104,length),(.283,.009,.136),0,.007)
        for side in [-1,1]:
            plate("Steel angular brace",[(side*.055,length),(side*.159,length+.078),
                  (side*.129,length+.083),(side*.030,length+.019),
                  (side*.129,length-.083),(side*.159,length-.078)],.012,-.114,2)
        plate("Gold angular rune",[(0,length+.076),(.030,length+.022),(.016,length),
              (.046,length-.052),(0,length-.026),(-.046,length-.052),
              (-.016,length),(-.030,length+.022)],.013,-.130,4)


def novice_shield():
    outline = [(0,-.58),(.27,-.31),(.36,.18),(.27,.49),(-.27,.49),(-.36,.18),(-.27,-.31)]
    plate("Thin brass heater rim", outline, .070, 0, 4)
    plate("Crimson shield field", [(x*.91,z*.93) for x,z in outline], .025, -.043, 6)
    # A small original seal leaves the field quiet and the silhouette readable.
    plate("Split dawn seal", [(0,-.10),(.086,.08),(0,.26),(-.086,.08)], .015, -.064, 4)
    box("Seal dark split", (0,-.077,.08), (.020,.012,.20), 7, .003)
    gem("Seal bead", (0,-.080,-.047), .020, 9)
    for side in [-1,1]:
        for z in [-.14,.25]:
            gem("Small rim pin", (side*.298,-.042,z), .017, 3)
    for z in [-.17,.17]:
        box("Back strap anchor", (0,.067,z), (.18,.035,.043), 3,.008)
    box("Leather back grip", (0,.116,0), (.060,.060,.34), 0,.015)


def shield(tier):
    if tier == 0:
        novice_shield()
        return
    extent = .42 + min(tier, 3)*.045
    top = .56 + min(tier, 3)*.035
    outline = [(0,-.66), (extent*.77,-.34), (extent,.22), (extent*.72,top),(-extent*.72,top),(-extent,.22),(-extent*.77,-.34)]
    plate("Solid rim", outline, .13, 0, 3 if tier == 0 else 4)
    inner = [(x*.87,z*.89) for x,z in outline]
    plate("Shield face", inner, .04, -.09, 1 if tier == 0 else (5 if tier < 3 else 7))
    box("Raised center ridge", (0,-.13,-.015), (.065,.055,1.01), 4, .014)
    sigil(.12,-.178,.23,tier)
    for side in [-1,1]:
        for z in [-.23,.28]:
            gem("Rim rivet", (side*extent*.80,-.077,z), .035, 2)
    # Back fittings deliberately exist as authored geometry, not a flat display prop.
    for z in [-.18,.18]:
        box("Back strap foot", (0,.10,z), (.24,.06,.065), 3,.012)
    box("Back grip", (0,.16,0), (.072,.065,.38), 0,.025)
    if tier >= 1:
        for side in [-1,1]:
            plate("Side chevron", [(side*.14,-.10),(side*.31,.03),(side*.28,-.19),(side*.13,-.32)], .02,-.13,4)
    if tier >= 2:
        plate("Crest", [(-.18,top*.86),(0,top+.14),(.18,top*.86)], .08,-.04,4)
    if tier == 3:
        for side in [-1,1]:
            gem("Mercy well", (side*.27,-.18,.23), .080,8)
        gem("Mercy crest stone", (0,-.10,top+.06), .063,8)
    if tier == 4:
        for side in [-1,1]:
            plate("Ward pennant", [(side*.10,.46),(side*.29,.42),(side*.24,.04)], .025,-.17,6)
        for z in [-.34,-.44]:
            box("Ward seal", (0,-.19,z), (.15,.025,.033),9,.005)
    if tier == 5:
        for side in [-1,1]:
            for z in [-.05,.15]:
                plate("Retaliation spur", [(side*extent*.93,z-.08),(side*(extent+.13),z+.07),(side*extent*.95,z+.10)],.07,0,2)


def export_asset(key):
    data = {k: [] for k in ["positions","normals","uvs","triangles"]}
    mapping = []
    for obj,color in pieces:
        mesh = obj.data
        mesh.calc_loop_triangles()
        start = len(data["triangles"])
        for tri in mesh.loop_triangles:
            corners = [Vector([round(c,7) for c in obj.matrix_world @ mesh.vertices[index].co]) for index in tri.vertices]
            if (corners[1]-corners[0]).cross(corners[2]-corners[0]).length <= 1e-9:
                continue
            indices=[]
            normal = (obj.matrix_world.to_3x3() @ tri.normal).normalized()
            for index in tri.vertices:
                v = obj.matrix_world @ mesh.vertices[index].co
                indices.append(len(data["positions"]))
                data["positions"].append([round(v.x,7),round(v.z,7),round(-v.y,7)])
                data["normals"].append([round(normal.x,7),round(normal.z,7),round(-normal.y,7)])
                data["uvs"].append([(color+.5)/len(COLORS),.5])
            data["triangles"].append(indices)
        mapping.append({"name":obj.name,"paletteIndex":color,"firstTriangle":start,"triangleCount":len(data["triangles"])-start})
    (OUT / (key+".source.json")).write_text(json.dumps(data,separators=(",",":"))+"\n")
    (OUT / (key+".pieces.json")).write_text(json.dumps(mapping,indent=2)+"\n")
    write_static_glb(OUT/(key+".glb"),data)
    return {"key":key,"triangles":len(data["triangles"]),"vertices":len(data["positions"]),"pieces":len(pieces)}


def aim(obj, point):
    obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()


def build():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for color in COLORS:
        mat=bpy.data.materials.new("Original palette "+color)
        mat.diffuse_color=tuple(int(color[i:i+2],16)/255 for i in (0,2,4))+(1,)
        mat.use_nodes=True
        shader=mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value=mat.diffuse_color
        shader.inputs["Metallic"].default_value=.65
        shader.inputs["Roughness"].default_value=.36
        MATS.append(mat)
    image=bpy.data.images.new("Original Paladin palette",width=len(COLORS),height=1)
    image.pixels=[v for c in COLORS for v in [*(int(c[i:i+2],16)/255 for i in (0,2,4)),1]]
    image.filepath_raw=str(OUT/"paladin-equipment-palette.png")
    image.file_format="PNG"
    image.save()
    records=[]
    for row,family in enumerate(["hammer-1h","hammer-2h","shield"]):
        for tier,name in enumerate(NAMES):
            pieces.clear()
            if family == "shield": shield(tier)
            else: hammer(family == "hammer-2h",tier)
            bpy.context.view_layer.update()
            key="paladin-"+family+"-"+name
            record=export_asset(key)
            record.update(family=family,band=["starter","early","midgame","endgame","endgame","endgame"][tier])
            records.append(record)
            for obj,_ in pieces:
                obj.location.x += (tier-2.5)*1.45
                obj.location.z += (2-row)*2.25
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.samples=32
    scene.world.color=(.16,.16,.16)
    bpy.ops.object.camera_add(location=(5,-17,11))
    camera=bpy.context.object
    aim(camera,(0,0,3.1))
    camera.data.type="ORTHO"
    camera.data.ortho_scale=10.8
    scene.camera=camera
    for loc,power,size in [((0,-7,10),1900,8),((-6,-2,5),1200,7),((4,4,9),2200,6)]:
        bpy.ops.object.light_add(type="AREA",location=loc)
        light=bpy.context.object
        light.data.energy=power
        light.data.shape="DISK"
        light.data.size=size
        aim(light,(0,0,3))
    scene.render.resolution_x=2100
    scene.render.resolution_y=1600
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.render.image_settings.file_format="PNG"
    scene.render.filepath=str(OUT/"paladin-equipment-lineup.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"paladin-equipment-studio.blend"))
    files={}
    for record in records:
        for suffix in [".glb",".source.json",".pieces.json"]:
            p=OUT/(record["key"]+suffix)
            files[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
    files["paladin-equipment-palette.png"]=hashlib.sha256((OUT/"paladin-equipment-palette.png").read_bytes()).hexdigest()
    manifest={"schema":"ftkmf.paladin-original-equipment.v1","generator":"build_blender.py","generatorSha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),"provenance":"All surfaces, UVs, normals, palette, ornaments and proportions are authored by this generator. No external art or native game data is read.","space":"Provisional original local space: Unity Y up, Z front. Native item attachment transforms and scale are unverified.","status":"Original offline assets only. Not integrated, placed, bound, animated or approved in game.","assets":records,"files":files}
    (OUT/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__": build()
