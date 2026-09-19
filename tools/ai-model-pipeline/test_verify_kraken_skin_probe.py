"""Original-only skin algebra/negative tests; not Unity rendering acceptance."""
from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy", "PIL")
import copy
import unittest
import json
import tempfile
from PIL import Image
from pathlib import Path
import numpy as np
from verify_kraken_skin_probe import original_glb,skin_vertices,inspect_capture,inspect_run,BONES,TOL,IMAGE_STEPS,BAKE_STEPS,MANIFEST_HASH,digest,fixed_camera
from local_inputs import skip_without_local_inputs


@skip_without_local_inputs(
    'scratch/kraken-owned-skin-probe-v1/kraken-owned-skin-probe-v1.glb',
    'scratch/kraken-owned-skin-probe-v1/manifest.json',
)
class OriginalSkinTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.geometry=original_glb(Path(__file__).resolve().parents[2]/'scratch/kraken-owned-skin-probe-v1/kraken-owned-skin-probe-v1.glb')

    def fixture(self,step=28):
        pos,joints,weights,ibms=self.geometry;top=np.eye(4);top[:3,3]=[100.8,0,132.82]
        models=np.linalg.inv(ibms)
        for i,m in enumerate(models):m[1,3]+=.001*step*i
        world=np.array([top@m for m in models]);inverse=np.linalg.inv(top)
        expected=skin_vertices(pos,joints,weights,ibms,world,inverse)
        output={'prefabRootModels':{p:m.tolist() for p,m in zip(BONES,models)},'locals':{p:{'instanceId':100+i} for i,p in enumerate(BONES)}}
        record=dict(step=step,unityFrame=100+step,captured=True,outputUnchanged=True,rendererDisabledBeforeYield=True,renderTextureActiveRestored=True,
                    output=output,outputTopLocalToWorld=top.tolist(),rendererLocalToWorld=top.tolist(),rendererWorldToLocal=inverse.tolist(),
                    boneWorldMatrices=world.tolist(),bakedVerticesRendererLocal=expected.tolist(),
                    bakedBounds={'center':((expected.min(0)+expected.max(0))/2).tolist(),'size':(expected.max(0)-expected.min(0)).tolist()},
                    cameraWorldToCamera=inverse.tolist(),cameraProjection=np.eye(4).tolist())
        return record,dict(step=step,unityFrame=100+step,endpointPolicy={'output':copy.deepcopy(output)})

    def run_fixture(self,directory):
        asset=getattr(self,'assetOverride',None) or json.loads((Path(__file__).resolve().parents[2]/'scratch/kraken-owned-skin-probe-v1/manifest.json').read_text())
        arm=dict(id='a'*32,session='s',op='kraken-skin-probe-arm',scenario='appear',manifestSha256=MANIFEST_HASH)
        request=dict(id='b'*32,session='s',scenario=getattr(self,'scenarioOverride','appear'));ready=dict(session='s',dungeonInstanceId=3,level=0,room=1)
        accepted=dict(scenario=getattr(self,'scenarioOverride','appear'),ok=True,status='one-shot-original-skin-probe-armed',id=arm['id'],session='s',pinnedReady=ready)
        image_steps=getattr(self,'imageStepsOverride',IMAGE_STEPS);bake_steps=getattr(self,'bakeStepsOverride',BAKE_STEPS);framing=getattr(self,'cameraOverride',asset['camera'])
        arm['scenario']=request['scenario']
        captures=[];frames=[None]*241;pins=[]
        for step in image_steps:
            record,frame=self.fixture(step)
            if step not in bake_steps:
                for key in ('bakedVerticesRendererLocal','bakedBounds','boneWorldMatrices'):record.pop(key)
            image=Path(directory)/f'{step}.png';pixels=np.zeros((512,512,3),dtype=np.uint8);pixels[10:20,10:20]=[255,30,10];Image.fromarray(pixels).save(image)
            record['image']={'sha256':digest(image),'width':512,'height':512};
            camera,projection=fixed_camera(np.array(record['outputTopLocalToWorld']),framing);record['cameraProjection']=projection.tolist();record['cameraWorldToCamera']=(np.diag([1,1,-1,1])@np.linalg.inv(camera)).tolist();pins.append({'path':str(image),'sha256':digest(image)});captures.append(record);frames[step]=frame
        identity=dict(armRequest=arm,manifestSha256=MANIFEST_HASH,armReady=ready,manifest=asset,vertexCount=120,indexCount=120,
                      bones=[{'path':p,'name':p.rsplit('/',1)[-1],'instanceId':100+i} for i,p in enumerate(BONES)],framing=framing,width=512,height=512,cameraWorld=camera.tolist(),cameraProjection=projection.tolist(),bindposes=self.geometry[3].tolist(),imageSteps=image_steps,bakeSteps=bake_steps)
        report=dict(ok=True,scenario=request['scenario'],session='s',pinnedReady=ready,frames=frames,originalSkinProbe=dict(identity=identity,captures=captures,
                    cleanup=dict(allOwnedUnityNull=True,disposed=True,errors=[])))
        return report,request,arm,accepted,asset,self.geometry,pins

    def test_full_skin_provenance_and_fixed_samples(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);result=inspect_run(*args)
            self.assertLess(result['maximumBakeError'],1e-12)
            self.assertGreater(result['markerMovement']['jaw'],.1)

    def test_arm_session_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[3]['session']='other'
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_original_manifest_identity_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['identity']['manifestSha256']='0'*64
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_missing_skin_cleanup_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['cleanup']['allOwnedUnityNull']=False
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_capture_reordering_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['captures'].reverse()
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_per_frame_camera_movement_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['captures'][1]['cameraWorldToCamera'][0][3]+=.01
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_changed_projection_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['identity']['cameraProjection'][0][0]*=1.2
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_bone_instance_identity_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['identity']['bones'][4]['instanceId']+=1
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_wrong_camera_dimension_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.run_fixture(d);args[0]['originalSkinProbe']['identity']['width']=256
            with self.assertRaises(ValueError):inspect_run(*args)

    def test_original_geometry_contract(self):
        self.assertEqual(self.geometry[0].shape,(120,3))
        self.assertEqual(self.geometry[3].shape,(5,4,4))

    def test_original_skin_formula_under_large_common_placement(self):
        record,frame=self.fixture();result,_=inspect_capture(record,frame,self.geometry)
        self.assertLess(result['maximumBakeError'],1e-12)

    def test_rigid_bone_movement_is_not_erased(self):
        pos,j,w,b=self.geometry;rest=np.linalg.inv(b);moved=rest.copy();moved[4,2,3]+=.4
        before=skin_vertices(pos,j,w,b,rest,np.eye(4));after=skin_vertices(pos,j,w,b,moved,np.eye(4))
        np.testing.assert_allclose(before[:96],after[:96],atol=1e-12)
        np.testing.assert_allclose(after[96:,2]-before[96:,2],.4,atol=1e-12)

    def test_wrong_baked_vertex_exceeds_unchanged_tolerance(self):
        record,frame=self.fixture();record['bakedVerticesRendererLocal'][10][0]+=.002
        baked=np.array(record['bakedVerticesRendererLocal']);record['bakedBounds']={'center':((baked.min(0)+baked.max(0))/2).tolist(),'size':np.ptp(baked,axis=0).tolist()}
        result,_=inspect_capture(record,frame,self.geometry);self.assertGreater(result['maximumBakeError'],TOL)

    def test_bone_world_model_mismatch_rejected(self):
        record,frame=self.fixture();record['boneWorldMatrices'][4][0][3]+=.01
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_renderer_coordinate_mismatch_rejected(self):
        record,frame=self.fixture();record['rendererWorldToLocal'][0][3]+=.1
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_nonfinite_bake_rejected(self):
        record,frame=self.fixture();record['bakedVerticesRendererLocal'][0][1]=float('nan')
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_wrong_frame_rejected(self):
        record,frame=self.fixture();record['unityFrame']+=1
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_unsynchronized_output_rejected(self):
        record,frame=self.fixture();record['output']['prefabRootModels'][BONES[0]][0][3]+=.1
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_unexpected_bake_step_rejected(self):
        record,frame=self.fixture(16)
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)

    def test_renderer_left_enabled_rejected(self):
        record,frame=self.fixture();record['rendererDisabledBeforeYield']=False
        with self.assertRaises(ValueError):inspect_capture(record,frame,self.geometry)


if __name__=='__main__':unittest.main()
