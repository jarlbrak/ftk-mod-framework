"""Read-only lease observer boundary checks, not Unity lifetime evidence."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'LeaseWatch.cs').read_text()
CODE = re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)


class LeaseWatchBoundary(unittest.TestCase):
    def test_world_arm_preserves_combat_and_dungeon_boundaries(self):
        arm = SOURCE.split('JObject WatchLease(JObject command)', 1)[1].split('JObject WatchPreviewLease', 1)[0]
        self.assertIn('CatalogNoLinks(root);RequireSinglePlayer();RequireOutsideCombat();', arm)
        self.assertIn('if(flow==null)throw', arm)
        self.assertIn('GetField("m_DungeonEntered",Members).GetValue(flow)!=null)RequireReadyPreparation();', arm)
        self.assertIn('LeaseObservationPin.ExactId(command,"heroInstanceId",true)', arm)
        self.assertIn('candidate.GetInstanceID()==heroId', arm)
        self.assertIn('cow==null || cow.m_Avatar==null', arm)
        self.assertIn('WatchAvatarLease(command,cow.m_Avatar,heroId', arm)

    def test_observer_cannot_mutate_or_retain_native_resources(self):
        for name in ('SetValue', 'Instantiate', 'Destroy', 'SetActive', 'EnsureRetained',
                     'RetainHierarchy', 'RetainForDetachedRenderer', 'Release', 'Append',
                     'ForceEquip', 'Equip', 'Unequip', 'SendEvent'):
            self.assertNotRegex(CODE, r'\b' + name + r'\s*\(')
        self.assertNotRegex(CODE, r'\bm_\w+\s*=(?!=)')
        self.assertIn('SceneOwner(cel) && cel.gameObject.activeInHierarchy', SOURCE)
        self.assertIn('entry.pin.Check(root,sessionId,PreviewCoreIdentity())', SOURCE)
        self.assertIn('!present && allNull?"observed-disposed"', SOURCE)

    def test_equipment_mutation_still_requires_ready(self):
        source = (ROOT / 'EquipmentFixture.cs').read_text()
        method = source.split('JObject ChangeBodyEquipment(JObject command,bool equip)', 1)[1]
        self.assertTrue(method.lstrip().startswith('{\n        RequireReadyPreparation();'))


if __name__ == '__main__':
    unittest.main()
