exec(open('scratch/dragon-frost-native-topology-analysis/audit_source.py').read().split('r=f.objects')[0])
import struct
out={}
for rid in (121561,121525):
 r=f.objects[rid];d=t(r);mesh=ptr(r,d['m_Mesh']);md=t(mesh)
 out[str(rid)]={'renderer':d,'meshId':mesh.path_id,'meshName':md['m_Name'],'bindposes':md['m_BindPose'],'boneNames':[name(ptr(ptr(r,p),t(ptr(r,p))['m_GameObject'])) for p in d['m_Bones']]}
print('mesh/bones/binds equality',[(k,out['121561'][k]==out['121525'][k]) for k in ['meshId','bindposes','boneNames']])
raw=next(o for o in UnityPy.load(str(assets.parent/'level1')).objects if o.path_id==30323).get_raw_data();p=74872
values={}
def read(key,kind='i'):
 global p
 if kind=='s':n=struct.unpack_from('<i',raw,p)[0];v=raw[p+4:p+4+n].decode();p=(p+4+n+3)//4*4
 elif kind=='a':n=struct.unpack_from('<i',raw,p)[0];v=list(struct.unpack_from('<'+'i'*n,raw,p+4));p+=4+4*n
 elif kind=='p':v=struct.unpack_from('<iq',raw,p);p+=12
 else:v=struct.unpack_from('<'+kind,raw,p)[0];p+=4
 values[key]=v
for k,kind in [('id','s'),('level','i'),('cel','p'),('overworldController','p'),('race','a'),('group','i'),('archtype','i'),('decay','i'),('isScourge','i'),('isBoss','i'),('rarity','s'),('day','i'),('night','i'),('land','i'),('water','i'),('dungeon','i'),('camp','i'),('dontUseCampName','i'),('boat','i'),('destroyHex','i'),('realmInclude','a'),('realmExclude','a'),('weapon','p'),('health','i'),('defPhys','i'),('defMag','i'),('evade','f'),('awareness','f'),('crit','f'),('profChance','f'),('firstProfAsRegular','i'),('slots','i'),('maxdamage','f'),('accuracy','f'),('quickness','f'),('damageType','i')]:read(k,kind)
for k in ['immuneBleed','immuneStun','immuneIce','immuneLightning','immuneFire','immuneDistract','immuneWater','reflectDamage','noFlee','distract','encourage','evasive','retaliates','smartCurse']:read(k)
out['row']=values;print('row',values)
raw=next(o for o in UnityPy.load(str(assets.parent/'sharedassets1.assets')).objects if o.path_id==4891).get_raw_data();out['proficiencies']=[]
for prof in ['enDragonFrostA','enDragonFrostB','enDragonFrostC']:
 p=raw.index(struct.pack('<i',len(prof))+prof.encode());start=p;values={}
 for k,kind in [('id','s'),('display','s'),('title','s'),('checkID','i')]:read(k,kind)
 p+=16+12+12
 for k,kind in [('weaponSize','i'),('direction','i'),('quickness','f'),('repeat','i'),('chanceAffect','f'),('damagePerAttack','i'),('boatDamage','i'),('fullSlots','i'),('target','i'),('alwaysHitFx','i'),('targetFriendly','i'),('harmless','i'),('suicide','i'),('gunShot','i'),('chaos','i')]:read(k,kind)
 values['rowOffset']=start;out['proficiencies'].append(values)
print('profs',out['proficiencies']);out['material']=t(f.objects[103]);out['behaviours']=[]
for p in t(f.objects[5951])['m_StateMachineBehaviours']:
 o=ptr(f.objects[5951],p);d=t(o);out['behaviours'].append(dict(id=o.path_id,script=name(ptr(o,d['m_Script']))))
(OUT/'details.json').write_text(json.dumps(out,indent=2)+'\n')
