"""Bounded revision joins preserve old pixels and reject prohibited icon substitutions."""
import copy
import json
from pathlib import Path
import unittest
import nonweapon_icon_revisions as revisions

class IconRevisionTests(unittest.TestCase):
    def setUp(self):
        self.old=json.loads(Path(__file__).with_name('nonweapon-icons.studio-v2.provenance.json').read_text())
        old={r['itemId']:r for r in self.old['items']}
        self.refresh={k:copy.deepcopy(v) for k,v in self.old.items() if k!='items'}
        self.refresh['items']=[]
        for package,keys in revisions.FRAMED.items():
            for key in sorted(keys):
                row=copy.deepcopy(old.get(key,{'package':package,'itemId':key,'path':'test/'+key+'.png'}))
                row['priorSha256']=row.get('sha256','old')
                row['sha256']='new-'+key
                self.refresh['items'].append(row)
    def merged(self):return revisions.merge_framed_icons(self.old,self.refresh,'history/icons-v1.json')
    def test_exact_union_and_history(self):
        ledger=self.merged()
        self.assertEqual(len(ledger['items']),40)
        self.assertEqual(sum(r['sourceRevision']=='studio-v2' for r in ledger['items']),14)
        self.assertEqual(sum('previous' in r for r in ledger['items']),15)
        self.assertEqual(len(revisions.validate_revision_join(ledger,self.old)),40)
    def test_reject_weapon(self):
        self.refresh['items'][0]['itemId']='thief_twins_street'
        with self.assertRaises(AssertionError):self.merged()
    def test_reject_duplicate(self):
        self.refresh['items'][1]=copy.deepcopy(self.refresh['items'][0])
        with self.assertRaises(AssertionError):self.merged()
    def test_wrong_predecessor(self):
        next(r for r in self.refresh['items'] if r['itemId']=='paladin_boots_novice')['priorSha256']='wrong'
        with self.assertRaises(AssertionError):self.merged()
    def test_cannot_relabel_retained_pixels(self):
        ledger=self.merged();next(r for r in ledger['items'] if r['sourceRevision']=='studio-v2')['sourceRevision']='studio-v3'
        with self.assertRaises(AssertionError):revisions.validate_revision_join(ledger,self.old)
    def test_cannot_rewrite_old_recipe(self):
        ledger=self.merged();ledger['revisions']['studio-v2']['recipe']['samples']=99
        with self.assertRaises(AssertionError):revisions.validate_revision_join(ledger,self.old)
    def test_cannot_rewrite_old_icon_pins(self):
        ledger=self.merged();next(r for r in ledger['items'] if 'previous' in r)['previous']['sha256']='wrong'
        with self.assertRaises(AssertionError):revisions.validate_revision_join(ledger,self.old)

if __name__=='__main__':unittest.main()
