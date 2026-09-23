#!/usr/bin/env python3
"""Reproduce twelve original Paladin accessories, without reading game surfaces."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys
import bpy
from mathutils import Vector, Matrix, Quaternion

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[1]
COLORS = ['B3BEC5','DDE5E5','565B64','292F3B','866749','B69A68','C49948','E9C771','19334D','2A5581','458FA8','98CDD1','EEE5CB','C9B996','802E3A','BD4D4D','161F2C']
TIN,SILVER,IRON,DARK,CORD,BRONZE,BRASS,GOLD,NAVY,BLUE,GLASS,PALE,IVORY,CREAM,WAX,RED,STONE=range(17)
FAMILIES=['novice','oathkeeper','highward','mercy','censure','verdict']
NAMES=[['Tin Oath Token',"Keeper's Seal",'Watchtower Reliquary','Lantern of Mercy','Seal of Censure','Scales of Verdict'],["Pilgrim's Pendant",'Oath Chain','Highward Gorget','Mercy Locket','Censure Medallion',"Judge's Collar"]]

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
B=load('paladin_original_primitives',ROOT/'art-experiments/paladin-equipment/build_blender.py')
I=load('paladin_original_icons',ROOT/'art-experiments/paladin-equipment/render_icons.py')
B.OUT=OUT;B.COLORS=COLORS
box,plate,cylinder=B.box,B.plate,B.cylinder

def save(path,data):path.write_text(json.dumps(data,indent=2)+'\n')
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def mesh(name,vertices,faces,color):
    data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update();obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);return B.finish(obj,name,color)
def ring(name,loc,rx,rz,thick,color,tilt=0,segments=12):
    # Closed faceted torus authored in the front X/Z plane; actual holes remain open.
    vertices=[]
    for i in range(segments):
        a=2*math.pi*i/segments
        for j in range(6):
            b=2*math.pi*j/6
            x=(rx+thick*math.cos(b))*math.cos(a);z=(rz+thick*math.cos(b))*math.sin(a)
            vertices.append((loc[0]+x*math.cos(tilt),loc[1]+thick*math.sin(b)+x*math.sin(tilt),loc[2]+z))
    faces=[]
    for i in range(segments):
        for j in range(6):faces.append((i*6+j,((i+1)%segments)*6+j,((i+1)%segments)*6+(j+1)%6,i*6+(j+1)%6))
    return mesh(name,vertices,faces,color)
def bar(name,a,b,r,color,vertices=8):
    delta=Vector(b)-Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=delta.length,location=(Vector(a)+Vector(b))*.5)
    obj=B.finish(bpy.context.object,name,color);obj.rotation_euler=delta.to_track_quat('Z','Y').to_euler();return obj

def disc(name,x,z,r,depth,y,color,vertices=12):
    obj=cylinder(name,(x,y,z),r,depth,color,vertices);obj.rotation_euler[0]=math.pi/2;return obj

def outline(z,w,h,n=8):return [(math.sin(2*math.pi*i/n)*w,z+math.cos(2*math.pi*i/n)*h) for i in range(n)]
def shield(name,z,w,h,y,color,depth=.025):
    return plate(name,[(-w*.8,z+h*.6),(-w,z+h*.25),(-w*.68,z-h*.5),(0,z-h),(w*.68,z-h*.5),(w,z+h*.25),(w*.8,z+h*.6)],depth,y,color)
def oath(z,y,size,color=BRASS):
    plate('Original split-oath raised stamp',[(0,z-size),(-size*.65,z),(0,z+size),(size*.65,z)],.013,y,color)
    box('Oath central incision',(0,y-.011,z+size*.12),(size*.18,.014,size*1.13),DARK,.001)

def cord_loop(z,rx,rz,color=CORD):
    ring('Simple braided cord loop',(0,.018,z),rx,rz,.017,color,segments=16)
    for x in [-.022,.022]:box('Cord knot',(x,-.018,z-rz+.015),(.033,.038,.057),color,.008)

def chain(z,rx,rz,color=BRASS,count=13):
    for i in range(count):
        a=2*math.pi*i/count
        ring('Broad individual chain link %02d'%i,(rx*math.sin(a),.025, z+rz*math.cos(a)),.043,.057,.012,color,tilt=(.72 if i%2 else -.18),segments=8)

def token():
    disc('Tin coin with rolled edge',0,0,.205,.058,0,TIN)
    disc('Recessed dull iron field',0,0,.167,.008,-.034,IRON)
    disc('Plain stamped tin face',0,0,.149,.012,-.043,TIN)
    oath(0,-.059,.099,IRON)
    # A physical notch interrupts the outer stamped border, without a luminous gem.
    box('Dark oath notch',(0,-.048,-.178),(.035,.018,.031),DARK,.002)
    ring('Tin suspension eye',(0,0,.220),.031,.043,.012,IRON,segments=8)
    cord_loop(.340,.093,.107)
    for x in [-.115,.115]:disc('Small struck maker dimple',x,-.043,.009,.008,-.055,IRON,6)

def seal():
    cylinder('Brass octagonal seal foot',(0,0,-.185),.193,.088,BRASS,8)
    cylinder('Steel engraved stamping sole',(0,0,-.235),.169,.021,DARK,8)
    cylinder('Stepped brass shoulder',(0,0,-.115),.119,.055,GOLD,8)
    # Continuous waisted handle has explicit radial rings, not a stack of loose blocks.
    rings=[(-.10,.074),(-.04,.054),(.12,.073),(.18,.104),(.25,.100)]
    vertices=[(r*math.cos(i*math.pi/4),r*math.sin(i*math.pi/4),z) for z,r in rings for i in range(8)]
    faces=[tuple(reversed(range(8))),tuple(range(32,40))]
    faces.extend((k*8+i,k*8+(i+1)%8,(k+1)*8+(i+1)%8,(k+1)*8+i) for k in range(4) for i in range(8))
    mesh('Waisted brass signet handle',vertices,faces,BRONZE)
    plate('Inset dark order escutcheon',outline(.065,.063,.09),.025,-.069,NAVY)
    oath(.065,-.089,.048,GOLD)
    cylinder('Seal crown cap',(0,0,.245),.109,.035,BRASS,8)
    disc('Maker seal on front foot',0,-.177,.040,.016,-.181,NAVY,8)
    oath(-.177,-.196,.026,GOLD)

def tower():
    box('Silver reliquary plinth',(0,0,-.25),(.35,.25,.079),SILVER,.02)
    box('Framed tower case',(0,0,-.015),(.265,.19,.44),TIN,.025)
    plate('Blue lancet inset',[(-.086,-.177),(-.086,.112),(0,.19),(.086,.112),(.086,-.177)],.020,-.102,NAVY)
    plate('Silver lancet inner field',[(-.057,-.141),(-.057,.095),(0,.151),(.057,.095),(.057,-.141)],.014,-.118,BLUE)
    for s in [-1,1]:
        box('Corner buttress',(s*.13,-.104,-.04),(.036,.055,.39),SILVER,.008)
        box('Tower crown merlon',(s*.115,0,.249),(.084,.22,.09),SILVER,.008)
    box('Tower crown parapet',(0,0,.203),(.325,.245,.05),SILVER,.009)
    box('Center merlon',(0,.07,.249),(.059,.082,.09),SILVER,.008)
    oath(-.052,-.141,.056,PALE)
    for z in [-.174,.139]:box('Rear reliquary hinge',(0,.107,z),(.088,.039,.035),IRON,.007)
    ring('Reliquary carrying eye',(0,0,.323),.041,.041,.013,TIN,segments=8)

def lantern():
    cylinder('Ivory octagonal lantern foot',(0,0,-.255),.184,.059,IVORY,8)
    cylinder('Bronze base reveal',(0,0,-.211),.150,.031,BRONZE,8)
    cylinder('Faceted opaque blue glass',(0,0,-.019),.133,.341,GLASS,8)
    for i in range(4):
        a=math.pi/4+i*math.pi/2;x=.145*math.cos(a);y=.145*math.sin(a)
        bar('Protective ivory corner rib',(x,y,-.21),(x,y,.192),.022,IVORY)
    cylinder('Upper bronze glass seat',(0,0,.169),.160,.035,BRONZE,8)
    bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.196,radius2=.080,depth=.115,location=(0,0,.235));B.finish(bpy.context.object,'Faceted ivory lantern hood',IVORY)
    cylinder('Hood bronze cap',(0,0,.302),.079,.025,BRONZE,8)
    ring('Rounded ivory carrying handle',(0,0,.377),.088,.071,.018,IVORY)
    for s in [-1,1]:bar('Front glass bronze frame',(s*.074,-.129,-.17),(s*.074,-.129,.138),.010,BRONZE)
    plate('Pale glass front reflection',[(-.045,-.115),(-.045,.095),(-.019,.114),(-.019,-.078)],.008,-.137,PALE)
    oath(-.014,-.146,.047,IVORY)

def censure_seal():
    poly=[(-.22,.16),(0,.27),(.22,.16),(.18,-.17),(0,-.28),(-.18,-.17)]
    plate('Angular dark steel seal case',poly,.135,0,DARK)
    plate('Raised iron bevel frame',[(x*.88,z*.88) for x,z in poly],.027,-.083,IRON)
    plate('Deep wax seat',[(x*.74,z*.74) for x,z in poly],.023,-.105,STONE)
    disc('Six sided crimson wax seal',0,-.005,.144,.037,-.127,WAX,6)
    oath(-.003,-.152,.091,BRONZE)
    for s in [-1,1]:
        plate('Restrained red folded ribbon',[(s*.041,-.14),(s*.115,-.13),(s*.146,-.327),(s*.090,-.290),(s*.031,-.32)],.026,.009,WAX)
        disc('Iron case corner rivet',s*.153,.115,.016,.019,-.119,TIN,8)
    ring('Angular case suspension bail',(0,0,.284),.040,.045,.015,IRON,segments=6)
    box('Rear case clasp',(0,.089,0),(.08,.029,.20),IRON,.009)

def scales():
    box('Broad black stone pedestal',(0,0,-.263),(.355,.255,.083),STONE,.025)
    box('Gold pedestal upper molding',(0,0,-.211),(.278,.19,.024),BRASS,.009)
    bar('Central hexagonal upright',(0,0,-.19),(0,0,.255),.028,BRASS,6)
    cylinder('Upright middle collar',(0,0,.057),.046,.038,GOLD,8)
    bar('Wide balanced gold crossbeam',(-.305,0,.243),(.305,0,.243),.022,GOLD)
    disc('Central pivot on balance',0,.241,.053,.069,0,BRASS,8)
    disc('Navy pivot enamel',0,.241,.030,.010,-.04,NAVY,8)
    for s in [-1,1]:
        x=s*.287
        for dx in [-.078,.078]:bar('Fine pan suspension',(x,0,.221),(x+dx,-.018,-.017),.008,BRONZE,6)
        # Broad shallow dish with a closed bottom and a raised gold rim.
        cylinder('Broad octagonal balance pan',(x,-.018,-.025),.113,.025,BRASS,8)
        lip=ring('Raised pan lip',(0,0,0),.112,.112,.008,GOLD,segments=8)
        lip.rotation_euler[0]=math.pi/2;lip.location=(x,-.018,-.01)
        # Broad open pan remains readable without a submillimeter decorative inset.
    plate('Gold finial spear',[(0,.379),(-.040,.318),(0,.279),(.040,.318)],.037,0,GOLD)
    oath(-.257,-.135,.039,BRASS)

def pilgrim():
    cord_loop(.20,.172,.255)
    ring('Plain iron bail',(0,-.025,-.060),.030,.041,.013,IRON,segments=8)
    shield('Plain iron pilgrim pendant',-.235,.117,.176,0,IRON,.051)
    shield('Worn tin inset',-.228,.086,.134,-.035,TIN,.019)
    oath(-.223,-.052,.063,IRON)
    disc('Humble fastening rivet',0,-.128,.014,.015,-.052,BRONZE,8)

def oath_chain():
    chain(.158,.185,.232,BRASS,13)
    ring('Badge suspension link',(0,0,-.105),.031,.048,.012,BRASS,segments=8)
    shield('Brass oath badge',-.270,.159,.168,0,BRASS,.054)
    shield('Blue enamel oath badge inset',-.263,.121,.126,-.036,NAVY,.020)
    oath(-.263,-.054,.083,GOLD)
    for s in [-1,1]:disc('Badge corner brass rivet',s*.093,-.186,.012,.010,-.054,GOLD,8)

def collar_band(name,rx,ry,z,width,depth,color,start=-155,end=-25):
    # Faceted curved neck guard, no rig or implied attachment.
    n=10;vertices=[]
    for i in range(n+1):
        a=math.radians(start+(end-start)*i/n)
        for r,h in [(0,0),(0,-width),(-depth,-width),(-depth,0)]:vertices.append(((rx+r)*math.cos(a),(ry+r)*math.sin(a),z+.14*abs(math.cos(a))+h))
    faces=[(3,2,1,0),(4*n,4*n+1,4*n+2,4*n+3)]
    for i in range(n):
        for j in range(4):faces.append((4*i+j,4*i+(j+1)%4,4*(i+1)+(j+1)%4,4*(i+1)+j))
    return mesh(name,vertices,faces,color)

def gorget():
    collar_band('Blue padded collar lining',.326,.186,.092,.20,.033,NAVY)
    collar_band('Silver curved gorget plate',.340,.202,.108,.193,.032,TIN)
    collar_band('Rolled silver upper rim',.349,.211,.115,.026,.037,SILVER)
    collar_band('Stepped silver lower rim',.346,.208,-.064,.027,.036,SILVER)
    shield('Strong central silver throat plate',-.133,.127,.156,-.235,SILVER,.043)
    shield('Recessed blue central plate',-.127,.082,.105,-.268,BLUE,.015)
    oath(-.13,-.283,.058,TIN)
    for s in [-1,1]:
        disc('Gorget fastening boss',s*.251,.070,.022,.023,-.161,SILVER,8)
        ring('Rear gorget closing link',(s*.306,-.073,.122),.024,.045,.010,IRON,tilt=s*.8,segments=8)

def locket():
    chain(.19,.163,.202,BRONZE,13)
    ring('Locket bronze bail',(0,-.005,-.047),.035,.041,.013,BRONZE)
    poly=outline(-.245,.171,.210,14)
    plate('Rounded bronze locket seam',poly,.072,0,BRONZE)
    plate('Warm ivory rounded locket lid',[(x*.9,-.245+(z+.245)*.9) for x,z in poly],.033,-.053,IVORY)
    plate('Ivory rear locket shell',[(x*.91,-.245+(z+.245)*.91) for x,z in poly],.017,.047,CREAM)
    for z in [-.31,-.20]:box('Bronze functioning hinge',(-.163,.006,z),(.027,.069,.045),BRONZE,.009)
    box('Blue enamel clasp',(.163,-.009,-.245),(.036,.090,.078),BLUE,.012)
    oath(-.244,-.083,.102,BRONZE)
    disc('Restrained blue clasp jewel',0,-.246,.027,.010,-.099,GLASS,8)

def medallion():
    chain(.19,.174,.235,IRON,12)
    ring('Angular medallion bail',(0,0,-.071),.027,.042,.012,DARK,segments=6)
    poly=[(0,-.080),(.174,-.201),(.123,-.369),(0,-.464),(-.123,-.369),(-.174,-.201)]
    plate('Faceted dark steel medallion',poly,.067,0,DARK)
    plate('Cold iron bevel inset',[(x*.87,-.273+(z+.273)*.87) for x,z in poly],.023,-.045,IRON)
    plate('Recessed angular red enamel',[(x*.69,-.273+(z+.273)*.70) for x,z in poly],.015,-.063,WAX)
    plate('Red central censure mark',[(0,-.127),(-.05,-.247),(-.022,-.323),(0,-.352),(.022,-.323),(.05,-.247)],.014,-.08,RED)
    for s in [-1,1]:bar('Raised dark split chevron',(s*.074,-.088,-.246),(s*.012,-.088,-.307),.012,DARK,6)
    disc('Medallion reverse fastening',0,-.262,.063,.021,.046,IRON,8)

def judges_collar():
    collar_band('Navy ceremonial collar foundation',.335,.18,.130,.191,.039,NAVY,start=-165,end=-15)
    collar_band('Continuous gold collar top piping',.346,.192,.143,.023,.039,GOLD,start=-165,end=-15)
    collar_band('Gold collar lower piping',.346,.192,-.042,.027,.039,BRASS,start=-165,end=-15)
    for s in [-1,1]:
        for i in range(3):
            x=s*(.075+.09*i);z=-.08+.055*i;y=-.207+.023*i
            p=[(x-.045,z+.096),(x+.045,z+.096),(x+.039,z-.055),(x,z-.081),(x-.039,z-.055)]
            plate('Articulated gold collar panel',p,.034,y,BRASS)
            plate('Navy inset ceremonial panel',[(x+(px-x)*.64,z+(pz-z)*.74) for px,pz in p],.015,y-.027,NAVY)
            disc('Gold panel pin',x,z+.061,.008,.009,y-.042,GOLD,6)
    shield('Central gold collar clasp',-.151,.094,.133,-.236,GOLD,.038)
    shield('Black stone clasp insert',-.144,.063,.084,-.263,STONE,.022)
    oath(-.143,-.281,.047,BRASS)
    for s in [-1,1]:box('Rear gold closing hinge',(s*.320,-.039,.126),(.038,.041,.060),BRASS,.007)

BUILDERS=[[token,seal,tower,lantern,censure_seal,scales],[pilgrim,oath_chain,gorget,locket,medallion,judges_collar]]

def strip_metadata(path):
    raw=path.read_bytes();chunks=[raw[:8]];offset=8
    while offset<len(raw):
        length=struct.unpack_from('>I',raw,offset)[0];kind=raw[offset+4:offset+8]
        if kind not in (b'tEXt',b'iTXt',b'zTXt'):chunks.append(raw[offset:offset+length+12])
        offset+=length+12
    path.write_bytes(b''.join(chunks))

def main():
    records=[]
    for row,slot in enumerate(['trinket','necklace']):
        for col,family in enumerate(FAMILIES):
            key='paladin-'+slot+'-'+family
            B.MATS=I.reset(COLORS);B.pieces.clear();BUILDERS[row][col]();bpy.context.view_layer.update()
            record=B.export_asset(key)
            # Invert only audited root orientation. No native surface fitting is implied.
            data=json.loads((OUT/(key+'.source.json')).read_text())
            q=Quaternion((-1.6292068494294654e-7,0,1,0)) if row==0 else Quaternion((1,0,0,0))
            transform=q.inverted().to_matrix().to_4x4()
            data['authorToRuntime']=[list(v) for v in transform]
            for field in ['positions','normals']:
                data[field]=[[round(c,7) for c in transform.to_3x3()@Vector(v)] for v in data[field]]
            (OUT/(key+'.source.json')).write_text(json.dumps(data,separators=(',',':'))+'\n')
            B.write_static_glb(OUT/(key+'.glb'),data)
            record['authorToRuntime']=data['authorToRuntime']
            record.update(name=NAMES[row][col],slot=slot,family=family,template='trinketDefense1' if row==0 else 'amuletVitality1',rendererPath='trinketHorn2' if row==0 else 'amuletLocket1');records.append(record)
            mats=I.reset(COLORS);points=I.add_source(OUT/(key+'.source.json'),mats)
            bpy.context.scene.view_settings.view_transform='AgX'
            I.render(OUT/(key+'-icon.png'),points);strip_metadata(OUT/(key+'-icon.png'))
    image=bpy.data.images.new('Original accessory palette',width=len(COLORS),height=1)
    image.pixels=[v for color in COLORS for v in [*(int(color[i:i+2],16)/255 for i in [0,2,4]),1]];image.filepath_raw=str(OUT/'paladin-accessory-palette.png');image.file_format='PNG';image.save()
    mats=I.reset(COLORS)
    for i,record in enumerate(records):I.add_source(OUT/(record['key']+'.source.json'),mats,Vector(((i%6)*.95,(1-i//6)*1.10,0)))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'paladin-accessories.blend'))
    files={p.name:digest(p) for p in sorted(OUT.iterdir()) if p.suffix=='.glb' or p.name.endswith(('.source.json','.pieces.json','-icon.png','-palette.png'))}
    save(OUT/'manifest.json',{'schema':'ftkmf.paladin-original-accessories.v1','generator':'build.py','generatorSha256':digest(Path(__file__)),'dependencies':{str(p.relative_to(ROOT)):digest(p) for p in [ROOT/'art-experiments/paladin-equipment/build_blender.py',ROOT/'art-experiments/paladin-equipment/render_icons.py',ROOT/'tools/ai-model-pipeline/export_ftk_glb.py']},'provenance':'Original Astra High authored display objects using approved Paladin accessory briefs. No native geometry, bounds, UV, normal, texture, weights or animation data read. Original order stamp reused as a visual motif.','space':'Unity Y up, Z front via orientation-preserving Blender (x,z,-y), followed by inverse audited prefab-root rotation. Source authorToRuntime records this rotation for icon reconstruction. Authored display-local dimensions; exact native fitting is unverified.','status':'Offline original art only. Display fit, native binding, inventory flows and lifecycle await separately authorized live testing. These slots are not visible on the character.','palette':COLORS,'assets':records,'files':files})
    print('PASS: twelve original accessories, source/piece maps, icons, palette and editable Blender studio')

if __name__=='__main__':main()
