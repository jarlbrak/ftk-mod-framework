"""Exact native binding setup only; no native surface and no finished creature mesh."""
import bpy,sys
from pathlib import Path
O=Path(__file__).resolve().parent;R=O.parent.parent;sys.path.insert(0,str(R/'tools/ai-model-pipeline'))
from create_blender_template import create
ref=R/'scratch/scourge-leprechaun-topology-analysis/reference-121222';temp=R/'scratch/thistlewick-bind-setup';temp.mkdir(exist_ok=True)
create(ref/'reference.npz',ref/'skeleton.json',temp/'original-probe-template.blend',R/'scratch/model-venv/bin/python',False,1,'weighted','none')
for o in list(bpy.data.objects):
 if o.type=='MESH':bpy.data.objects.remove(o,do_unlink=True)
bpy.context.scene['ftk_art_status']='EMPTY ORIGINAL AUTHORING SETUP; no creature mesh yet'
bpy.context.scene['ftk_native_accessory_boundary']='Native enLuckysHat rigid mesh remains in game only; not copied here'
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(O/'thistlewick-bind-setup.blend'))
