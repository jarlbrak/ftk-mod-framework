#!/usr/bin/env python3
"""Freeze this reviewed authoring generation without changing geometry or textures."""
import hashlib,json
from pathlib import Path
A=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert not (A/'manifest.json').exists(), 'Preserve existing frozen manifest.'
review=json.loads((A/'root-offline-draft-review.json').read_text())
for n,h in review['files'].items():assert sha(A/n)==h,n
assert sha(A/'amberwake.glb')=='b03bc6738f652447ba9579cd1d3cdfcdc655eee20d5fd61c7f43461dbaeb0d36'
files={str(p.relative_to(A)):sha(p) for p in sorted(A.rglob('*')) if p.is_file() and '__pycache__' not in p.parts and p != A/'manifest.json'}
assert not any(Path(n).suffix.lower() in ('.dll','.assets','.npz') for n in files)
r={'status':'FROZEN_ORIGINAL_REDUCED_SCALE_TRIAL_CANDIDATE_NOT_LIVE_ACCEPTED','nativeEnemy':'dragonFrost','sourceRendererId':121561,'sharedRigRepresentativeRendererId':121525,'rendererPath':'enDragon','nativeScale':1,'publicVisualScaleFactor':0.25,'disableNativeEmission':True,'vertices':9084,'triangles':3028,'boneCount':70,'originalAssets':{n:files[n] for n in ['amberwake.glb','amberwake_basecolor.png']},'files':files,'limits':['Scale0.25 is a camera-fit hypothesis only, not accepted framing.','Selected offline studio/pass views and331pose calculations do not prove all views, wing culling, tail, flat lower jaw or live anatomy.','Native stats/controller/frost weapon effects preserved; dungeon fixture differs from native natural arena.','Both portraits, ordinary hit/lethal and final resource disposal remain unverified.']}
(A/'manifest.json').write_text(json.dumps(r,indent=2)+'\n');print(sha(A/'manifest.json'))
