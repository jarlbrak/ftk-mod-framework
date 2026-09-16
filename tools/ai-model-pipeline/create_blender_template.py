#!/usr/bin/env python3
"""Run inside Blender: create a local-only bind scene for original FTK mesh authoring."""
import argparse
import hashlib
import json
import math
import subprocess
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

UNITY_TO_BLENDER=np.array([[1,0,0,0],[0,0,-1,0],[0,1,0,0],[0,0,0,1]],dtype=float)


def create(reference_path,skeleton_path,output,python_executable,with_reference,radius_scale=1.0,joint_scope="all",connections="hierarchy",marker_radius=None):
    if marker_radius is not None and (not math.isfinite(marker_radius) or marker_radius <= 0): raise ValueError('marker-radius must be finite and positive')
    if connections not in ('hierarchy','none'): raise ValueError('connections must be hierarchy or none')
    if joint_scope not in ('all','weighted'): raise ValueError('joint-scope must be all or weighted')
    if not math.isfinite(radius_scale) or not .25 <= radius_scale <= 16:
        raise ValueError('radius-scale must be finite and between 0.25 and 16')
    output=output.resolve();output.parent.mkdir(parents=True,exist_ok=True)
    repo=Path(__file__).resolve().parents[2]
    if subprocess.run(['git','check-ignore','--quiet',str(output)],cwd=repo).returncode:
        raise ValueError('Bind scenes contain native skeleton metadata and optional reference geometry; save under ignored scratch/')
    reference_path=reference_path.resolve();skeleton_path=skeleton_path.resolve()
    ref=np.load(reference_path,allow_pickle=False);skeleton=json.loads(skeleton_path.read_text())
    names=ref['bone_names'].tolist()
    if names!=skeleton['bone_names']: raise ValueError('Skeleton/reference bone order mismatch')
    matrices=np.linalg.inv(ref['bindposes']); rotations=matrices[:,:3,:3]
    # Edit bones represent orientation, not an absolute uniform bind-space scale. The local hairTop
    # profile uses one common 1.0483983 factor. Normalize only display orientations; retain native
    # mesh-local joint centers and exact inverse binds for export.
    singular=np.linalg.svd(rotations,compute_uv=False)
    uniform_scale=float(np.median(singular))
    if uniform_scale<=0 or not np.allclose(singular,uniform_scale,atol=2e-5):
        raise ValueError('Rig has nonuniform scale/shear; scaffold supports rigid or common uniform-scale rest frames')
    orientation=rotations/uniform_scale
    if not np.allclose(orientation.transpose(0,2,1)@orientation,np.eye(3),atol=2e-5) or not np.allclose(np.linalg.det(orientation),1,atol=2e-5):
        raise ValueError('Rig has sheared/reflected rest frames')
    display_matrices=matrices.copy();display_matrices[:,:3,:3]=orientation
    probe_dir=output.parent/(output.stem+'-probe')
    subprocess.run([str(python_executable.resolve()),str(Path(__file__).with_name('make_rig_probe.py')),
                    '--reference',str(reference_path),'--skeleton',str(skeleton_path),'--output-dir',str(probe_dir),
                    '--radius-scale',str(radius_scale),'--joint-scope',joint_scope,'--connections',connections]+(['--marker-radius',str(marker_radius)] if marker_radius is not None else []),check=True,stdout=subprocess.PIPE,text=True)
    probe=json.loads((probe_dir/'probe-source.json').read_text())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    original=bpy.data.collections.new('FTK Original Geometry');bpy.context.scene.collection.children.link(original)
    arm_data=bpy.data.armatures.new('FTK Bind Skeleton');arm=bpy.data.objects.new('FTK_Rig',arm_data)
    original.objects.link(arm);arm['ftk_armature']=True;arm.show_in_front=True
    bpy.context.view_layer.objects.active=arm;arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    scale=max(float(np.linalg.norm(np.ptp(matrices[:,:3,3],axis=0))),1e-3)
    for i,name in enumerate(names):
        bone=arm_data.edit_bones.new(name)
        rest=UNITY_TO_BLENDER@display_matrices[i]
        bone.head=Vector(rest[:3,3]);bone.tail=bone.head+Vector(rest[:3,1])*(scale*.025)
        bone.align_roll(Vector(rest[:3,2]))
    for i,parent in enumerate(skeleton['bone_parents']):
        if parent>=0: arm_data.edit_bones[names[i]].parent=arm_data.edit_bones[names[parent]]
    bpy.ops.object.mode_set(mode='OBJECT');arm_data.pose_position='REST'
    orientation_error=max(float(np.max(np.abs(np.asarray(arm.data.bones[name].matrix_local)-UNITY_TO_BLENDER@display_matrices[i]))) for i,name in enumerate(names))
    for i,name in enumerate(names):
        if not np.allclose(arm.data.bones[name].matrix_local,UNITY_TO_BLENDER@display_matrices[i],atol=1e-4):
            raise ValueError('Blender could not represent native rest orientation: '+name+' max error '+str(np.max(np.abs(np.asarray(arm.data.bones[name].matrix_local)-UNITY_TO_BLENDER@display_matrices[i]))))
    def mesh_object(name,positions,faces,collection):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata((np.asarray(positions)@UNITY_TO_BLENDER[:3,:3].T).tolist(),[],np.asarray(faces).tolist());mesh.update()
        obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);return obj
    obj=mesh_object('Original Rig Calibration Probe',probe['positions'],probe['triangles'],original)
    obj['ftk_export']=True;obj['ftk_art_status']='CALIBRATION ONLY, replace with original creature art'
    for i,name in enumerate(names):obj.vertex_groups.new(name=name)
    for vi,(joints,weights) in enumerate(zip(probe['joints'],probe['weights'])):
        for joint,weight in zip(joints,weights):
            if weight>0:obj.vertex_groups[joint].add([vi],weight,'REPLACE')
    mod=obj.modifiers.new('FTK Skeleton','ARMATURE');mod.object=arm
    uv=obj.data.uv_layers.new(name='UVMap')
    for loop in obj.data.loops:
        u,v=probe['uvs'][loop.vertex_index];uv.data[loop.index].uv=(u,1-v)
    image=bpy.data.images.load(str(probe_dir/'probe-palette.png'));image.pack()
    material=bpy.data.materials.new('FTK Original Probe Palette');material.use_nodes=True
    shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.85
    texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image
    material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color']);obj.data.materials.append(material)
    if with_reference:
        guides=bpy.data.collections.new('FTK LOCAL REFERENCE - NEVER EXPORT');bpy.context.scene.collection.children.link(guides)
        guide=mesh_object('LOCAL COPYRIGHTED REFERENCE - DO NOT EXPORT',ref['positions'],ref['triangles'],guides)
        guide['ftk_reference_only']=True;guide.hide_render=True;guide.display_type='WIRE';guide.hide_set(True)
    metadata=dict(version=1,calibration_radius_scale=radius_scale,calibration_joint_scope=joint_scope,calibration_connections=connections,calibration_marker_radius_override=marker_radius,native_uniform_bind_scale=uniform_scale,blender_rest_max_error=orientation_error,reference_path=str(reference_path),reference_sha256=hashlib.sha256(reference_path.read_bytes()).hexdigest(),
        bone_names=names,bone_parents=skeleton['bone_parents'],inverse_bind_matrices=ref['bindposes'].tolist(),
        blender_rest_matrices={name:[list(row) for row in arm.data.bones[name].matrix_local] for name in names},
        coordinate_conversion='Blender to Unity (x,z,-y), preserve triangle winding',
        source_restriction='Reference guide never exports; only original tagged/selected meshes may export')
    text=bpy.data.texts.new('FTK_BIND_METADATA.json');text.write(json.dumps(metadata,indent=2))
    bpy.context.scene['ftk_bind_metadata']='FTK_BIND_METADATA.json'
    bpy.context.scene['ftk_art_status']='CALIBRATION ONLY'
    arm.select_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print(f'Created {output}: {len(names)} bones, original probe and '+('local guide' if with_reference else 'no native surface guide'))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference',type=Path,required=True);p.add_argument('--skeleton',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--python-executable',type=Path,required=True)
    p.add_argument('--with-reference',action='store_true')
    p.add_argument('--radius-scale',type=float,default=1.0)
    p.add_argument('--joint-scope',choices=['all','weighted'],default='all')
    p.add_argument('--connections',choices=['hierarchy','none'],default='hierarchy')
    p.add_argument('--marker-radius',type=float)
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    create(a.reference,a.skeleton,a.output,a.python_executable,a.with_reference,a.radius_scale,a.joint_scope,a.connections,a.marker_radius)
