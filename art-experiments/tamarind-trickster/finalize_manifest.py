"""Freeze explicit original authoring files; live evidence is added separately."""
import json,hashlib
from pathlib import Path
O=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not (O/'manifest.json').exists(), 'Do not overwrite a frozen manifest; preserve/version the candidate first.'
files=sorted(p for p in O.rglob('*') if p.is_file() and '__pycache__' not in p.parts)
d=json.loads((O/'tamarind.source.json').read_text());pose=json.loads((O/'native-arrival-pose-audit.json').read_text())
m=dict(status='FROZEN_ORIGINAL_OFFLINE_NATIVE_POSE_CANDIDATE_LIVE_PENDING',nativeEnemy='monkeyC',sourceRendererId=121301,celId=138586,controllerId=5979,rendererPath='enMonkeyBasey',nativeScale=1,publicVisualScaleFactor=1,boneCount=len(d['bone_names']),vertices=len(d['positions']),triangles=len(d['triangles']),weightedBones=35,nativeUnusedRetained=7,originalAssets={n:sha(O/n) for n in ['tamarind.glb','tamarind_basecolor.png']},nativePoseEvidence=pose,files={str(p.relative_to(O)):sha(p) for p in files},limits=['Exact monkeyC only; no sibling inheritance.','Offline selected appearance and pose review; no original live acceptance.','Native self-removal hides body108–119; hidden death poses analytical only.','Native first-action diagnostic has no ordinary hero hit.','Keep native weapon, FX, AI, ragdoll, colliders and controller.','Disable native emission explicitly for original palette and verify live.','No final native resource disposal proof.'])
(O/'manifest.json').write_text(json.dumps(m,indent=2)+'\n');print(json.dumps({'manifestSha256':sha(O/'manifest.json'),'pinnedFiles':len(files)}))
