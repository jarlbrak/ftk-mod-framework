"""Presentation-coordinate algebra and source-boundary checks; not Unity execution."""
import unittest
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent
class PresentationTranslationTests(unittest.TestCase):
    def test_shared_translation_preserves_skinning_and_camera_projection(self):
        top=np.eye(4);top[:3,3]=[100.800003,0,132.820007]
        parent=np.eye(4);parent[:3,3]=-top[:3,3]
        renderer_local=np.eye(4);renderer_local[:3,3]=[.13,.2,-.4]
        bone_local=np.eye(4);bone_local[:3,:3]=[[0,-1,0],[1,0,0],[0,0,1]];bone_local[:3,3]=[.7,1.3,-2.1]
        ibm=np.eye(4);ibm[:3,3]=[-.2,.1,.8]
        camera_local=np.eye(4);camera_local[:3,3]=[19.934626,-.951947,-3.142890]
        point=np.array([.2,.3,.4,1])
        world=top@bone_local@ibm@point
        moved=parent@top@bone_local@ibm@point
        np.testing.assert_allclose(np.linalg.inv(top@renderer_local)@world,np.linalg.inv(parent@top@renderer_local)@moved,atol=1e-12)
        np.testing.assert_allclose(np.linalg.inv(top@camera_local)@world,np.linalg.inv(parent@top@camera_local)@moved,atol=1e-12)
        np.testing.assert_array_equal((parent@top)[:3,3],np.zeros(3))
        np.testing.assert_array_equal(top[:3,3],[100.800003,0,132.820007])
    def test_local_model_products_exclude_presentation_parent(self):
        local=np.eye(4);local[:3,3]=[.7,-.2,3]
        top=np.eye(4);top[:3,3]=[100.8,0,132.82]
        parent=np.eye(4);parent[:3,3]=-top[:3,3]
        np.testing.assert_allclose(np.linalg.inv(parent@top)@(parent@top@local),local,atol=1e-12)
    def test_owned_parent_lifetime_and_local_preservation_guards(self):
        source=(ROOT/'runtime-test/KrakenEndpointFixture.cs').read_text()
        start=source.index('public void EnableOriginPresentation');end=source.index('public JObject Presentation()',start)
        method=source[start:end]
        self.assertIn('output.transform.SetParent(presentation.transform,false)',method)
        self.assertIn('Vector3 translation=-output.transform.localPosition',method)
        self.assertIn('presentation preserves output top LOCAL',method)
        self.assertIn('Presentation changed endpoint locals/models',method)
        self.assertNotIn('modern.',method);self.assertNotIn('sampler.',method)
        self.assertIn('if(presentation!=null)UnityEngine.Object.Destroy(presentation)',source)
        self.assertIn('return output==null && PresentationClean()',source)
    def test_camera_and_renderer_share_shifted_output_only(self):
        source=(ROOT/'runtime-test/KrakenSkinProbe.cs').read_text()
        self.assertLess(source.index('plan.EnableOriginPresentation(id)'),source.index('camera.transform.position=plan.output.transform.TransformPoint(position)'))
        self.assertIn('Decompose(rendererSource.localToWorldMatrix).Set(rendererObject.transform)',source)
        self.assertIn('endpoint.Presentation()',source)
        dispose=source[source.index('public void Dispose()'):source.index('public JObject Cleanup()')]
        self.assertNotIn('Destroy(endpoint',dispose);self.assertNotIn('Destroy(plan',dispose)
if __name__=='__main__':unittest.main()
