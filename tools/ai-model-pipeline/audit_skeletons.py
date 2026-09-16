#!/usr/bin/env python3
"""Extract and validate representative native rigs locally; never claims live support."""
import argparse
import contextlib
import io
import json
import subprocess
from pathlib import Path
import numpy as np
import UnityPy
from extract_reference import extract
from export_ftk_glb import write_glb
from inventory_skeletons import file_hash
from validate_glb import validate


def audit(assets, inventory_path, output, renderer_ids=None):
    # Derived copyrighted geometry must stay under gitignored local storage.
    output=output.resolve();output.mkdir(parents=True,exist_ok=True)
    probe=output/'reference.npz'
    check=subprocess.run(['git','check-ignore','--quiet',str(probe)],cwd=Path(__file__).resolve().parents[2])
    if check.returncode: raise ValueError('Audit output must be under a gitignored directory (use scratch/)')
    catalog=json.loads(inventory_path.read_text())
    if file_hash(assets)!=catalog['source_sha256']: raise ValueError('Inventory source hash does not match assets')
    profiles=catalog['candidate_profiles']
    if renderer_ids:
        by_renderer={r['renderer_path_id']:r for r in catalog['renderers']}
        profiles=[dict(representative_renderer_path_id=i,rig_profile_fingerprint=by_renderer[i].get('rig_profile_fingerprint')) for i in renderer_ids]
    env=UnityPy.load(str(assets))
    results=[]
    report=dict(source_sha256=catalog['source_sha256'],inventory_file=str(inventory_path),
                scope='One native mesh roundtrip per exact bind/topology candidate profile, or explicit renderer selection.',
                limitations=['Local extraction/export validation only, not custom art or in-game renderer/animation support.',
                             'Source native UVs flipped in a copy for runtime GLB V contract.',
                             'A failed representative does not imply every mesh on that skeleton fails.'],results=results)
    for profile in profiles:
        renderer=profile['representative_renderer_path_id'];target=output/str(renderer)
        entry=dict(renderer_path_id=renderer,rig_profile_fingerprint=profile['rig_profile_fingerprint'])
        phase='extract'
        transcript=io.StringIO()
        try:
            with contextlib.redirect_stdout(transcript):
                extract(assets,renderer,target,env=env)
                phase='export'
                reference=np.load(target/'reference.npz',allow_pickle=False)
                source={k:reference[k].copy() for k in reference.files}
                source['uvs'][:,1]=1-source['uvs'][:,1]
                write_glb(target/'native-roundtrip.glb',source,reference)
                phase='validate'
                entry['validation']=validate(target/'native-roundtrip.glb',reference)
                reference.close()
            entry['status']='offline_roundtrip_pass'
        except Exception as exc:
            entry.update(status='failed',phase=phase,error_type=type(exc).__name__,error=str(exc))
        target.mkdir(parents=True,exist_ok=True)
        (target/'audit.log').write_text(transcript.getvalue())
        results.append(entry)
        report['summary']=dict(attempted=len(results),passed=sum(r['status']=='offline_roundtrip_pass' for r in results),
                               failed=sum(r['status']=='failed' for r in results),selected_profiles=len(profiles))
        (output/'audit.json').write_text(json.dumps(report,indent=2)+'\n')
        print(f"{renderer}: {entry['status']}"+(f" ({entry.get('phase')}): {entry.get('error')}" if entry['status']=='failed' else ''),flush=True)
    return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--assets',type=Path,required=True);p.add_argument('--inventory',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--renderer-id',type=int,action='append',help='Optional repeatable subset; default all candidate profile representatives')
    a=p.parse_args();r=audit(a.assets,a.inventory,a.output,a.renderer_id);print(json.dumps(r['summary'],indent=2))
