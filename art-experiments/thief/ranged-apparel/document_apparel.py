#!/usr/bin/env python3
"""Derive the apparel tier ledger from definitions and original art inventory."""
import hashlib,json
from pathlib import Path
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2]
art=json.loads((OUT/'manifest.json').read_text())
entries={x['id']:x for x in json.loads((ROOT/'marketplace/packages/thief/content.json').read_text())['entries']}
rows=[];text=['# Thief apparel inventory','',
 'Generated from current content definitions and original art manifest. No balance changes are made by the apparel builder. This is a 28-item art inventory, not native acceptance.', '',
 '| Tier | Slot / ID | Display name | Native template | Item levels | Rarity / gold | Explicit modifiers | Visual motif |',
 '| --- | --- | --- | --- | --- | --- | --- | --- |']
for item in art['items']:
 if item['family']=='bow':continue
 e=entries[item['id']];f=e['fields'];mods=e.get('modifiers',{})
 record={'id':item['id'],'tier':item['band'],'slot':item['family'],'displayName':e['displayName'],'nativeTemplate':e['template'],
         'levelBand':[f['minlevel'],f['maxlevel']],'rarity':f['rarity'],'gold':f['goldvalue'],'modifiers':mods,
         'acquisition':{k:f.get(k) for k in ['dropable','townmarket','m_NightMarket','m_DungeonMerchant','_shopStock']},
         'models':item['models'],'display':item['display'],'icon':item['icon'],'palette':item['palette'],'motif':item['motif']}
 rows.append(record)
 text.append('| '+ ' | '.join([item['band'],item['family']+' / `'+item['id']+'`',e['displayName'],e['template'],str(f['minlevel'])+'-'+str(f['maxlevel']),str(f['rarity'])+' / '+str(f['goldvalue']),', '.join(k+' '+str(v) for k,v in mods.items()),item['motif']])+' |')
text+=['','Every item retains the definition-backed ordinary drop/shop routes recorded in `apparel-inventory.json`. Eligibility does not guarantee an observed shop or loot spawn.','',
'Coats have female and male skinned exports; boots share the established ten-joint binding. Headpieces use the existing rigid item route. Charms provide display/icon art and imply no new visible avatar attachment. Native body, face, hair, hands, races, equipment motion and lifecycle need separate live review.','']
(OUT/'APPAREL-INVENTORY.md').write_text('\n'.join(text))
(OUT/'apparel-inventory.json').write_text(json.dumps({'schema':'ftkmf.thief-apparel-inventory.v1','items':rows,'totalItems':len(rows),
 'generator':'document_apparel.py','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()},indent=2)+'\n')
print('Documented',len(rows),'apparel items')

# A compact handoff seals only this owner's apparel bytes. Weapons have their
# own provenance and may be regenerated independently in the same package.
package=ROOT/'marketplace/packages/thief/assets'
owned=[p for p in sorted(OUT.iterdir()) if p.is_file() and (p.name.startswith(('thief-coat-','thief-hood-','thief-boots-','thief-charm-')) or p.name=='thief-apparel-palette.png') and (p.suffix=='.glb' or p.name.endswith(('-icon.png','-palette.png')))]
files={}
for path in owned:
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    assert hashlib.sha256((package/path.name).read_bytes()).hexdigest()==digest,path.name
    files[path.name]={'source':str(path.relative_to(ROOT)),'package':'assets/'+path.name,'sha256':digest}
review=OUT/'apparel-review/manifest.json'
delivery={'schema':'ftkmf.thief-apparel-delivery.v1','items':28,'equippedMeshes':35,'displayMeshes':28,'icons':28,'palette':'assets/thief-apparel-palette.png','files':files,
    'inventory':'apparel-inventory.json','validation':'apparel-validation.json','review':'apparel-review/manifest.json',
    'reviewSha256':hashlib.sha256(review.read_bytes()).hexdigest() if review.exists() else None,
    'remainingGates':['Art approval','Native rigid headwear mount and retained hair visibility','Male/female and alternate race fit','Ordinary motion and equipment rebuilds','Native displays and lifecycle'],
    'scope':'Original apparel art candidate. All content IDs and established renderer and skin binding metadata retained. No weapons or content definitions edited by the apparel builder.'}
(OUT/'apparel-delivery.json').write_text(json.dumps(delivery,indent=2)+'\n')
print('Sealed',len(files),'apparel package files')
