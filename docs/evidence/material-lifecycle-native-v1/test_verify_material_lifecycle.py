"""Corrupt the lossless live trace; no Unity lifecycle simulation or game calls."""
import copy,json,shutil,tempfile,unittest
from pathlib import Path
from verify_material_lifecycle import verify,run

EVIDENCE=Path(__file__).resolve().parents[2]/'docs/evidence/material-lifecycle-native-v1'
class Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data=[json.loads((EVIDENCE/(n+'.json')).read_text()) for n in ['result','request','before-ready','after-ready','deployment']]
 def bad(self,change):
  data=copy.deepcopy(self.data);change(data)
  with self.assertRaises((ValueError,KeyError,TypeError)):verify(*data)
 def test_live_raw_phase_identity_and_disposal(self):
  r=run(EVIDENCE/'manifest.json');self.assertEqual(r['status'],'owned_material_lifecycle_audit_pass');self.assertEqual(r['rawPhaseChecks'],100);self.assertEqual(r['maximumPhaseError'],0)
 def test_assertion_booleans_are_not_the_oracle(self):
  d=copy.deepcopy(self.data)
  for case in d[0]['cases']:case['assertions']=[]
  self.assertEqual(verify(*d)['rawPhaseChecks'],100)
 def test_raw_phase_and_offset_corruption(self):
  self.bad(lambda d:d[0]['cases'][0]['frames'][1]['owners'][0]['phase'].__setitem__(1,.5))
  self.bad(lambda d:d[0]['cases'][0]['frames'][1]['owners'][0]['offsets'][0].__setitem__(0,.1))
  self.bad(lambda d:d[0]['cases'][0]['frames'][3]['owners'][1]['offsets'][1].__setitem__(1,.5))
 def test_frame_gap_or_paused_clock(self):
  self.bad(lambda d:d[0]['cases'][0]['frames'][3].update(frame=12345))
  self.bad(lambda d:d[0]['cases'][0]['frames'][3].update(deltaTime=0))
  self.bad(lambda d:d[0]['cases'][0]['frames'][3].update(deltaTime=float('nan')))
 def test_same_reported_pass_does_not_hide_missing_native_update(self):
  def mutate(d):
   a,b=d[0]['cases'][0]['frames'][:2];b['owners'][0]['phase']=copy.deepcopy(a['owners'][0]['phase']);b['owners'][0]['offsets']=copy.deepcopy(a['owners'][0]['offsets'])
  self.bad(mutate)
 def test_clone_identity_and_bone_remap(self):
  self.bad(lambda d:d[0]['cases'][0]['frames'][3]['owners'][1].update(root=d[0]['cases'][0]['frames'][3]['owners'][0]['root']))
  self.bad(lambda d:d[0]['cases'][0]['frames'][3]['owners'][1].update(parentIndex=2))
  self.bad(lambda d:d[0]['cases'][0]['frames'][4]['owners'][1].update(boneInstanceId=999))
 def test_private_clone_and_grandclone_material_identity(self):
  def corrupt(d):
   f=d[0]['cases'][0]['frames'][8];r=f['owners'][2];r['materialIds']=copy.deepcopy(f['owners'][1]['materialIds']);r['serializedMaterialIds']=r['materialIds'][:]
  self.bad(corrupt)
  self.bad(lambda d:d[0]['cases'][0]['frames'][5]['owners'][1]['serializedMaterialIds'].__setitem__(1,999))
  self.bad(lambda d:d[0]['cases'][0]['frames'][5]['owners'][1]['textureIds'].__setitem__(1,999))
 def test_never_enabled_does_not_allocate(self):
  self.bad(lambda d:d[0]['cases'][0]['frames'][10]['owners'][3].update(rendererEnabled=True))
  self.bad(lambda d:d[0]['cases'][0]['frames'][10].update(resourceCount=11))
 def test_teardown_owner_and_lease_progress(self):
  self.bad(lambda d:d[0]['cases'][1]['frames'][16].update(references=1))
  self.bad(lambda d:d[0]['cases'][1]['frames'][18]['owners'][0].update(index=0))
  self.bad(lambda d:d[0]['cases'][0]['frames'].pop())
 def test_second_lineage_cannot_overlap_precede_or_follow_completion(self):
  def shifted(d,amount):
   for frame in d[0]['cases'][1]['frames']:
    frame['frame']+=amount
    for row in frame['owners']:row['frame']+=amount
  for delta in (-100,-22,100):self.bad(lambda d,delta=delta:shifted(d,delta))
 def test_exact_retired_asset_disposal(self):
  self.bad(lambda d:d[0]['cases'][0]['resourceDisposal'][0].update(unityNull=False))
  self.bad(lambda d:d[0]['cases'][0]['resourceDisposal'][0].update(instanceId=999))
  self.bad(lambda d:d[0]['cases'][0].update(leaseAbsent=False))
  self.bad(lambda d:d[0]['cases'][0]['resourceDisposal'][0].update(type='UnityEngine.Material'))
 def test_request_ready_and_binary_pins(self):
  self.bad(lambda d:d[1].update(session='f'*32))
  self.bad(lambda d:d[3]['dungeon'].update(room=3))
  self.bad(lambda d:d[3]['sessions'].update(esInCombat=True))
  self.bad(lambda d:d[4]['new'].update({'BepInEx/plugins/FtkRuntimeModelTest.dll':'0'*64}))
 def test_changed_lossless_file_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   for p in EVIDENCE.iterdir():
    if p.is_file():shutil.copy2(p,Path(temp)/p.name)
    elif p.is_dir():shutil.copytree(p,Path(temp)/p.name)
   with (Path(temp)/'result.json').open('a') as f:f.write(' ')
   with self.assertRaises(ValueError):run(Path(temp)/'manifest.json')
if __name__=='__main__':unittest.main()
