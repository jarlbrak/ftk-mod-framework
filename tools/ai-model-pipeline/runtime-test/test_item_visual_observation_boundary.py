from pathlib import Path
import unittest

SOURCE = (Path(__file__).parent / 'ItemVisualObservation.cs').read_text()


class ItemVisualObservationBoundaryTests(unittest.TestCase):
    def test_card_uses_owned_native_inventory_route(self):
        for guard in ('RequireOutsideCombat();', 'int ownedCount = OwnedAcrossEquipment(hero, item.itemId)', 'if (ownedCount < 1)',
                      '{ "ownedCount", ownedCount }',
                      'Field(inventory, "m_InventoryOwner")', 'inventory.m_IsShowing',
                      'card.Show(item.itemId, inventory.transform, hero, uiItemDetail.Mode.Inventory)',
                      'card.GetCurrentItemID() != item.itemId'):
            self.assertIn(guard, SOURCE)

    def test_observation_does_not_extract_native_surfaces(self):
        self.assertIn('mesh.name.StartsWith("ftkmf_", StringComparison.Ordinal)', SOURCE)
        for forbidden in ('.vertices', '.triangles', 'BakeMesh(', 'Camera.Render(', 'camera.Render(',
                          '.localPosition =', '.localRotation =', '.sharedMesh ='):
            self.assertNotIn(forbidden, SOURCE)

    def test_apparel_observation_has_no_surface_or_material_mutation(self):
        for value in ('OriginalApparelMaterials', 'mesh.name.StartsWith("ftkmf_", StringComparison.Ordinal)',
                      'renderer.sharedMaterials', 'material.HasProperty(name)', 'material.GetColor(name)',
                      'material.GetFloat(name)', '"_Color"', '"_SpecColor"', '"_Glossiness"',
                      '"_Shininess"', '"_Metallic"', '"_Smoothness"', '"mainTexture"', '"sceneLighting"'):
            self.assertIn(value, SOURCE)
        for forbidden in ('.vertices', '.triangles', '.bindposes', 'GetPixels(',
                          '.material =', '.materials =', 'SetColor(', 'SetFloat(', 'SetTexture(', 'new Material(',
                          'camera.Render(', 'UpdatePaperDoll('):
            self.assertNotIn(forbidden, SOURCE)

    def test_inventory_observation_requires_both_avatar_pins(self):
        for value in ('cel != hero.m_Avatar', 'inventory.m_IsUpdatePaperDoll', 'inventory.m_InventoryOwner != hero',
                      'inventory.m_PaperDoll.texture != camera.m_RenderTexture', 'cel.m_OffscreenCamera != camera',
                      'LeaseObservationPin.ExactId(command, "inventoryCelInstanceId", true)',
                      'cel.GetInstanceID() != cloneId', '"sourceCelInstanceId", sourceCelId'):
            self.assertIn(value, SOURCE)

    def test_pose_metadata_is_bounded_and_only_for_original_apparel(self):
        method = SOURCE[SOURCE.index('static JArray OriginalApparelMaterials'):SOURCE.index('static JObject ItemSceneLighting')]
        self.assertLess(method.index('mesh.name.StartsWith("ftkmf_"'), method.index('Transform[] bones = renderer.bones'))
        for value in ('bones.Length > 256', '"index", index', '"name", bone == null ? null : bone.name',
                      'Matrix(bone.localToWorldMatrix)', 'Matrix(renderer.localToWorldMatrix)', '"bones", boneTransforms'):
            self.assertIn(value, method)
        for forbidden in ('.bindposes', '.boneWeights', '.uv', '.vertices'):
            self.assertNotIn(forbidden, method)
        for value in ('renderer.quality', 'QualitySettings.blendWeights', 'rendererQuality == SkinQuality.Auto',
                      '"derivedInfluenceLimit"', '"influenceLimitBasis"'):
            self.assertIn(value, method)
        self.assertNotIn('renderer.quality =', method)
        self.assertNotIn('QualitySettings.blendWeights =', method)

    def test_temporary_observer_is_removed_on_failure(self):
        self.assertIn('try { harmony.Unpatch(method, observer); }', SOURCE)
        self.assertIn('finally { try { harmony.Unpatch(method, completed); } finally { itemCardObserver = null; } }', SOURCE)
        self.assertIn('itemCardObservedItemId == item.numericId', SOURCE)

    def test_package_card_requires_explicit_pins_and_preserves_legacy_route(self):
        for value in ('Str(command, "session") != sessionId', 'bool package = command["configSha256"] != null',
                      'RequirePackageFitIsolation();', 'if (packageFitUncertain)',
                      'package ? ExactPackageFitHero(heroId) : ExactBlacksmithHero(heroId)',
                      'ResolvePackageFitGear(Str(command, "item"), PackageFitItems(Str(command, "configSha256")))',
                      ': ResolveBlacksmithGear(Str(command, "item"))'):
            self.assertIn(value, SOURCE)

    def test_capture_rejects_stale_ambiguous_incomplete_and_blank_evidence(self):
        for value in ('command["capture"].Type != JTokenType.Boolean',
                      'capture && Str(command, "action") != "show"',
                      'itemCardRenderObservations.Count == 1', 'render["observationError"] != null',
                      '!(bool)render["completed"]', '(string)render["cameraId"] != expectedCameraId',
                      'texture.GetInstanceID() != (int)render["textureInstanceId"]',
                      'Time.frameCount != (int)render["renderFrame"]',
                      'if ((bool)pixels["blankCandidate"]) throw', 'FileMode.CreateNew', 'string expectedCameraId = "Item," + width + "," + height',
                      'texture.width != width || texture.height != height',
                      'card.m_OffscreenCamera.CameraID != expectedCameraId'):
            self.assertIn(value, SOURCE)
        self.assertLess(SOURCE.index('card.Show(item.itemId'), SOURCE.index('JObject image = capture ?'))

    def test_capture_reads_only_native_cpu_texture_and_rechecks_identity(self):
        method = SOURCE[SOURCE.index('JObject CaptureNativeItemCard'):SOURCE.index('JObject NativeItemCard')]
        for value in ('card.m_ItemDisplay.texture as Texture2D', 'texture.EncodeToPNG()',
                      'card.GetCurrentItemID() != item.itemId', 'card.m_ItemDisplay.texture != texture',
                      'card.m_LastOwner != inventory.transform', 'OwnedAcrossEquipment(hero, item.itemId) < 1'):
            self.assertIn(value, method)
        for forbidden in ('new Texture2D', 'RenderTexture.active =', '.ReadPixels(', '.Render(',
                          '.texture =', '.SetPixels', '.Apply()', 'Destroy('):
            self.assertNotIn(forbidden, method)

    def test_render_completion_is_bound_to_prefix_texture_target_camera_and_frame(self):
        method = SOURCE[SOURCE.index('static void ObserveNativeItemRenderCompleted'):SOURCE.index('JObject CaptureNativeItemCard')]
        for value in ('__state == null', '__instance.m_Texture2D != null', '__instance.m_TargetObject != null',
                      '__instance.GetInstanceID() == (int)__state["offscreenCameraInstanceId"]',
                      '__instance.m_Texture2D.GetInstanceID() == (int)__state["textureInstanceId"]',
                      '__instance.m_TargetObject.GetInstanceID() == (int)__state["targetInstanceId"]',
                      'Time.frameCount == (int)__state["renderFrame"]'):
            self.assertIn(value, method)

    def test_native_metadata_has_no_native_surface_read_or_hide(self):
        method = SOURCE[SOURCE.index('static JArray NativeSkinnedRendererMetadata'):SOURCE.index('static JObject ItemSceneLighting')]
        for value in ('renderers.Length > 128', 'assigned.Length > 32', 'mesh.subMeshCount',
                      '"slot", slot', 'ItemMaterialObservation(assigned[slot])', '"path", Relative',
                      '"nativeSkinnedRenderers", NativeSkinnedRendererMetadata(cel.gameObject)'):
            self.assertIn(value, SOURCE)
        for forbidden in ('.vertices', '.triangles', '.normals', '.uv', '.boneWeights', '.bindposes',
                          '.bounds', 'GetIndices(', 'GetTriangles(', '.enabled =', 'SetActive(',
                          '.materials =', '.sharedMaterials =', 'new Material('):
            self.assertNotIn(forbidden, method)


if __name__ == '__main__':
    unittest.main()
