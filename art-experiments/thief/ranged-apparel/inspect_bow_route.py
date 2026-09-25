#!/usr/bin/env python3
"""Inspect bowShort identity and transforms without reading native surface data."""
import argparse,hashlib,json
from pathlib import Path
import UnityPy
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--assets',type=Path,required=True);args=parser.parse_args()
    env=UnityPy.load(str(args.assets));rows=[]
    def walk(ob,path):
        d=ob.read();components=[c.component.deref() for c in d.m_Component]
        t=next(c.read() for c in components if c.type.name=='Transform')
        row={'path':path,'gameObjectId':ob.path_id,'components':[c.type.name for c in components],
             'position':[t.m_LocalPosition.x,t.m_LocalPosition.y,t.m_LocalPosition.z],
             'rotation':[t.m_LocalRotation.x,t.m_LocalRotation.y,t.m_LocalRotation.z,t.m_LocalRotation.w],
             'scale':[t.m_LocalScale.x,t.m_LocalScale.y,t.m_LocalScale.z]}
        for c in components:
            if c.type.name in ['MeshRenderer','SkinnedMeshRenderer']:row['rendererId']=c.path_id
        rows.append(row)
        for child in t.m_Children:
            go=child.read().m_GameObject.deref();walk(go,path+'/'+go.read().m_Name)
    for ob in env.objects:
        if ob.type.name=='GameObject' and ob.read().m_Name=='bowShort':walk(ob,'bowShort')
    assert len(rows)==8 and rows[0]['gameObjectId']==7333
    result={'sourceAsset':args.assets.name,'sourceSha256':hashlib.sha256(args.assets.read_bytes()).hexdigest(),
            'scope':'Identity, component and transform metadata only; no surfaces, weights, textures or animations decoded.','rows':rows}
    Path(__file__).with_name('bow-route.json').write_text(json.dumps(result,indent=2)+'\n')
    print('PASS: bowShort hierarchy metadata; zero native surface reads')
if __name__=='__main__':main()
