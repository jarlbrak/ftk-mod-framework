import copy
import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('fixture_builder', Path(__file__).with_name('build.py'))
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class FixtureBoundaries(unittest.TestCase):
    def test_only_inventory_and_explicit_non_guardian_class_change(self):
        original = json.loads((builder.SOURCE / 'content.json').read_text())
        before = copy.deepcopy(original)
        actual, gear, old = builder.fixture_content(original)
        self.assertEqual(original, before)
        self.assertEqual(len(gear), 36)
        self.assertEqual(len(actual['entries']), 40)
        self.assertNotIn('playerModels', next(row for row in actual['entries'] if row['id'] == 'paladin'))
        expected = copy.deepcopy(original)
        next(row for row in expected['entries'] if row['id'] == 'paladin')['fields']['startitems'] = gear
        self.assertEqual(actual['entries'][:-1], expected['entries'])
        hunter = actual['entries'][-1]
        self.assertEqual(hunter['template'], 'hunter')
        self.assertFalse(hunter['guardian'])
        self.assertEqual(hunter['fields']['startitems'], gear)
        self.assertNotIn('playerModels', hunter)
        self.assertNotIn('icon', hunter)

    def test_output_refuses_source_or_existing_directory(self):
        for target in (builder.SOURCE, builder.ROOT / 'scratch'):
            with self.assertRaises(ValueError):
                builder.build(target)


if __name__ == '__main__':
    unittest.main()
