#!/usr/bin/env python3
"""Offline matrix audit only. Reads local FTK metadata; never writes game assets."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import UnityPy
from scipy.spatial.transform import Rotation

MAPPING={
 'Root_M/joint1':'Root_M/base/body',
 'Root_M/joint1/neck':'Root_M/base/body/neck',
 'Root_M/joint1/neck/head':'Root_M/base/body/neck/head',
 'Root_M/joint1/neck/head/topHead':'Root_M/base/body/neck/head/topHead'}
JAW='Root_M/joint1/neck/jaw'

def sha(value):return hashlib.sha256(value).hexdigest()
def matrix(data):
    q=[data['m_LocalRotation'][k] for k in 'xyzw']
    result=np.eye(4);result[:3,:3]=Rotation.from_quat(q).as_matrix()@np.diag([data['m_LocalScale'][k] for k in 'xyz'])
    result[:3,3]=[data['m_LocalPosition'][k] for k in 'xyz'];return result

def extract(assets):
    env=UnityPy.load(str(assets));source=next(f for f in env.files.values() if Path(f.name).name==assets.name)
    def tree(pid):return source.objects[pid].read_typetree(check_read=False)
    def hierarchy(renderer_id):
        row=tree(renderer_id);locals={};palette=[]
        def add(pid):
            tr=tree(pid);parent=tr['m_Father']['m_PathID']
            if not parent:return '' # Common prefab-root coordinate space excludes root object's own TRS.
            name=tree(tr['m_GameObject']['m_PathID'])['m_Name'];prefix=add(parent)
            path=(prefix+'/' if prefix else '')+name;locals[path]=matrix(tr);return path
        for bone in row['m_Bones']:palette.append(add(bone['m_PathID']))
        mesh=row['m_Mesh'];mesh=source.objects[mesh['m_PathID']].read()
        ibms=np.array([[[getattr(m,f'e{r}{c}') for c in range(4)] for r in range(4)] for m in mesh.m_BindPose])
        return locals,palette,ibms
    return hierarchy(121260),hierarchy(121035)

def models(locals, root=None):
    out={}
    for path in sorted(locals,key=lambda p:p.count('/')):
        parent=path.rpartition('/')[0]
        out[path]=(out[parent] if parent else np.eye(4))@locals[path]
    if root is not None:
        correction=root@np.linalg.inv(out['Root_M'])
        out={path:correction@pose for path,pose in out.items()}
    return out

def adapt(old_rest_locals,modern_rest_locals,modern_root_animated,driver_locals,actual_shared_root):
    """Modern root pose is an explicit independent input, never inferred from actual shared root."""
    target_rest=models(old_rest_locals);source_rest=models(modern_rest_locals)
    source_snapshot=models(driver_locals,modern_root_animated)
    desired={target:source_snapshot[source]@np.linalg.inv(source_rest[source])@target_rest[target]
             for target,source in MAPPING.items()}
    desired[JAW]=desired['Root_M/joint1/neck']@old_rest_locals[JAW]
    output={}
    for path in sorted(desired,key=lambda p:p.count('/')):
        parent=path.rpartition('/')[0]
        parent_pose=actual_shared_root if parent=='Root_M' else desired[parent]
        output[path]=np.linalg.inv(parent_pose)@desired[path]
    return output,desired,source_snapshot

def trs_error(m):
    linear=m[:3,:3];scale=np.linalg.norm(linear,axis=0)
    if not np.isfinite(m).all() or np.min(scale)<1e-10 or np.linalg.det(linear)<=0:return 1e300
    axes=linear/scale
    return float(np.max(np.abs(axes.T@axes-np.eye(3))))

def audit(assets,samples=None):
    (old,palette,ibms),(modern,_,_)=extract(assets)
    rest=models(old);modern_rest=models(modern);input_hash=sha(ibms.tobytes());palette_before=list(palette)
    checks=[];cases=[]
    def check(name,actual,tolerance=1e-5):checks.append({'name':name,'error':float(actual),'tolerance':tolerance,'pass':bool(actual<=tolerance)})
    def exercise(name,root,drivers,actual_root):
        root_before=actual_root.copy();driver_before={p:m.copy() for p,m in drivers.items()}
        output,desired,snapshot=adapt(old,modern,root,drivers,actual_root)
        for p,m in output.items():check(name+':trs:'+p,trs_error(m))
        rebuilt=models(dict({'Root_M':actual_root},**output))
        check(name+':topdown_reconstruction',max(np.max(np.abs(rebuilt[p]-desired[p])) for p in desired))
        check(name+':shared_root_readonly',np.max(np.abs(actual_root-root_before)))
        check(name+':driver_snapshots_readonly',max(np.max(np.abs(drivers[p]-driver_before[p])) for p in drivers))
        check(name+':jaw_rest_local',np.max(np.abs(output[JAW]-old[JAW])))
        repeated=0.
        for _ in range(100):
            again,_,_=adapt(old,modern,root,drivers,actual_root)
            repeated=max(repeated,max(np.max(np.abs(again[p]-output[p])) for p in output))
        check(name+':100_frame_repeat_stability',repeated)
        cases.append({'name':name,'source':'synthetic_matrix_case','maxArticulatedModelDelta':float(max(np.max(np.abs(desired[p]-rest[p])) for p in desired)),
                      'targetModelMatrices':{p:m.tolist() for p,m in desired.items()}})
        return output,desired
    actual=rest['Root_M'].copy()
    _,neutral=exercise('neutral-modern-source-old-actual-root',modern_rest['Root_M'],modern,actual)
    check('neutral_exact_old_palette_rest',max(np.max(np.abs(neutral[p]-rest[p])) for p in palette))
    check('neutral_skin_matrices_unchanged',max(np.max(np.abs(neutral[p]@ibms[i]-rest[p]@ibms[i])) for i,p in enumerate(palette)))
    different=np.eye(4);different[:3,:3]=Rotation.from_euler('xyz',[20,-35,11],degrees=True).as_matrix();different[:3,3]=[3,-2,1]
    _,neutral_other=exercise('neutral-modern-source-arbitrary-actual-root',modern_rest['Root_M'],modern,different)
    check('actual_root_independence',max(np.max(np.abs(neutral_other[p]-neutral[p])) for p in palette))
    compound={p:m.copy() for p,m in modern.items()}
    for path,angles in [('Root_M/base',[8,0,12]),('Root_M/base/body',[0,-18,0]),('Root_M/base/body/neck',[13,0,-9]),('Root_M/base/body/neck/head',[0,22,16])]:
        rotation=np.eye(4);rotation[:3,:3]=Rotation.from_euler('xyz',angles,degrees=True).as_matrix();compound[path]=compound[path]@rotation
    animated_root=different@modern_rest['Root_M']
    _,motion=exercise('compound-root-base-body-neck-head',animated_root,compound,actual)
    check('compound_motion_nonconstant',0 if max(np.max(np.abs(motion[p]-rest[p])) for p in palette)>0.1 else 1,0)
    # Negative control: virtual-root local incorrectly applied beneath actual shared root.
    virtual_root=modern_rest['Root_M']@np.linalg.inv(modern_rest['Root_M'])@rest['Root_M']
    wrong=different@np.linalg.inv(virtual_root)@neutral['Root_M/joint1']
    check('negative_control_wrong_parent_detected',0 if np.max(np.abs(wrong-neutral['Root_M/joint1']))>0.1 else 1,0)
    native_status='not_available';native_hash=None;native_motion=[]
    if samples:
        data=json.loads(samples.read_text());native_hash=sha(samples.read_bytes())
        if data.get('appearanceSamplesOnly') is True:raise ValueError('Appearance-only samples require independent baseline/transition audit')
        if data.get('coordinateConvention')!='independent-modern-root-and-local-drivers':raise ValueError('Unverified sample coordinate convention')
        grouped={}
        for frame in data['frames']:
            if frame['clip'] not in ('krakenAttack','krakenIdle','krakenDamage','krakenDisappear'):
                raise ValueError('Only four main clips supported; appearance baseline is unresolved')
            if not set(frame['driverLocals']).issubset(modern):raise ValueError('Unknown sampled driver path')
            for pose in [frame['modernRootAnimated'],*frame['driverLocals'].values()]:
                pose=np.asarray(pose,float)
                if pose.shape!=(4,4) or not np.isfinite(pose).all():raise ValueError('Invalid sampled matrix')
            drivers={p:m.copy() for p,m in modern.items()};drivers.update({p:np.asarray(m,float) for p,m in frame['driverLocals'].items()})
            grouped.setdefault(frame['clip'],[]).append((float(frame['time']),np.asarray(frame['modernRootAnimated'],float),drivers))
            exercise('sample:'+frame['clip']+':'+str(frame['time']),np.asarray(frame['modernRootAnimated'],float),drivers,actual)
            cases[-1]['source']='supplied_clip_sample_unity_evaluation_not_reproduced'
        for clip,rows in grouped.items():
            root_delta=max((float(np.max(np.abs(row[1]-rows[0][1]))) for row in rows),default=0.)
            local_delta=max((float(np.max(np.abs(row[2][p]-rows[0][2][p]))) for row in rows for p in modern if p!='Root_M'),default=0.)
            enough=len({row[0] for row in rows})>1
            native_motion.append({'clip':clip,'frames':len(rows),'distinctSampleTimes':len({row[0] for row in rows}),
                'modernRootMaximumDelta':root_delta,'modernDriverLocalMaximumDelta':local_delta,
                'status':'insufficient_samples' if not enough else 'pose_variation_observed' if max(root_delta,local_delta)>1e-5 else 'constant_sampled_poses',
                'localArticulationObserved':enough and local_delta>1e-5})
        native_status='supplied_samples_audited_algebra_only'
        if native_motion and all(r['status']=='constant_sampled_poses' for r in native_motion):
            native_status='supplied_constant_samples_no_motion_evidence'
    check('palette_identity_order_unchanged',0 if palette==palette_before else 1,0)
    check('bind_matrix_bytes_unchanged',0 if sha(ibms.tobytes())==input_hash else 1,0)
    return {'status':'numerical_prototype_pass' if all(c['pass'] for c in checks) else 'numerical_prototype_fail',
        'sourceAssetSha256':sha(assets.read_bytes()),'rendererIds':{'old':121260,'modern':121035},'originalPalette':palette,
        'originalBindposesSha256':input_hash,'mapping':MAPPING,'checks':checks,'cases':cases,'nativeClipSamples':native_status,'nativeSampleSha256':native_hash,'nativeSampleMotionEvidence':native_motion,
        'coordinateConvention':'One prefab-root space. Independent modernRootAnimated input reconstructs source snapshots. Desired=Sanimated*inverse(Srest)*Trest. joint1 local uses inverse ACTUAL sharedRoot; descendants use inverse desired parent. SharedRoot never written.',
        'limitations':['No Unity adapter, native Animator evaluation, event or visual test.','Modern root extraction/unkeyed channel reset remains unresolved.','Appearance direct old-jaw animation and crossfade blend baseline remain unresolved; jaw rest-local only for main-state synthetic audit.','Serialized quaternions normalized through scipy Rotation; positive scales retained. TRS shear tolerance1e-5 checks existing scale imprecision; no runtime decomposition policy established.','Palette/IBM preservation is mathematical input immutability, not a live renderer claim.']}

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--assets',type=Path,required=True);parser.add_argument('--samples',type=Path);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    if 'scratch' not in args.output.resolve().parts:raise ValueError('Derived game transforms must stay in ignored scratch')
    result=audit(args.assets,args.samples);args.output.write_text(json.dumps(result,indent=2,allow_nan=False)+'\n');print(result['status'],len(result['checks']),'checks; native samples:',result['nativeClipSamples'])
    raise SystemExit(0 if result['status']=='numerical_prototype_pass' else 1)

if __name__=='__main__':main()
