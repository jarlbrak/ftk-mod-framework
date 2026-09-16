#!/usr/bin/env python3
"""Append Amberwake trial409 to pinned408, copying assets only into a fresh stage."""
import argparse,copy,hashlib,json,shutil
from pathlib import Path
import jsonschema
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--repo',type=Path);parser.add_argument('--game-root',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
R=args.repo.resolve() if args.repo else next((p for p in Path(__file__).resolve().parents if (p/'FTKModFramework').is_dir() and (p/'tools').is_dir()),None)
assert R and (R/'FTKModFramework').is_dir() and (R/'tools').is_dir(), 'Use --repo to identify repository.'
A=R/'art-experiments/amberwake-dragon';G=args.game_root.resolve();O=args.output.resolve();assert not O.exists(), 'Existing output refused; choose a fresh --output.'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();read=lambda p:json.loads(p.read_text())
m=read(A/'manifest.json')
for n,h in m['files'].items():assert sha(A/n)==h,n
catalog=G/'model-test-profiles.json';priorhash='97f568867d98676814309102a5fde7313b4d19748fa374a212aec1df3d462a37';assert sha(catalog)==priorhash;old=read(catalog);assert len(old['profiles'])==408
profile={'key':'ftkmf_modeltest_amberwake','baseEnemy':'dragonFrost','displayName':'Amberwake Dragon','combatProfile':'48b22cd9296f3f95ff8c032ebf9427d7081a6986f8437f5d005f5d9cc837dd7f','renderers':[{'rendererPath':'enDragon','glbFile':'amberwake.glb','textureFile':'amberwake_basecolor.png','disableNativeEmission':True}],'visualScale':0.25}
assert next(p for p in old['profiles'] if p['key']=='ftkmf_modeltest_probe_dragonfrost')['combatProfile']==profile['combatProfile']
# No health override: preserve the cloned native boss stats exactly.
assert not any(p['key']==profile['key'] for p in old['profiles']);new=copy.deepcopy(old);new['profiles'].append(profile)
jsonschema.validate(new,read(R/'tools/ai-model-pipeline/runtime-test-content/profiles.schema.json'))
models=G/'BepInEx/plugins/FTKModFramework_content/models';prior={p.name:sha(p) for p in models.iterdir() if p.is_file()};assert len(prior)==319
assets=m['originalAssets'];assert all(n not in prior for n in assets)
binaries={'FTKModFramework.dll':'c7684e91aab3902297698dc753b43818d972e59285d0e091280e353dc6fa8095','FtkRuntimeModelTestContent.dll':'5d08d2db552543b9287ed16194a09e74a77b522e977c485a8990531d4fb38fb5','FtkRuntimeModelTest.dll':'6ad7d5064f5a9a1971d39df4c85ae58cdaac2a0d9252afda3515dd10a473bad0'}
for n,h in binaries.items():assert sha(G/'BepInEx/plugins'/n)==h,n
O.mkdir(parents=True);shutil.copytree(models,O/'models')
for n,h in assets.items():assert sha(A/n)==h;shutil.copy2(A/n,O/'models'/n)
for n,h in prior.items():assert sha(O/'models'/n)==h
(O/'model-test-profiles.json').write_text(json.dumps(new,indent=2)+'\n');shutil.copy2(__file__,O/'stage-script.py');shutil.copy2(A/'manifest.json',O/'authoring-manifest.json')
r={'status':'OFFLINE_STAGED_CAMERA_FIT_HYPOTHESIS_NOT_DEPLOYED','profileCount':409,'priorCatalogSha256':priorhash,'catalogSha256':sha(O/'model-test-profiles.json'),'prior408RowsDeepEqual':new['profiles'][:-1]==old['profiles'],'topLevelMetadataUnchanged':{k:v for k,v in new.items() if k!='profiles'}=={k:v for k,v in old.items() if k!='profiles'},'priorAssetHashes':prior,'newAssetHashes':assets,'allPriorAssetsByteEqual':True,'deployedBinarySha256Unchanged':binaries,'manifestSha256':sha(A/'manifest.json'),'stageScriptSha256':sha(O/'stage-script.py'),'newProfile':profile,'registrationRoute':'existing explicit plural API; no bindingKind, no code build','limits':m['limits']}
(O/'receipt.json').write_text(json.dumps(r,indent=2)+'\n')
assert sha(catalog)==priorhash
for n,h in prior.items():assert sha(models/n)==h
for n,h in binaries.items():assert sha(G/'BepInEx/plugins'/n)==h
print(json.dumps({'receiptSha256':sha(O/'receipt.json'),'catalogSha256':r['catalogSha256'],'manifestSha256':r['manifestSha256']}))
