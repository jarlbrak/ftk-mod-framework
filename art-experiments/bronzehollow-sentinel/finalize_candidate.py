#!/usr/bin/env python3
"""Freeze original authoring pins after root and architect trial reviews; refuses refreeze."""
from pathlib import Path
import json,hashlib
O=Path(__file__).resolve().parent;R=O.parent.parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not (O/'manifest.json').exists(),'Existing manifest must be preserved; freeze a new version instead'
assert (O/'root-offline-review.json').exists() and (O/'architect-trial-review.json').exists()
review=json.loads((O/'root-offline-review.json').read_text())
for n,h in review['pins'].items():assert sha(R/n)==h,n
assert sha(O/'bronzehollow.glb')=='8d13200e7dee5ab2e50096fb9da90c9f27ffed012ff8fac2cfc5522ff169132c'
assert sha(O/'bronzehollow_basecolor.png')=='d729f43c3eea712a0741e0a6a3f970a6059b9d4d2102133cc6b093b75817dfda'
sourcepaths=['scratch/skeleton-audit/121217/reference.npz','scratch/skeleton-audit/121217/skeleton.json','scratch/deathknight-native-topology-analysis/findings.json','docs/evidence/deathknight-native-source-v1/archive-validation.json','tools/ai-model-pipeline/export_ftk_glb.py','tools/ai-model-pipeline/export_blender_model.py','tools/ai-model-pipeline/create_blender_template.py','tools/ai-model-pipeline/validate_glb.py']
sources={n:sha(R/n) for n in sourcepaths}
m={'status':'FROZEN_OFFLINE_CANDIDATE_LIVE_TRIAL_PENDING','name':'Bronzehollow Sentinel','baseEnemy':'deathknightA','rendererPathId':121217,'rendererPath':'deathKnight','boneCount':36,'controllerPathId':5983,'nativeScale':1.2,'visualScale':1.0,'disableNativeEmission':True,'bodyOnlyRetainedNativeEquipment':['helmet','shield','weapon'],'primaryGlb':'bronzehollow.glb','texture':'bronzehollow_basecolor.png','sourcePins':sources,'files':{str(p.relative_to(O)):sha(p) for p in sorted(O.rglob('*')) if p.is_file()},'limits':['Offline original only; no live acceptance','Native equipment is not original artwork and not packaged','Recorded pass/block/explicit-death audit, not damaging hit or ordinary lethal','No sibling Deathknight acceptance']}
(O/'manifest.json').write_text(json.dumps(m,indent=2)+'\n');print(sha(O/'manifest.json'))
