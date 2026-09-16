#!/usr/bin/env python3
"""Original Gloamfin skin checks composed with the explicit fixed-input output policy."""
import argparse,copy,json
from pathlib import Path
from bridge_kraken_native_samples import read_pin
from verify_kraken_composed_fixture import COMPOSED,run as verify_composition
from verify_kraken_skin_probe import GLOAMFIN_HASH,CAPTURE_PLAN_HASH,variant_contract,original_glb,inspect_run,digest,require,TOL

def inspect_composed(report,request,arm_request,arm_result,asset,geometry,images,capture_plan):
 require(request.get('endpointPolicy')==report.get('endpointPolicy')==COMPOSED,'Actual composed request/policy required')
 require(arm_request.get('endpointPolicy')==arm_result.get('endpointPolicy')==COMPOSED,'Exact composed skin arm pin required')
 require(report['originalSkinProbe']['identity']['armRequest']==arm_request,'Original unmodified arm identity mismatch')
 require(asset.get('variant')=='gloamfin-v1','Only original Gloamfin composed skin authorized')
 # Geometry inspector gets an explicitly derived arm-only view; all original arm fields were joined above.
 derived=copy.deepcopy(report);ar=dict(arm_request);rr=dict(arm_result)
 ar.pop('endpointPolicy');rr.pop('endpointPolicy');derived['originalSkinProbe']['identity']['armRequest']=ar
 return inspect_run(derived,request,ar,rr,asset,geometry,images,capture_plan)

def run(path):
 m=json.loads(Path(path).read_text());require(set(m)=={'schema','evidence','images'} and m['schema']=='kraken-composed-gloamfin-v1','Exact composed skin manifest')
 expected={'composedManifest','assetManifest','glb','png','firstArmRequest','firstArmResult','repeatArmRequest','repeatArmResult'}
 if 'capturePlan' in m['evidence']:expected.add('capturePlan')
 require(set(m['evidence'])==expected and set(m['images'])=={'first','repeat'},'Exact composed skin evidence pins')
 paths={k:read_pin(v) for k,v in m['evidence'].items()};asset=json.loads(paths['assetManifest'].read_text());variant,manifest_hash,_,_=variant_contract(asset)
 require(variant=='gloamfin-v1' and manifest_hash==digest(paths['assetManifest'])==GLOAMFIN_HASH,'Original pinned Gloamfin required')
 for kind in ('glb','png'):require(digest(paths[kind])==asset[kind]['sha256'],'Original asset changed')
 plan=None
 if 'capturePlan' in paths:require(digest(paths['capturePlan'])==CAPTURE_PLAN_HASH,'Pinned camera/schedule plan required');plan=json.loads(paths['capturePlan'].read_text())
 endpoint=verify_composition(paths['composedManifest']);require(endpoint['status']=='composed_endpoint_match','Composed numerical input/output gate failed')
 cm=json.loads(paths['composedManifest'].read_text());inputs={k:read_pin(v) for k,v in cm['evidence'].items()};require(digest(inputs['assets'])==asset['sourceResourcesSha256'],'Native source binding mismatch')
 geometry=original_glb(paths['glb'],asset);results={}
 for role in ('first','repeat'):
  load=lambda p:json.loads(p.read_text())
  results[role]=inspect_composed(load(inputs[role]),load(inputs[role+'Request']),load(paths[role+'ArmRequest']),load(paths[role+'ArmResult']),asset,geometry,m['images'][role],plan)
 maximum=max(r['maximumBakeError'] for r in results.values());framed=all(r['imagesWithinFrustum'] for r in results.values())
 return {'status':'composed_gloamfin_numerical_match_pending_visual_review' if maximum<=TOL and framed else 'composed_gloamfin_mismatch','tolerance':TOL,'maximumBakeError':maximum,'runs':results,'endpointMaximumError':endpoint['maximumError'],'repeatImageHashesEqual':results['first']['imageHashes']==results['repeat']['imageHashes'],'manifestSha256':digest(path),'evidence':m['evidence'],'variant':variant,'scope':'Original weighted Gloamfin on composed owned output only. All original arm/policy fields pinned before derived geometry validation view. No native avatar, attack, gameplay, production adapter or final art claim.'}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();require('scratch' in a.output.resolve().parts,'Scratch output required');r=run(a.manifest)
 with a.output.open('x') as f:json.dump(r,f,indent=2,allow_nan=False)
 print(r['status']);raise SystemExit(r['status']!='composed_gloamfin_numerical_match_pending_visual_review')
