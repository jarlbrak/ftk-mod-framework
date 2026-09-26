"""Package the approved original scene as deterministic, deduplicated embedded resources."""
import argparse,gzip,hashlib,json,struct
from pathlib import Path

sha=lambda b:hashlib.sha256(b).hexdigest()
def main():
    parser=argparse.ArgumentParser();parser.add_argument('source',type=Path);parser.add_argument('output',type=Path);parser.add_argument('--provenance',type=Path,required=True)
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    original=(args.source/'diorama.json').read_bytes();scene=json.loads(original)
    assert scene['version']==1 and len(scene['meshes'])<=512
    vertices=0;triangles=0;texture_names={};resources={};aliases={}
    for mesh in scene['meshes']:
        count=len(mesh['vertices'])//3;assert len(mesh['vertices'])%3==0 and count<=65000
        vertices+=count;triangles+=len(mesh['triangles'])//3
        assert len(mesh['triangles'])%3==0 and all(0<=i<count for i in mesh['triangles'])
        name=mesh.get('texture')
        if not name:continue
        assert Path(name).name==name and name.endswith('.png')
        raw=(args.source/name).read_bytes();digest=sha(raw)
        assert raw[:8]==b'\x89PNG\r\n\x1a\n'
        width,height=struct.unpack('>II',raw[16:24]);assert 0<width<=8192 and 0<height<=8192
        canonical=texture_names.setdefault(digest,name);aliases[name]=canonical;mesh['texture']=canonical
        if canonical not in resources:
            (args.output/canonical).write_bytes(raw)
            resources[canonical]={'sha256':digest,'bytes':len(raw),'width':width,'height':height}
    assert vertices<=500000
    packed=json.dumps(scene,separators=(',',':'),ensure_ascii=False).encode()
    assert len(packed)<64000000
    compressed=gzip.compress(packed,compresslevel=9,mtime=0)
    (args.output/'diorama.json.gz').write_bytes(compressed)
    resources['diorama.json.gz']={'sha256':sha(compressed),'bytes':len(compressed),'decodedBytes':len(packed)}
    manifest={'schemaVersion':1,'sourceSceneSha256':sha(original),'meshCount':len(scene['meshes']),'vertices':vertices,'triangles':triangles,'textureAliases':aliases,'resources':resources,'provenance':{'geometry':'Original Rodin-generated citadel, gatehouse, quay, cliff and airship with authored fitting, masonry, rigging and propeller separation.','sky':'Original generated distant sky plate. Foreground geometry is 3D.','sourceBuild':'Fortress Arrival approved composition; see provenance.json.','gameAssetsIncluded':False}}
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    build=json.loads(args.provenance.read_text())
    provenance={'authoringRevision':'fortress-arrival-v2-flight','sourceBuildSha256':sha(args.provenance.read_bytes()),'conceptSha256':sha((args.provenance.parent/'fortress-arrival-concept-v1.png').read_bytes()),'sources':[{'name':asset['name'],'generator':'Hyper3D Rodin','generationId':asset['generationId'],'hashes':asset.get('sourceHashes',{'base_basic_pbr.glb':asset.get('sha256')})} for asset in build['assets']],'skySha256':sha((args.source/'cloud-sky.png').read_bytes()),'description':'Original generated source meshes and sky, assembled and fitted with authored geometry. No extracted game assets.'}
    (args.output/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
    print('Packaged' ,len(resources),'resources,',sum(r['bytes'] for r in resources.values()),'bytes,',vertices,'vertices')
if __name__=='__main__':main()
