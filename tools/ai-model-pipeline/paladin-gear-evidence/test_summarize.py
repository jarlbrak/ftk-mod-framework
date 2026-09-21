import hashlib
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('summarizer', Path(__file__).with_name('summarize.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class IdentityTests(unittest.TestCase):
    def test_identity_binds_package_path_and_bytes(self):
        actual = module.package_identity('mod', 'assets/body.glb', 'ab')
        self.assertEqual(actual, hashlib.sha256(b'mod\nassets/body.glb\nab').hexdigest())
        for args in [('other','assets/body.glb','ab'),('mod','assets/other.glb','ab'),('mod','assets/body.glb','cd')]:
            self.assertNotEqual(actual, module.package_identity(*args))

    def test_current_production_models_have_unambiguous_keys(self):
        table = module.assets()
        files = list((module.PACKAGE / 'assets').glob('*.glb'))
        self.assertEqual(len(table), len(files))
        # Packaged optional art may be unused; every resolved mapping still pins bytes.
        self.assertTrue(all(len(row['sha256']) == 64 for row in table.values()))
        self.assertTrue(any(row['assignments'] for row in table.values()))


if __name__ == '__main__': unittest.main()
