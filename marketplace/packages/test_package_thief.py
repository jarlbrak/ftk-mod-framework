"""Game-free archive boundary and platform-claim regression checks."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('package_thief', Path(__file__).with_name('package_thief.py'))
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class PackageThiefTests(unittest.TestCase):
    def fixture(self, root):
        package = root / 'package'
        (package / 'assets').mkdir(parents=True)
        (package / 'promo').mkdir()
        manifest = {'modGuid': 'com.ftkmf.thief', 'name': 'Thief', 'author': 'Fixture',
                    'description': 'Fixture', 'version': '1.1.0', 'frameworkVersion': '1.6.3'}
        (package / 'manifest.json').write_text(json.dumps(manifest))
        (package / 'content.json').write_text(json.dumps({'entries': [{'model': 'assets/current.glb'}]}))
        (package / 'listing.json').write_text(json.dumps({'packageId': 'ftkmf.thief',
                                                        'platforms': ['windows', 'macos', 'linux']}))
        (package / 'assets/current.glb').write_bytes(b'fixture model')
        (package / 'assets/unused.glb').write_bytes(b'legacy must not ship')
        (package / 'promo/thief-playtest-banner.png').write_bytes(b'\x89PNG\r\n\x1a\n' + b'\0'*8 + struct.pack('>II', 1, 1))
        game = root / 'Assembly-CSharp.dll'
        game.write_bytes(b'fixture fingerprint')
        return package, game

    def run_builder(self, root, package, game, release=False):
        output = root / 'scratch/output'
        args = ['package_thief', '--output', str(output), '--game-assembly', str(game), '--platform', 'macos']
        if release:
            args.append('--release')
        with patch.object(builder, 'ROOT', root), patch.object(builder, 'PACKAGE', package), \
                patch.object(builder.subprocess, 'run') as validate, patch('sys.argv', args), \
                contextlib.redirect_stdout(io.StringIO()):
            builder.main()
            validate.assert_called_once()
        return output

    def test_release_and_draft_never_inherit_overbroad_platforms(self):
        for release in (False, True):
            with self.subTest(release=release), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp).resolve()
                package, game = self.fixture(root)
                output = self.run_builder(root, package, game, release)
                descriptor = json.loads(next(output.glob('*.descriptor.json')).read_text())
                self.assertEqual(descriptor['platforms'], ['macos'])
                self.assertEqual(descriptor['frameworkVersion'], '1.6.3')
                self.assertEqual(descriptor['gameFingerprints'], [builder.digest(game.read_bytes())])
                archive = next(output.glob('*.zip'))
                with zipfile.ZipFile(archive) as z:
                    self.assertEqual(set(z.namelist()), {'manifest.json', 'content.json', 'assets/current.glb'})
                original = archive.read_bytes()
                self.run_builder(root, package, game, release)
                self.assertEqual(archive.read_bytes(), original)

    def test_asset_escape_and_symlink_rejected_before_archive(self):
        for mode in ('escape', 'symlink'):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp).resolve()
                package, game = self.fixture(root)
                if mode == 'escape':
                    (package / 'content.json').write_text(json.dumps({'model': '../outside.glb'}))
                else:
                    target = package / 'assets/current.glb'
                    target.unlink()
                    target.symlink_to(game)
                with self.assertRaises(ValueError):
                    self.run_builder(root, package, game, True)
                self.assertFalse(list((root / 'scratch/output').glob('*.zip')))


if __name__ == '__main__':
    unittest.main()
