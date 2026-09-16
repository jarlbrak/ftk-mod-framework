"""Generate original two-layer triangles for the actual C# loader tests; no native assets."""
import sys,json,copy,struct
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from test_multi_primitive_glb import fixture,mutate
from export_ftk_glb import write_glb
def write_static(path):
    binary=bytearray();views=[];accessors=[]
    def add(values,fmt,component,kind):
        while len(binary)%4:binary.append(0)
        raw=struct.pack('<'+fmt*len(values),*values);offset=len(binary);binary.extend(raw)
        views.append({'buffer':0,'byteOffset':offset,'byteLength':len(raw)})
        width={'VEC3':3,'VEC2':2,'SCALAR':1}[kind]
        accessors.append({'bufferView':len(views)-1,'byteOffset':0,'componentType':component,'count':len(values)//width,'type':kind})
        return len(accessors)-1
    pos=add([0,0,0,1,0,0,0,1,0],'f',5126,'VEC3')
    normal=add([0,0,1,0,0,1,0,0,1],'f',5126,'VEC3')
    uv=add([.25,.2,.75,.2,.25,.8],'f',5126,'VEC2')
    indices=add([0,1,2],'H',5123,'SCALAR')
    root={'asset':{'version':'2.0','generator':'FTK static-loader test fixture'},'buffers':[{'byteLength':len(binary)}],
          'bufferViews':views,'accessors':accessors,
          'meshes':[{'primitives':[{'attributes':{'POSITION':pos,'NORMAL':normal,'TEXCOORD_0':uv},'indices':indices,'mode':4}]}]}
    encoded=json.dumps(root,separators=(',',':')).encode();encoded+=b' '*(-len(encoded)%4);binary.extend(b'\0'*(-len(binary)%4))
    path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary)

out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True);d,r=fixture();write_glb(out/'legacy.glb',d,r);d['primitives']=[dict(triangles=[d['triangles'][0]]),dict(triangles=[d['triangles'][1]])];write_glb(out/'two.glb',d,r)
changes=dict(attribute=lambda x:x['meshes'][0]['primitives'][1]['attributes'].update(POSITION=1),material=lambda x:x['meshes'][0]['primitives'][1].update(material=0),indices=lambda x:x['meshes'][0]['primitives'][1].update(indices=x['meshes'][0]['primitives'][0]['indices']),topology=lambda x:x['meshes'][0]['primitives'][1].update(mode=1),morph=lambda x:x['meshes'][0]['primitives'][1].update(targets=[]),component=lambda x:x['accessors'][x['meshes'][0]['primitives'][1]['indices']].update(componentType=5125),range=lambda x:x['accessors'][x['meshes'][0]['primitives'][1]['indices']].update(count=999999))
for name,change in changes.items():p=out/f'bad-{name}.glb';p.write_bytes((out/'two.glb').read_bytes());mutate(p,change)
write_static(out/'static.glb')
static_changes=dict(skin=lambda x:x.update(skins=[{'joints':[]}]),joints=lambda x:x['meshes'][0]['primitives'][0]['attributes'].update(JOINTS_0=0),primitives=lambda x:x['meshes'][0]['primitives'].append(copy.deepcopy(x['meshes'][0]['primitives'][0])))
for name,change in static_changes.items():p=out/f'bad-static-{name}.glb';p.write_bytes((out/'static.glb').read_bytes());mutate(p,change)
