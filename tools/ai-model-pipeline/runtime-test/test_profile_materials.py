import copy,unittest,json,tempfile,argparse
from pathlib import Path
from datetime import datetime,timezone
from unittest.mock import patch
from run_case import Runner
import profile_materials as p


def assignment():
 return dict(rendererPath='enCube',glbFile='cube.glb',materialSlots=[dict(primitiveIndex=0,nativeMaterialSlot=1,textureFile='shell.png'),dict(primitiveIndex=1,nativeMaterialSlot=0,disableNativeEmission=True)])

class Tests(unittest.TestCase):
 def test_preserves_legacy_and_all_slot_pins(self):
  self.assertIsNone(p.slots(dict(glbFile='old.glb',textureFile=None)))
  self.assertEqual(p.asset_names({'renderers':[assignment(),dict(glbFile='old.glb',textureFile='old.png')]}),['cube.glb','old.glb','old.png','shell.png'])
 def test_invalid_bijections_and_types(self):
  for field,value in [('primitiveIndex',True),('nativeMaterialSlot',1.0),('primitiveIndex',2),('textureFile',None),('textureFile','../bad.png'),('disableNativeEmission',None),('unknown',True)]:
   a=assignment();a['materialSlots'][0][field]=value
   with self.assertRaises(ValueError):p.slots(a)
  for field in ('primitiveIndex','nativeMaterialSlot'):
   a=assignment();a['materialSlots'][1][field]=a['materialSlots'][0][field]
   with self.assertRaises(ValueError):p.slots(a)
 def test_ambiguous_or_null_mode_rejected(self):
  for field in ('textureFile','disableNativeEmission'):
   a=assignment();a[field]=None
   with self.assertRaises(ValueError):p.slots(a)
  for value in (None,[],[{}],5):
   a=assignment();a['materialSlots']=value
   with self.assertRaises(ValueError):p.slots(a)
 def test_static_assignment_forbids_native_slot_mode(self):
  static=dict(rendererPath='eye',glbFile='eye.glb',rendererKind='MeshRenderer')
  self.assertIsNone(p.slots(static))
  static['materialSlots']=assignment()['materialSlots']
  with self.assertRaises(ValueError):p.slots(static)
 def test_runner_pins_changed_secondary_texture_before_operation(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp).resolve();models=root/'BepInEx/plugins/FTKModFramework_content/models';models.mkdir(parents=True)
   a=assignment();key='ftkmf_modeltest_cube';(root/'model-test-profiles.json').write_text(json.dumps({'profiles':[{'key':key,'renderers':[a]}]}))
   for name in p.asset_names({'renderers':[a]}):(models/name).write_bytes(name.encode())
   registration={'status':'registered','registered':[{'key':key}],'run':'a'*32,'updatedUtc':datetime.now(timezone.utc).isoformat()}
   (root/'model-test-registration.json').write_text(json.dumps(registration))
   r=Runner.__new__(Runner);r.root=root;r.a=argparse.Namespace(enemy=key);r.session_mtime=0;r.log=lambda *args:None;r.check_session=lambda:None;r.binary_pins={}
   Runner.profile_preflight(r)
   with patch('run_case.measured_binaries',return_value={}):
    r.check_inputs();(models/'shell.png').write_bytes(b'changed second native slot texture')
    with self.assertRaisesRegex(RuntimeError,'asset changed'):r.check_inputs()
 def fixture(self):
  renderer=dict(instanceId=5,ownerInstanceId=6,celInstanceId=7)
  slot=lambda index:dict(slot=index,instanceId=20+index,inCurrentLeaseResources=True,emissionKeyword=False,_EmissionMapSupported=False)
  observed=dict(rendererInstanceId=5,ownerInstanceId=6,celInstanceId=7,celRelativeRendererPath='enCube',slots=[slot(0),slot(1)],lease=dict(acquired=True,applied=True),geometry=dict(submeshCount=2,submeshes=[dict(nativeMaterialSlot=i,indices=36) for i in range(2)]))
  observed['slots'][1]['_MainTex']=dict(name='ftkmf_shell.png',inCurrentLeaseResources=True)
  return renderer,observed
 def test_exact_observed_slot_coverage(self):
  r,o=self.fixture();p.validate_observation(o,r,assignment(),True)
 def test_wrong_owner_texture_lease_and_submesh_rejected(self):
  changes=[lambda o:o.update(ownerInstanceId=8),lambda o:o['slots'][1]['_MainTex'].update(name='native'),lambda o:o['slots'][0].update(inCurrentLeaseResources=False),lambda o:o['lease'].update(acquired=False),lambda o:o['geometry'].update(submeshCount=1),lambda o:o['slots'][0].update(emissionKeyword=True)]
  for change in changes:
   r,o=self.fixture();change(o)
   with self.assertRaises(ValueError):p.validate_observation(o,r,assignment(),True)
if __name__=='__main__':unittest.main()
