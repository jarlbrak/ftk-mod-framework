import struct,json,zlib
from pathlib import Path
p=Path('scratch/perf-game/BepInEx/plugins/FTKModFramework_content/models');p.mkdir(parents=True,exist_ok=True)
chunks=[struct.pack('<9f',0,0,0,1,0,0,0,1,0),struct.pack('<9f',0,0,1,0,0,1,0,0,1),struct.pack('<6f',0,0,1,0,0,1),struct.pack('<3H',0,1,2)]
b=b'';views=[]
for c in chunks:views.append({'buffer':0,'byteOffset':len(b),'byteLength':len(c)});b+=c;b+=b'\0'*((-len(b))%4)
g={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(b)}],'bufferViews':views,'accessors':[{'bufferView':i,'componentType':5126 if i<3 else 5123,'count':3,'type':['VEC3','VEC3','VEC2','SCALAR'][i]} for i in range(4)],'meshes':[{'primitives':[{'attributes':{'POSITION':0,'NORMAL':1,'TEXCOORD_0':2},'indices':3,'mode':4}]}]}
s=json.dumps(g).encode();s+=b' '*((-len(s))%4);data=struct.pack('<III',0x46546c67,2,12+8+len(s)+8+len(b))+struct.pack('<II',len(s),0x4e4f534a)+s+struct.pack('<II',len(b),0x004e4942)+b
(p/'resource-fixture.glb').write_bytes(data)
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
w=h=1024;raw=b''.join(b'\0'+b''.join(bytes((x%256,y%256,(x^y)%256,255)) for x in range(w)) for y in range(h))
png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b'')
(p/'resource-fixture.png').write_bytes(png)
print(len(data),len(png))
