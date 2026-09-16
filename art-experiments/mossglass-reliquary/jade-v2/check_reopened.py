"""Reopen texture-only scene and export without changing the deployed original GLB."""
import bpy,sys
from pathlib import Path
O=Path(__file__).resolve().parent;R=O.parents[2];sys.path.insert(0,str(R/'tools/ai-model-pipeline'))
from export_blender_model import export
bpy.ops.wm.open_mainfile(filepath=str(O/'mossglass-jade-v2.blend'));export(O/'mossglass-jade-v2-reopened.glb',native_material_slots=2)
