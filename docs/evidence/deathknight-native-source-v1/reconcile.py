import json,hashlib,struct,subprocess,concurrent.futures,collections
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent;DATA=Path.home()/'Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data';asset=DATA/'resources.assets';env=UnityPy.load(str(asset));f=next(x for x in env.files.values() if Path(x.name).name==asset.name)
def tree(o):return o.read_typetree(check_read=False)
def ptr(o,p):return PPtr(m_FileID=p['m_FileID'],m_PathID=p['m_PathID'],assetsfile=o.assets_file).deref() if p['m_PathID'] else None
def name(o):return tree(o).get('m_Name','') if o else None
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as handle:
  for b in iter(lambda:handle.read(1048576),b''):h.update(b)
 return h.hexdigest()
def strings(o):
 b=o.get_raw_data();ss=[]
 for i in range(0,len(b)-4,4):
  n=struct.unpack_from('<i',b,i)[0]
  if 0<n<4096 and i+4+n<=len(b):
   try:s=b[i+4:i+4+n].decode()
   except:continue
   if all(c.isprintable() for c in s):ss.append({'offset':i,'text':s})
 return ss
def roottr(o):
 g=ptr(o,tree(o)['m_GameObject']);tr=next(ptr(g,c['component']) for c in tree(g)['m_Component'] if ptr(g,c['component']).type.name=='Transform')
 while tree(tr)['m_Father']['m_PathID']:tr=ptr(tr,tree(tr)['m_Father'])
 return tr
def hierarchy(tr):
 rows=[]
 def walk(tr,path=''):
  td=tree(tr);g=ptr(tr,td['m_GameObject']);path+='/'+name(g);cs=[]
  for cp in tree(g)['m_Component']:
   o=ptr(g,cp['component']);d=tree(o);c={'id':o.path_id,'type':o.type.name}
   if o.type.name=='MonoBehaviour':c.update(script=name(ptr(o,d['m_Script'])),strings=strings(o),typedFields=d)
   if o.type.name in ('Rigidbody','CharacterJoint','BoxCollider','CapsuleCollider','SphereCollider','Animator','Camera','SkinnedMeshRenderer'):c['fields']=d
   cs.append(c)
  rows.append({'path':path,'transformId':tr.path_id,'active':tree(g)['m_IsActive'],'localScale':td['m_LocalScale'],'localPosition':td['m_LocalPosition'],'localRotation':td['m_LocalRotation'],'components':cs})
  for p in td['m_Children']:walk(ptr(tr,p),path)
 walk(tr);return rows
raw=next(o for o in UnityPy.load(str(DATA/'level1')).objects if o.path_id==30323).get_raw_data()
def parse_row(offset):
 pos=offset;values={}
 def get(k,kind='i'):
  nonlocal pos
  if kind=='s':n=struct.unpack_from('<i',raw,pos)[0];v=raw[pos+4:pos+4+n].decode();pos=(pos+4+n+3)//4*4
  elif kind=='a':n=struct.unpack_from('<i',raw,pos)[0];v=list(struct.unpack_from('<'+'i'*n,raw,pos+4));pos+=4+4*n
  elif kind=='p':v=struct.unpack_from('<iq',raw,pos);pos+=12
  else:v=struct.unpack_from('<'+kind,raw,pos)[0];pos+=4
  values[k]=v
 for k,kind in [('id','s'),('level','i'),('cel','p'),('overworldController','p'),('race','a'),('group','i'),('archtype','i'),('decay','i'),('isScourge','i'),('isBoss','i'),('rarity','s'),('day','i'),('night','i'),('land','i'),('water','i'),('dungeon','i'),('camp','i'),('dontUseCampName','i'),('boat','i'),('destroyHex','i'),('realmInclude','a'),('realmExclude','a'),('weapon','p'),('health','i'),('defPhys','i'),('defMag','i'),('evade','f'),('awareness','f'),('crit','f'),('profChance','f'),('firstProfAsRegular','i'),('slots','i'),('maxdamage','f'),('accuracy','f'),('quickness','f'),('damageType','i')]:get(k,kind)
 for k in ['immuneBleed','immuneStun','immuneIce','immuneLightning','immuneFire','immuneDistract','immuneWater','reflectDamage','noFlee','distract','encourage','evasive','retaliates','smartCurse']:get(k)
 return values
mapping=json.loads((ROOT/'scratch/enemy-rig-mapping.json').read_text());names=['deathknightA','deathknightB','deathknightC','deathknightDboss','deathknightEboss','deathknightEbossEasy','harazuelMinionB'];rows=[];bodies={};weapons={};controllers={};materials={}
for n in names:
 m=next(x for x in mapping['enemies'] if x['enemy_id']==n);v=parse_row(m['db_byte_offset']);assert v['id']==n and v['cel'][1]==m['cel_path_id'] and v['weapon'][1]==m['weapon_path_id']
 rid=m['renderers'][0]['renderer_path_id'];ro=f.objects[rid];rd=tree(ro);mesh=ptr(ro,rd['m_Mesh']);md=tree(mesh);boneNames=[name(ptr(ptr(ro,p),tree(ptr(ro,p))['m_GameObject'])) for p in rd['m_Bones']]
 if rid not in bodies:bodies[rid]={'rendererId':rid,'meshId':mesh.path_id,'meshName':name(mesh),'orderedBones':boneNames,'inverseBindMatrices':md['m_BindPose'],'rootBone':rd['m_RootBone'],'nativeMaterialIds':[x['m_PathID'] for x in rd['m_Materials']],'hierarchy':hierarchy(roottr(ro))}
 for mp in rd['m_Materials']:
  mo=ptr(ro,mp);mt=tree(mo);materials[mo.path_id]={'id':mo.path_id,'name':name(mo),'shader':name(ptr(mo,mt['m_Shader'])),'keywords':mt['m_ShaderKeywords'],'properties':mt['m_SavedProperties']}
 wid=m['weapon_path_id']
 if wid not in weapons:weapons[wid]={'id':wid,'name':m['weapon_name'],'strings':strings(f.objects[wid]),'hierarchy':hierarchy(roottr(f.objects[wid]))}
 cid=m['weapon_animation_controller_candidates'][0]['controller_path_id']
 if cid not in controllers:
  co=f.objects[cid];cd=tree(co);clips=[]
  for cp in cd['m_AnimationClips']:
   o=ptr(co,cp);d=tree(o);clips.append({'id':o.path_id,'name':name(o),'stopTime':d.get('m_MuscleClip',{}).get('m_StopTime'),'events':d.get('m_Events')})
  controllers[cid]={'id':cid,'name':name(co),'clips':clips,'behaviours':[{'id':(o:=ptr(co,p)).path_id,'script':name(ptr(o,tree(o)['m_Script']))} for p in cd['m_StateMachineBehaviours']]}
 rows.append({'enemy':n,'mapping':m,'verifiedNativeRow':v,'rendererId':rid,'weaponId':wid,'combatControllerId':cid})
cmd=['/opt/homebrew/opt/dotnet/libexec/dotnet',str(Path.home()/'.dotnet/tools/.store/ilspycmd/10.1.0.8386/ilspycmd/10.1.0.8386/tools/net10.0/any/ilspycmd.dll'),str(DATA/'Managed/Assembly-CSharp.dll')]
def dec(n):(OUT/(n+'.cs')).write_bytes(subprocess.check_output(cmd+['-t',('GridEditor.'+n if n.startswith('FTK_') else n)]))
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(dec,['FTK_enemyCombat','FTK_proficiencyTable','Weapon','EnemyDummy','CharacterDummy','CharacterEventListener','AttackSchedule']))
result={'scope':'FRESH_NATIVE_METADATA_AND_ASSEMBLY_SOURCE_NO_RUNTIME_ACCEPTANCE','rows':rows,'bodies':bodies,'weapons':weapons,'controllers':controllers,'materials':materials}
(OUT/'native-metadata.json').write_text(json.dumps(result,indent=2)+'\n');print('rows',[(x['enemy'],x['rendererId'],x['weaponId'],x['combatControllerId']) for x in rows]);print('weapons',[(k,[s['text'] for s in v['strings']]) for k,v in weapons.items()]);print('body counts',[(k,dict(collections.Counter(c['type'] for r in v['hierarchy'] for c in r['components']))) for k,v in bodies.items()])
