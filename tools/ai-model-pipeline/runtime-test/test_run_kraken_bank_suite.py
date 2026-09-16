import argparse,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import run_kraken_bank_suite as m
class Tests(unittest.TestCase):
 def setup_suite(self,root):
  root=root.resolve();s=m.Suite.__new__(m.Suite);s.root=root;s.output=root/'evidence';s.output.mkdir();(root/'model-test-output').mkdir();s.session='a'*32;s.timeout=.01;s.seq=0;s.pending=None;s.check=lambda:None;s.ready_pin=None;s.native_ready_pin=None
  m.save(root/'model-test-session.json',{'session':s.session});return s
 def test_uncertain_timeout_never_resubmits(self):
  with tempfile.TemporaryDirectory() as d:
   s=self.setup_suite(Path(d));dest=s.output/'first.json'
   with patch.object(m.time,'sleep',return_value=None):
    with self.assertRaises(TimeoutError):s.command('kraken-controller-fixture',dest,{'scenario':'intro','modernInputMixer':m.MODE})
   sent=m.read(s.root/'model-test-command.json');self.assertEqual(sent['id'],s.pending)
   with self.assertRaises(ValueError):s.command('fixture-state',s.output/'second.json')
   self.assertEqual(m.read(s.root/'model-test-command.json'),sent)
 def test_completed_result_raw_bytes_preserved_once(self):
  with tempfile.TemporaryDirectory() as d:
   s=self.setup_suite(Path(d));s.timeout=1
   def publish(_):
    result={'id':s.pending,'session':s.session,'ok':True};(s.root/'model-test-output'/(s.pending+'.json')).write_text(json.dumps(result,separators=(',',':')))
   with patch.object(m.time,'sleep',side_effect=publish):r=s.command('fixture-state',s.output/'result.json')
   self.assertIsNone(s.pending);self.assertEqual((s.output/'result.json').read_bytes(),(s.root/'model-test-output'/(r['id']+'.json')).read_bytes())
   events=[json.loads(x) for x in (s.output/'journal.jsonl').read_text().splitlines()];self.assertEqual([x['kind'] for x in events],['submission-intent','submitted','result'])
 def test_pending_foreign_command_never_overwritten(self):
  with tempfile.TemporaryDirectory() as d:
   s=self.setup_suite(Path(d));p=s.root/'model-test-command.json';m.save(p,{'id':'b'*32,'session':s.session,'op':'capture'})
   original=p.read_bytes()
   with self.assertRaises(ValueError):s.command('fixture-state',s.output/'result.json')
   self.assertEqual(p.read_bytes(),original);self.assertFalse((s.output/'journal.jsonl').exists())
 def test_wrong_result_identity_stops_with_raw_evidence(self):
  with tempfile.TemporaryDirectory() as d:
   s=self.setup_suite(Path(d));s.timeout=1
   def publish(_):m.save(s.root/'model-test-output'/(s.pending+'.json'),{'id':'c'*32,'session':s.session,'ok':True})
   with patch.object(m.time,'sleep',side_effect=publish):
    with self.assertRaisesRegex(ValueError,'identity'):s.command('fixture-state',s.output/'result.json')
   self.assertTrue((s.output/'result.json').exists());self.assertIsNotNone(s.pending)
 def test_changed_pins_prevent_submission(self):
  with tempfile.TemporaryDirectory() as d:
   s=self.setup_suite(Path(d));s.check=lambda:(_ for _ in ()).throw(ValueError('Changed pin'))
   with self.assertRaises(ValueError):s.command('fixture-state',s.output/'result.json')
   self.assertFalse((s.root/'model-test-command.json').exists())
 def test_timeouts_finite_positive(self):
  for value in ('nan','inf','-inf','0','-1'):
   with self.assertRaises(ValueError):m.finite_positive(value)
 def test_wrong_ready_owner_and_changed_slot(self):
  s=m.Suite.__new__(m.Suite);s.root=Path('/scratch/game');s.session='a'*32;s.ready_pin=None
  value={'identity':{'root':str(s.root),'session':s.session},'strictReady':{'ok':True,'room':2},'dungeon':{'room':2}}
  s.command=lambda *a:value;s.ready(Path('unused'))
  value['strictReady']={'ok':True,'room':3}
  with self.assertRaises(ValueError):s.ready(Path('unused'))
 def test_offline_explicit_deployment_catalog_and_session(self):
  with tempfile.TemporaryDirectory() as d:
   base=Path(d).resolve();root=base/'scratch/game';root.mkdir(parents=True)
   files={'model-test-profiles.json':b'catalog','BepInEx/plugins/FtkRuntimeModelTest.dll':b'helper','BepInEx/plugins/FTKModFramework.dll':b'core','BepInEx/plugins/FtkRuntimeModelTestContent.dll':b'content'}
   pins={}
   for rel,data in files.items():
    p=root/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data);pins[rel]=m.digest(p)
   assets=root/'FTK_Data/resources.assets';assets.parent.mkdir();assets.write_bytes(b'assets')
   assembly=root/'FTK_Data/Managed/Assembly-CSharp.dll';assembly.parent.mkdir();assembly.write_bytes(b'assembly')
   m.save(root/'model-test-session.json',{'session':'a'*32})
   dep=base/'deployment.json';m.save(dep,{'root':str(root),'new':pins})
   args=argparse.Namespace(root=root,output=base/'scratch/fresh',session='a'*32,timeout=1,deployment=dep,deployment_sha256=m.digest(dep),catalog_sha256=pins['model-test-profiles.json'])
   with patch.object(m,'HELPER',pins['BepInEx/plugins/FtkRuntimeModelTest.dll']),patch.object(m,'ASSETS',m.digest(assets)),patch.object(m,'ASSEMBLY',m.digest(assembly)):
    suite=m.Suite(args);self.assertFalse(args.output.exists());self.assertFalse((root/'model-test-command.json').exists())
    args.catalog_sha256='0'*64
    with self.assertRaises(ValueError):m.Suite(args)
    args.catalog_sha256=pins['model-test-profiles.json'];args.session='b'*32
    with self.assertRaises(ValueError):m.Suite(args)
 def test_no_other_operations(self):
  s=m.Suite.__new__(m.Suite)
  with self.assertRaises(ValueError):s.command('start_run',Path('unused'))
if __name__=='__main__':unittest.main()
