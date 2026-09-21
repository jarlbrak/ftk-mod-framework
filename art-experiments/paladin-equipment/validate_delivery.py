#!/usr/bin/env python3
"""Verify derived fragment/icon hashes and assemble complete delivery manifests."""
import hashlib
import json
from pathlib import Path
import struct
import numpy as np
from PIL import Image,ImageDraw

OUT=Path(__file__).resolve().parent
CHAR=OUT.parent/'paladin-characters'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    fragments=json.loads((OUT/'fragments-manifest.json').read_text())
    assert fragments['sourceManifestSha256']==sha(OUT/'manifest.json')
    assert fragments['generatorSha256']==sha(OUT/'build_fragments.py')
    for name,digest in fragments['files'].items():assert sha(OUT/name)==digest
    count=0
    for mapping in fragments['mappings']:
        source=json.loads((OUT/(mapping['item']+'.source.json')).read_text());seen=set()
        expected=['Break','Break/Break'] if mapping['basePrefab']=='SmithHammer' else ['break','break/break2','break/break1']
        assert [f['rendererPath'] for f in mapping['fragments']]==expected
        for fragment in mapping['fragments']:
            tids=fragment['sourceTriangleIndices'];assert not seen.intersection(tids);seen.update(tids)
            original_vertices=[v for t in tids for v in source['triangles'][t]]
            own=json.loads((OUT/fragment['glbFile'].replace('.glb','.source.json')).read_text())
            for field in ['positions','normals','uvs']:assert own[field]==[source[field][i] for i in original_vertices]
            raw=(OUT/fragment['glbFile']).read_bytes();assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
            jlen=struct.unpack_from('<I',raw,12)[0];gltf=json.loads(raw[20:20+jlen]);binary=raw[28+jlen:]
            assert 'skins' not in gltf
            primitive=gltf['meshes'][0]['primitives'][0]
            def read(index):
                a=gltf['accessors'][index];v=gltf['bufferViews'][a['bufferView']]
                n={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
                return np.frombuffer(binary,dtype={5126:'<f4',5123:'<u2'}[a['componentType']],count=a['count']*n,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,n)
            for attr,field in [('POSITION','positions'),('NORMAL','normals'),('TEXCOORD_0','uvs')]:assert np.allclose(read(primitive['attributes'][attr]),own[field],atol=1e-7)
            assert np.array_equal(read(primitive['indices']).reshape(-1,3),own['triangles'])
            count+=1
        assert seen==set(range(len(source['triangles'])))
    assert count==30
    icon_images=[];icon_count=0
    for directory in [OUT,CHAR]:
        base=json.loads((directory/'manifest.json').read_text());icons=json.loads((directory/'icons-manifest.json').read_text())
        assert icons['generatorSha256']==sha(OUT/'render_icons.py')
        files=dict(base['files']);files.update(icons['files'])
        if directory==OUT:files.update(fragments['files'])
        for name,digest in files.items():assert sha(directory/name)==digest,(name,'hash')
        for icon in icons['icons']:
            image=Image.open(directory/icon['file']);assert image.size==(256,256) and image.mode=='RGBA'
            alpha=np.array(image.getchannel('A'));assert alpha.min()==0 and alpha.max()>240
            coverage=float((alpha>0).mean());assert .03<coverage<.95,(icon['file'],coverage)
            icon_images.append((image.copy(),icon['owner']));icon_count+=1
        delivery={'schema':'ftkmf.paladin-delivery-manifest.v1','baseManifestSha256':sha(directory/'manifest.json'),'iconsManifestSha256':sha(directory/'icons-manifest.json'),'files':files,'scope':'Source assets and derived original icons/fragments. Native attachment, motion and UI remain unverified.'}
        if directory==OUT:delivery['fragmentsManifestSha256']=sha(OUT/'fragments-manifest.json')
        (directory/'delivery-manifest.json').write_text(json.dumps(delivery,indent=2)+'\n')
    assert icon_count==37
    sheet=Image.new('RGBA',(7*256,6*290),(20,25,34,255));draw=ImageDraw.Draw(sheet)
    for index,(icon,name) in enumerate(icon_images):
        x=(index%7)*256;y=(index//7)*290;sheet.alpha_composite(icon,(x,y));draw.text((x+8,y+259),name.replace('paladin-',''),fill=(235,230,210,255))
    sheet.convert('RGB').save(OUT/'paladin-icons-contact-sheet.png')
    report={'status':'PASS','fragments':count,'icons':icon_count,'fragmentChecks':'Exact disjoint complete original source-triangle partition; GLB reread agrees with original positions, normals, UVs and indices. No native fragment surface remains in supplied assets.','iconChecks':'256x256 RGBA, transparent background, nonempty visible subject, manifest hashes. Native UI unverified.'}
    (OUT/'delivery-validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print('PASS: 30 original fragments, 37 transparent icons, delivery hashes')


if __name__=='__main__':main()
