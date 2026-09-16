"""Read-only prefab component and death animation metadata; no surface extraction."""
import json,hashlib
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr
OUT=Path(__file__).resolve().parent
assets=Path.home()/'Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets'
env=UnityPy.load(str(assets));f=next(f for f in env.files.values()if Path(f.name).name==assets.name)
def ref(obj,p):return PPtr(m_FileID=p['m_FileID'],m_PathID=p['m_PathID'],assetsfile=obj.assets_file).deref() if p['m_PathID'] else None
def tree(o):return o.read_typetree()
r=f.objects[121306];g=ref(r,tree(r)['m_GameObject']);t=next(ref(g,c['component'])for c in tree(g)['m_Component']if ref(g,c['component']).type.name=='Transform')
while tree(t)['m_Father']['m_PathID']:t=ref(t,tree(t)['m_Father'])
rows=[]
def walk(t,path=''):
 d=tree(t);g=ref(t,d['m_GameObject']);gd=tree(g);path=path+'/'+gd['m_Name'];comps=[]
 for c in gd['m_Component']:
  o=ref(g,c['component']);od=tree(o) if o.type.name!='MonoBehaviour' else {};entry=dict(type=o.type.name,id=o.path_id)
  if o.type.name in ['MeshRenderer','SkinnedMeshRenderer']:entry['enabled']=od.get('m_Enabled')
  comps.append(entry)
 rows.append(dict(path=path,active=gd['m_IsActive'],components=comps))
 for p in d['m_Children']:walk(ref(t,p),path)
walk(t);clips=[]
for o in f.objects.values():
 if o.type.name!='AnimationClip':continue
 d=tree(o)
 if 'clam' not in d.get('m_Name','').lower() or 'death' not in d['m_Name'].lower():continue
 clips.append(dict(id=o.path_id,name=d['m_Name'],events=d.get('m_Events'),floatCurves=d.get('m_FloatCurves'),pptrCurves=d.get('m_PPtrCurves')))
result=dict(source_sha256=hashlib.sha256(assets.read_bytes()).hexdigest(),prefab_hierarchy=rows,death_clips=clips,scope='Serialized metadata only. Runtime event branch requires source behavior verification.')
(OUT/'native-death-source-metadata.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
