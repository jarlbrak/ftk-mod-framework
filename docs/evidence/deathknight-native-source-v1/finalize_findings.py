exec((__import__('pathlib').Path(__file__).parent/'reconcile.py').read_text().split('mapping=json.loads')[0])
meta=json.loads((OUT/'native-metadata.json').read_text());profraw=next(o for o in UnityPy.load(str(DATA/'sharedassets1.assets')).objects if o.path_id==4891).get_raw_data()
def proficiency(n):
 b=profraw;p=b.index(struct.pack('<i',len(n))+n.encode());start=p;v={}
 def get(k,kind='i'):
  nonlocal p
  if kind=='s':c=struct.unpack_from('<i',b,p)[0];x=b[p+4:p+4+c].decode();p=(p+4+c+3)//4*4
  else:x=struct.unpack_from('<'+kind,b,p)[0];p+=4
  v[k]=x
 for k,t in [('id','s'),('display','s'),('title','s'),('checkID','i')]:get(k,t)
 p+=16+12+12
 for k,t in [('weaponSize','i'),('direction','i'),('quickness','f'),('repeat','i'),('chanceAffect','f'),('damagePerAttack','i'),('boatDamage','i'),('fullSlots','i'),('target','i'),('alwaysHitFx','i'),('targetFriendly','i'),('harmless','i'),('suicide','i'),('gunShot','i'),('chaos','i')]:get(k,t)
 v['rowOffset']=start;return v
weapons={};allprofs={}
for wid,w in meta['weapons'].items():
 entry=next(s for s in w['strings'] if s['text'].startswith('[{"Key"'));profs=[x['Key']['m_ID'] for x in json.loads(entry['text'])];schedule=None
 for row in w['hierarchy']:
  for c in row['components']:
   if c.get('script')=='AttackSchedule':
    b=f.objects[c['id']].get_raw_data();shuffle=struct.unpack_from('<i',b,32)[0];count=struct.unpack_from('<i',b,36)[0];assert shuffle in [0,1] and 0<count<100;items=list(struct.unpack_from('<'+'i'*count,b,40));assert all(x in [-2,-1,0,1,2] for x in items);schedule={'componentId':c['id'],'shuffle':bool(shuffle),'items':items,'serializedFirstItemBeforeShuffle':profs[items[0]] if items[0]>=0 else 'native normal/direct action','actualFirstAction':'runtime shuffled; not established' if shuffle else (profs[items[0]] if items[0]>=0 else 'native normal/direct action'),'fieldOffsets':{'shuffle':32,'count':36,'items':40}}
 for n in profs:allprofs[n]=proficiency(n)
 weapons[wid]={'name':w['name'],'orderedProficiencies':profs,'dictionaryStringOffset':entry['offset'],'attackSchedule':schedule,'componentTypes':dict(collections.Counter(c.get('script',c['type']) for r in w['hierarchy'] for c in r['components']))}
assert all(p['suicide']==0 for p in allprofs.values())
base=meta['bodies']['121217'];body={}
for rid,b in meta['bodies'].items():
 counts=collections.Counter(c['type'] for r in b['hierarchy'] for c in r['components']);scripts=collections.Counter(c.get('script') for r in b['hierarchy'] for c in r['components'] if c['type']=='MonoBehaviour')
 body[rid]={'sameMeshAs121217':b['meshId']==base['meshId'],'orderedBoneNamesEqual121217':b['orderedBones']==base['orderedBones'],'IBMsEqual121217':b['inverseBindMatrices']==base['inverseBindMatrices'],'boneCount':len(b['orderedBones']),'meshId':b['meshId'],'nativeRootScale':b['hierarchy'][0]['localScale'],'nativeMaterialIds':b['nativeMaterialIds'],'componentCounts':dict(counts),'scripts':dict(scripts),'bodyUVScrollerComponents':scripts.get('ScrollingUVs',0),'rigidAttachmentPaths':[r['path'] for r in b['hierarchy'] if any(c['type']=='MeshRenderer' for c in r['components'])],'markers':[{'path':r['path'],'transformId':r['transformId'],'localPosition':r['localPosition'],'localRotation':r['localRotation']} for r in b['hierarchy'] if any(c['type']=='Camera' for c in r['components'])]}
material={}
for mid,m in meta['materials'].items():
 mo=f.objects[int(mid)];d=tree(mo);sh=ptr(mo,d['m_Shader']);material[mid]={'name':m['name'],'shaderId':sh.path_id,'shaderName':tree(sh).get('m_ParsedForm',{}).get('m_Name'),'keywords':m['keywords'],'emissionColor':dict(m['properties']['m_Colors']).get('_EmissionColor'),'textures':[{'property':k,'textureId':v['m_Texture']['m_PathID'],'textureName':name(ptr(mo,v['m_Texture'])),'offset':v['m_Offset'],'scale':v['m_Scale']} for k,v in m['properties']['m_TexEnvs'] if v['m_Texture']['m_PathID']]}
profiles=json.loads((ROOT/'scratch/mirewarden-game/model-test-profiles.json').read_text())['profiles'];rows=[]
for r in meta['rows']:
 matches=[p for p in profiles if p['key']=='ftkmf_modeltest_probe_'+r['enemy'].lower()];assert len(matches)==1
 sharedobj=next(o for o in UnityPy.load(str(DATA/'sharedassets1.assets')).objects if o.path_id==5124);sharedraw=sharedobj.get_raw_data()
 oldraw=raw;raw=sharedraw;offset=sharedraw.index(struct.pack('<i',len(r['enemy']))+r['enemy'].encode());second=parse_row(offset);raw=oldraw;comparison=__import__('copy').deepcopy(second)
 for field in ['cel','overworldController','weapon']:
  fid,pid=second[field]
  if pid:assert fid>0 and Path(sharedobj.assets_file.externals[fid-1].path).name=='resources.assets'
  comparison[field]=(r['verifiedNativeRow'][field][0],pid)
 assert comparison=={k:(tuple(v) if k in ['cel','overworldController','weapon'] else v) for k,v in r['verifiedNativeRow'].items()}, (r['enemy'],second,r['verifiedNativeRow'])
 rows.append({'sharedTablePrefixEqualAfterResolvedExternalFileIds':True,'sharedTableRowOffset':offset,'enemy':r['enemy'],'nativeRow':r['verifiedNativeRow'],'celId':r['mapping']['cel_path_id'],'rendererId':r['rendererId'],'weaponId':r['weaponId'],'combatControllerId':r['combatControllerId'],'runtimeProbeProfile':matches[0]})
result={'status':'SOURCE_RECONCILED_7_NATIVE_ROWS_5_RENDERERS_NO_LIVE_ACCEPTANCE','rows':rows,'bodies':body,'materials':material,'weapons':weapons,'proficiencies':allprofs,'controllers':meta['controllers'],'firstDiagnostic':{'enemy':'deathknightA','rendererId':121217,'reason':'Native dungeon-eligible nonboss, lowest native health50/physical defense12, sole enDeathKnightDaze proficiency and no AttackSchedule. Exact representative renderer avoids sibling substitution.','nativeScale':1.2,'firstActionExpectation':'With chanceToProf1 and sole non-suicide proficiency, ordinary RNG path selects enDeathKnightDaze; actual first action and removal still require native observation.','risks':['Physical armor12 can reduce ordinary hit to zero; do not fabricate damage or silently lower defenses.','Helmet and shield are separate native rigid renderers, plus separately instantiated weapon. Body-only GLB leaves them present.','Emissive native material can wash out original colors; choose and document explicit opt-out for an original only if appropriate.','Native joints imply ragdoll path eligibility; count includes detachable/equipment bodies and is not all-dynamic proof.']},'nativeLifecycleSource':{'ragdoll':'CharacterEventListener.OnEnable sets m_DoRagdoll when m_AnimRoot contains CharacterJoint; DoRagDoll skips FTKArrow and Detachable bodies and disables Animator. CheckRagdollDeathSound invokes it and replays selected death events.','deathEvents':'Blunt5983 death clips include DeathFallOff, DropWeapon, DropShield. Bladed5982 heavy includes these; light clip has Foley only. DeathFallOff returns for DeathRevive or DeathLight.','cleanup':'No body FallOffLimb or ScrollingUVs components found. Native Detachable helmet and Shield persist as separate scoped components; native resource/final corpse cleanup must be observed.','removalRisk':'All seven discovered weapon proficiencies have m_Suicide=0. No first-action suicide path indicated. Generic flee flag and external encounter/quest paths remain possible; this is not a universal no-removal proof.'},'differences':['A/B/C use same weapon134921 and controller5983 but different rows/health/defenses, C adds ice material/particles/light.','Dboss134922 has separate schedule/proficiencies and1.3scale, native dungeon spawning false.','Eboss/EbossEasy share CEL137589/renderer121220/weapon135088/controller5982 and1.3scale but differ health/defenses/proficiency chance/race/immunities.','harazuelMinionB shares C CEL/render bindings but separate ice weapon134923, schedule and stats; normal chanceToProf0 does not override AttackSchedule precedence.'],'limitations':['Recognizers use verified decompiled serialized prefixes, not complete custom MonoBehaviour deserialization.','Exact bones/IBMs/mesh equality is source compatibility only, not appearance/motion/physics/material or sibling runtime acceptance.','Current runtime combatProfile is64hex from pinned deployed catalog; older mapping compact fingerprints are not interchangeable.','No native geometry/texture pixels/animation curves exported by these scripts; matrices/paths/events remain ignored local metadata.']}
(OUT/'findings.json').write_text(json.dumps(result,indent=2)+'\n')
paths=[asset,DATA/'level1',DATA/'sharedassets1.assets',DATA/'Managed/Assembly-CSharp.dll',ROOT/'scratch/enemy-rig-mapping.json',ROOT/'scratch/mirewarden-game/model-test-profiles.json']+list(OUT.glob('*.cs'))+list(OUT.glob('*.py'))
(OUT/'source-pins.json').write_text(json.dumps({'sources':[{'path':str(p),'sha256':sha(p),'bytes':p.stat().st_size} for p in paths]},indent=2)+'\n')
print('schedules',weapons);print('proficiencies',allprofs);print('findings',sha(OUT/'findings.json'))
