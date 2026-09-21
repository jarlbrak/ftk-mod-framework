#!/usr/bin/env python3
"""Verify rigid helmet placement using binding metadata and a read-only native observation."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--observation',type=Path,default=ROOT/'scratch/paladin-preview-female-output.txt')
    args=parser.parse_args()
    raw=args.observation.read_text();observed=json.loads(raw[raw.index('{'):])
    assert observed['ok'] and observed['status']=='observed_actual_native_player_preview'
    body=next(r for r in observed['renderers'] if r['celRelativeRendererPath']=='playerBlacksmith')
    bones={b['name']:b for b in body['bones']}
    head_world=np.array(bones['Head_M']['localToWorld']).reshape(4,4)
    hair_world=np.array(bones['Hair_M']['localToWorld']).reshape(4,4)
    head_bind=np.array(bones['Head_M']['bindposeRowMajor']).reshape(4,4)
    center=np.eye(4);center[:3,3]=np.linalg.inv(head_bind)[:3,3]
    mount=np.array([[0.,-1,0,0],[0,0,1,0],[-1,0,0,0],[0,0,0,1]])
    desired=head_world@head_bind@center
    current=hair_world@mount
    results=[]
    manifest=json.loads((OUT/'manifest.json').read_text())
    for item in manifest['assets']:
        if item['part']!='helmet':continue
        source=json.loads((OUT/(item['key']+'.source.json')).read_text())
        correction=np.array(source['authorToRuntime'])
        actual=np.c_[source['positions'],np.ones(len(source['positions']))]
        authored=actual@np.linalg.inv(correction).T
        error=np.linalg.norm((actual@current.T-authored@desired.T)[:,:3],axis=1)
        assert error.max()<3e-5,(item['key'],float(error.max()))
        old_error=np.linalg.norm((authored@current.T-authored@desired.T)[:,:3],axis=1)
        results.append({'key':item['key'],'maxCorrectedWorldError':float(error.max()),'maxPriorWorldError':float(old_error.max())})
    assert len(results)==6
    report={'status':'PASS','observationSha256':hashlib.sha256(args.observation.read_bytes()).hexdigest(),
        'formula':'inverse(nativeMountEulerMatrix) * HairInverseBind * Translate(HeadBindOrigin)',
        'nativeMountEuler':[-90,90,0],'authorToRuntime':correction.tolist(),'assets':results,
        'scope':'Permitted binding/transform metadata only. Corrected original vertices reproduce intended Head-centered world placement in the captured female pose. No native surface, bounds or animation curves read. Male shares rest correction within 1e-6. Fresh live rendering and other classes remain unverified.'}
    (OUT/'helmet-mount-validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS six helmets; maximum captured-pose placement error',max(r['maxCorrectedWorldError'] for r in results))


if __name__=='__main__':main()
