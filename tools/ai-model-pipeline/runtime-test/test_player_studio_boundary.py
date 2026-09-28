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
    def test_combat_camera_follows_live_bone_extents(self):
        self.assertIn('Vector3 offset=bone.position-avatar.transform.position;', SOURCE)
        self.assertIn('center+=right*((left+rightmost)*.5f)+direction*((near+far)*.5f);', SOURCE)
        self.assertIn('(rightmost-left)/.75f', SOURCE)
        self.assertIn('"framingCenter",Vec(center)', SOURCE)
        self.assertIn('"live-bone-projected-extents"', SOURCE)

    def test_head_closeup_uses_exact_live_bone_and_explicit_views(self):
        self.assertIn('bone.name=="Head_M"', SOURCE)
        self.assertIn('headBone!=null && headBone!=bone', SOURCE)
        self.assertIn('if(headBone==null)throw', SOURCE)
        self.assertIn('center=headBone.position+up*(span*.05f)', SOURCE)
        self.assertIn('view=="side"?90f:view=="other-side"?-90f', SOURCE)
        self.assertIn('"headBoneInstanceId"', SOURCE)
        self.assertIn('framing!="body" && framing!="head"', SOURCE)

    def test_native_lighting_only_omits_temporary_lights(self):
        self.assertIn('Str(command,"lighting")??"supplemental"', SOURCE)
        self.assertIn('lighting!="supplemental" && lighting!="native"', SOURCE)
        self.assertIn('bool supplementalLighting=true', SOURCE)
        lights=SOURCE[SOURCE.index('            if(supplementalLighting)'):SOURCE.index('            if(materialDiagnostic==null)camera.Render();')]
        self.assertIn('keyObject=new GameObject', lights)
        self.assertIn('fillObject=new GameObject', lights)
        self.assertEqual(SOURCE.count('AddComponent<Light>()'), 2)
        self.assertIn('"lighting",supplementalLighting?"supplemental":"native"', SOURCE)
        self.assertIn('"addedLightCount",supplementalLighting?2:0', SOURCE)
        self.assertNotIn('RenderSettings.', CODE)
        self.assertNotIn('FindObjectsOfType<Light>', CODE)

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

    def test_exact_render_camera_matrices_are_observed_before_cleanup(self):
        start=SOURCE.index('studioCamera=new JObject')
        self.assertLess(SOURCE.index('camera.Render();'),start)
        self.assertLess(start,SOURCE.index('DestroyImmediate(cameraObject)'))
        for field in ('Matrix(camera.worldToCameraMatrix)', 'Matrix(camera.projectionMatrix)',
                      '"frame",Time.frameCount', '"studioCamera",studioCamera'):
            self.assertIn(field,SOURCE)
        self.assertNotIn('camera.projectionMatrix=',CODE)
        self.assertNotIn('camera.worldToCameraMatrix=',CODE)
        apparel=SOURCE.index('studioApparel=OriginalApparelMaterials(avatar.gameObject);')
        self.assertLess(start,apparel)
        self.assertLess(apparel,SOURCE.index('image.ReadPixels('))
        self.assertIn('"studioOriginalApparelMaterials",studioApparel',SOURCE)

    def test_material_diagnostic_is_inventory_only_and_cannot_be_art_acceptance(self):
        self.assertIn('materialDiagnostic==null || source!="inventory"', SOURCE)
        self.assertIn('avatar==cow.m_Avatar', SOURCE)
        self.assertIn('inventory.m_IsUpdatePaperDoll', SOURCE)
        self.assertIn('inventory.m_InventoryOwner!=cow', SOURCE)
        self.assertIn('result["diagnosticCapture"]=true;result["artAcceptanceEligible"]=false;', SOURCE)
        self.assertIn('delegate { camera.Render(); }', SOURCE)
        diagnostic=(ROOT/'StudioMaterialDiagnostic.cs').read_text()
        for forbidden in ('.vertices', '.triangles', '.boneWeights', '.uv', 'BakeMesh',
                          '.SetActive(', '.materials=', 'owner.m_Avatar.sharedMaterials'):
            self.assertNotIn(forbidden, diagnostic)
        self.assertIn('owner.m_Avatar==avatar', diagnostic)
        self.assertIn('avatar.GetInstanceID()!=cloneId', diagnostic)

if __name__=='__main__':unittest.main()
