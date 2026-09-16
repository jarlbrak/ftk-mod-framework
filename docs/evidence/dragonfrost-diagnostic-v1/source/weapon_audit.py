exec(open('scratch/dragon-frost-native-topology-analysis/audit_source.py').read().split('r=f.objects')[0])
import struct,subprocess,concurrent.futures
w=f.objects[136713];go=ptr(w,t(w)['m_GameObject']);rows=[]
for c in t(go)['m_Component']:
 o=ptr(go,c['component']);d=t(o);row={'id':o.path_id,'type':o.type.name}
 if o.type.name=='MonoBehaviour':
  row['script']=name(ptr(o,d['m_Script']));b=o.get_raw_data();ss=[]
  for p in range(0,len(b)-4,4):
   n=struct.unpack_from('<i',b,p)[0]
   if 0<n<4096 and p+4+n<=len(b):
    try:s=b[p+4:p+4+n].decode()
    except:continue
    if all(ch.isprintable()for ch in s):ss.append({'offset':p,'text':s})
  row['strings']=ss
 rows.append(row)
print(rows);(OUT/'weapon-components.json').write_text(json.dumps(rows,indent=2)+'\n')
cmd=['/opt/homebrew/opt/dotnet/libexec/dotnet',str(Path.home()/'.dotnet/tools/.store/ilspycmd/10.1.0.8386/ilspycmd/10.1.0.8386/tools/net10.0/any/ilspycmd.dll'),str(assets.parent/'Managed/Assembly-CSharp.dll')]
def dec(n):
 s=subprocess.check_output(cmd+['-t',n]);(OUT/(n+'.cs')).write_bytes(s)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:list(ex.map(dec,['AttackSchedule','EnemyDummy','CharacterEventListener','CombatAction','AttackStart']))
