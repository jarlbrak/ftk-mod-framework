# Mournglass Wraith

Original bind candidate for exact chaosBeast renderer121008/enChaosBeast, CEL135754 and actual weapon controller5960 ghostController. A narrow ivory funerary mask sits within a faceted blue-green hood; long tapered sleeves end in ivory grasping fingers, while two separate hanging mantle lobes follow the native hip/knee chains. This is newly authored geometry, not a recolor or distributed native surface.

Full31 palette retained, including3 native-unused entries. The original generator reads only bone names and bind matrices; independent regeneration reproduces original geometry/palette exactly. Direct/reopened GLB checks, normalized weights, native bind bounds and outward closed-piece volume are required. Separate closed pieces overlap; no watertight union or collision-fit claim.

Source scale, materials/emission, portrait markers, attachments and death behavior await the architect's review after the Core rollback priority. No presumed hovering, fade, physics or sibling-controller behavior follows from the ghostController name. No runtime profile, frozen appearance or live acceptance yet. Native motion must establish shoulder, mask/cowl and split-mantle attachment before finalization.

Reproduce with scratch/model-venv/bin/python on build_geometry.py, audit_surfaces.py and verify_original_geometry.py; Blender --background --python build_blender.py creates the editable armature, saved/reopened export and studio views. All source/native references stay in ignored scratch; deliverable meshes are original only.

## Newly reported material constraint

Architect's initial exact-source inspection reports native scale1.5, two materials71 matChaosBeast and70 matCHaosBeastFace, plus body ScrollingUVs135814. Therefore the current one-material export is only an original bind/studio candidate and must not be deployed as faithful native support. Exact two-slot mapping, scroller property/rate/phase and per-slot texture treatment require the reviewed multi-material API after source details are pinned. Preserve this first generation before revising the export. Native8 particle systems and FlickerLight/Light remain separate effects. Source has no rigidbodies/joints; DeathFade event around.776761s is conditional, not proof of actual runtime hide.
