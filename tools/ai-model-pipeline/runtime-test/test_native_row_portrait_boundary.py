"""Source authority guards for owned native UI fixture; not a simulated Unity test."""
from pathlib import Path
import re
import unittest
SOURCE=Path(__file__).with_name('NativeRowPortraitFixture.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"','',SOURCE)

class NativeRowPortraitBoundaryTests(unittest.TestCase):
    def test_only_one_native_ui_call_without_direct_render_or_synthetic_dummy(self):
        self.assertEqual(len(re.findall(r'ui\.Initialize\(',CODE)),1)
        for name in ('Snapshot','DoRender','GetOSC','Stop','EnsureRetained','Release','PruneDestroyedOwners','SetResources'):
            self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertNotRegex(CODE,r'AddComponent\s*<\s*(?:EnemyDummy|CharacterDummy)\s*>')
        self.assertNotRegex(CODE,r'\.materialForRendering\b')

    def test_preflight_precedes_allocations_and_proof_precedes_publish(self):
        pre=CODE.split('owned=RowUi',1)[0]
        for token in ('pin.Check(sessionId)','portraitTrace.Count!=0','camera.m_TargetObject!=null','camera.m_Texture2D!=null','RowPortraitUiPrefab(prefab)'):
            self.assertIn(token,pre)
        publish=CODE.split('File.Move',1)[0]
        self.assertIn('PortraitFinalization.Successful(trace)',publish)
        self.assertIn('priorLeases.Contains(leaseId)',publish)
        self.assertIn('sourceUnchanged=JToken.DeepEquals(before,after)',publish)

    def test_cleanup_never_destroys_shared_source_or_camera(self):
        targets=re.findall(r'UnityEngine\.Object\.Destroy\((\w+)\)',CODE)
        self.assertEqual(set(targets),{'pixels','owned'})
        self.assertIn('finally{RenderTexture.active=active;}',CODE)
        self.assertIn('unknownTarget=true',CODE)
        self.assertIn('resources.Clear();busy=false',CODE)

if __name__=='__main__':unittest.main()
