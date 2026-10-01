"""Focused retained-authoring-root checks; no game or package assets required."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from validate_paladin import validate_adopted_sources


def digest(data):
    return hashlib.sha256(data).hexdigest()


class SourceRootTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=Path(tempfile.gettempdir()).resolve())
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.package = root / 'clean/marketplace/packages/paladin'
        self.package.mkdir(parents=True)
        (self.package / 'content.json').write_text(json.dumps({'entries': []}))
        self.sources = root / 'retained'
        self.sources.mkdir()
        stem = 'art-experiments/paladin-polish/fixture'
        self.output = stem + '/output.glb'
        self.recipe = stem + '/build.py'
        self.inputs = stem + '/input.png'
        self.manifest = stem + '/inputs.json'
        files = {self.output: b'authored output', self.recipe: b'authored recipe',
                 self.inputs: b'authored input'}
        for name, data in files.items():
            path = self.sources / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        pins = {name: digest(data) for name, data in files.items()}
        closure = {'schema': 'ftkmf.paladin.adoption-inputs.v1',
                   'output': {'path': self.output, 'sha256': pins[self.output]},
                   'recipe': {'path': self.recipe, 'sha256': pins[self.recipe]},
                   'inputs': {self.inputs: pins[self.inputs]}}
        manifest_bytes = json.dumps(closure).encode()
        (self.sources / self.manifest).write_bytes(manifest_bytes)
        pins[self.manifest] = digest(manifest_bytes)
        self.receipt = {'helmetHairPolicies': {},
                        'sourceStatus': 'unreleased-presentation-candidate',
                        'files': {'assets/output.glb': {
                            'source': self.output, 'sha256': pins[self.output],
                            'recipe': self.recipe, 'inputsManifest': self.manifest,
                            'adoption': {'originalContentOnly': True,
                                         'sourceKind': 'original-authored',
                                         'reproductionStatus': 'reproduced',
                                         'durableFiles': pins}}}}

    def test_separate_root_verifies_all_pins(self):
        validate_adopted_sources(self.package, self.receipt, self.sources)
        self.assertFalse((self.package.parents[2] / self.output).exists())

    def test_changed_source_hash_rejected(self):
        (self.sources / self.inputs).write_bytes(b'changed')
        with self.assertRaises(AssertionError):
            validate_adopted_sources(self.package, self.receipt, self.sources)

    def test_path_escape_rejected(self):
        pins = self.receipt['files']['assets/output.glb']['adoption']['durableFiles']
        pins['art-experiments/paladin-polish/../escape'] = digest(b'escape')
        with self.assertRaises(AssertionError):
            validate_adopted_sources(self.package, self.receipt, self.sources)

    def test_symlink_source_rejected(self):
        path = self.sources / self.inputs
        target = self.sources / 'target.png'
        target.write_bytes(path.read_bytes())
        path.unlink()
        path.symlink_to(target)
        with self.assertRaises(AssertionError):
            validate_adopted_sources(self.package, self.receipt, self.sources)

    def test_symlink_root_rejected(self):
        link = self.sources.parent / 'retained-link'
        link.symlink_to(self.sources, target_is_directory=True)
        with self.assertRaises(AssertionError):
            validate_adopted_sources(self.package, self.receipt, link)


if __name__ == '__main__':
    unittest.main()
