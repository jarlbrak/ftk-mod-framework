#!/usr/bin/env python3
"""Generate original calibration geometry from bind-joint positions, never native surfaces."""
import argparse
import contextlib
import hashlib
import io
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image
from export_ftk_glb import write_glb
from validate_glb import validate

PALETTE = [(239,173,69), (57,163,165), (117,197,210), (218,96,76)]


def checked_radius_scale(value):
    factor=float(value)
    if not math.isfinite(factor) or not .25 <= factor <= 16:
        raise ValueError('radius-scale must be finite and between 0.25 and 16')
    return factor


def checked_marker_radius(value):
    radius=float(value)
    if not math.isfinite(radius) or radius <= 0:
        raise ValueError('marker-radius must be finite and positive')
    return radius


def selected_joint_indices(reference, joint_count, joint_scope):
    if joint_scope not in ('all', 'weighted'):
        raise ValueError('joint-scope must be all or weighted')
    if joint_scope == 'all':
        return set(range(joint_count))
    weights=np.asarray(reference['weights'])
    joints=np.asarray(reference['joints'])
    if weights.ndim != 2 or weights.shape[1] != 4 or joints.shape != weights.shape:
        raise ValueError('Native joint usage requires matching Nx4 weights and joints')
    if not np.isfinite(weights).all() or (weights < 0).any():
        raise ValueError('Native joint usage weights must be finite and nonnegative')
    active=joints[weights > 0]
    if not len(active):
        raise ValueError('Native weighted joint selection is empty')
    if not np.isfinite(active).all() or (active != np.floor(active)).any() or (active < 0).any() or (active >= joint_count).any():
        raise ValueError('Invalid weighted native joint usage index')
    return set(active.astype(int).tolist())


def make_probe(reference_path, skeleton_path, output, radius_scale=1.0, joint_scope="all",connections="hierarchy",marker_radius=None):
    radius_scale=checked_radius_scale(radius_scale)
    if connections not in ('hierarchy','none'): raise ValueError('connections must be hierarchy or none')
    reference=np.load(reference_path,allow_pickle=False)
    skeleton=json.loads(skeleton_path.read_text())
    names=reference['bone_names'].tolist()
    if names!=skeleton['bone_names']: raise ValueError('Skeleton and reference bone orders differ')
    selected=selected_joint_indices(reference,len(names),joint_scope)
    parents=skeleton['bone_parents']
    if len(parents)!=len(names): raise ValueError('Parent count differs from bone count')
    for i,p in enumerate(parents):
        if p < -1 or p >= len(names) or p==i: raise ValueError('Invalid bone parent index')
    # Only inverse bind matrices and hierarchy determine generated positions and dimensions.
    # The native positions/triangles are read solely by the independent contract validator below.
    centers=np.linalg.inv(reference['bindposes'])[:,:3,3]
    if not np.isfinite(centers).all(): raise ValueError('Nonfinite bind-joint position')
    extent=float(np.linalg.norm(np.ptp(centers,axis=0)))
    scale=extent if extent>1e-6 else 1.0
    lengths=[float(np.linalg.norm(centers[i]-centers[p])) for i,p in enumerate(parents) if p>=0]
    nonzero=[v for v in lengths if v>scale*1e-7]
    radius=max(scale*.0025,min(scale*.012,float(np.median(nonzero))*.10 if nonzero else scale*.01))
    radius*=radius_scale
    if marker_radius is not None: radius=checked_marker_radius(marker_radius)
    positions=[];normals=[];uvs=[];triangles=[];joints=[];weights=[]
    def triangle(points,bones,color):
        face=np.asarray(points,dtype=float)
        cross=np.cross(face[1]-face[0],face[2]-face[0])
        area=float(np.linalg.norm(cross))
        if area<=radius*radius*1e-10: raise ValueError('Probe generated a degenerate triangle')
        normal=cross/area
        first=len(positions)
        for point,bone in zip(face,bones):
            positions.append(point.tolist());normals.append(normal.tolist())
            uvs.append([(color+.5)/len(PALETTE),.5])
            joints.append([int(bone),0,0,0]);weights.append([1.,0.,0.,0.])
        triangles.append([first,first+1,first+2])
    # Each selected joint has a rigid octahedron, including isolated/coincident joints.
    axes=np.eye(3)*radius
    oct_faces=[(0,2,4),(2,1,4),(1,3,4),(3,0,4),(2,0,5),(1,2,5),(3,1,5),(0,3,5)]
    for bone,center in enumerate(centers):
        if bone not in selected: continue
        vertices=[center+axes[0],center-axes[0],center+axes[1],center-axes[1],center+axes[2],center-axes[2]]
        for face in oct_faces: triangle([vertices[i] for i in face],[bone]*3,0 if parents[bone]>=0 else 3)
    segment_count=0
    for child,parent in enumerate(parents):
        if connections == 'none' or parent<0 or parent not in selected or child not in selected: continue
        start,end=centers[parent],centers[child]
        delta=end-start;length=float(np.linalg.norm(delta))
        if length<=scale*1e-7: continue
        axis=delta/length
        helper=np.eye(3)[int(np.argmin(np.abs(axis)))]
        u=np.cross(axis,helper);u/=np.linalg.norm(u)
        v=np.cross(axis,u)
        rings=[]
        for center,r in [(start,radius*.62),(end,radius*.40)]:
            rings.append([center+r*(np.cos(k*np.pi/3)*u+np.sin(k*np.pi/3)*v) for k in range(6)])
        for k in range(6):
            nxt=(k+1)%6
            triangle([rings[0][k],rings[0][nxt],rings[1][nxt]],[parent,parent,child],1)
            triangle([rings[0][k],rings[1][nxt],rings[1][k]],[parent,child,child],1)
            triangle([start,rings[0][nxt],rings[0][k]],[parent]*3,2)
            triangle([end,rings[1][k],rings[1][nxt]],[child]*3,2)
        segment_count+=1
    data=dict(positions=positions,normals=normals,uvs=uvs,triangles=triangles,joints=joints,weights=weights,bone_names=names)
    used=set(np.asarray(joints)[:,0].tolist())
    if used!=selected: raise ValueError('Probe does not weight exactly the selected joints')
    output.mkdir(parents=True,exist_ok=True)
    source=output/'probe-source.json';source.write_text(json.dumps(data,separators=(',',':'))+'\n')
    texture=Image.new('RGB',(256,64))
    for i,color in enumerate(PALETTE): texture.paste(color,(i*64,0,(i+1)*64,64))
    texture.save(output/'probe-palette.png')
    glb=output/'rig-probe.glb'
    write_glb(glb,data,reference)
    validation=validate(glb,reference)
    result=dict(label='CALIBRATION ONLY: original diagnostic geometry, not a finished creature or art acceptance',
        geometry_source='Octahedra and tapered hexagonal segments derived only from inverse bind joint centers and parent indices',
        native_surface_copied=False,joint_count=len(names),weighted_joint_count=len(used),
        joint_scope=joint_scope,connections=connections,selected_joint_count=len(selected),omitted_joint_count=len(names)-len(selected),
        selected_joint_indices=sorted(selected),
        joint_marker_count=len(selected),segment_count=segment_count,marker_radius=radius,radius_scale=radius_scale,
        marker_radius_override=marker_radius,radius_source='explicit mesh units (radius-scale ignored)' if marker_radius is not None else 'automatic joint extent times radius-scale',
        coincident_or_zero_length_parent_edges=sum(v<=scale*1e-7 for v in lengths),
        validation=validation,glb_sha256=hashlib.sha256(glb.read_bytes()).hexdigest(),
        limitations=['Coincident joint markers overlap; each still has explicit weighted geometry.',
                     'A passed export does not demonstrate runtime renderer selection or animation correctness.',
                     'Hard-surface calibration geometry is intentionally unsuitable as finished enemy art.'])
    (output/'probe.json').write_text(json.dumps(result,indent=2)+'\n')
    reference.close()
    return result


def batch(audit_dir,output,radius_scale=1.0,joint_scope="all",connections="hierarchy",marker_radius=None):
    radius_scale=checked_radius_scale(radius_scale)
    audit=json.loads((audit_dir/'audit.json').read_text());results=[]
    for item in audit['results']:
        renderer=item['renderer_path_id'];reference=audit_dir/str(renderer)
        record=dict(renderer_path_id=renderer,rig_profile_fingerprint=item['rig_profile_fingerprint'],radius_scale=radius_scale)
        try:
            with contextlib.redirect_stdout(io.StringIO()):
                result=make_probe(reference/'reference.npz',reference/'skeleton.json',output/str(renderer),radius_scale,joint_scope,connections,marker_radius)
            record.update(status='calibration_export_pass',joint_count=result['joint_count'],
                          joint_scope=joint_scope,connections=connections,selected_joint_count=result['selected_joint_count'],omitted_joint_count=result['omitted_joint_count'],
                          weighted_joint_count=result['weighted_joint_count'],glb_sha256=result['glb_sha256'])
        except Exception as exc:
            record.update(status='failed',error_type=type(exc).__name__,error=str(exc))
        results.append(record)
    report=dict(label='Offline original calibration exports only; no runtime or art acceptance',
                source_sha256=audit['source_sha256'],radius_scale=radius_scale,joint_scope=joint_scope,connections=connections,marker_radius_override=marker_radius,results=results,
                summary=dict(attempted=len(results),passed=sum(r['status']=='calibration_export_pass' for r in results),
                             failed=sum(r['status']=='failed' for r in results)))
    output.mkdir(parents=True,exist_ok=True);(output/'probes.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report['summary'],indent=2));return report

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference',type=Path);p.add_argument('--skeleton',type=Path)
    p.add_argument('--audit-dir',type=Path,help='Generate every representative from an audit_skeletons output instead')
    p.add_argument('--output-dir',type=Path,required=True)
    p.add_argument('--radius-scale',type=checked_radius_scale,default=1.0,help='Original marker/segment thickness multiplier, finite 0.25..16 (default 1)')
    p.add_argument('--joint-scope',choices=['all','weighted'],default='all',help='Use all palette joints or only joints with positive native vertex weights')
    p.add_argument('--connections',choices=['hierarchy','none'],default='hierarchy',help='Draw parent-child segments or joint markers only')
    p.add_argument('--marker-radius',type=checked_marker_radius,help='Explicit positive mesh-unit radius; overrides automatic radius and radius-scale')
    a=p.parse_args()
    if a.audit_dir:
        if a.reference or a.skeleton: p.error('--audit-dir cannot be combined with --reference/--skeleton')
        batch(a.audit_dir,a.output_dir,a.radius_scale,a.joint_scope,a.connections,a.marker_radius)
    else:
        if not a.reference or not a.skeleton: p.error('--reference and --skeleton are required')
        make_probe(a.reference,a.skeleton,a.output_dir,a.radius_scale,a.joint_scope,a.connections,a.marker_radius)
