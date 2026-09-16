"""Read-only native material scalar metadata; never export native surfaces or textures."""
import json,hashlib,argparse
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr
p=argparse.ArgumentParser();p.add_argument('--assets',type=Path,required=True);a=p.parse_args();e=UnityPy.load(str(a.assets));f=next(f for f in e.files.values() if Path(f.name).name==a.assets.name);rows=[]
for rid in [121306]:
 obj=f.objects[rid];t=obj.read_typetree();mats=[]
 for ptr in t['m_Materials']:
  m=PPtr(m_FileID=ptr['m_FileID'],m_PathID=ptr['m_PathID'],assetsfile=obj.assets_file).deref();d=m.read_typetree();props=d['m_SavedProperties'];mats.append(dict(asset=Path(m.assets_file.name).name,path_id=m.path_id,name=d['m_Name'],keywords=d.get('m_ShaderKeywords'),colors=props['m_Colors'],floats=props['m_Floats']))
 rows.append(dict(renderer_id=rid,materials=mats))
result=dict(source_assets=a.assets.name,source_sha256=hashlib.file_digest(a.assets.open('rb'),'sha256').hexdigest(),renderers=rows)
Path(__file__).with_name('native-material-metadata.json').write_text(json.dumps(result,indent=2)+'\n')
for row in rows:print(row['renderer_id'],[(m['path_id'],m['name'],m['keywords']) for m in row['materials']])
