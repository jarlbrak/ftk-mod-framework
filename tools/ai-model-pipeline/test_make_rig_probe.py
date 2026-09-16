"""Multipart joint selection must not bridge unweighted native palette joints."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import numpy as np
from make_rig_probe import make_probe, selected_joint_indices, checked_marker_radius


class JointScopeTests(unittest.TestCase):
    def reference(self, indices, weights=None):
        return dict(joints=np.asarray(indices,dtype=float),
                    weights=np.asarray(weights if weights is not None else [[1,0,0,0]]*len(indices),dtype=float))

    def test_all_preserves_palette_without_reading_surface_usage(self):
        self.assertEqual(selected_joint_indices({},4,'all'),{0,1,2,3})

    def test_weighted_ignores_zero_slots_but_includes_tiny_positive_weights(self):
        ref=self.reference([[1,3,999,float('nan')]],[[1,1e-30,0,0]])
        self.assertEqual(selected_joint_indices(ref,4,'weighted'),{1,3})

    def test_invalid_used_indices_fail_closed(self):
        for bad in [-1,4,.5,float('nan'),float('inf')]:
            with self.subTest(index=bad),self.assertRaises(ValueError):
                selected_joint_indices(self.reference([[bad,0,0,0]]),4,'weighted')

    def test_invalid_weights_and_empty_usage_fail_closed(self):
        for bad in [-1,float('nan'),float('inf'),0]:
            with self.subTest(weight=bad),self.assertRaises(ValueError):
                selected_joint_indices(self.reference([[0,0,0,0]],[[bad,0,0,0]]),4,'weighted')
        with self.assertRaises(ValueError):
            selected_joint_indices(self.reference([[0,0]]),4,'weighted')
        with self.assertRaises(ValueError):
            selected_joint_indices({},4,'unknown')

    def test_only_direct_selected_edges_and_full_bind_palette(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);names=['root','unused-middle','leaf','leaf-child']
            binds=np.repeat(np.eye(4)[None,:,:],4,axis=0);binds[:,0,3]=-np.arange(4)
            reference=self.reference([[0,2,3,999]],[[.2,.3,.5,0]])
            np.savez(root/'ref.npz',bone_names=names,bindposes=binds,**reference)
            (root/'skeleton.json').write_text(json.dumps(dict(bone_names=names,bone_parents=[-1,0,1,2])))
            observed={}
            def writer(path,data,ref):
                observed.update(data);self.assertEqual(data['bone_names'],names)
                np.testing.assert_array_equal(ref['bindposes'],binds)
                path.write_bytes(b'writer fixture')
            with patch('make_rig_probe.write_glb',writer),patch('make_rig_probe.validate',return_value={'fixture':True}):
                result=make_probe(root/'ref.npz',root/'skeleton.json',root/'out',joint_scope='weighted')
            self.assertEqual(result['selected_joint_indices'],[0,2,3])
            self.assertEqual(result['joint_marker_count'],3)
            self.assertEqual(result['omitted_joint_count'],1)
            self.assertEqual(result['segment_count'],1)  # 2->3 only; never skip missing1 to connect0->2
            self.assertEqual(set(np.asarray(observed['joints'])[:,0]),{0,2,3})
            points=np.asarray(observed['positions']);tris=np.asarray(observed['triangles'])
            cross=np.cross(points[tris[:,1]]-points[tris[:,0]],points[tris[:,2]]-points[tris[:,0]])
            self.assertTrue((np.linalg.norm(cross,axis=1)>0).all())
            normals=np.asarray(observed['normals']);self.assertTrue(np.isfinite(normals).all())
            np.testing.assert_allclose(np.linalg.norm(normals,axis=1),1)

    def test_markers_only_preserves_selected_joints_without_connecting_faces(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);binds=np.repeat(np.eye(4)[None,:,:],2,axis=0)
            binds[1,0,3]=-1
            np.savez(root/'ref.npz',bone_names=['parent','child'],bindposes=binds,
                     **self.reference([[0,1,0,0]],[[.032258,.967742,0,0]]))
            (root/'skeleton.json').write_text(json.dumps(dict(bone_names=['parent','child'],bone_parents=[-1,0])))
            def writer(path,data,ref):
                self.assertEqual(len(data['triangles']),16)
                for tri in data['triangles']:
                    self.assertEqual(len({data['joints'][v][0] for v in tri}),1)
                path.write_bytes(b'fixture')
            with patch('make_rig_probe.write_glb',writer),patch('make_rig_probe.validate',return_value={}):
                result=make_probe(root/'ref.npz',root/'skeleton.json',root/'out',joint_scope='weighted',connections='none',radius_scale=6,marker_radius=.2)
            self.assertEqual(result['selected_joint_indices'],[0,1])
            self.assertEqual(result['segment_count'],0)
            self.assertEqual(result['connections'],'none')
            self.assertEqual(result['marker_radius'],.2)
            self.assertEqual(result['marker_radius_override'],.2)
            with self.assertRaises(ValueError):
                make_probe(root/'ref.npz',root/'skeleton.json',root/'bad',connections='unknown')

    def test_explicit_radius_requires_finite_positive_without_arbitrary_upper_limit(self):
        for bad in [0,-1,float('nan'),float('inf')]:
            with self.subTest(radius=bad),self.assertRaises(ValueError):
                checked_marker_radius(bad)
        self.assertEqual(checked_marker_radius(1234),1234)


if __name__=='__main__':
    unittest.main()
