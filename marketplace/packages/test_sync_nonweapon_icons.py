"""Guard the fixed nonweapon-only icon adoption contract without canonical writes."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import sync_nonweapon_icons_v3 as icons

MANIFEST = icons.ROOT/'scratch/gear-consolidation/thief-coat-fit/nonweapon-icons-v2/manifest.json'


class NonweaponIconAdoptionTests(unittest.TestCase):
    def setUp(self):
        self.manifest = json.loads(MANIFEST.read_text())
        self.digest = icons.MANIFEST_SHA
        self.ledger = icons.LEDGER
        self.temp = tempfile.TemporaryDirectory(prefix='icon-guards-',dir=icons.ROOT/'scratch')
        self.addCleanup(self.temp.cleanup)
        self.addCleanup(setattr,icons,'MANIFEST_SHA',self.digest)
        self.addCleanup(setattr,icons,'LEDGER',self.ledger)

    def reject_manifest(self, mutate, allow_new_digest=True):
        value = copy.deepcopy(self.manifest)
        mutate(value)
        path = Path(self.temp.name)/'manifest.json'
        path.write_text(json.dumps(value))
        if allow_new_digest: icons.MANIFEST_SHA = icons.digest(path)
        with self.assertRaises(AssertionError): icons.generate(path)

    def test_exact_delivery_reproduces_ledger_and_pngs_without_writes(self):
        ledger,copies = icons.generate(MANIFEST)
        self.assertEqual(ledger,json.loads(icons.LEDGER.read_text()))
        self.assertEqual(len(copies),26)
        for source,destination in copies: self.assertEqual(source.read_bytes(),destination.read_bytes())

    def test_changed_manifest_pin_rejected(self):
        self.reject_manifest(lambda m:m.update(scope='changed'),False)

    def test_weapon_substitution_rejected(self):
        self.reject_manifest(lambda m:m['items'][0].update(itemId='thief_twins_street'))

    def test_duplicate_item_rejected(self):
        self.reject_manifest(lambda m:m['items'].__setitem__(1,copy.deepcopy(m['items'][0])))

    def test_destination_change_rejected(self):
        self.reject_manifest(lambda m:m['items'][0].update(canonicalIconPath='marketplace/packages/thief/assets/thief-street-twins-icon.png'))

    def test_modified_model_pin_rejected(self):
        self.reject_manifest(lambda m:m['items'][0]['sourceModelAndTexturePins'][0].update(sha256='0'*64))

    def test_render_recipe_source_change_rejected(self):
        self.reject_manifest(lambda m:m['generator'].update(sha256='0'*64))

    def test_current_validator_rejects_stale_png_hash(self):
        ledger=json.loads(icons.LEDGER.read_text());next(r for r in ledger['items'] if r['package']=='thief')['sha256']='0'*64
        path=Path(self.temp.name)/'ledger.json';path.write_text(json.dumps(ledger));icons.LEDGER=path
        entries=json.loads((icons.HERE/'thief/content.json').read_text())['entries']
        with self.assertRaises(AssertionError): icons.validate_current(ledger,'thief',entries)

    def test_current_validator_rejects_changed_display_source(self):
        ledger=json.loads(icons.LEDGER.read_text());next(r for r in ledger['items'] if r['package']=='thief')['inputs'][0]['sha256']='0'*64
        path=Path(self.temp.name)/'ledger.json';path.write_text(json.dumps(ledger));icons.LEDGER=path
        entries=json.loads((icons.HERE/'thief/content.json').read_text())['entries']
        with self.assertRaises(AssertionError): icons.validate_current(ledger,'thief',entries)


if __name__=='__main__':unittest.main()
