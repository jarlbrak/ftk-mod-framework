"""Studio authority checks. Image framing and appearance require live review."""
from pathlib import Path
import re
import unittest
ROOT=Path(__file__).parent
SOURCE=(ROOT/'PlayerStudio.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)
class PlayerStudioBoundary(unittest.TestCase):
    def test_no_native_geometry_export_or_equipment_pose_mutation(self):
        for value in ('.vertices','.triangles','.bounds','.sharedMesh','.materials',
                      '.sharedMaterials','BakeMesh','SetFocus','SendEvent','EquipItem',
                      'Animator','SetActive','Instantiate'):
            self.assertNotIn(value,CODE)
        self.assertNotRegex(CODE,r'\bm_\w+\s*=(?!=)')
        self.assertIn('renderer.bones',CODE)
    def test_exact_session_actor_and_owned_path(self):
        self.assertIn('RequireSinglePlayer();CatalogNoLinks(root);CatalogNoLinks(output);',SOURCE)
        self.assertIn('avatar.GetInstanceID()!=celId',CODE)
        self.assertIn('avatar.m_uiQuickPlayerCreate!=preview',CODE)
        self.assertIn('avatar.m_CharacterOverworld!=cow',CODE)
        self.assertIn('Path.Combine(output,Token(Str(command,"id"))+".png")',SOURCE)
        self.assertIn('if(File.Exists(path))',CODE)
    def test_cleanup_covers_success_and_failure(self):
        cleanup=CODE[CODE.index('finally'):CODE.index('File.WriteAllBytes')]
        self.assertIn('part.Key.layer=part.Value',cleanup)
        self.assertIn('RenderTexture.active=prior',cleanup)
        for name in ('cameraObject','keyObject','fillObject','target','image'):
            self.assertIn('DestroyImmediate('+name+')',cleanup)
        self.assertIn('target.Release()',cleanup)
        self.assertIn('camera.enabled=false',CODE)
        self.assertIn('camera.cullingMask=1<<layer',CODE)
    def test_combat_supplement_does_not_replace_native_capture(self):
        capture=(ROOT/'Plugin.cs').read_text()
        self.assertIn('op!="capture" || Scope(command)!="player-combat"',capture)
        self.assertIn('if(currentOwner.scope!="player-combat")',capture)
        self.assertIn('currentOwner.owner.GetInstanceID()!=capturedOwnerId',capture)
        self.assertIn('currentOwner.cel.GetInstanceID()!=capturedCelId',capture)
        self.assertIn('screen.ReadPixels(',capture)
        self.assertIn('index.ToString("D4")+"-studio.png"',capture)
        self.assertLess(capture.index('JObject pose=Snapshot(renderer,false)'),
                        capture.index('pose["studio"]=RenderPlayerStudioAvatar'))
        self.assertIn('if(studioView!=null)captureResult["studioView"]=studioView;',capture)

if __name__=='__main__':unittest.main()
