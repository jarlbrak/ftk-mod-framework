import pathlib,unittest
ROOT=pathlib.Path(__file__).resolve().parents[3]
class Boundary(unittest.TestCase):
    def test_observer_never_manufactures_ownership_or_cleanup(self):
        source=(ROOT/'tools/ai-model-pipeline/runtime-test/EnemyLifetimeWatch.cs').read_text()
        for forbidden in ['.EnsureRetained(','.ValidLease(','.ScrollingMaterials(','.SetResources(','.SetTargets(','.Release(','.Destroy(','.Instantiate(','.AddComponent(','.SetActive(','.Stop(','.Snapshot(','.DoRender(','.materials']:
            self.assertNotIn(forbidden,source)
    def test_invalid_readout_stops_new_samples(self):
        source=(ROOT/'tools/ai-model-pipeline/runtime-test/EnemyLifetimeWatch.cs').read_text()
        self.assertIn('if(w==null||w.readoutInvalid)return;',source)
        self.assertIn('EnemyLifetimePolicy.CheckIdentity(ref enemyLifetime.readoutInvalid,EnemyLifetimePinsCore)',source)
    def test_preserved_arrival_candidate_contains_no_lifetime_draft(self):
        import json,hashlib
        folder=ROOT/'scratch/candidate-enemy-arrival-helper-fbd04bed'
        if not folder.exists():self.skipTest('local preserved candidate unavailable')
        receipt=json.loads((folder/'receipt.json').read_text())
        self.assertEqual(hashlib.sha256((folder/receipt['binary']['file']).read_bytes()).hexdigest(),receipt['binary']['sha256'])
        for pin in receipt['sourcePins']:self.assertNotIn('EnemyLifetime',pin['path'])
    def test_hooks_preserve_exception_and_normal_portrait_record(self):
        source=(ROOT/'tools/ai-model-pipeline/runtime-test/PortraitTrace.cs').read_text()
        self.assertIn('PortraitFinalization.Complete(scope.record,__exception);',source)
        self.assertIn('EnemyLifetimeFinalized(scope.lifetime,__exception)',source)
        self.assertIn('return __exception;',source)
        self.assertIn('EnemyLifetimeBeforeRender(s.lifetime,__instance)',source)
if __name__=='__main__':unittest.main()
