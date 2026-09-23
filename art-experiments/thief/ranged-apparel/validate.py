#!/usr/bin/env python3
"""Decode the delivered GLBs independently, validate surfaces, and seal art inventory."""
import argparse,hashlib,json,shutil,struct
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent;ROOT=OUT.parents[2];PACKAGE=ROOT/'marketplace/packages/thief/assets'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def accessor(root,blob,index):
    a=root['accessors'][index];v=root['bufferViews'][a['bufferView']]
    dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
    width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    return np.frombuffer(blob,dtype=dtype,count=a['count']*width,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(a['count'],width)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--apparel-only',action='store_true');ap.add_argument('--baseline-dir',type=Path);ap.add_argument('--bindings',type=Path);args=ap.parse_args()
    def selected_file(f):return f.name.startswith(('thief-coat-','thief-hood-','thief-boots-','thief-charm-')) or f.name=='thief-apparel-palette.png'
    m=json.loads((OUT/'manifest.json').read_text());assert len(m['items'])==35
    items=[r for r in m['items'] if not args.apparel_only or r['family']!='bow']
    assert len({x['id'] for x in m['items']})==35
    checked=[]
    for f in sorted(OUT.glob('*.glb')):
        if args.apparel_only and not selected_file(f):continue
        raw=f.read_bytes();magic,version,length=struct.unpack_from('<4sII',raw)
        assert magic==b'glTF' and version==2 and length==len(raw),f.name
        size,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
        root=json.loads(raw[20:20+size]);offset=20+size
        bsize,bkind=struct.unpack_from('<II',raw,offset);assert bkind==0x004e4942
        blob=raw[offset+8:offset+8+bsize];prim=root['meshes'][0]['primitives'][0]
        points=accessor(root,blob,prim['attributes']['POSITION']);normals=accessor(root,blob,prim['attributes']['NORMAL']);uv=accessor(root,blob,prim['attributes']['TEXCOORD_0'])
        tris=accessor(root,blob,prim['indices']).reshape(-1,3)
        assert len(points)>0 and np.isfinite(points).all() and np.isfinite(normals).all()
        assert tris.min()>=0 and tris.max()<len(points)
        assert np.max(np.abs(np.linalg.norm(normals,axis=1)-1))<1e-4
        cross=np.cross(points[tris[:,1]]-points[tris[:,0]],points[tris[:,2]]-points[tris[:,0]])
        areas=np.linalg.norm(cross,axis=1);assert areas.min()>1e-10,(f.name,areas.min())
        alignment=np.einsum('ij,ij->i',cross/areas[:,None],normals[tris[:,0]])
        assert alignment.min()>.99,(f.name,alignment.min())
        assert uv.min()>=0 and uv.max()<=1
        source=json.loads(f.with_suffix('.source.json').read_text())
        assert np.allclose(points,source['positions'],atol=1e-6)
        assert np.array_equal(tris,source['triangles'])
        piece_path=f.with_suffix('.pieces.json')
        if piece_path.exists():
            for part in json.loads(piece_path.read_text()):
                start=part['vertex_start'];end=start+part['vertex_count'];selected=tris[(tris[:,0]>=start)&(tris[:,0]<end)]
                volume=np.einsum('ij,ij->i',points[selected[:,0]],np.cross(points[selected[:,1]],points[selected[:,2]])).sum()/6
                assert volume>0,(f.name,part['name'],float(volume))
        if 'JOINTS_0' in prim['attributes']:
            joints=accessor(root,blob,prim['attributes']['JOINTS_0']);weights=accessor(root,blob,prim['attributes']['WEIGHTS_0'])
            count=len(root['skins'][0]['joints'])
            assert np.allclose(weights.sum(axis=1),1) and (weights>=0).all()
            assert set(joints[weights>0].tolist())==set(range(count)),f.name
            if args.bindings:
                row=next(model for item in items for model in item['models'] if model['file']==f.name)
                binding_path=args.bindings/row['bindingInput'];binding=json.loads(binding_path.read_text())
                assert digest(binding_path)==row['bindingSha256'],f.name
                assert [root['nodes'][j]['name'] for j in root['skins'][0]['joints']]==binding['bone_names'],f.name
                actual=accessor(root,blob,root['skins'][0]['inverseBindMatrices']).reshape(-1,4,4)
                assert np.array_equal(actual,np.array(binding['bindposes'],dtype='<f4').transpose(0,2,1)),f.name
            if args.baseline_dir:
                old=(args.baseline_dir/f.name).read_bytes();oldsize=struct.unpack_from('<I',old,12)[0];olddoc=json.loads(old[20:20+oldsize]);oldblob=old[28+oldsize:]
                assert [root['nodes'][j]['name'] for j in root['skins'][0]['joints']]==[olddoc['nodes'][j]['name'] for j in olddoc['skins'][0]['joints']],f.name
                assert np.array_equal(accessor(root,blob,root['skins'][0]['inverseBindMatrices']),accessor(olddoc,oldblob,olddoc['skins'][0]['inverseBindMatrices'])),f.name
        checked.append({'file':f.name,'vertices':len(points),'triangles':len(tris),'sha256':digest(f)})
    geometry_variants={}
    for family in ['coat','hood','boots','charm']:
        hashes=[]
        for item in items:
            if item['family']!=family:continue
            model=next((r for r in item['models'] if r['sex']=='female'),item['models'][0])
            source=json.loads((OUT/model['source']).read_text())
            hashes.append(hashlib.sha256(json.dumps(source['positions'],separators=(',',':')).encode()).hexdigest())
        if hashes:assert len(set(hashes))==7,(family,'Each tier needs original geometric progression')
        geometry_variants[family]=len(set(hashes))
    for item in items:
        icon=OUT/item['icon'];im=Image.open(icon).convert('RGBA');assert im.size==(256,256)
        box=im.getchannel('A').getbbox();assert box and min(box[:2])>0 and max(box[2:])<256,(icon.name,box)
        assert min(box[2]-box[0],box[3]-box[1])>20 and max(box[2]-box[0],box[3]-box[1])>120,(icon.name,box)
        # Strip renderer path metadata from distributable original raster files.
        clean=Image.frombytes('RGBA',im.size,im.tobytes());clean.save(icon)
    for preview in OUT.glob('*-preview.png'):
        if args.apparel_only and not selected_file(preview):continue
        im=Image.open(preview).convert('RGBA');Image.frombytes('RGBA',im.size,im.tobytes()).save(preview)
    sheet=Image.new('RGB',(1400,880 if args.apparel_only else 1100),'#20262c');draw=ImageDraw.Draw(sheet)
    bands=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer'];families=['coat','hood','boots','charm'] if args.apparel_only else ['bow','coat','hood','boots','charm']
    for item in items:
        x=bands.index(item['band'])*200;y=families.index(item['family'])*220
        icon=Image.open(OUT/item['icon']).resize((190,190));sheet.paste(icon,(x,y),icon)
        draw.text((x+7,y+195),item['name'],fill='#eee5cf')
    sheet.save(OUT/('apparel-contact-sheet.png' if args.apparel_only else 'contact-sheet.png'))
    files=[f for f in sorted(OUT.iterdir()) if f.suffix=='.glb' or f.name.endswith(('-icon.png','-palette.png','.source.json','.pieces.json'))]
    if args.apparel_only:files=[f for f in files if selected_file(f)]
    m.setdefault('files',{}).update({f.name:digest(f) for f in files});m['generators']={p.name:digest(p) for p in sorted(OUT.glob('*.py'))}
    (OUT/'manifest.json').write_text(json.dumps(m,indent=2)+'\n')
    prior_provenance=PACKAGE/'ranged-apparel.provenance.json'
    package=json.loads(prior_provenance.read_text())['files'] if args.apparel_only and prior_provenance.exists() else {}
    for f in files:
        if f.suffix=='.glb' or f.name.endswith(('-icon.png','-palette.png')):
            shutil.copyfile(f,PACKAGE/f.name);assert digest(PACKAGE/f.name)==digest(f)
            package[f.name]={'source':str(f.relative_to(ROOT)),'sha256':digest(f)}
    (PACKAGE/'ranged-apparel.provenance.json').write_text(json.dumps({'schema':'ftkmf.thief-original-art-provenance.v1','provenance':m['provenance'],'status':m['status'],'files':package},indent=2)+'\n')
    result={'status':'PASS','scope':'Offline exported geometry, authored source equality, finite outward triangle normals, unit normals, UV bounds, skin palette use, icon alpha framing and package hashes. Native fit, draw animation and game integration not tested.','geometryVariantsPerFamily':geometry_variants,'exactBindingInputsChecked':bool(args.bindings),'priorBindingBytesCompared':bool(args.baseline_dir),'items':len(items),'glbs':len(checked),'icons':len(items),'packageFiles':len(package),'meshes':checked}
    (OUT/('apparel-validation.json' if args.apparel_only else 'validation.json')).write_text(json.dumps(result,indent=2)+'\n')
    print('PASS:',len(checked),'GLBs,',len(items),'icons,',len(package),'package assets')
if __name__=='__main__':main()
