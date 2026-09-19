"""Synthetic contract tests, not Unity runtime evidence."""
from local_inputs import require_python_modules
# audit_kraken_adapter imports UnityPy at module level; skip instead of failing to import.
require_python_modules("UnityPy", "numpy", "scipy")
import copy
import unittest
from unittest.mock import patch
import numpy as np
import verify_kraken_adapter_fixture as verifier
from audit_kraken_adapter import MAPPING, JAW, models, adapt


class AdapterReportTests(unittest.TestCase):
    def setUp(self):
        identity = np.eye(4)
        self.old = {'Root_M': identity.copy()}
        self.modern = {'Root_M': identity.copy(), 'Root_M/base': identity.copy()}
        for path in list(MAPPING) + [JAW]:
            value = identity.copy(); value[1, 3] = .2
            self.old[path] = value
        for path in list(MAPPING.values()) + ['Root_M/base/body/neck/eye']:
            value = identity.copy(); value[0, 3] = .3
            self.modern[path] = value
        old_models, modern_models = models(self.old), models(self.modern)
        source = {p: m.tolist() for p, m in self.modern.items() if p != 'Root_M'}
        source['Root_M/base/body'][2][3] = .7
        drivers = dict(self.modern); drivers.update({p: np.array(v) for p, v in source.items()})
        locals_, desired, _ = adapt(self.old, self.modern, identity, drivers, identity)
        self.request = {'op': 'kraken-adapter-fixture', 'method': 'clip-playable', 'clip': 'krakenAttack', 'id': 'case', 'session': 'nonce', 'times': [.5]}
        playable = {'animatorInitialized': True, 'runtimeControllerAssigned': False, 'fireEvents': False}
        frame = {'clip': 'krakenAttack', 'time': .5, 'fullRestResetBeforeSample': True,
                 'modernRootAnimated': identity.tolist(), 'oldRootAnimated': identity.tolist(), 'driverLocals': source,
                 'modernPlayable': playable, 'oldPlayable': playable,
                 'oldAdapter': dict.fromkeys(('ok', 'sharedRootUnchanged', 'targetIdentitiesUnchanged', 'jawRestLocal', 'sourceSnapshotsBeforeWrites'), True)}
        frame['oldAdapter'].update({'modelReadbackMethod': 'actual-local-TRS-chain-excluding-owned-top', 'repeatMaximumLocalError': 0, 'maximumModelReadbackError': 0,
                                   'targetLocals': {p: m.tolist() for p, m in locals_.items()}, 'targetModels': {p: m.tolist() for p, m in desired.items()}})
        self.sample = {'id': 'case', 'session': 'nonce', 'ok': True, 'error': None,
                       'provenance': 'isolated_old_kraken_adapter_single_clip_diagnostic', 'sameReadyAfter': True, 'pinnedReady': {'session': 'nonce'},
                       'cleanup': dict.fromkeys(('graphsDisposedBeforeTargets', 'modernUnityNull', 'oldUnityNull'), True),
                       'identity': {'controllerName': 'krakenHeadController', 'controllerInstanceId': 1, 'clipName': 'krakenAttack', 'clipInstanceId': 2,
                                    'clipLength': 3, 'modernPrefabInstanceId': 4, 'oldPrefabInstanceId': 5},
                       'adapterChecks': dict.fromkeys(('injectedCommitRollback', 'singularShearNonfiniteRejected', 'exactSavedLocalsRestored', 'nonfiniteReadbackRejected'), True),
                       'adapterRest': {'capturedBeforeAnimatorInitialization': True, 'modernRestModels': {p: m.tolist() for p, m in modern_models.items()},
                                       'oldRestModels': {p: m.tolist() for p, m in old_models.items()}, 'jawRestLocal': self.old[JAW].tolist()}, 'frames': [frame]}
        self.reference = copy.deepcopy(self.sample)
        self.reference['provenance'] = 'isolated_controller_free_manual_clip_playable_native_avatar'

    def run_check(self):
        with patch.object(verifier, 'extract', return_value=((self.old, list(MAPPING) + [JAW], None), (self.modern, [], None))):
            return verifier.compare(None, self.sample, self.request, self.reference)

    def test_independent_algebra_matches(self):
        self.assertEqual(self.run_check()['status'], 'owned_adapter_mechanics_match')

    def test_wrong_parent_output_detected(self):
        self.sample['frames'][0]['oldAdapter']['targetLocals'][next(iter(MAPPING))][0][3] += .5
        self.assertEqual(self.run_check()['status'], 'owned_adapter_mechanics_mismatch')

    def test_wrong_reference_pose_detected(self):
        self.reference['frames'][0]['driverLocals']['Root_M/base'][0][3] += .1
        self.assertEqual(self.run_check()['status'], 'owned_adapter_mechanics_mismatch')

    def test_fail_closed_metadata(self):
        original = copy.deepcopy(self.sample)
        mutations = [('ok', False), ('sameReadyAfter', False), ('session', 'other'), ('provenance', 'other')]
        for key, value in mutations:
            self.sample = copy.deepcopy(original); self.sample[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError): self.run_check()

    def test_missing_nonfinite_check_rejected(self):
        del self.sample['adapterChecks']['nonfiniteReadbackRejected']
        with self.assertRaises(ValueError): self.run_check()

    def test_cleanup_rejected(self):
        self.sample['cleanup']['oldUnityNull'] = False
        with self.assertRaises(ValueError): self.run_check()

    def test_appearance_rejected(self):
        self.request['clip'] = 'kraken_appear'
        with self.assertRaises(ValueError): self.run_check()

    def test_different_clock_rejected(self):
        self.request['times'] = [.51]
        with self.assertRaises(ValueError): self.run_check()

    def test_nonfinite_output_rejected(self):
        self.sample['frames'][0]['oldAdapter']['targetModels'][JAW][0][0] = float('nan')
        with self.assertRaises(ValueError): self.run_check()


if __name__ == '__main__':
    unittest.main()
