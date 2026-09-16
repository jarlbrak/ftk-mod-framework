"""Independent authored-policy algebra tests, not a Unity or native-blend test."""
import unittest
import numpy as np
from scipy.spatial.transform import Rotation, Slerp

TOL = 1e-5


def trs(p, q, s):
    result = np.eye(4)
    result[:3, :3] = Rotation.from_quat(q).as_matrix() @ np.diag(s)
    result[:3, 3] = p
    return result


def decompose(m):
    if not np.isfinite(m).all():
        raise ValueError('nonfinite')
    s = np.linalg.norm(m[:3, :3], axis=0)
    if min(s) < 1e-8 or np.linalg.det(m[:3, :3]) <= 0:
        raise ValueError('singular/reflected')
    r = m[:3, :3] / s
    if np.max(np.abs(r.T @ r - np.eye(3))) > TOL:
        raise ValueError('shear')
    q = Rotation.from_matrix(r).as_quat()
    if np.max(np.abs(trs(m[:3, 3], q, s) - m)) > TOL:
        raise ValueError('reconstruction')
    return m[:3, 3], q, s


def locals_for(models, root):
    return [decompose(np.linalg.solve(root if i == 0 else models[i-1], m)) for i, m in enumerate(models)]


def blend(a, b, weight):
    if not np.isfinite(weight) or not 0 <= weight <= 1:
        raise ValueError('weight')
    result = []
    for (ap, aq, az), (bp, bq, bz) in zip(a, b):
        if np.dot(aq, bq) < 0:
            bq = -bq
        q = Slerp([0, 1], Rotation.from_quat([aq, bq]))([weight]).as_quat()[0]
        result.append(trs(ap * (1-weight) + bp * weight, q, az * (1-weight) + bz * weight))
    return result


def models_for(locals_, root):
    result = []
    for local in locals_:
        root = root @ local
        result.append(root)
    return result


class EndpointPolicyTests(unittest.TestCase):
    def setUp(self):
        self.root = trs([0.4, -0.2, 1.7], Rotation.from_euler('xyz', [0.1, 0.3, -0.5]).as_quat(), [1, 1, 1])
        self.a = models_for([trs([0, i*.2, .1], Rotation.from_euler('y', i*.1).as_quat(), [1, 1, 1]) for i in range(4)], self.root)
        self.b = models_for([trs([.1, i*.1, .3], Rotation.from_euler('z', i*.2).as_quat(), [1, 1, 1]) for i in range(4)], self.root)

    def test_endpoints_recover_models_under_actual_root(self):
        a, b = locals_for(self.a, self.root), locals_for(self.b, self.root)
        for weight, expected in [(0, self.a), (1, self.b)]:
            np.testing.assert_allclose(models_for(blend(a, b, weight), self.root), expected, atol=1e-12)

    def test_shortest_arc_quaternion_sign_invariance(self):
        a, b = locals_for(self.a, self.root), locals_for(self.b, self.root)
        sign_flipped = [(p, -q, s) for p, q, s in b]
        np.testing.assert_allclose(blend(a, b, .37), blend(a, sign_flipped, .37), atol=1e-12)

    def test_repeat_does_not_feed_output_into_endpoints(self):
        a, b = locals_for(self.a, self.root), locals_for(self.b, self.root)
        first = blend(a, b, .37)
        for _ in range(5):
            np.testing.assert_array_equal(first, blend(a, b, .37))

    def test_bad_trs_rejected(self):
        for kind in ['nan', 'inf', 'zero', 'reflection', 'shear']:
            with self.subTest(kind=kind):
                m = np.eye(4)
                if kind == 'nan': m[0, 3] = np.nan
                if kind == 'inf': m[1, 1] = np.inf
                if kind == 'zero': m[1, 1] = 0
                if kind == 'reflection': m[1, 1] = -1
                if kind == 'shear': m[0, 1] = .01
                with self.assertRaises(ValueError): decompose(m)

    def test_bad_weights_rejected(self):
        a = locals_for(self.a, self.root)
        for weight in [-.01, 1.01, np.nan, np.inf]:
            with self.assertRaises(ValueError): blend(a, a, weight)

    def test_wrong_parent_endpoint_is_detectable(self):
        bad = [decompose(np.linalg.solve(self.root, m)) for m in self.b]
        actual = models_for([trs(*v) for v in bad], self.root)
        self.assertGreater(np.max(np.abs(np.array(actual)-self.b)), .01)


if __name__ == '__main__':
    unittest.main()
