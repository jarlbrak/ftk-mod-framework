#!/usr/bin/env python3
"""Stage additive411 over frozen410; offline only and refuses existing output."""
import argparse,copy,hashlib,json,shutil
from pathlib import Path
import jsonschema
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--repo',type=Path);p.add_argument('--source-stage',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 R=a.repo.resolve() if a.repo else next(x for x in Path(__file__).resolve().parents if (x/'FTKModFramework').is_dir() and (x/'tools').is_dir());O=R/'art-experiments/bronzehollow-sentinel';D=a.output.resolve();S=a.source_stage.resolve()
 if D.exists():p.error('Output exists; choose a fresh output to preserve frozen stage.')
 manifest=json.loads((O/'manifest.json').read_text())
 for n,h in manifest['files'].items():assert sha(O/n)==h,n
 for n,h in manifest['sourcePins'].items():assert sha(R/n)==h,n
 src=S/'model-test-profiles.json';assert sha(src)=='50ca3e8f41cbd90f40c5f226e60d78c00d555eb066d8078723bb3ee390976a3b'
 old=json.loads(src.read_text());assert len(old['profiles'])==410;new=copy.deepcopy(old)
 row=copy.deepcopy(next(x for x in old['profiles'] if x['key']=='ftkmf_modeltest_probe_deathknighta'));assert row['baseEnemy']=='deathknightA' and row['combatProfile']=='a3b78641519f6b987f05e408f4ac0b40f41d1d23588ee0b8701029fc36e9fb8c'
 assert row['renderers']==[{'rendererPath':'deathKnight','glbFile':'probe_121217.glb','textureFile':'probe_palette.png'}]
 row.update(key='ftkmf_modeltest_bronzehollow',displayName='Bronzehollow Sentinel',visualScale=1.0)
 row['renderers']=[{'rendererPath':'deathKnight','glbFile':'bronzehollow.glb','textureFile':'bronzehollow_basecolor.png','disableNativeEmission':True}]
 assert not any(x['key']==row['key'] for x in old['profiles']);new['profiles'].append(row)
 jsonschema.validate(new,json.loads((R/'tools/ai-model-pipeline/runtime-test-content/profiles.schema.json').read_text()))
 assets={str(x.relative_to(S/'models')):sha(x) for x in sorted((S/'models').rglob('*')) if x.is_file()};assert len(assets)==321
 for n in ['bronzehollow.glb','bronzehollow_basecolor.png']:assert n not in assets
 D.mkdir(parents=True);shutil.copytree(S/'models',D/'models')
 for n in ['bronzehollow.glb','bronzehollow_basecolor.png']:shutil.copy2(O/n,D/'models'/n)
 (D/'model-test-profiles.json').write_text(json.dumps(new,indent=2)+'\n');shutil.copy2(__file__,D/'stage-script.py');shutil.copy2(O/'manifest.json',D/'authoring-manifest.json')
 assert new['profiles'][:-1]==old['profiles'];assert {k:v for k,v in new.items() if k!='profiles'}=={k:v for k,v in old.items() if k!='profiles'}
 assert all(sha(D/'models'/n)==h for n,h in assets.items())
 # This stage carries no replacement DLL; generic approved loader handles the appended plural row.
 receipt={'status':'STAGED_NOT_DEPLOYED_LIVE_ORIGINAL_PENDING','sourceStage':str(S),'sourceCatalogSha256':sha(src),'catalogSha256':sha(D/'model-test-profiles.json'),'sourceReceiptSha256':sha(S/'receipt.json'),'authoringManifestSha256':sha(O/'manifest.json'),'stageScriptSha256':sha(D/'stage-script.py'),'priorRows':410,'candidateRows':411,'priorRowsAndTopMetadataDeepEqual':True,'priorAssetCount':321,'assetCount':323,'allPriorAssetsUnchanged':True,'assetSha256':{str(x.relative_to(D/'models')):sha(x) for x in sorted((D/'models').rglob('*')) if x.is_file()},'addedProfile':row,'dllChanges':False,'limitations':['Body-only original; native helmet/shield/weapon retained','No live original acceptance','Exact A only, no sibling acceptance','Emission off on body only, palette pending live review']}
 (D/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(str(D/'receipt.json'),sha(D/'receipt.json'))
if __name__=='__main__':main()
