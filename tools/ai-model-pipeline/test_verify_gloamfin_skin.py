"""Actual original asset plus independent weighted-point/negative checks; no Unity calls."""
from local_inputs import require_local_inputs, require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy", "PIL")
import copy,json,tempfile,unittest
from pathlib import Path
import numpy as np
import test_verify_kraken_skin_probe as original_tests
from verify_kraken_skin_probe import original_glb,validate_organic,variant_contract,skin_vertices,inspect_capture,inspect_run,GLOAMFIN_HASH,GLOAMFIN_FILE,TOL,digest

OLD_PROBE='scratch/kraken-owned-skin-probe-v1/kraken-owned-skin-probe-v1.glb'

class GloamfinTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root=Path(__file__).resolve().parents[2]/'art-experiments/gloamfin-kraken'
        cls.asset=json.loads((cls.root/GLOAMFIN_FILE).read_text())
        cls.geometry=original_glb(cls.root/'gloamfin.glb',cls.asset)
    def builder(self):
        b=original_tests.OriginalSkinTests();b.geometry=self.geometry;b.assetOverride=self.asset;return b
    def args(self,d):
        args=self.builder().run_fixture(d);report,request,arm,result,asset,geometry,pins=args
        arm.update(variant='gloamfin-v1',manifestSha256=GLOAMFIN_HASH)
        result.update(variant='gloamfin-v1',manifestSha256=GLOAMFIN_HASH)
        report['originalSkinProbe']['identity'].update(variant='gloamfin-v1',manifestFile=GLOAMFIN_FILE,manifestSha256=GLOAMFIN_HASH,vertexCount=5640,indexCount=5640,weightProof=copy.deepcopy(asset['weightProof']),shader='Unlit/Texture',meshName='ftkmf_glb_gloamfin.glb')
        return args
    def test_actual_pinned_geometry_and_immutable_old_binds(self):
        self.assertEqual(digest(self.root/GLOAMFIN_FILE),GLOAMFIN_HASH)
        self.assertEqual(digest(self.root/'gloamfin.glb'),self.asset['glb']['sha256'])
        self.assertEqual(len(self.geometry[0]),5640)
        # The committed asset checks above always run; only the old-bind comparison needs the local probe.
        require_local_inputs(OLD_PROBE)
        old=original_glb(Path(__file__).resolve().parents[2]/OLD_PROBE)
        np.testing.assert_array_equal(old[3],self.geometry[3])
    def test_soft_vertex_obeys_two_independently_translated_endpoints(self):
        pos,j,w,b=self.geometry;i=int(np.flatnonzero(np.sum(w>0,axis=1)==2)[0]);rest=np.linalg.inv(b.astype(float));moved=rest.copy()
        first,second=j[i,:2].astype(int);moved[first,0,3]+=.7;moved[second,1,3]-=.4
        before=skin_vertices(pos,j,w,b,rest,np.eye(4));after=skin_vertices(pos,j,w,b,moved,np.eye(4))
        np.testing.assert_allclose(after[i]-before[i],[.7*float(w[i,0]),-.4*float(w[i,1]),0],atol=1e-12)
    def test_full_organic_run_scope_and_camera_far60(self):
        with tempfile.TemporaryDirectory() as d:
            result=inspect_run(*self.args(d));self.assertLess(result['maximumBakeError'],1e-12)
            self.assertIn('Overlapping',result['movementScope'])
    def test_unknown_variant_and_overbound_counts_rejected(self):
        for key,value in [('variant','unknown'),('vertexCount',8193),('indexCount',49155)]:
            asset=copy.deepcopy(self.asset);asset[key]=value
            with self.assertRaises(ValueError):variant_contract(asset)
    def test_variant_provenance_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as d:
            args=self.args(d);args[3]['variant']='wrong'
            with self.assertRaises(ValueError):inspect_run(*args)
    def test_soft_weight_and_missing_bone_proofs_rejected(self):
        for key in ['softVertexCount','positiveVertexCountByBone']:
            asset=copy.deepcopy(self.asset)
            if key=='softVertexCount':asset['weightProof'][key]-=1
            else:asset['weightProof'][key][4]=0
            pos,j,w,b=self.geometry
            with self.assertRaises(ValueError):validate_organic(pos,j,w,np.arange(len(pos)),b,asset)
    def test_nonfinite_negative_unnormalized_duplicate_weights_rejected(self):
        pos,j,w,b=self.geometry
        for kind in ['nan','negative','sum','duplicate']:
            jj=j.copy();ww=w.copy()
            if kind=='nan':ww[0,0]=np.nan
            elif kind=='negative':ww[0,0]=-1
            elif kind=='sum':ww[0,0]+=.1
            else:ww[0]=[.5,.5,0,0];jj[0,1]=jj[0,0]
            with self.assertRaises(ValueError):validate_organic(pos,jj,ww,np.arange(len(pos)),b,self.asset)
    def test_last_vertex_of_full_array_is_checked(self):
        r,f=self.builder().fixture();r['bakedVerticesRendererLocal'][-1][0]+=.002
        baked=np.array(r['bakedVerticesRendererLocal']);r['bakedBounds']={'center':((baked.min(0)+baked.max(0))/2).tolist(),'size':np.ptp(baked,axis=0).tolist()}
        result,_=inspect_capture(r,f,self.geometry);self.assertGreater(result['maximumBakeError'],TOL)
    def test_changed_inverse_bind_and_outside_indices_rejected(self):
        p,j,w,b=self.geometry;bb=b.copy();bb[4,0,3]+=.001
        with self.assertRaises(ValueError):validate_organic(p,j,w,np.arange(len(p)),bb,self.asset)
        ix=np.arange(len(p));ix[-1]=len(p)
        with self.assertRaises(ValueError):validate_organic(p,j,w,ix,b,self.asset)
if __name__=='__main__':unittest.main()
