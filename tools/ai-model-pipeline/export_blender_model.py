#!/usr/bin/env python3
"""Run inside Blender: export tagged original bind-pose geometry through FTK's strict bridge."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix
sys.path.insert(0,str(Path(__file__).resolve().parent))
from export_ftk_glb import write_glb
from validate_glb import validate


def export(output,selected=False,native_material_slots=None):
    if native_material_slots is not None and native_material_slots not in (2,3,4):
        raise ValueError("Native material slot mode requires2..4 slots")
    metadata_name=bpy.context.scene.get('ftk_bind_metadata')
    if not metadata_name or metadata_name not in bpy.data.texts: raise ValueError('Missing FTK scaffold binding metadata')
    metadata=json.loads(bpy.data.texts[metadata_name].as_string())
    reference_path=Path(metadata['reference_path'])
    if hashlib.sha256(reference_path.read_bytes()).hexdigest()!=metadata['reference_sha256']:
        raise ValueError('Local reference changed since this scene was scaffolded')
    ref=np.load(reference_path,allow_pickle=False);names=ref['bone_names'].tolist()
    if names!=metadata['bone_names'] or not np.array_equal(ref['bindposes'],metadata['inverse_bind_matrices']):
        raise ValueError('Native binding metadata mismatch')
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE' and o.get('ftk_armature')]
    if len(rigs)!=1: raise ValueError('Scene requires exactly one tagged FTK armature')
    rig=rigs[0]
    if len(rig.constraints): raise ValueError('Remove rig object constraints before bind export')
    if not np.allclose(rig.matrix_world, np.eye(4),atol=1e-6): raise ValueError('Apply/reset the rig object transform')
    if set(rig.data.bones.keys())!=set(names): raise ValueError('Rig bone names were changed')
    if rig.animation_data and (rig.animation_data.action or len(rig.animation_data.nla_tracks)):
        raise ValueError('Remove active armature animation/NLA before bind export')
    for i,name in enumerate(names):
        bone=rig.data.bones[name]
        parent=names[metadata['bone_parents'][i]] if metadata['bone_parents'][i]>=0 else None
        if (bone.parent.name if bone.parent else None)!=parent: raise ValueError('Rig hierarchy changed: '+name)
        if not np.allclose(bone.matrix_local,metadata['blender_rest_matrices'][name],atol=1e-6):
            raise ValueError('Rig rest transform changed: '+name)
        pose=rig.pose.bones[name]
        if len(pose.constraints) or not np.allclose(pose.matrix_basis,np.eye(4),atol=1e-6):
            raise ValueError('Armature must be unposed and unconstrained: '+name)
    objects=[o for o in (bpy.context.selected_objects if selected else bpy.context.scene.objects)
             if o.type=='MESH' and (selected or o.get('ftk_export'))]
    if not objects: raise ValueError('No original meshes selected/tagged ftk_export')
    positions=[];normals=[];uvs=[];triangles=[];joints=[];weights=[]
    image=None;images={};primitive_faces=[[] for _ in range(native_material_slots or 1)]
    for obj in sorted(objects,key=lambda o:o.name):
        if obj.get('ftk_reference_only'): raise ValueError('Native reference guide cannot be exported: '+obj.name)
        if obj.animation_data and (obj.animation_data.action or len(obj.animation_data.nla_tracks)):
            raise ValueError('Animated source object must be baked to a deliberate bind mesh: '+obj.name)
        if len(obj.constraints): raise ValueError('Bake/remove source object constraints: '+obj.name)
        if obj.matrix_world.to_3x3().determinant()<=0: raise ValueError('Source has singular/negative transform: '+obj.name)
        arm_mods=[]
        for mod in obj.modifiers:
            if not mod.show_viewport and not mod.show_render: continue
            if mod.type!='ARMATURE' or mod.object!=rig:
                raise ValueError('Apply destructive modifiers before export: '+obj.name+'/'+mod.name)
            if not mod.use_vertex_groups or mod.use_bone_envelopes or mod.use_deform_preserve_volume:
                raise ValueError('Use ordinary vertex-group linear skinning on FTK_Rig: '+obj.name)
            arm_mods.append(mod)
        if len(arm_mods)!=1: raise ValueError('Each source needs exactly one active modifier for FTK_Rig: '+obj.name)
        mesh=obj.data
        if mesh.shape_keys and any(abs(key.value)>1e-6 for key in list(mesh.shape_keys.key_blocks)[1:]):
            raise ValueError('Nonzero shape keys must be baked to a deliberate bind mesh: '+obj.name)
        if not mesh.uv_layers.active: raise ValueError('Source has no active UV layer: '+obj.name)
        used={p.material_index for p in mesh.polygons}
        if native_material_slots is None and len(used)!=1: raise ValueError('Source must use one material: '+obj.name)
        if native_material_slots and any(i<0 or i>=native_material_slots for i in used): raise ValueError('Source material index outside explicit slot count')
        for material_index in sorted(used):
            material=obj.material_slots[material_index].material
            if material is None or not material.use_nodes: raise ValueError('Source requires a texture material: '+obj.name)
            shaders=[n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED']
            if len(shaders)!=1: raise ValueError('Material needs one Principled BSDF')
            links=list(shaders[0].inputs['Base Color'].links)
            if len(links)!=1 or links[0].from_node.type!='TEX_IMAGE' or links[0].from_socket.name!='Color':
                raise ValueError('Base Color must link directly from one image texture; bake procedural materials first')
            outputs=[n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output]
            if len(outputs)!=1 or len(outputs[0].inputs['Surface'].links)!=1 or outputs[0].inputs['Surface'].links[0].from_node!=shaders[0]:
                raise ValueError('Material output must connect directly to the Principled BSDF')
            node=links[0].from_node
            if node.inputs['Vector'].is_linked or node.projection!='FLAT':
                raise ValueError('Use the active mesh UV map directly, without mapping/projection nodes')
            if node.image is None or node.image.is_dirty: raise ValueError('Save the original PNG image before export')
            slot=material_index if native_material_slots else 0
            if slot in images and node.image!=images[slot]: raise ValueError('Each primitive slot must share one PNG across all objects')
            images[slot]=node.image;image=node.image
        vertex_weights=[]
        for vertex in mesh.vertices:
            influences=[]
            for group in vertex.groups:
                if group.weight<=0: continue
                name=obj.vertex_groups[group.group].name
                if name not in names: raise ValueError('Unknown weighted bone '+name+' on '+obj.name)
                influences.append((names.index(name),float(group.weight)))
            if not 1<=len(influences)<=4: raise ValueError('Each vertex must have 1..4 positive bone influences: '+obj.name)
            if abs(sum(w for _,w in influences)-1)>1e-4: raise ValueError('Normalize source weights before export: '+obj.name)
            influences.sort(key=lambda x:(-x[1],x[0]));influences+=[(0,0.)]*(4-len(influences))
            vertex_weights.append(influences)
        mesh.calc_loop_triangles()
        matrix=obj.matrix_world;normal_matrix=matrix.to_3x3().inverted().transposed()
        for triangle in mesh.loop_triangles:
            first=len(positions)
            for loop_index in triangle.loops:
                loop=mesh.loops[loop_index];point=matrix@mesh.vertices[loop.vertex_index].co
                normal=(normal_matrix@mesh.corner_normals[loop_index].vector).normalized()
                if normal.length<.99: raise ValueError('Degenerate source normal: '+obj.name)
                uv=mesh.uv_layers.active.data[loop_index].uv
                positions.append([point.x,point.z,-point.y]);normals.append([normal.x,normal.z,-normal.y]);uvs.append([uv.x,1-uv.y])
                influence=vertex_weights[loop.vertex_index]
                joints.append([j for j,w in influence]);weights.append([w for j,w in influence])
            face=np.asarray(positions[first:first+3])
            cross=np.cross(face[1]-face[0],face[2]-face[0])
            if not np.isfinite(cross).all() or np.linalg.norm(cross)==0:
                raise ValueError('Degenerate source triangle: '+obj.name)
            triangles.append([first,first+1,first+2])
            primitive_faces[triangle.material_index if native_material_slots else 0].append(triangles[-1])
    if image is None: raise ValueError('No texture selected')
    encoded_images={}
    for slot,image in images.items():
        texture_bytes=bytes(image.packed_file.data) if image.packed_file else Path(bpy.path.abspath(image.filepath)).read_bytes()
        if not texture_bytes.startswith(b'\x89PNG\r\n\x1a\n'): raise ValueError('Source image must be an original PNG; save/pack as PNG first')
        encoded_images[slot]=texture_bytes
    if any(not faces for faces in primitive_faces): raise ValueError('Every requested primitive must have original triangles')
    data=dict(positions=positions,normals=normals,uvs=uvs,triangles=triangles,joints=joints,weights=weights,bone_names=names)
    if native_material_slots: data["primitives"]=[dict(triangles=faces) for faces in primitive_faces]
    output=output.resolve();output.parent.mkdir(parents=True,exist_ok=True)
    write_glb(output,data,ref);report=validate(output,ref)
    texture_files=[]
    for slot,texture_bytes in sorted(encoded_images.items()):
        texture_path=output.with_suffix(f'.slot{slot}.png' if native_material_slots else '.png');texture_path.write_bytes(texture_bytes);texture_files.append(texture_path.name)
    output.with_suffix('.source.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
    report.update(blender_scene=bpy.data.filepath,objects=[o.name for o in objects],texture=texture_files[0] if not native_material_slots else None,textures=texture_files,
                  art_status=bpy.context.scene.get('ftk_art_status','Original model; artistic acceptance not established'))
    output.with_suffix('.validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('FTK Blender bridge export PASS: '+str(output))
    ref.close()

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--selected',action='store_true')
    p.add_argument('--native-material-slots',type=int,choices=[2,3,4],help='Opt in: Blender material index is primitive index; emits one PNG per slot')
    a=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    export(a.output,a.selected,a.native_material_slots)
