#!/usr/bin/env python3
"""Offline helper fixtures. Never launches, deploys, or changes an existing non-fixture state."""
import argparse, copy, hashlib, io, json, struct, subprocess, tempfile, uuid, zipfile, zlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
PACKAGE=ROOT/'marketplace/packages/paladin'
VERSIONS={'base':'1.0.0','update':'1.0.1','bad':'1.0.2','bad-model':'1.0.3','bad-icon':'1.0.4'}
MARKER='.hot-reload-fixtures.json'
def data(value): return (json.dumps(value,indent=2,sort_keys=True)+'\n').encode()
def sha(value): return hashlib.sha256(value).hexdigest()
def immutable(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    if path.exists() and path.read_bytes()!=value: raise ValueError('Different fixture already exists: '+str(path))
    path.write_bytes(value)
def checker():
    def chunk(kind,value): return struct.pack('>I',len(value))+kind+value+struct.pack('>I',zlib.crc32(kind+value))
    rows=b''.join(b'\0'+b''.join(bytes((255,40,220,255) if (x//8+y//8)%2 else (20,240,220,255)) for x in range(32)) for y in range(32))
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',32,32,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b'')
def widen(value):
    value=bytearray(value);magic,version,total=struct.unpack_from('<III',value)
    assert magic==0x46546c67 and version==2 and total==len(value)
    size,kind=struct.unpack_from('<II',value,12);doc=json.loads(value[20:20+size])
    acc=doc['accessors'][doc['meshes'][0]['primitives'][0]['attributes']['POSITION']]
    assert acc['componentType']==5126 and acc['type']=='VEC3' and 'sparse' not in acc
    view=doc['bufferViews'][acc['bufferView']];base=20+size+8+view.get('byteOffset',0)+acc.get('byteOffset',0)
    for i in range(acc['count']):
        offset=base+i*view.get('byteStride',12);x=struct.unpack_from('<f',value,offset)[0];struct.pack_into('<f',value,offset,x*1.04)
    for bound in ('min','max'):
        if bound in acc: acc[bound][0] *= 1.04
    json_bytes=json.dumps(doc,separators=(',',':')).encode();json_bytes+=b' '*((-len(json_bytes))%4)
    binary_chunk=bytes(value[20+size:])
    return struct.pack('<III',magic,version,20+len(json_bytes)+len(binary_chunk))+struct.pack('<II',len(json_bytes),kind)+json_bytes+binary_chunk
def build(a):
    files={p.relative_to(PACKAGE).as_posix():p.read_bytes() for p in [PACKAGE/'manifest.json',PACKAGE/'content.json']+sorted((PACKAGE/'assets').iterdir()) if p.is_file()}
    catalog=[];changes={}
    for variant,version in VERSIONS.items():
        payload=copy.copy(files);manifest=json.loads(payload['manifest.json']);manifest['version']=version
        content=json.loads(payload['content.json']);entries={e['id']:e for e in content['entries']}
        if variant=='update':
            entries['paladin']['displayName']='Paladin Reload Fixture';weapon=entries['paladin_hammer_1h_novice'];weapon['fields']['damage']+=1
            weapon['icon']='assets/hot-reload-checker.png';payload[weapon['icon']]=checker()
            mesh=weapon['itemModels'][0]['model'];payload[mesh]=widen(payload[mesh])
            perk=next(e for e in content['entries'] if e.get('guardianBonuses'));perk['guardianBonuses']['guardHealPercent']=7
            removed=next(e for e in content['entries'] if e is not perk and e.get('guardianBonuses'));del removed['guardianBonuses']
            changes[variant]={'weapon':weapon['id'],'damage':weapon['fields']['damage'],'mesh':mesh,'guardianChanged':perk['id'],'guardianRemoved':removed['id']}
        if variant=='bad':
            entries['paladin_hammer_1h_novice']['fields']['damage']='invalid-runtime-number'
            changes[variant]={'expectedFailure':'strict runtime damage scalar conversion after partial registration'}
        if variant=='bad-model':
            mesh=entries['paladin_hammer_1h_novice']['itemModels'][0]['model']
            broken=bytearray(payload[mesh]);size=struct.unpack_from('<I',broken,12)[0];doc=json.loads(broken[20:20+size])
            acc=doc['accessors'][doc['meshes'][0]['primitives'][0]['attributes']['POSITION']]
            assert acc['componentType']==5126 and acc['type']=='VEC3'
            view=doc['bufferViews'][acc['bufferView']];offset=20+size+8+view.get('byteOffset',0)+acc.get('byteOffset',0)
            struct.pack_into('<I',broken,offset,0x7fc00000);payload[mesh]=bytes(broken)
            changes[variant]={'asset':mesh,'expectedFailure':'nonfinite required hammer vertex; bounded GLB container remains valid'}
        if variant=='bad-icon':
            icon=entries['paladin_hammer_1h_novice']['icon'];payload[icon]=payload[icon][:33]
            changes[variant]={'asset':icon,'expectedFailure':'PNG has header and dimensions but no image data; runtime decode must fail'}
        payload['manifest.json']=data(manifest);payload['content.json']=data(content)
        stream=io.BytesIO()
        with zipfile.ZipFile(stream,'w') as archive:
            for name,value in sorted(payload.items()):
                info=zipfile.ZipInfo(name,date_time=(1980,1,1,0,0,0));info.create_system=3;info.external_attr=0o100644<<16
                archive.writestr(info,value,compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)
        archive=stream.getvalue();digest=sha(archive);descriptor=json.loads((PACKAGE/'listing.json').read_text())
        descriptor.update(modGuid=manifest['modGuid'],name=manifest['name']+' fixture '+variant,author=manifest['author'],description=manifest['description'],version=version,frameworkVersion=manifest['frameworkVersion'],frameworkRange='>=1.0.0 <2.0.0',gameFingerprints=[sha(a.game_assembly.read_bytes())],platforms=['macos'],packageUrl='https://github.com/jarlbrak/ftk-mod-framework/releases/download/LOCAL-HOT-RELOAD/paladin-'+version+'.zip',sha256=digest,compressedSize=len(archive),expandedSize=sum(map(len,payload.values())),fileCount=len(payload),screenshots=[])
        immutable(a.fixtures/(variant+'.zip'),archive);immutable(a.fixtures/(variant+'.descriptor.json'),data(descriptor))
        subprocess.run([str(a.helper),'marketplace-validate','--descriptor',str(a.fixtures/(variant+'.descriptor.json')),'--archive',str(a.fixtures/(variant+'.zip'))],check=True)
        catalog.append(descriptor)
    immutable(a.fixtures/'catalog.json',data({'schemaVersion':1,'packages':catalog}))
    immutable(a.fixtures/'receipt.json',data({'sourceContentSha256':sha(files['content.json']),'gameAssemblySha256':sha(a.game_assembly.read_bytes()),'helperSha256':sha(a.helper.read_bytes()),'variants':changes,'limitations':'Offline package evidence only. No runtime or visual acceptance.'}))
    print(json.dumps({'fixtures':str(a.fixtures),'versions':VERSIONS,'changes':changes},indent=2))
def seed(a):
    state=a.state_root.resolve()
    if state.exists() and any(state.iterdir()) and not (state/MARKER).is_file(): raise ValueError('Refusing existing non-fixture state: '+str(state))
    immutable(state/MARKER,data({'fixtureRoot':str(a.fixtures)}));immutable(state/'catalog.json',(a.fixtures/'catalog.json').read_bytes())
    for variant in VERSIONS:
        if not (a.fixtures/(variant+'.descriptor.json')).exists(): continue
        descriptor=json.loads((a.fixtures/(variant+'.descriptor.json')).read_text());immutable(state/'artifacts'/(descriptor['sha256']+'.zip'),(a.fixtures/(variant+'.zip')).read_bytes())
def operation(a,verb,**extra):
    state=a.state_root.resolve()
    if not (state/MARKER).is_file(): raise ValueError('Fixture marker required')
    token=uuid.uuid4().hex;request=dict(schemaVersion=1,operationId=token,stateRoot=str(state),frameworkVersion='1.0.0',gameAssemblyPath=str(a.game_assembly.resolve()),platform='macos',manualRoots=[],manualGuids=[],bundledGuids=[]);request.update(extra)
    directory=state/'fixture-operations';directory.mkdir(exist_ok=True);req=directory/(token+'.request.json');res=directory/(token+'.result.json');req.write_bytes(data(request))
    process=subprocess.run([str(a.helper),'marketplace',verb,'--request',str(req),'--result',str(res)],capture_output=True,text=True);result=json.loads(res.read_text())
    if process.returncode or not result.get('ok'): raise RuntimeError(json.dumps(result)+'\n'+process.stderr)
    return result
def prepare(a):
    choice=a.selection;selection=[] if choice in ('empty','removed') else [dict(packageId='ftkmf.paladin',version=VERSIONS['base' if choice in ('enabled','disabled') else choice],enabled=choice!='disabled')]
    plan=operation(a,'prepare',selection=selection,dryRun=True);result=operation(a,'prepare',selection=selection,expectedRevision=plan['planRevision'])
    print(json.dumps({'selection':choice,'active':result.get('active',{}).get('generationId') if result.get('active') else None,'pending':result['pending']['generationId']},indent=2));return result
def self_test(a):
    with tempfile.TemporaryDirectory(prefix='ftkmf-hot-reload-fixtures-') as directory:
        a.state_root=Path(directory)/'marketplace';seed(a);active=''
        choices=['empty','enabled','update','bad']+[v for v in ('bad-model','bad-icon') if (a.fixtures/(v+'.descriptor.json')).exists()]+['disabled','removed','enabled']
        for choice in choices:
            a.selection=choice;result=prepare(a);pending=result['pending']['generationId']
            operation(a,'hot-validate',expectedCurrent=active,expectedPending=pending);committed=operation(a,'hot-commit',expectedCurrent=active,expectedPending=pending)
            assert committed['active']['generationId']==pending;active=pending
        print('PASS:',len(choices),'cached-only helper prepare/validate/commit transitions. Bad archives are helper-valid; runtime rejection remains a live gate.')
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('command',choices=['build','seed','prepare','self-test']);p.add_argument('--fixtures',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);p.add_argument('--game-assembly',type=Path,required=True);p.add_argument('--state-root',type=Path);p.add_argument('--selection',choices=['empty','enabled','update','bad','bad-model','bad-icon','disabled','removed']);a=p.parse_args();a.helper=a.helper.resolve();a.fixtures=a.fixtures.resolve()
    if a.command in ('seed','prepare') and a.state_root is None:p.error('--state-root required')
    if a.command=='prepare' and a.selection is None:p.error('--selection required')
    {'build':build,'seed':seed,'prepare':prepare,'self-test':self_test}[a.command](a)
if __name__=='__main__':main()
