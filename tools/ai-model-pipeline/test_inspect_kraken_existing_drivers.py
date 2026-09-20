from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy")
import copy,unittest
from inspect_kraken_existing_drivers import inspect,PATHS
I=[[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]]
def fixture(missing=False):
 paths=['Root_M']+([] if missing else PATHS)
 pose={'locals':{p:{'matrix':copy.deepcopy(I),'instanceId':i+1} for i,p in enumerate(paths)},'prefabRootModels':{p:copy.deepcopy(I) for p in paths}}
 obs={'scope':'owned_native_controller_input_observation_no_adapter','status':'unavailable_missing_native_paths' if missing else 'available','existingPaths':['']+paths,'requiredPaths':PATHS,'missingPaths':PATHS if missing else [],'pose':pose}
 frame={'old':{'existingDrivers':obs,'pose':pose},'modern':{'pose':copy.deepcopy(pose)}}
 for side in ('old','modern'):
  frame[side].update(current={'fullPathHash':1,'normalizedTime':0.},next=None,inTransition=False,transition=None,currentClips=[{'name':'idle','weight':1.}],nextClips=[])
 return {'ok':True,'sameReadyAfter':True,'scenario':'damaged','identity':{'oldExistingDriverRest':copy.deepcopy(obs)},'frames':[dict(copy.deepcopy(frame),step=i) for i in range(241)]}
class Tests(unittest.TestCase):
 def test_present(self):self.assertEqual(inspect(fixture())['status'],'observed_existing_native_drivers')
 def test_absent_is_not_failure_or_synthesized(self):self.assertEqual(inspect(fixture(True))['missingPaths'],PATHS)
 def test_motion_difference_is_measured_not_waived(self):
  r=fixture();r['frames'][100]['modern']['pose']['prefabRootModels'][PATHS[1]][0][3]=2
  self.assertEqual(inspect(r)['maximumDifferencesFromModernInput'][PATHS[1]]['model'],2)
 def test_false_available(self):
  r=fixture(True);r['identity']['oldExistingDriverRest']['status']='available'
  with self.assertRaises(ValueError):inspect(r)
 def test_shifted_clock(self):
  r=fixture();r['frames'][50]['modern']['current']['normalizedTime']=.1
  with self.assertRaises(ValueError):inspect(r)
 def test_frame_gap(self):
  r=fixture();r['frames'][10]['step']=11
  with self.assertRaises(ValueError):inspect(r)
 def test_stale_identity(self):
  r=fixture();r['frames'][10]['old']['existingDrivers']['pose']['locals']['Root_M']['instanceId']=99
  with self.assertRaises(ValueError):inspect(r)
if __name__=='__main__':unittest.main()
