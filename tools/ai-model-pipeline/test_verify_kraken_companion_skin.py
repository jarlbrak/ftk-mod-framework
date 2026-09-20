from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy", "PIL")
import copy,json,tempfile,unittest
from pathlib import Path
import numpy as np
from verify_kraken_skin_probe import *
import test_verify_gloamfin_skin as organic_tests
from local_inputs import skip_without_local_inputs
@skip_without_local_inputs('scratch/gloamfin-four-scenario-plan-v1/'+CAPTURE_PLAN_FILE)
class CompanionTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  organic_tests.GloamfinTests.setUpClass();cls.path=Path(__file__).resolve().parents[2]/'scratch/gloamfin-four-scenario-plan-v1'/CAPTURE_PLAN_FILE;cls.plan=json.loads(cls.path.read_text())
 def args(self,d,scenario):
  g=organic_tests.GloamfinTests();b=g.builder();images,bakes,camera=capture_contract(g.asset,scenario,self.plan)
  b.imageStepsOverride=images;b.bakeStepsOverride=bakes;b.cameraOverride=camera;b.scenarioOverride=scenario
  args=b.run_fixture(d);r,request,arm,result,asset,geometry,pins=args
  arm.update(variant='gloamfin-v1',manifestSha256=GLOAMFIN_HASH,capturePlanSha256=CAPTURE_PLAN_HASH)
  result.update(variant='gloamfin-v1',manifestSha256=GLOAMFIN_HASH,capturePlanSha256=CAPTURE_PLAN_HASH)
  r['originalSkinProbe']['identity'].update(variant='gloamfin-v1',manifestFile=GLOAMFIN_FILE,manifestSha256=GLOAMFIN_HASH,vertexCount=5640,indexCount=5640,weightProof=copy.deepcopy(asset['weightProof']),shader='Unlit/Texture',meshName='ftkmf_glb_gloamfin.glb',capturePlanSha256=CAPTURE_PLAN_HASH,capturePlanFile=CAPTURE_PLAN_FILE,capturePlan=copy.deepcopy(self.plan))
  return (*args,self.plan)
 def test_exact_pin(self):self.assertEqual(digest(self.path),CAPTURE_PLAN_HASH)
 def test_four_scenarios_full_weighted_bakes(self):
  for scenario in ('damaged','damaged-heavy','death','death-light'):
   with self.subTest(scenario=scenario),tempfile.TemporaryDirectory() as d:
    r=inspect_run(*self.args(d,scenario));self.assertLess(r['maximumBakeError'],1e-12);self.assertEqual(sum(c['maximumBakeError'] is not None for c in r['captures']),4)
 def test_heavy_pure_and_late_death_landmarks(self):
  self.assertTrue(any(95<=s<=110 for s in capture_contract(organic_tests.GloamfinTests.asset,'damaged-heavy',self.plan)[1]))
  self.assertIn(191,capture_contract(organic_tests.GloamfinTests.asset,'death',self.plan)[1])
 def test_no_nonappearance_without_plan(self):
  with self.assertRaises(ValueError):capture_contract(organic_tests.GloamfinTests.asset,'death')
 def test_no_appearance_plan_or_marker_expansion(self):
  for asset,scenario in [(organic_tests.GloamfinTests.asset,'appear'),({},'damaged')]:
   with self.assertRaises(ValueError):capture_contract(asset,scenario,self.plan)
 def test_wrong_plan_identity(self):
  with tempfile.TemporaryDirectory() as d:
   args=self.args(d,'death');args[0]['originalSkinProbe']['identity']['capturePlanSha256']='0'*64
   with self.assertRaises(ValueError):inspect_run(*args)
 def test_removed_heavy_bake(self):
  with tempfile.TemporaryDirectory() as d:
   args=self.args(d,'damaged-heavy');args[0]['originalSkinProbe']['identity']['bakeSteps']=[0,28,80,112]
   with self.assertRaises(ValueError):inspect_run(*args)
 def test_bad_actual_camera(self):
  with tempfile.TemporaryDirectory() as d:
   args=self.args(d,'death');args[0]['originalSkinProbe']['identity']['cameraWorld'][0][3]+=1
   with self.assertRaises(ValueError):inspect_run(*args)
 def test_one_soft_vertex_corruption(self):
  with tempfile.TemporaryDirectory() as d:
   args=self.args(d,'damaged');args[0]['originalSkinProbe']['captures'][0]['bakedVerticesRendererLocal'][5000][0]+=.1
   r=inspect_run(*args);self.assertGreater(r['maximumBakeError'],TOL)
if __name__=='__main__':unittest.main()
