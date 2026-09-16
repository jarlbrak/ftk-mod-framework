"""Static authority-boundary checks; not Unity readback or image quality tests."""
from pathlib import Path
import re
import unittest

SOURCE=Path(__file__).with_name('PortraitTextureCapture.cs').read_text()
# Ignore prose strings/comments so tests constrain executed C# API tokens.
CODE=re.sub(r'//[^\n]*|"(?:\\.|[^"\\])*"','',SOURCE)


class PortraitReadOnlyBoundaryTests(unittest.TestCase):
    def test_no_native_render_or_object_texture_mutation_api(self):
        forbidden=('Snapshot','DoRender','Render','SetActive','Instantiate','Destroy','DestroyImmediate',
                   'ReadPixels','SetPixels','SetPixel','Apply','Blit','GetTemporary','SetTexture','SetFloat',
                   'SetColor','GetModifiedMaterial','SetMaterial','SetPropertyBlock')
        for name in forbidden:self.assertNotRegex(CODE,r'\b'+name+r'\s*\(')
        self.assertNotRegex(CODE,r'\.materialForRendering\b')
        self.assertNotRegex(CODE,r'new\s+(?:Texture2D|RenderTexture|Material|GameObject)\b')

    def test_native_identity_rechecked_after_single_cpu_encode_before_file_write(self):
        self.assertEqual(len(re.findall(r'\.EncodeToPNG\s*\(',CODE)),1)
        after=CODE.split('.EncodeToPNG()',1)[1]
        before_write=after.split('new FileStream',1)[0]
        for token in ('EncounterSession.Instance!=encounter','uiActiveTime.Instance!=timeline','currentOwners!=1',
                      'dummy.m_EnemyCombat,row','dummy.FID','TryGetValue(dummy.FID,out after)','after!=texture'):
            self.assertIn(token,before_write)
        self.assertIn('FileMode.CreateNew',after)
        self.assertLess(after.index('new FileStream'),after.index('File.Move'))

    def test_no_persistent_unity_reference_added(self):
        # Export-only code must not add a lease, static Unity object cache, or coroutine lifetime.
        self.assertNotRegex(CODE,r'\b(?:static|readonly)\s+(?:Texture2D|Texture|Material|EnemyDummy|RawImage|List<RawImage>)\s+\w+\s*[;=]')
        self.assertNotRegex(CODE,r'\b(?:StartCoroutine|EnsureRetained|SetResources)\s*\(')
        self.assertNotRegex(CODE,r'\byield\b')


if __name__=='__main__':unittest.main()
