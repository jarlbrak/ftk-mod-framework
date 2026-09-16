#!/usr/bin/env python3
"""Run in Blender: audit scaffold/save/reopen/export for each native rig representative."""
import argparse
import contextlib
import hashlib
import io
import json
import math
import subprocess
import sys
import traceback
from pathlib import Path
import bpy
sys.path.insert(0,str(Path(__file__).resolve().parent))
from create_blender_template import create
from export_blender_model import export


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def audit(audit_dir,output,python_executable,renderer_ids=None,radius_scale=1.0,joint_scope="all",connections="hierarchy",marker_radius=None):
    if marker_radius is not None and (not math.isfinite(marker_radius) or marker_radius <= 0): raise ValueError('marker-radius must be finite and positive')
    if connections not in ('hierarchy','none'): raise ValueError('connections must be hierarchy or none')
    if joint_scope not in ('all','weighted'): raise ValueError('joint-scope must be all or weighted')
    if not math.isfinite(radius_scale) or not .25 <= radius_scale <= 16:
        raise ValueError('radius-scale must be finite and between 0.25 and 16')
    audit_dir=audit_dir.resolve();output=output.resolve();python_executable=python_executable.resolve()
    source=json.loads((audit_dir/'audit.json').read_text())
    entries=source['results']
    if renderer_ids:
        selected=set(renderer_ids);entries=[r for r in entries if r['renderer_path_id'] in selected]
        if len(entries)!=len(selected):raise ValueError('Renderer subset contains an unknown/duplicate audit representative')
    output.mkdir(parents=True,exist_ok=True)
    report=dict(scope='Original calibration scaffold/save/reopen/export validation in Blender; no live or artistic support claim',
        blender_version=bpy.app.version_string,radius_scale=radius_scale,joint_scope=joint_scope,connections=connections,marker_radius_override=marker_radius,source_sha256=source['source_sha256'],
        audit_manifest_sha256=sha(audit_dir/'audit.json'),results=[])
    for entry in entries:
        renderer=entry['renderer_path_id'];directory=output/str(renderer);directory.mkdir(parents=True,exist_ok=True)
        reference=audit_dir/str(renderer)/'reference.npz';skeleton=audit_dir/str(renderer)/'skeleton.json'
        scene=directory/'bind.blend';glb=directory/'calibration.glb';phase='create';log=io.StringIO()
        result=dict(renderer_path_id=renderer,rig_profile_fingerprint=entry['rig_profile_fingerprint'],
                    reference_sha256=sha(reference),skeleton_sha256=sha(skeleton),radius_scale=radius_scale)
        try:
            with contextlib.redirect_stdout(log),contextlib.redirect_stderr(log):
                create(reference,skeleton,scene,python_executable,False,radius_scale,joint_scope,connections,marker_radius)
                phase='reopen'
                bpy.ops.wm.open_mainfile(filepath=str(scene))
                phase='export'
                export(glb)
                phase='independent_validation'
                validation=subprocess.run([str(python_executable),str(Path(__file__).with_name('validate_glb.py')),
                    str(glb),'--reference',str(reference)],capture_output=True,text=True)
                log.write(validation.stdout+validation.stderr)
                if validation.returncode:raise RuntimeError('Independent validator failed: '+validation.stderr.strip())
                result['validation']=json.loads(validation.stdout)
            result.update(status='offline_blender_bridge_pass',scene_sha256=sha(scene),glb_sha256=sha(glb),
                          source_mesh_sha256=sha(glb.with_suffix('.source.json')),texture_sha256=sha(glb.with_suffix('.png')))
        except Exception as exc:
            log.write(traceback.format_exc());result.update(status='failed',phase=phase,error_type=type(exc).__name__,error=str(exc))
        (directory/'bridge-audit.log').write_text(log.getvalue())
        report['results'].append(result)
        report['summary']=dict(selected=len(entries),attempted=len(report['results']),
            passed=sum(r['status']=='offline_blender_bridge_pass' for r in report['results']),
            failed=sum(r['status']=='failed' for r in report['results']))
        (output/'bridge-audit.json').write_text(json.dumps(report,indent=2)+'\n')
        print(f"{renderer}: {result['status']}"+(f" [{phase}] {result.get('error')}" if result['status']=='failed' else ''),flush=True)
    print(json.dumps(report['summary'],indent=2))
    return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--audit-dir',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True)
    p.add_argument('--python-executable',type=Path,required=True);p.add_argument('--renderer-id',type=int,action='append')
    p.add_argument('--radius-scale',type=float,default=1.0)
    p.add_argument('--joint-scope',choices=['all','weighted'],default='all')
    p.add_argument('--connections',choices=['hierarchy','none'],default='hierarchy')
    p.add_argument('--marker-radius',type=float)
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    result=audit(a.audit_dir,a.output_dir,a.python_executable,a.renderer_id,a.radius_scale,a.joint_scope,a.connections,a.marker_radius)
    if result['summary']['failed']: raise SystemExit(1)
