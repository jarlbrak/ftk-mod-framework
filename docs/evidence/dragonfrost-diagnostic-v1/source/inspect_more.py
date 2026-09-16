exec((__import__('pathlib').Path(__file__).parent/'audit_source.py').read_text().split('r=f.objects')[0])
import struct
for pid in [138558,136713,5951]:
 o=f.objects[pid];d=t(o);print('\nOBJECT',pid,o.type.name,'tree keys',list(d));
 if pid!=5951:
  raw=o.get_raw_data();strings=[]
  for pos in range(0,len(raw)-4,4):
   n=struct.unpack_from('<i',raw,pos)[0]
   if 0<n<20000 and pos+4+n<=len(raw):
    try:s=raw[pos+4:pos+4+n].decode('utf-8')
    except UnicodeDecodeError:continue
    if s.isprintable():strings.append(dict(offset=pos,value=s))
  (OUT/('strings-'+str(pid)+'.json')).write_text(json.dumps(strings,indent=2)+'\n');print('STRINGS',strings)
 else:
  print('controllerdata',list(d.get('m_Controller',{})));print('behaviours',d.get('m_StateMachineBehaviours'));print('state0',str(d['m_Controller']['m_StateMachineArray'][0]['data']['m_StateConstantArray'][0])[:4500])
