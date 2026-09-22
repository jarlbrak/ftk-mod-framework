"""Preview fixture authority boundaries, not race-fit acceptance."""
from pathlib import Path
import re
import unittest
ROOT=Path(__file__).parent
SOURCE=(ROOT/'PreviewRaceFixture.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
class PreviewRaceBoundary(unittest.TestCase):
    def test_only_native_rebuild_and_skin_assignment(self):
        writes=re.findall(r'\b(m_\w+)\s*=(?!=)',CODE)
        self.assertEqual(writes,['m_SkinType'])
        for method in ('SyncSettings','SaveSettings','OnSkinClick','EnterFahrul','SetUnlock','SetValue','SetFocus'):
            self.assertNotRegex(CODE,r'\b'+method+r'\s*\(')
        self.assertIn('GetMethod("SetClass",Members,null,new[]{typeof(int)},null)',SOURCE)
        self.assertIn('method.Invoke(previewRace.owner,new object[]{previewRace.classId})',CODE)
    def test_native_zero_skin_index_and_supported_rows(self):
        self.assertIn('if(result<0)',CODE)
        self.assertNotIn('if(result==0)',CODE)
        self.assertIn('PreviewRaceIndex(command,"skinType")',SOURCE)
        self.assertIn('skin>6',CODE)
        self.assertIn('row.m_Skinsets[skin]==FTK_skinset.ID.None',CODE)
    def test_inventory_pin_restoration_and_later_frame_gate(self):
        self.assertIn('object.ReferenceEquals(previewRace.owner.m_PlayerInventory,previewRace.inventory)',CODE)
        self.assertIn('JToken.DeepEquals(previewRace.preserved,PreviewRacePreserved(previewRace.owner))',CODE)
        self.assertIn('Time.frameCount>previewRace.changedFrame',CODE)
        self.assertIn('previewRace.retired==null',CODE)
        self.assertIn('PreviewRaceRebuild(previewRace.original)',CODE)
        self.assertRegex((ROOT/'Plugin.cs').read_text(),r'void OnDestroy\(\)\{[^}]*PreviewRaceCleanup\(\);')
        self.assertIn('PreviewRaceRequireStudioReady(ownerId)',(ROOT/'PlayerStudio.cs').read_text())
if __name__=='__main__':unittest.main()
