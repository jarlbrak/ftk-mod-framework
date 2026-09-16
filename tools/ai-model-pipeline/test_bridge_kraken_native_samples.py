"""Synthetic cross-process bridge rejection tests, not live evidence."""
import copy
import json
import tempfile
import unittest
from pathlib import Path
import numpy as np
from bridge_kraken_native_samples import compare_poses, read_pin, MODERN, OLD


class BridgeTests(unittest.TestCase):
    def setUp(self):
        matrix = np.eye(4).tolist()
        view = dict.fromkeys(('animatorEnabled', 'animatorInitialized', 'avatarValid', 'graphValid'), True)
        view.update(dict.fromkeys(('runtimeControllerAssigned', 'fireEvents', 'applyRootMotion', 'footIK'), False))
        view.update(graphUpdateMode='Manual', playableCount=1, outputCount=1, avatarName='enKrakenHeadAvatar')
        self.request = dict(op='kraken-sample-fixture', method='clip-playable', clip='krakenAttack', id='new', session='new-session', times=[.5])
        self.sample = dict(ok=True, error=None, session='new-session', id='new',
            provenance='isolated_controller_free_manual_clip_playable_native_avatar', samplingMethod='clip-playable',
            coordinateConvention='independent-modern-root-and-local-drivers', appearanceSamplesOnly=False,
            cleanup=dict.fromkeys(('graphsDisposedBeforeTargets', 'modernUnityNull', 'oldUnityNull'), True),
            identity=dict(clipName='krakenAttack', controllerName='krakenHeadController', modernSource='native-enemy:krakenHead',
                oldSource='Resources:enkrakenhead', clipLength=2., modernTransformCount=22, oldTransformCount=24),
            frames=[dict(clip='krakenAttack', time=.5, fullRestResetBeforeSample=True, samplingMethod='clip-playable',
                modernPlayable=copy.deepcopy(view), oldPlayable=copy.deepcopy(view), modernRootAnimated=copy.deepcopy(matrix), oldRootAnimated=copy.deepcopy(matrix),
                driverLocals={p: copy.deepcopy(matrix) for p in MODERN}, oldTargetLocals={p: copy.deepcopy(matrix) for p in OLD})])
        self.historical = copy.deepcopy(self.sample); self.historical['session'] = 'historical'; self.historical['id'] = 'old'

    def check(self, source='a'*64):
        return compare_poses(self.sample, self.historical, self.request, source, 'a'*64)

    def test_match(self):
        self.assertEqual(self.check()['status'], 'cross_process_pose_bridge_match')

    def test_source_hash_rejected(self):
        with self.assertRaises(ValueError): self.check('b'*64)

    def test_exact_time_rejected(self):
        self.historical['frames'][0]['time'] = .501
        with self.assertRaises(ValueError): self.check()

    def test_clip_rejected(self):
        self.historical['identity']['clipName'] = 'krakenIdle'
        with self.assertRaises(ValueError): self.check()

    def test_old_root_difference_detected(self):
        self.sample['frames'][0]['oldRootAnimated'][0][3] = .01
        result = self.check()
        self.assertEqual(result['status'], 'cross_process_pose_bridge_mismatch')
        self.assertTrue(any(not row['pass'] and 'prefab-root-model:Root_M/joint1' in row['name'] for row in result['checks']))

    def test_modern_local_difference_detected(self):
        self.sample['frames'][0]['driverLocals']['Root_M/base'][0][3] = .01
        self.assertEqual(self.check()['status'], 'cross_process_pose_bridge_mismatch')

    def test_nonfinite_rejected(self):
        self.sample['frames'][0]['oldTargetLocals'][next(iter(OLD))][0][0] = float('nan')
        with self.assertRaises(ValueError): self.check()

    def test_file_hash_pin_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'sample.json'; path.write_text(json.dumps(self.sample))
            with self.assertRaises(ValueError): read_pin({'path': str(path), 'sha256': 'f'*64})


if __name__ == '__main__':
    unittest.main()
