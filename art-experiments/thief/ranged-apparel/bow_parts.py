#!/usr/bin/env python3
"""Split authored bows into rigid body, string and exact hierarchy-local break art."""
import copy,importlib.util,json,sys
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2]
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_static_glb

def matrix(row):
    x,y,z,w=row['rotation'];m=np.eye(4)
    m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                     [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                     [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(row['scale'])
    m[:3,3]=row['position'];return m
def subset(data,selected):
    keep=[]
    for piece in selected:keep+=list(range(piece['vertex_start'],piece['vertex_start']+piece['vertex_count']))
    mapping={v:i for i,v in enumerate(keep)}
    result={field:[copy.deepcopy(data[field][n]) for n in keep] for field in ['positions','normals','uvs']}
    result['triangles']=[[mapping[n] for n in t] for t in data['triangles'] if t[0] in mapping]
    return result
def write(key,data,transform=None):
    if transform is not None:
        data['positions']=(np.array(data['positions'])@transform[:3,:3].T+transform[:3,3]).tolist()
        ns=np.array(data['normals'])@np.linalg.inv(transform[:3,:3]);data['normals']=(ns/np.linalg.norm(ns,axis=1)[:,None]).tolist()
    (OUT/(key+'.source.json')).write_text(json.dumps(data,separators=(',',':'))+'\n')
    write_static_glb(OUT/(key+'.glb'),data)

def main():
    manifest=json.loads((OUT/'manifest.json').read_text());route=json.loads((OUT/'bow-route.json').read_text())
    rows={r['path']:r for r in route['rows']};weapon=matrix(rows['bowShort/shortbow']);string=matrix(rows['bowShort/shortbow/shortbowString'])
    fragment=matrix(rows['bowShort/shortbow/Break']);fragment2=fragment@matrix(rows['bowShort/shortbow/Break/Break'])
    for item in manifest['items']:
        if item['family']!='bow':continue
        key=item['id'].replace('_','-');data=json.loads((OUT/(key+'.source.json')).read_text());pieces=json.loads((OUT/(key+'.pieces.json')).read_text())
        strings=[p for p in pieces if 'bowstring' in p['name']];body=[p for p in pieces if p not in strings]
        lower=[p for p in body if p['name'].endswith('-1')];upper=[p for p in body if p not in lower]
        for suffix,selected,transform in [('body',body,None),('string',strings,np.linalg.inv(string)),
            ('fragment-1',upper,np.linalg.inv(fragment)),('fragment-2',lower,np.linalg.inv(fragment2)),
            ('display-body',body,np.linalg.inv(weapon)),('display-string',strings,np.linalg.inv(weapon@string))]:
            write(key+'-'+suffix,subset(data,selected),transform)
        item['rigidRoute']={'template':'bowShort','metadata':'bow-route.json','body':key+'-body.glb','bodyPath':'.','string':key+'-string.glb','stringPath':'shortbowString','fragments':[{'path':'Break','file':key+'-fragment-1.glb'},{'path':'Break/Break','file':key+'-fragment-2.glb'}],'displayBody':{'path':'shortbow','file':key+'-display-body.glb'},'displayString':{'path':'shortbow/shortbowString','file':key+'-display-string.glb'},'status':'Exact hierarchy-local original assets only. Native string component mesh layout, draw response and marker alignment are not yet integrated or verified.'}
    (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')

if __name__=='__main__':main()
