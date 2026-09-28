"""Game-free adoption identity, scope and rollback checks with synthetic assets."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import censure_visual_revision as c


class RevisionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.png = b'\x89PNG\r\n\x1a\n'+b'\0\0\0\rIHDR'+(512).to_bytes(4,'big')*2+b'\x08\x06'
        self.baseline = {}; self.files = []; self.entries = []; self.after = []
        self.pal = {'files': {}, 'packageVersion': 'fixture'}
        self.put('scratch/generator.py', b'# frozen fixture')
        generator = self.ref('scratch/generator.py')
        for key in sorted(c.ITEMS):
            part = key.removeprefix('paladin_').removesuffix('_censure')
            stem = 'assets/paladin-'+part+'-censure'
            model, display = stem+'.glb', stem+'-display.glb'
            old_texture = 'assets/atlas-'+part+'-old.png'
            texture_bytes = self.png+part.encode()
            texture = 'assets/atlas-'+c.sha(texture_bytes)+'.png'
            icon = stem+'-icon.png'
            for name, data in [(model,b'old mesh'),(display,b'old display'),(old_texture,self.png),(icon,self.png)]:
                self.put(c.PACKAGE+name, data)
                self.pal['files'][name] = {'sha256':c.sha(data),'bytes':len(data),'source':'original'}
            entry = {'id':key,'kind':'item' if key in c.ICONS else 'weapon','icon':icon,
                     'fields':{'armor':12},'itemModels':[{'path':'.','model':model,'texture':old_texture}],
                     'displayModels':[{'path':'native','model':display,'texture':old_texture}]}
            self.entries.append(entry); current = copy.deepcopy(entry)
            current['itemModels'][0]['texture'] = texture
            current['displayModels'][0]['texture'] = texture
            self.after.append(current)
            for name, data in [(model,b'new mesh '+part.encode()),(display,b'new display '+part.encode()),(texture,texture_bytes)]:
                source = 'scratch/delivery/'+Path(name).name; self.put(source,data)
                self.files.append({'path':c.PACKAGE+name,'sourceId':source.removeprefix('scratch/'),
                    'sha256':c.sha(data),'bytes':len(data),'previous':self.pal['files'].get(name)})
        self.before = {'entries': self.entries}; self.content = c.encode({'entries':self.after})
        self.put(c.PACKAGE+'content.json',c.encode(self.before))
        old_icons=[]; old_display=[]
        for entry in self.entries:
            if entry['id'] in {'paladin_helmet_censure','paladin_boots_censure'}:
                old_icons.append({'itemId':entry['id'],'package':'paladin','path':c.PACKAGE+entry['icon'],'sha256':c.sha(self.png)})
                old_display.append({'itemId':entry['id'],'package':'paladin','output':{'path':c.PACKAGE+entry['displayModels'][0]['model']}})
        old_icons += [{'itemId':'thief_fixture_'+str(i),'package':'thief','unchanged':i} for i in range(38)]
        old_display += [{'itemId':'unrelated_'+str(i),'package':'thief','unchanged':i} for i in range(24)]
        self.put(c.META+c.LEDGERS[0],c.encode(self.pal))
        self.put(c.META+c.LEDGERS[1],c.encode({'items':old_display}))
        self.put('scratch/old-studio/manifest.json',c.encode({'items':[]}))
        self.put(c.META+c.LEDGERS[2],c.encode({'items':old_icons,'studioManifest':{
            'sourceId':'old-studio/manifest.json','sha256':self.ref('scratch/old-studio/manifest.json')['sha256']}}))
        self.put('art-experiments/thief-advanced/provenance.py',b'def build_document(ledger_bytes=None):\n return {"ledgerNames":sorted(ledger_bytes)}\n')
        self.put('art-experiments/thief-advanced/provenance.json',b'{"old":"complete"}\n')
        self.candidate = {'baselineSha256':'baseline-fixture','previousPaladinLedgerSha256':c.sha(c.encode(self.pal)),
            'content':{'visualOverrides':{e['id']:{} for e in self.entries},'prospectiveSha256':c.sha(self.content),
                       'previousSha256':c.sha(c.encode(self.before))},
            'files':self.files,'deliveries':[{'manifest':'scratch/delivery/manifest.json','manifestSha256':'delivery-fixture',
                'freeze':'scratch/delivery/frozen.json','freezeSha256':'freeze-fixture','sourceProvenance':{'generator':generator}}],
            'retainHistoricalUnreferencedTextures':sorted(c.refs(self.before)-c.refs(json.loads(self.content)))}
        self.baseline={p.relative_to(self.root).as_posix():c.sha(p.read_bytes()) for p in (self.root/c.PACKAGE).rglob('*') if p.is_file()}
        self.put('scratch/accepted-content.json',self.content)
        self.put('scratch/native-evidence.json',b'{"native":true}')
        accepted={'schema':'ftkmf.censure-native-acceptance.v1','approved':True,'authority':'parent-native-review',
            'baselineSha256':self.candidate['baselineSha256'],'deliveryManifestSha256s':['delivery-fixture'],
            'assetSha256s':{r['path']:r['sha256'] for r in self.files},'contentSha256':c.sha(self.content),
            'items':[{'itemId':key,'nativeEquipped':True,'nativeDisplay':True,'nativeCombatMotion':True,
                      'wearers':['fixture-class-male'],'evidence':[self.ref('scratch/native-evidence.json')]} for key in sorted(c.ITEMS)],
            'limitations':['Synthetic test fixture; never real acceptance.']}
        self.put('scratch/acceptance.json',c.encode(accepted)); self.acceptance=self.ref('scratch/acceptance.json')
        rows=[]; icon_rows=[]
        for index,entry in enumerate(self.after):
            key=entry['id']; image='paladin/'+key+'.png'; image_data=self.png+bytes([index])
            self.put('scratch/studio/'+image,image_data)
            sources=[self.ref('scratch/delivery/'+Path(p).name) for p in sorted(c.refs(entry['displayModels']))]
            camera={'direction':[0,1,0],'orthographicScale':1}
            rows.append({'id':key,'package':'paladin','image':image,'sha256':c.sha(image_data),
                         'sources':sources,'content':self.ref('scratch/accepted-content.json'),
                         'assignments':[{'rendererPath':'native'}],'camera':camera})
            if key in c.ICONS:
                output='scratch/icons/'+Path(entry['icon']).name;self.put(output,image_data)
                icon_rows.append({'itemId':key,'package':'paladin','canonicalIconPath':c.PACKAGE+entry['icon'],
                    'canonicalBeforeSha256':c.sha(self.png),'outputPath':output,'outputSha256':c.sha(image_data),
                    'sourceRenderPath':'scratch/studio/'+image,'sourceSha256':c.sha(image_data),'exactCopy':True,
                    'sourceModelAndTexturePins':sources,'sourceRenderCamera':camera,'native64Bounds':[1,1,63,63],'native64AlphaThreshold':16})
        studio={'complete':True,'resolution':[512,512],'mode':'RGBA','items':rows,'recipe':{'engine':'fixture'},
                'generator':generator,'renderer':generator,'decoder':generator}
        self.studio=self.freeze('scratch/studio',studio)
        icons={'sourceStudioManifest':self.ref('scratch/studio/manifest.json'),'sourceStudioFrozenReceipt':self.ref('scratch/studio/frozen.json'),
               'recipe':studio['recipe'],'generator':generator,'outputFormat':{'width':512,'height':512,'mode':'RGBA'},'items':icon_rows}
        self.icons=self.freeze('scratch/icons',icons)

    def put(self,name,data):
        path=self.root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    def ref(self,name): return {'path':name,'sha256':c.sha((self.root/name).read_bytes())}
    def freeze(self,folder,manifest):
        self.put(folder+'/manifest.json',c.encode(manifest));base=self.root/folder
        files={p.relative_to(base).as_posix():c.sha(p.read_bytes()) for p in base.rglob('*') if p.is_file() and p.name!='frozen.json'}
        self.put(folder+'/frozen.json',c.encode({'manifestSha256':files['manifest.json'],'files':files}))
        return [str(base/'manifest.json'),files['manifest.json'],str(base/'frozen.json'),c.sha((base/'frozen.json').read_bytes())]
    def prepare(self): return c.prepare(self.root,self.candidate,self.content,self.baseline,self.studio,self.icons,self.acceptance)
    def change_acceptance(self,key,value):
        d=json.loads((self.root/'scratch/acceptance.json').read_text());d[key]=value
        self.put('scratch/acceptance.json',c.encode(d));self.acceptance=self.ref('scratch/acceptance.json')

    def test_dry_run_then_exact_adoption(self):
        revision,writes,expected=self.prepare()
        for name,digest in self.baseline.items(): self.assertEqual(c.sha((self.root/name).read_bytes()),digest)
        self.assertEqual(set(revision['changedIcons']),{c.PACKAGE+e['icon'] for e in self.after if e['id'] in c.ICONS})
        c.apply(self.root,writes,expected)
        pal=json.loads((self.root/c.META/c.LEDGERS[0]).read_text())
        self.assertEqual(c.validate_paladin(pal,self.root),set(self.candidate['retainHistoricalUnreferencedTextures']))
        icons=json.loads((self.root/c.META/c.LEDGERS[2]).read_text())
        self.assertEqual(len(c.validate_icons(icons,'paladin',self.after,self.root)),4)
        display=json.loads((self.root/c.META/c.LEDGERS[1]).read_text())
        for row in display['items'][:2]: c.validate_display_row(display,row,next(e for e in self.after if e['id']==row['itemId']),self.root)
        self.assertEqual(len(icons['items']),42)

    def test_no_parent_approval(self):
        self.change_acceptance('approved',False)
        with self.assertRaises(AssertionError): self.prepare()

    def test_motion_required(self):
        doc=json.loads((self.root/'scratch/acceptance.json').read_text())
        next(r for r in doc['items'] if r['itemId']=='paladin_armor_censure')['nativeCombatMotion']=False
        self.change_acceptance('items',doc['items'])
        with self.assertRaises(AssertionError): self.prepare()

    def test_wrong_candidate_receipt(self):
        self.change_acceptance('deliveryManifestSha256s',['wrong'])
        with self.assertRaises(AssertionError): self.prepare()

    def test_stale_render_inputs_even_with_new_freeze(self):
        doc=json.loads((self.root/'scratch/studio/manifest.json').read_text())
        doc['items'][0]['sources'][0]=self.ref('scratch/generator.py')
        self.studio=self.freeze('scratch/studio',doc)
        icons=json.loads((self.root/'scratch/icons/manifest.json').read_text())
        icons['sourceStudioManifest']=self.ref('scratch/studio/manifest.json')
        icons['sourceStudioFrozenReceipt']=self.ref('scratch/studio/frozen.json')
        self.icons=self.freeze('scratch/icons',icons)
        with self.assertRaises(AssertionError): self.prepare()

    def test_unrelated_icon_history_cannot_be_relabelled(self):
        _,writes,expected=self.prepare();c.apply(self.root,writes,expected)
        ledger=json.loads((self.root/c.META/c.LEDGERS[2]).read_text())
        next(r for r in ledger['items'] if r['package']=='thief')['unchanged']='modified'
        with self.assertRaises(AssertionError):c.validate_icons(ledger,'paladin',self.after,self.root)

    def test_failed_replace_rolls_back_only_owned_writes(self):
        _,writes,expected=self.prepare()
        real=c.os.replace;count=[0]
        def fail_second(source,destination):
            count[0]+=1
            if count[0]==2: raise OSError('injected write failure')
            real(source,destination)
        with patch.object(c.os,'replace',fail_second):
            with self.assertRaises(OSError):c.apply(self.root,writes,expected)
        for name,digest in expected.items():
            path=self.root/name
            self.assertEqual(c.sha(path.read_bytes()) if path.exists() else None,digest)
        self.assertFalse(list(self.root.rglob('*.censure-tmp')))

    def test_weapon_icon_rejected(self):
        doc=json.loads((self.root/'scratch/icons/manifest.json').read_text())
        doc['items'][0]['itemId']='paladin_hammer_1h_censure';self.icons=self.freeze('scratch/icons',doc)
        with self.assertRaises(AssertionError): self.prepare()

    def test_native_route_or_gameplay_rejected(self):
        for field,value in [('fields',{'armor':99}),('itemModels',[{'path':'different','model':'assets/x.glb','texture':'assets/x.png'}])]:
            after=json.loads(self.content);after['entries'][0][field]=value
            with self.assertRaises(AssertionError): c.content_scope(self.before,after)

    def test_unrelated_baseline_edit_rejected(self):
        (self.root/next(p for p in self.baseline if p.endswith('.glb'))).write_bytes(b'unrelated edit')
        with self.assertRaises(AssertionError): self.prepare()

    def test_recheck_and_rollback(self):
        _,writes,expected=self.prepare()
        with self.assertRaises(RuntimeError): c.apply(self.root,writes,expected,lambda:(_ for _ in ()).throw(RuntimeError('postcheck')))
        for name,digest in expected.items():
            path=self.root/name
            self.assertEqual(c.sha(path.read_bytes()) if path.exists() else None,digest)
        changed=next(iter(writes));(self.root/changed).write_bytes(b'concurrent edit')
        with self.assertRaises(AssertionError): c.apply(self.root,writes,expected)
        self.assertEqual((self.root/changed).read_bytes(),b'concurrent edit')

    def test_path_escape_and_symlink_rejected(self):
        with self.assertRaises(AssertionError): c.safe(self.root,'../outside')
        (self.root/'link').symlink_to(self.root/'scratch/generator.py')
        with self.assertRaises(AssertionError): c.safe(self.root,'link')

    def test_optional_oath_is_separate_and_composes_before_any_write(self):
        # The real Oath selector is independently checked against retained inputs.
        # Here exercise composition with an already checked successor contract.
        base='assets/paladin-oathkeeper-helmet'
        entry={'id':c.OATH,'kind':'item','icon':base+'-icon.png',
               'itemModels':[{'path':'.','model':base+'.glb','texture':base+'.png'}],
               'displayModels':[{'path':'helmKettle','model':base+'-display.glb','texture':base+'.png'}]}
        self.entries.append(entry);self.after.append(copy.deepcopy(entry))
        self.before={'entries':self.entries};self.content=c.encode({'entries':self.after})
        self.put(c.PACKAGE+'content.json',c.encode(self.before));self.put('scratch/accepted-content.json',self.content)
        files=[]
        for suffix in ('.glb','-display.glb','-icon.png','.png'):
            name=base+suffix;data=self.png if suffix.endswith('.png') else b'old Oath'
            self.put(c.PACKAGE+name,data);self.pal['files'][name]={'sha256':c.sha(data),'bytes':len(data)}
            if suffix!='.png':
                source='scratch/oath/'+Path(name).name;new=self.png+b'new' if suffix.endswith('.png') else b'new Oath'
                self.put(source,new);files.append({'path':c.PACKAGE+name,'source':source,'sha256':c.sha(new),'previousSha256':c.sha(data)})
        self.put(c.META+c.LEDGERS[0],c.encode(self.pal))
        icons=json.loads((self.root/c.META/c.LEDGERS[2]).read_text())
        icons['items'][-1]={'itemId':c.OATH,'package':'paladin','path':c.PACKAGE+entry['icon'],'sha256':c.sha(self.png)}
        self.put(c.META+c.LEDGERS[2],c.encode(icons))
        displays=json.loads((self.root/c.META/c.LEDGERS[1]).read_text())
        displays['items'][-1]={'itemId':c.OATH,'package':'paladin','output':{'path':c.PACKAGE+base+'-display.glb'}}
        self.put(c.META+c.LEDGERS[1],c.encode(displays))
        self.candidate['previousPaladinLedgerSha256']=c.sha(c.encode(self.pal))
        self.candidate['content'].update(previousSha256=c.sha(c.encode(self.before)),prospectiveSha256=c.sha(self.content))
        self.baseline={p.relative_to(self.root).as_posix():c.sha(p.read_bytes()) for p in (self.root/c.PACKAGE).rglob('*') if p.is_file()}
        studio=json.loads((self.root/'scratch/studio/manifest.json').read_text())
        for row in studio['items']:row['content']=self.ref('scratch/accepted-content.json')
        self.studio=self.freeze('scratch/studio',studio)
        icons=json.loads((self.root/'scratch/icons/manifest.json').read_text())
        icons['sourceStudioManifest']=self.ref('scratch/studio/manifest.json');icons['sourceStudioFrozenReceipt']=self.ref('scratch/studio/frozen.json')
        self.icons=self.freeze('scratch/icons',icons)
        icon_file=files[2]
        oath={'schema':'ftkmf.oathkeeper-visual-successor.v1','itemId':c.OATH,'files':files,'contentEntry':entry,
              'sourceManifest':{'sourceId':'fixture.json'},'iconManifest':{'sourceId':'fixture-icons.json'},'nativeEvidence':[],
              'portrait':{'sha256':icon_file['sha256']},'iconRow':{
                  'itemId':c.OATH,'package':'paladin','path':icon_file['path'],'sha256':icon_file['sha256'],
                  'priorSha256':icon_file['previousSha256'],'inputs':[
                      {'path':c.PACKAGE+base+'-display.glb','sha256':files[1]['sha256']},
                      {'path':c.PACKAGE+base+'.png','sha256':c.sha(self.png)}]}}
        self.put('scratch/oath-successor.json',c.encode(oath));oath_ref=self.ref('scratch/oath-successor.json')
        self.change_acceptance('contentSha256',c.sha(self.content));self.change_acceptance('oathkeeperSuccessorSha256',oath_ref['sha256'])
        with self.assertRaises(AssertionError):self.prepare()
        with patch('sync_display_framing.prepare_oathkeeper_successor',return_value=oath):
            revision,writes,expected=c.prepare(self.root,self.candidate,self.content,self.baseline,self.studio,self.icons,self.acceptance,oath_ref)
        self.assertEqual(len(revision['changedIcons']),5)
        c.apply(self.root,writes,expected)
        pal=json.loads((self.root/(c.META+c.LEDGERS[0])).read_text());c.validate_paladin(pal,self.root)
        current=json.loads((self.root/(c.META+c.LEDGERS[2])).read_text())
        self.assertEqual(len(c.validate_icons(current,'paladin',self.after,self.root)),5)
        current=json.loads((self.root/(c.META+c.LEDGERS[1])).read_text())
        c.validate_display_row(current,current['items'][-1],entry,self.root)


if __name__=='__main__': unittest.main()
