import json,hashlib
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr
OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent;assets=Path.home()/'Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets';e=UnityPy.load(str(assets));f=next(f for f in e.files.values()if Path(f.name).name==assets.name)
def ptr(o,p):return PPtr(m_FileID=p['m_FileID'],m_PathID=p['m_PathID'],assetsfile=o.assets_file).deref() if p['m_PathID'] else None
def t(o):return o.read_typetree(check_read=False)
def name(o):return t(o).get('m_Name','') if o else None
r=f.objects[121561];go=ptr(r,t(r)['m_GameObject']);trans=next(ptr(go,c['component'])for c in t(go)['m_Component']if ptr(go,c['component']).type.name=='Transform')
while t(trans)['m_Father']['m_PathID']:trans=ptr(trans,t(trans)['m_Father'])
rows=[];monos=[];mats=[]
def walk(tr,path=''):
 d=t(tr);g=ptr(tr,d['m_GameObject']);gd=t(g);path=path+'/'+gd['m_Name'];cs=[]
 for c in gd['m_Component']:
  o=ptr(g,c['component']);od=t(o);entry=dict(type=o.type.name,id=o.path_id)
  if o.type.name=='MonoBehaviour':entry['script']=name(ptr(o,od['m_Script']));monos.append(dict(id=o.path_id,script=entry['script'],fields=od))
  if o.type.name in ['MeshRenderer','SkinnedMeshRenderer']:
   for p in od.get('m_Materials',[]):
    m=ptr(o,p);md=t(m);mats.append(dict(id=m.path_id,name=name(m),keywords=md.get('m_ShaderKeywords'),colors=md['m_SavedProperties']['m_Colors']))
  cs.append(entry)
 row=dict(path=path,transform_id=tr.path_id,active=gd['m_IsActive'],components=cs,localPosition=d['m_LocalPosition'],localRotation=d['m_LocalRotation'],localScale=d['m_LocalScale'])
 if gd['m_Name'].endswith('Cam')or gd['m_Name']=='CameraRoot':row.update(localPosition=d['m_LocalPosition'],localRotation=d['m_LocalRotation'],localScale=d['m_LocalScale'])
 rows.append(row)
 for c in d['m_Children']:walk(ptr(tr,c),path)
walk(trans);controller=f.objects[5951];cd=t(controller);clips=[]
for p in cd['m_AnimationClips']:
 o=ptr(controller,p);d=t(o);clips.append(dict(id=o.path_id,name=name(o),events=d.get('m_Events'),stopTime=d.get('m_MuscleClip',{}).get('m_StopTime')))
result=dict(sourceAssetsSha256=hashlib.sha256(assets.read_bytes()).hexdigest(),hierarchy=rows,materials=mats,controller=dict(id=5951,name=name(controller),clips=clips),monos=monos)
(OUT/'native-metadata.json').write_text(json.dumps(result,indent=2)+'\n');print('components',[(m['id'],m['script'],list(m['fields']))for m in monos]);print('materials',mats);print('markers',[r for r in rows if r['path'].endswith('Cam')]);print('deathclips',[c for c in clips if 'death' in c['name'].lower()])
