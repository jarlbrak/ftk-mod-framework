"""Exercise exact display-only preflight and historical lineage without canonical writes."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import sync_display_framing as framing

MANIFEST=framing.ROOT/'scratch/gear-consolidation/thief-coat-fit/display-framing-family-v1/manifest.json'

class DisplayFramingTests(unittest.TestCase):
    def setUp(self):
        self.manifest=json.loads(MANIFEST.read_text())
        self.pin=framing.MANIFEST_SHA
        self.temp=tempfile.TemporaryDirectory(prefix='display-guards-',dir=framing.ROOT/'scratch')
        self.addCleanup(self.temp.cleanup)
        self.addCleanup(setattr,framing,'MANIFEST_SHA',self.pin)

    def reject(self, mutate, repin=True):
        data=copy.deepcopy(self.manifest);mutate(data)
        path=Path(self.temp.name)/'manifest.json';path.write_text(json.dumps(data))
        if repin: framing.MANIFEST_SHA=framing.digest(path)
        with self.assertRaises((AssertionError,ValueError,KeyError)):
            framing.generate(path)

    def test_exact_delivery_only_changes_six_model_routes(self):
        ledger,copies,contents=framing.generate(MANIFEST)
        self.assertEqual(len(copies),26)
        self.assertEqual(len(ledger['items']),26)
        changed=[]
        for package,new in contents.items():
            old=json.loads((framing.HERE/package/'content.json').read_text())
            for before,after in zip(old['entries'],new['entries']):
                if before!=after:
                    changed.append(before['id'])
                    a,b=copy.deepcopy(before),copy.deepcopy(after)
                    del a['displayModels'];del b['displayModels']
                    self.assertEqual(a,b)
        self.assertTrue(set(changed) <= {f'paladin_helmet_{tier}' for tier in framing.PALADIN_TIERS})
        self.assertEqual(sum(r['displayAssignmentBefore']['model']!=r['displayAssignment']['model'] for r in ledger['items']),6)
        self.assertIn(sum(not destination.exists() for _,destination in copies),(0,6))

    def test_paladin_history_retained_verbatim(self):
        ledger,_,contents=framing.generate(MANIFEST)
        original=json.loads((framing.HERE/'paladin-assets.provenance.json').read_text())
        updated=framing.paladin_provenance(ledger,contents['paladin'])
        for row in ledger['items']:
            if row['package']!='paladin':continue
            existing=original['files'].get(row['displayAssignment']['model'],{})
            prior=existing.get('previous',original['files'][row['displayAssignmentBefore']['model']])
            current=updated['files'][row['displayAssignment']['model']]
            self.assertEqual(current['previous'],prior)
            self.assertEqual(current['baseline'],prior.get('baseline',prior))
        self.assertEqual(updated['helmetClearanceRevision'],original['helmetClearanceRevision'])
        self.assertEqual(updated['developmentRevision'],original['developmentRevision'])

    def test_manifest_pin(self): self.reject(lambda m:m.update(scope='changed'),False)
    def test_duplicate(self): self.reject(lambda m:m['items'].__setitem__(1,copy.deepcopy(m['items'][0])))
    def test_weapon(self): self.reject(lambda m:m['items'][0].update(itemId='paladin_hammer_1h_novice'))
    def test_source_hash(self): self.reject(lambda m:m['items'][0].update(sourceSha256='0'*64))
    def test_output_hash(self): self.reject(lambda m:m['items'][0].update(outputSha256='0'*64))
    def test_equipped_hash(self): self.reject(lambda m:m['items'][0]['equippedAssetPins'][0].update(sha256='0'*64))
    def test_changed_texture(self): self.reject(lambda m:m['items'][0]['unchangedTexture'].update(sha256='0'*64))
    def test_bad_scale(self): self.reject(lambda m:m['items'][0].update(bakedUniformScale=-1))
    def test_changed_normals(self): self.reject(lambda m:m['items'][0].update(normalsUvsIndicesByteIdentical=False))
    def test_route_escape(self): self.reject(lambda m:m['items'][0]['displayAssignmentAfter'].update(model='../wrong.glb'))
    def test_changed_public_helper(self): self.reject(lambda m:m['authoringSources'][2].update(sha256='0'*64))

class HelmetRevisionTests(unittest.TestCase):
    def setUp(self):
        self.path=framing.ROOT/'scratch/gear-consolidation/paladin-helmet-silhouette-v6/pilot-manifest.json'
        self.previous=json.loads(framing.LEDGER.read_text())
        self.next,self.files=framing.prepare_helmet_revision(self.path)

    def test_exact_four_files_and_twenty_four_unchanged_rows(self):
        self.assertEqual(len(self.files),4)
        old={r['itemId']:r for r in self.previous['items']}
        for row in self.next['items']:
            if row['itemId'] in framing.HELMET_IDS:
                self.assertEqual(row['previous'],old[row['itemId']])
                self.assertEqual(row['texture'],old[row['itemId']]['texture'])
            else:self.assertEqual(row,old[row['itemId']])
        self.assertEqual(sum(a!=b for a,b in zip(self.previous['items'],self.next['items'])),2)

    def test_true_original_recipe_not_rejected_predecessor(self):
        for row in self.next['helmetRevision']['originals']:
            self.assertNotEqual(row['original']['sha256'],row['previousEquippedSha256'])
            self.assertEqual(row['fit']['scale'],1.16)
        self.assertEqual(self.next['helmetRevision']['manifest']['sha256'],framing.HELMET_REVISION_SHA)

    def test_no_canonical_write(self):
        self.assertEqual(framing.digest(framing.LEDGER),framing.HELMET_BASELINE_LEDGER_SHA)
        for source,destination in self.files:self.assertNotEqual(source.read_bytes(),destination.read_bytes())

    def test_frozen_six_file_delivery_preserves_other_icon_rows(self):
        manifest=framing.ROOT/'scratch/gear-consolidation/thief-coat-fit/nonweapon-icons-v3/manifest.json'
        before=json.loads((framing.HERE/'nonweapon-icons.provenance.json').read_text())
        displays,icons,copies=framing.prepare_helmet_delivery(self.path,manifest)
        self.assertEqual(len(copies),6)
        self.assertEqual(displays['helmetRevision']['publicRecipe']['profile'],'original-shape-v6')
        self.assertEqual(sum(a!=b for a,b in zip(before['items'],icons['items'])),2)
        for a,b in zip(before['items'],icons['items']):
            if a['itemId'] in framing.HELMET_IDS:self.assertEqual(b['previous'],a)
            else:self.assertEqual(a,b)
        for key,row in before['revisions'].items():self.assertEqual(row,icons['revisions'][key])
        self.assertEqual(json.loads((framing.HERE/'nonweapon-icons.provenance.json').read_text()),before)

    def test_delivery_rejects_changed_public_recipe(self):
        path=next(iter(framing.HELMET_PUBLIC_SOURCES))
        old=framing.HELMET_PUBLIC_SOURCES[path]
        try:
            framing.HELMET_PUBLIC_SOURCES[path]='0'*64
            with self.assertRaises(AssertionError):
                framing.prepare_helmet_delivery(self.path,framing.ROOT/'scratch/gear-consolidation/thief-coat-fit/nonweapon-icons-v3/manifest.json')
        finally:framing.HELMET_PUBLIC_SOURCES[path]=old

    def prospective_validation(self, mutate=None):
        icon_manifest=framing.ROOT/'scratch/gear-consolidation/thief-coat-fit/nonweapon-icons-v3/manifest.json'
        displays,icons,copies=framing.prepare_helmet_delivery(self.path,icon_manifest)
        histories={name:json.loads((framing.HERE/name).read_text()) for name in framing.HELMET_HISTORIES}
        receipt=framing.helmet_paladin_provenance(histories['paladin-assets.provenance.json'],displays,icons,copies,
            {'sourceId':'test-only-native-evidence','sha256':'0'*64})
        if mutate:mutate(displays,icons,receipt)
        original_digest=framing.digest
        future={str(b):original_digest(a) for a,b in copies}
        future[str(framing.LEDGER)]=framing.serialized_sha(displays)
        future[str(framing.HERE/'nonweapon-icons.provenance.json')]=framing.serialized_sha(icons)
        entries=json.loads((framing.HERE/'paladin/content.json').read_text())['entries']
        with patch.object(framing,'helmet_history',side_effect=lambda name:histories[name]), \
             patch.object(framing,'digest',side_effect=lambda path:future.get(str(path),None) or original_digest(path)):
            for row in displays['items']:
                if row['itemId'] in framing.HELMET_IDS:framing.validate_helmet_display_row(displays,row)
            icon_rows=framing.validate_helmet_icons(icons,'paladin',entries)
            framing.validate_helmet_paladin(receipt,[r for r in displays['items'] if r['package']=='paladin'],icon_rows)
        return histories,receipt

    def test_prospective_current_branches_and_complete_prior_history(self):
        histories,receipt=self.prospective_validation()
        old=histories['paladin-assets.provenance.json']
        self.assertEqual(sum(old['files'][k]!=v for k,v in receipt['files'].items()),6)
        self.assertEqual(receipt['helmetClearanceRevision'],old['helmetClearanceRevision'])
        self.assertEqual(receipt['sourceEvidence'],old['sourceEvidence'])

    def test_prospective_rejects_unrelated_icon_change(self):
        def mutate(displays,icons,receipt):
            next(r for r in icons['items'] if r['itemId'] not in framing.HELMET_IDS)['camera']={}
        with self.assertRaises(AssertionError):self.prospective_validation(mutate)

    def test_prospective_rejects_rewritten_predecessor(self):
        def mutate(displays,icons,receipt):
            receipt['files']['assets/paladin-oathkeeper-helmet.glb']['previous']['sha256']='wrong'
        with self.assertRaises(AssertionError):self.prospective_validation(mutate)

    def test_display_unknown_item_rejected(self):
        rows=[copy.deepcopy(r) for r in self.next['items'] if r['itemId'] in framing.HELMET_IDS]
        rows[0]['itemId']='paladin_helmet_novice'
        with self.assertRaises(AssertionError):framing.merge_helmet_display_revision(self.previous,rows,self.next['helmetRevision'])

    def test_display_wrong_predecessor_rejected(self):
        rows=[copy.deepcopy(r) for r in self.next['items'] if r['itemId'] in framing.HELMET_IDS]
        rows[0]['previous']['output']['sha256']='wrong'
        with self.assertRaises(AssertionError):framing.merge_helmet_display_revision(self.previous,rows,self.next['helmetRevision'])

    def test_display_changed_texture_rejected(self):
        rows=[copy.deepcopy(r) for r in self.next['items'] if r['itemId'] in framing.HELMET_IDS]
        rows[0]['texture']['sha256']='wrong'
        with self.assertRaises(AssertionError):framing.merge_helmet_display_revision(self.previous,rows,self.next['helmetRevision'])

    def icon_fixture(self):
        previous=json.loads((framing.HERE/'nonweapon-icons.provenance.json').read_text())
        refresh={'deliveryManifest':{'sha256':'test-only'},'items':[]}
        for row in previous['items']:
            if row['itemId'] in framing.HELMET_IDS:
                new=copy.deepcopy(row);new.pop('previous',None);new.pop('sourceRevision',None)
                new['priorSha256']=row['sha256'];new['sha256']='new-'+row['itemId']
                refresh['items'].append(new)
        return previous,refresh

    def test_exact_two_icon_updates_keep_thirty_eight_records(self):
        previous,refresh=self.icon_fixture()
        result=framing.merge_helmet_icon_revision(previous,refresh,'test-helmet-revision')
        self.assertEqual(sum(a!=b for a,b in zip(previous['items'],result['items'])),2)
        for old,new in zip(previous['items'],result['items']):
            if old['itemId'] in framing.HELMET_IDS:self.assertEqual(new['previous'],old)
            else:self.assertEqual(new,old)
        for key,value in previous['revisions'].items():self.assertEqual(result['revisions'][key],value)

    def test_icon_wrong_predecessor_rejected(self):
        previous,refresh=self.icon_fixture();refresh['items'][0]['priorSha256']='wrong'
        with self.assertRaises(AssertionError):framing.merge_helmet_icon_revision(previous,refresh,'test-helmet-revision')

    def test_icon_unknown_item_rejected(self):
        previous,refresh=self.icon_fixture();refresh['items'][0]['itemId']='paladin_boots_novice'
        with self.assertRaises(AssertionError):framing.merge_helmet_icon_revision(previous,refresh,'test-helmet-revision')

    def test_icon_existing_revision_cannot_be_overwritten(self):
        previous,refresh=self.icon_fixture()
        with self.assertRaises(AssertionError):framing.merge_helmet_icon_revision(previous,refresh,'studio-v3')

if __name__=='__main__': unittest.main()
