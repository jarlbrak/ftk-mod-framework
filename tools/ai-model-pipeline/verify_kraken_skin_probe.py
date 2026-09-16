#!/usr/bin/env python3
"""Independently verify original-only skin probes; never native mesh or visual acceptance."""
import argparse
import hashlib
import json
import struct
from pathlib import Path
import numpy as np
from PIL import Image
from bridge_kraken_native_samples import read_pin
from verify_kraken_endpoint_fixture import run as verify_endpoints

MANIFEST_HASH='df046f883286b3cf1723ec7bd87454e99e131a1673221c09aced61fd7d833e17'
GLOAMFIN_HASH='8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e'
CAPTURE_PLAN_HASH='1229bb59766cc1b5e47bdcb21448d85adeb07290df0c8c60dd1b450b07dbe21b'
CAPTURE_PLAN_FILE='gloamfin-four-scenario-capture-plan-v1.json'
GLOAMFIN_FILE='gloamfin-kraken-blockout-v1.manifest.json'
IMAGE_STEPS=[0,16,28,40,41,80,104,105,112,119,120,240]
BAKE_STEPS=[0,28,80,112]
BONES=['Root_M/joint1','Root_M/joint1/neck','Root_M/joint1/neck/head','Root_M/joint1/neck/head/topHead','Root_M/joint1/neck/jaw']
TOL=1e-5


def require(ok,reason):
    if not ok: raise ValueError(reason)


def digest(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def array(value,shape):
    v=np.asarray(value,dtype=float)
    require(v.shape==shape and np.isfinite(v).all(),'Finite array with exact shape required')
    return v


def variant_contract(asset):
    variant=asset.get('variant')
    if variant is None:return None,MANIFEST_HASH,120,120
    require(variant=='gloamfin-v1','Unknown original skin variant')
    vertices=asset['vertexCount'];indices=asset['indexCount']
    require(type(vertices) is int and type(indices) is int and 1<=vertices<=8192 and 3<=indices<=49152 and indices%3==0 and asset['triangleCount']==indices//3,'Bounded organic counts')
    require(asset['nativeRendererId']==121260 and asset['boneNames']==[p.rsplit('/',1)[-1] for p in BONES],'Organic native binding identity')
    return variant,GLOAMFIN_HASH,vertices,indices


def validate_organic(pos,joints,weights,indices,ibms,asset):
    _,_,vertices,index_count=variant_contract(asset)
    require(pos.shape==(vertices,3) and joints.shape==weights.shape==(vertices,4) and len(indices)==index_count,'Organic geometry counts')
    require(np.all((indices>=0)&(indices<vertices)) and np.all((joints>=0)&(joints<5)),'Organic joint/index bounds')
    require(np.isfinite(weights).all() and np.all(weights>=0) and np.max(abs(weights.sum(1)-1))<1e-6,'Organic finite normalized weights')
    influences=np.sum(weights>0,axis=1);require(np.all((influences>=1)&(influences<=2)),'Organic one/two influences')
    for i in range(4):
        for j in range(i+1,4):require(not np.any((weights[:,i]>0)&(weights[:,j]>0)&(joints[:,i]==joints[:,j])),'Duplicate organic positive joints')
    proof=asset['weightProof'];hist={str(i):int(np.sum(influences==i)) for i in range(1,5)}
    positives=[int(np.sum(np.any((joints==i)&(weights>0),axis=1))) for i in range(5)]
    require(hist==proof['influenceCountHistogram'] and int(np.sum(influences>1))==proof['softVertexCount']>0 and positives==proof['positiveVertexCountByBone'] and all(positives),'Organic weight proof mismatch')
    require(np.max(abs(ibms-array(asset['bindposes'],(5,4,4))))<=TOL,'Organic pinned inverse binds')


def original_glb(path,asset=None):
    raw=Path(path).read_bytes()
    require(struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw)),'GLB header')
    count,kind=struct.unpack_from('<II',raw,12);require(kind==0x4e4f534a,'GLB JSON chunk')
    doc=json.loads(raw[20:20+count]);size,kind=struct.unpack_from('<II',raw,20+count)
    require(kind==0x004e4942 and 28+count+size==len(raw),'GLB binary chunk');data=raw[28+count:]
    def read(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        require('byteStride' not in v,'Strided original probe unsupported')
        width={'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16,'SCALAR':1}[a['type']]
        dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
        result=np.frombuffer(data,dtype=dtype,count=a['count']*width,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(a['count'],width)
        require(np.isfinite(result).all(),'Nonfinite GLB');return result.copy()
    organic=asset is not None and variant_contract(asset)[0] is not None
    if organic:
        require(len(doc['meshes'])==len(doc['skins'])==1 and len(doc.get('materials',[]))<=1 and len(doc['meshes'][0]['primitives'])==1,'Organic single mesh/skin/material/primitive')
        require(len(doc['buffers'])==1 and 'uri' not in doc['buffers'][0] and doc['buffers'][0]['byteLength']<=len(data),'Organic owned binary buffer')
        require(all('sparse' not in a and not a.get('normalized',False) for a in doc['accessors']),'Organic sparse/normalized accessor excluded')
        require(all(v.get('buffer',0)==0 and 'byteStride' not in v for v in doc['bufferViews']),'Organic contiguous buffer views')
    primitive=doc['meshes'][0]['primitives'][0];attrs=primitive['attributes'];skin=doc['skins'][0]
    if organic:
        require(primitive.get('mode',4)==4 and ('material' not in primitive if not doc.get('materials') else primitive.get('material')==0) and 'targets' not in primitive,'Organic triangle material contract')
        for key,type_,component in [('POSITION','VEC3',5126),('NORMAL','VEC3',5126),('TEXCOORD_0','VEC2',5126),('JOINTS_0','VEC4',5123),('WEIGHTS_0','VEC4',5126)]:
            a=doc['accessors'][attrs[key]];require(a['type']==type_ and a['componentType']==component,'Organic attribute storage contract')
        require(doc['accessors'][primitive['indices']]['componentType']==5123,'Organic uint16 index contract')
    pos=read(attrs['POSITION']);joints=read(attrs['JOINTS_0']);weights=read(attrs['WEIGHTS_0']);indices=read(primitive['indices']).ravel()
    ibms=read(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
    names=[doc['nodes'][i]['name'] for i in skin['joints']]
    require(names==[p.rsplit('/',1)[-1] for p in BONES],'Original palette mismatch')
    if organic:
        require(read(attrs['NORMAL']).shape==pos.shape and read(attrs['TEXCOORD_0']).shape==(len(pos),2),'Organic normal/UV counts')
        validate_organic(pos,joints,weights,indices,ibms,asset)
        return pos,joints,weights,ibms
    require(pos.shape==(120,3) and joints.shape==weights.shape==(120,4) and len(indices)==120 and ibms.shape==(5,4,4),'Original probe counts')
    require(np.all(weights>=0) and np.max(abs(weights.sum(axis=1)-1))<1e-6 and joints.max()<5,'Original weights')
    # Independently confirm the reviewed geometry is five rigid .30-radius octahedra.
    centers=np.linalg.inv(ibms)[:,:3,3]
    for bone in range(5):
        sl=slice(bone*24,(bone+1)*24)
        require(np.all(weights[sl,0]==1) and np.all(weights[sl,1:]==0) and np.all(joints[sl,0]==bone),'Rigid marker mapping')
        offsets=pos[sl]-centers[bone]
        require(np.max(abs(np.sort(abs(offsets),axis=1)-[0,0,.3]))<1e-5,'Original octahedron offsets')
        require(np.all((indices[sl]>=bone*24)&(indices[sl]<(bone+1)*24)),'Cross-marker triangles')
    return pos,joints,weights,ibms


def skin_vertices(pos,joints,weights,ibms,bone_world,renderer_inverse):
    require(len(bone_world)==len(ibms)==5,'Five skin matrices required')
    matrices=np.array([renderer_inverse@bone_world[i]@ibms[i] for i in range(5)])
    homogeneous=np.column_stack([pos,np.ones(len(pos))]);result=np.zeros((len(pos),3))
    for slot in range(4):
        result+=np.einsum('nij,nj->ni',matrices[joints[:,slot].astype(int)],homogeneous)[:,:3]*weights[:,slot,None]
    require(np.isfinite(result).all(),'Nonfinite skin result');return result


def capture_contract(asset,scenario,plan=None):
    if plan is None:
        require(scenario=='appear','Nonappearance requires pinned companion capture plan')
        return IMAGE_STEPS,BAKE_STEPS,asset['camera']
    require(asset.get('variant')=='gloamfin-v1' and scenario in ('damaged','damaged-heavy','death','death-light'),'Companion plan scenario/variant scope')
    require(plan['schema']=='ftkmf.gloamfin.capture-plan.v1' and plan['variant']=='gloamfin-v1' and plan['assetManifest']=={'file':GLOAMFIN_FILE,'sha256':GLOAMFIN_HASH},'Companion original manifest identity')
    require(plan['glb']==asset['glb'] and plan['png']==asset['png'] and plan['sourceResourcesSha256']==asset['sourceResourcesSha256'],'Companion asset/source identity')
    entries=plan['scenarios'];require(len(entries)==4 and {e['scenario'] for e in entries}=={'damaged','damaged-heavy','death','death-light'},'Exact four companion scenarios')
    item=next(e for e in entries if e['scenario']==scenario);images=item['imageSteps'];bakes=item['bakeSteps']
    require(len(images)==12 and len(bakes)==4 and all(type(i) is int and 0<=i<=240 for i in images+bakes) and images==sorted(set(images)) and bakes==sorted(set(bakes)) and set(bakes)<=set(images),'Fixed bounded companion schedule')
    return images,bakes,item['camera']


def inspect_capture(record,frame,geometry,bake_steps=BAKE_STEPS):
    require(record.get('captured') is True and all(record.get(k) is True for k in ('outputUnchanged','rendererDisabledBeforeYield','renderTextureActiveRestored')),'Incomplete original capture')
    require(record['step']==frame['step'] and record['unityFrame']==frame['unityFrame'],'Capture/graph clock mismatch')
    require(record['output']==frame['endpointPolicy']['output'],'Capture not synchronized with endpoint output')
    world=array(record['rendererLocalToWorld'],(4,4));inverse=array(record['rendererWorldToLocal'],(4,4))
    require(np.max(abs(world@inverse-np.eye(4)))<=TOL,'Renderer coordinate inverse mismatch')
    top=array(record['outputTopLocalToWorld'],(4,4));bone_models=[array(record['output']['prefabRootModels'][p],(4,4)) for p in BONES]
    predicted_world=np.array([top@m for m in bone_models]);pos,joints,weights,ibms=geometry
    predicted=skin_vertices(pos,joints,weights,ibms,predicted_world,inverse)
    bake_error=None
    if record['step'] in bake_steps:
        actual_world=array(record['boneWorldMatrices'],(5,4,4))
        require(np.max(abs(predicted_world-actual_world))<=TOL,'Bone world/model coordinate mismatch')
        # Use actual observed Unity world matrices, independently composed with authored weights/IBMs.
        expected=skin_vertices(pos,joints,weights,ibms,actual_world,inverse)
        baked=array(record['bakedVerticesRendererLocal'],(len(pos),3));bake_error=float(np.max(abs(expected-baked)))
        bounds=record['bakedBounds'];center=array(bounds['center'],(3,));size=array(bounds['size'],(3,))
        require(np.max(abs(center-(baked.max(0)+baked.min(0))/2))<=TOL and np.max(abs(size-(baked.max(0)-baked.min(0))))<=TOL,'Baked bounds inconsistent')
    else: require('bakedVerticesRendererLocal' not in record,'Unexpected adaptive bake step')
    view=array(record['cameraWorldToCamera'],(4,4));projection=array(record['cameraProjection'],(4,4))
    camera_points=(view@world@np.column_stack([predicted,np.ones(len(pos))]).T).T
    clip=(projection@camera_points.T).T;require(np.all(abs(clip[:,3])>1e-10),'Invalid clip W')
    ndc=clip[:,:3]/clip[:,3,None]
    require(np.isfinite(ndc).all(),'Nonfinite projected probe')
    return {'step':record['step'],'maximumBakeError':bake_error,'withinXYFrustum':bool(np.all(abs(ndc[:,:2])<=1)),
            'depthRange':[-float(camera_points[:,2].max()),-float(camera_points[:,2].min())],
            'maximumProjectedXY':float(abs(ndc[:,:2]).max())},predicted


def fixed_camera(top,framing):
    require(framing['orthographic'] is True and framing['width']==framing['height']==512,'Fixed orthographic resolution required')
    position=array(framing['position'],(3,));look=array(framing['lookAt'],(3,));up=array(framing['up'],(3,))
    require(np.max(abs(top[:3,:3].T@top[:3,:3]-np.eye(3)))<=TOL,'Rigid output-top camera frame required')
    forward=top[:3,:3]@(look-position);require(np.linalg.norm(forward)>1e-8,'Camera direction');forward/=np.linalg.norm(forward)
    right=np.cross(top[:3,:3]@up,forward);require(np.linalg.norm(right)>1e-8,'Camera up');right/=np.linalg.norm(right)
    actual_up=np.cross(forward,right);world=np.eye(4);world[:3,:3]=np.column_stack([right,actual_up,forward]);world[:3,3]=(top@np.r_[position,1])[:3]
    size=float(framing['orthographicSize']);near=float(framing['nearClipPlane']);far=float(framing['farClipPlane'])
    require(np.isfinite([size,near,far]).all() and size>0 and 0<near<far,'Fixed camera planes')
    projection=np.zeros((4,4));projection[0,0]=projection[1,1]=1/size;projection[2,2]=-2/(far-near);projection[2,3]=-(far+near)/(far-near);projection[3,3]=1
    return world,projection


def inspect_run(report,request,arm_request,arm_result,asset_manifest,geometry,image_pins,capture_plan=None):
    variant,manifest_hash,vertices,index_count=variant_contract(asset_manifest)
    image_steps,bake_steps,framing=capture_contract(asset_manifest,report['scenario'],capture_plan)
    skin=report['originalSkinProbe'];identity=skin['identity']
    require(report['ok'] is True and report['scenario']==request['scenario'],'Failed/wrong controller scenario')
    require(identity['armRequest']==arm_request,'Arm request provenance mismatch')
    require(set(arm_request)==({'id','session','op','scenario','manifestSha256'}|({'variant'} if variant else set())|({'capturePlanSha256'} if capture_plan else set())) and arm_request['op']=='kraken-skin-probe-arm','Exact arm schema')
    require(arm_request['session']==request['session']==report['session']==arm_result['session'],'Skin session mismatch')
    require(arm_request['id']==arm_result['id'] and arm_request['id']!=request['id'] and arm_request['scenario']==report['scenario']==arm_result['scenario'],'Arm identity mismatch')
    if capture_plan is not None:
        require(arm_request['capturePlanSha256']==arm_result.get('capturePlanSha256')==identity.get('capturePlanSha256')==CAPTURE_PLAN_HASH and identity.get('capturePlanFile')==CAPTURE_PLAN_FILE and identity.get('capturePlan')==capture_plan,'Companion plan provenance mismatch')
    else:require('capturePlanSha256' not in arm_result and 'capturePlanSha256' not in identity,'Unexpected companion provenance')
    require(arm_request['manifestSha256']==identity['manifestSha256']==manifest_hash,'Skin manifest mismatch')
    if variant:
        require(arm_request.get('variant')==arm_result.get('variant')==identity.get('variant')==variant and arm_result.get('manifestSha256')==manifest_hash and identity['manifestFile']==GLOAMFIN_FILE,'Organic variant provenance mismatch')
        require(identity['weightProof']==asset_manifest['weightProof'],'Organic runtime weight proof provenance')
        require(identity['shader']=='Unlit/Texture' and identity['meshName']=='ftkmf_glb_'+asset_manifest['glb']['file'],'Organic runtime mesh/material identity')
    else:require('variant' not in arm_result and 'variant' not in identity,'Unexpected original variant metadata')
    require(arm_result.get('ok') is True and arm_result.get('status')=='one-shot-original-skin-probe-armed','Arm was not accepted')
    require(identity['armReady']==arm_result['pinnedReady']==report['pinnedReady'],'Arm Ready scope mismatch')
    require(identity['manifest']==asset_manifest and identity['vertexCount']==vertices and identity['indexCount']==index_count,'Original asset report mismatch')
    require([x['path'] for x in identity['bones']]==BONES and [x['name'] for x in identity['bones']]==[p.rsplit('/',1)[-1] for p in BONES],'Runtime skin palette mismatch')
    require(np.max(abs(array(identity['bindposes'],(5,4,4))-geometry[3]))<=TOL,'Runtime IBM mismatch')
    require(identity['imageSteps']==image_steps and identity['bakeSteps']==bake_steps,'Fixed sample selection changed')
    require(skin['cleanup']['allOwnedUnityNull'] is True and skin['cleanup']['disposed'] is True and skin['cleanup']['errors']==[],'Skin cleanup incomplete')
    require(identity['framing']==framing and identity['width']==identity['height']==512,'Fixed manifest framing/dimensions mismatch')
    camera_world=array(identity['cameraWorld'],(4,4));camera_projection=array(identity['cameraProjection'],(4,4))
    # Unity camera space looks down -Z, unlike Transform forward +Z (official Camera.worldToCameraMatrix contract).
    camera_view=np.diag([1,1,-1,1])@np.linalg.inv(camera_world)
    captures=skin['captures'];require([r['step'] for r in captures]==image_steps and len(image_pins)==12,'Incomplete/nonfixed capture steps')
    results=[];movement=[];image_hashes=[]
    for record,pin in zip(captures,image_pins):
        frame=report['frames'][record['step']]
        require([x['instanceId'] for x in identity['bones']]==[frame['endpointPolicy']['output']['locals'][p]['instanceId'] for p in BONES],'Skin bones are not the actual output bones')
        expected_world,expected_projection=fixed_camera(array(record['outputTopLocalToWorld'],(4,4)),framing)
        require(np.max(abs(expected_world-camera_world))<=TOL and np.max(abs(expected_projection-camera_projection))<=TOL,'Camera identity does not implement fixed manifest framing')
        require(np.max(abs(array(record['cameraWorldToCamera'],(4,4))-camera_view))<=TOL and np.max(abs(array(record['cameraProjection'],(4,4))-camera_projection))<=TOL,'Per-frame camera moved or projection changed')
        require(record['image']['width']==record['image']['height']==512,'Declared image dimensions changed')
        image_path=read_pin(pin);require(digest(image_path)==record['image']['sha256'],'Image evidence mismatch')
        image=np.asarray(Image.open(image_path).convert('RGB'));require(image.shape==(512,512,3),'Image dimensions')
        require(np.max(np.ptp(image.reshape(-1,3).astype(int),axis=0))>10,'Blank/near-constant original probe image')
        result,vertices=inspect_capture(record,report['frames'][record['step']],geometry,bake_steps)
        results.append(result);movement.append(vertices);image_hashes.append(digest(image_path))
    movement=np.array(movement)
    if variant:
        _,joints,weights,_=geometry
        per_marker=[float(np.max(np.ptp(movement[:,np.any((joints==i)&(weights>0),axis=1)],axis=0))) for i in range(5)]
    else:per_marker=[float(np.max(np.ptp(movement[:,i*24:(i+1)*24],axis=0))) for i in range(5)]
    require(max(per_marker)>TOL,'No weighted marker movement')
    return {'captures':results,'maximumBakeError':max(r['maximumBakeError'] or 0 for r in results),'markerMovement':dict(zip([p.rsplit('/',1)[-1] for p in BONES],per_marker)),
            'movementScope':'Overlapping positive-weight vertex cohorts; displacement is not independent bone motion.' if variant else 'Disjoint rigid marker cohorts.',
            'imagesWithinFrustum':all(r['withinXYFrustum'] and r['depthRange'][0]>=framing['nearClipPlane'] and r['depthRange'][1]<=framing['farClipPlane'] for r in results),'imageHashes':image_hashes}


def run(path):
    manifest=json.loads(Path(path).read_text());require(set(manifest)=={'schema','evidence','images'} and manifest['schema']=='kraken-original-skin-v1','Exact skin evidence manifest')
    expected={'endpointManifest','assetManifest','glb','png','firstArmRequest','firstArmResult','repeatArmRequest','repeatArmResult'}
    if 'capturePlan' in manifest['evidence']:expected.add('capturePlan')
    require(set(manifest['evidence'])==expected and set(manifest['images'])=={'first','repeat'},'Exact skin evidence pins')
    paths={k:read_pin(v) for k,v in manifest['evidence'].items()}
    asset=json.loads(paths['assetManifest'].read_text());variant,manifest_hash,_,_=variant_contract(asset)
    require(digest(paths['assetManifest'])==manifest_hash,'Unreviewed original asset manifest')
    require(digest(paths['glb'])==asset['glb']['sha256'] and digest(paths['png'])==asset['png']['sha256'],'Original asset bytes mismatch')
    capture_plan=None
    if 'capturePlan' in paths:
        require(digest(paths['capturePlan'])==CAPTURE_PLAN_HASH,'Unreviewed companion capture plan');capture_plan=json.loads(paths['capturePlan'].read_text())
    geometry=original_glb(paths['glb'],asset);endpoint=verify_endpoints(paths['endpointManifest'])
    require(endpoint['status']=='endpoint_policy_mechanics_match','Unchanged endpoint numerical verifier failed')
    endpoint_manifest=json.loads(paths['endpointManifest'].read_text());inputs={k:read_pin(v) for k,v in endpoint_manifest['evidence'].items()}
    if variant:require(digest(inputs['assets'])==asset['sourceResourcesSha256'],'Organic source binding assets differ from endpoint evidence')
    results={}
    for role in ['first','repeat']:
        report=json.loads(inputs[role].read_text());request=json.loads(inputs[role+'Request'].read_text())
        results[role]=inspect_run(report,request,json.loads(paths[role+'ArmRequest'].read_text()),json.loads(paths[role+'ArmResult'].read_text()),asset,geometry,manifest['images'][role],capture_plan)
    maximum=max(r['maximumBakeError'] for r in results.values());framed=all(r['imagesWithinFrustum'] for r in results.values())
    return {'status':'original_skin_numerical_match_pending_visual_review' if maximum<=TOL and framed else 'original_skin_probe_mismatch',
            'tolerance':TOL,'maximumBakeError':maximum,'runs':results,'endpointMaximumError':endpoint['maximumError'],
            'repeatImageHashesEqual':results['first']['imageHashes']==results['repeat']['imageHashes'],'manifestSha256':digest(path),
            'variant':variant,
            'scope':'Original organic weighted blockout only; constructed endpoint fixture, not gameplay, native geometry, production rig support or final art acceptance.' if variant else 'Five original rigid markers only. Images still require visual inspection. No soft skin, native geometry, gameplay or production rig-support claim.'}


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',required=True,type=Path);p.add_argument('--output',required=True,type=Path);args=p.parse_args()
    require('scratch' in args.output.resolve().parts,'Evidence output must remain in scratch');result=run(args.manifest)
    with args.output.open('x') as f:json.dump(result,f,indent=2,allow_nan=False)
    print(result['status']);raise SystemExit(0 if result['status']=='original_skin_numerical_match_pending_visual_review' else 1)

if __name__=='__main__':main()
