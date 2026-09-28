"""Readback authority boundaries; valid native pixels require a live trial."""
from pathlib import Path
import re
import unittest
ROOT=Path(__file__).parent
SOURCE=(ROOT/'NativeInventoryCapture.cs').read_text()
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"', '', SOURCE)

class NativeInventoryCaptureBoundary(unittest.TestCase):
    def test_cannot_render_or_change_native_scene(self):
        for forbidden in ('.Render(', 'DoRender(', 'SetActive(', 'Instantiate(', 'AddComponent',
                          '.vertices', '.triangles', '.sharedMesh', '.materials', 'SetTexture(',
                          'StartRenderStream(', 'SetTargetObject(', 'GL.', 'Graphics.'):
            self.assertNotIn(forbidden,CODE)
        self.assertNotRegex(CODE,r'\b(?:m_\w+|layer|enabled|targetTexture|cullingMask)\s*=(?!=)')
        self.assertEqual(CODE.count('new Texture2D('),1)
        self.assertNotIn('new RenderTexture(',CODE)

    def test_exact_existing_stream_is_checked_before_and_after(self):
        for guard in ('RequirePackageFitIsolation();', 'ExactPackageFitHero(ownerId)',
                      'source.GetInstanceID()!=sourceId', 'stream.GetInstanceID()!=cameraId',
                      'clone.GetInstanceID()!=cloneId', 'clone.m_OffscreenCamera!=stream',
                      'clone.m_CharacterOverworld!=hero', '!texture.IsCreated()',
                      'texture.width>2048', 'Time.frameCount!=frame'):
            self.assertIn(guard,CODE)
        for guard in ('inventory.m_IsUpdatePaperDoll','inventory.m_PaperDoll.texture!=texture',
                      'camera.targetTexture!=texture','inventory.m_InventoryOwner!=hero'):
            self.assertGreaterEqual(CODE.count(guard),2)
        self.assertLess(CODE.index('Time.frameCount!=frame'),CODE.index('FileStream(path'))

    def test_readback_restores_active_texture_even_on_failure(self):
        cleanup=CODE[CODE.index('finally'):CODE.index('if(uiPlayerInventory.Instance')]
        self.assertIn('RenderTexture.active=prior',cleanup)
        self.assertIn('DestroyImmediate(image)',cleanup)
        self.assertNotIn('Release()',CODE)
        self.assertIn('FileMode.CreateNew',CODE)

    def test_receipt_does_not_claim_freshness_or_blank_success(self):
        for text in ('"lastNativeRenderFrame",null','"readbackFrame",frame',
                     '"imageEvidenceAvailable",!(bool)pixels["blankCandidate"]',
                     '"blankCandidate",uniform || nonblack==0', '"configSha256",packageGearConfigHash',
                     '"registeredAssets",packageFitAssets'):
            self.assertIn(text,SOURCE)

if __name__=='__main__':unittest.main()
