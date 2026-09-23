#!/usr/bin/env python3
"""Build deterministic Paladin artifacts without publishing them."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import uuid
import zipfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
PACKAGE=HERE/'paladin'


def digest(data):return hashlib.sha256(data).hexdigest()


def write_json(path,value):path.write_text(json.dumps(value,indent=2,sort_keys=True)+'\n')


def immutable(path,data):
    if path.exists():
        if path.read_bytes()!=data:raise ValueError('Refusing to overwrite different artifact: '+str(path))
    else:path.write_bytes(data)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'scratch/paladin-package-build')
    parser.add_argument('--game-assembly',type=Path,default=ROOT/'scratch/paladin-game/FTK.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll')
    parser.add_argument('--platform',choices=['macos','windows','linux'],default='macos')
    parser.add_argument('--helper',type=Path,help='Current helper executable to validate this local archive')
    parser.add_argument('--fixture',action='store_true',help='Prepare a separate ignored local managed state; never activate it')
    parser.add_argument('--preview-state-root',type=Path,help='Seed the local draft banner into this marketplace state cache')
    parser.add_argument('--release',action='store_true',help='Use versioned release URLs in the descriptor; does not upload or publish')
    args=parser.parse_args()
    output=args.output.resolve();output.relative_to(ROOT/'scratch')
    if args.fixture and not args.helper:parser.error('--fixture requires --helper')
    if not args.game_assembly.is_file():parser.error('Configured isolated game assembly is missing')
    subprocess.run([sys.executable,str(HERE/'validate_paladin.py')],check=True)
    output.mkdir(parents=True,exist_ok=True)
    paths=[PACKAGE/'manifest.json',PACKAGE/'content.json']+sorted((PACKAGE/'assets').iterdir())
    files={}
    for path in paths:
        if path.is_symlink() or not path.is_file():raise ValueError('Nonregular runtime asset: '+str(path))
        name=path.relative_to(PACKAGE).as_posix()
        if name not in ['manifest.json','content.json'] and path.suffix not in ['.glb','.png']:raise ValueError('Unsupported runtime file: '+name)
        files[name]=path.read_bytes()
    stream=io.BytesIO()
    with zipfile.ZipFile(stream,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
        for name,data in sorted(files.items()):
            info=zipfile.ZipInfo(name,date_time=(1980,1,1,0,0,0))
            info.compress_type=zipfile.ZIP_DEFLATED;info.create_system=3;info.external_attr=0o100644<<16
            archive.writestr(info,data,compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)
    payload=stream.getvalue();sha=digest(payload);manifest=json.loads(files['manifest.json'])
    stem='paladin-'+manifest['version']+'-'+sha[:12]
    tag='paladin-v'+manifest['version'] if args.release else 'LOCAL-DRAFT-NOT-PUBLISHED'
    release_url='https://github.com/jarlbrak/ftk-mod-framework/releases/download/'+tag+'/'
    preview_url=release_url+'paladin-censure-banner.png'
    if args.preview_state_root:
        state_root=args.preview_state_root.resolve()
        if not state_root.is_dir() or not (state_root/'state.json').is_file():
            parser.error('--preview-state-root must be an existing local marketplace state')
        banner=PACKAGE/'promo/paladin-censure-banner.png'
        banner_bytes=banner.read_bytes()
        if len(banner_bytes)>2*1024*1024 or banner_bytes[:8]!=b'\x89PNG\r\n\x1a\n':
            parser.error('The promotional banner is not a valid-sized PNG preview')
        cache=state_root/'cache/screenshots'/(digest(preview_url.encode())+'.png')
        cache.parent.mkdir(parents=True,exist_ok=True)
        immutable(cache,banner_bytes)
    archive_path=output/(stem+'.zip');descriptor_path=output/(stem+'.descriptor.json')
    immutable(archive_path,payload)
    descriptor=json.loads((PACKAGE/'listing.json').read_text())
    # Runtime identity and summary come only from the manifest; artifact facts
    # come only from the finished bytes. Listing copy cannot override either.
    descriptor.update({
        'modGuid':manifest['modGuid'],'name':manifest['name'] if args.release else manifest['name']+' (LOCAL DRAFT)',
        'author':manifest['author'],'description':manifest['description'],
        'version':manifest['version'],'frameworkVersion':manifest['frameworkVersion'],
        'frameworkRange':'>='+manifest['frameworkVersion']+' <'+str(int(manifest['frameworkVersion'].split('.')[0])+1)+'.0.0',
        'gameFingerprints':[digest(args.game_assembly.read_bytes())],'platforms':[args.platform],
        'packageUrl':release_url+stem+'.zip',
        'sha256':sha,'compressedSize':len(payload),'expandedSize':sum(len(data) for data in files.values()),'fileCount':len(files),
        'screenshots':[preview_url]})
    descriptor_bytes=(json.dumps(descriptor,indent=2,sort_keys=True)+'\n').encode()
    immutable(descriptor_path,descriptor_bytes)
    receipt={'archive':archive_path.name,'descriptor':descriptor_path.name,'archiveSha256':sha,'descriptorSha256':digest(descriptor_bytes),
             'fileCount':len(files),'files':{name:digest(data) for name,data in sorted(files.items())},
             'status':'Unpublished local candidate. Archive bytes and declared metadata only; no live acceptance.'}
    write_json(output/(stem+'.build.json'),receipt)
    if args.helper:
        helper=args.helper.resolve()
        command=[str(helper),'marketplace-validate','--descriptor',str(descriptor_path),'--archive',str(archive_path)]
        result=subprocess.run(command,check=False,capture_output=True,text=True)
        write_json(output/(stem+'.helper-validation.json'),{'command':command,'helperSha256':digest(helper.read_bytes()),'exitCode':result.returncode,'stdout':result.stdout,'stderr':result.stderr})
        if result.returncode:raise RuntimeError(result.stderr or result.stdout)
        print(result.stdout.strip())
        if args.fixture:
            token=uuid.uuid4().hex;state=output/('fixture-state-'+token)
            request={'schemaVersion':1,'operationId':token,'stateRoot':str(state),'frameworkVersion':manifest['frameworkVersion'],
                     'gameAssemblyPath':str(args.game_assembly.resolve()),'platform':args.platform,'manualRoots':[],'manualGuids':[],'bundledGuids':[]}
            request_path=output/(stem+'.fixture-request-'+token+'.json');result_path=output/(stem+'.fixture-result-'+token+'.json')
            write_json(request_path,request)
            command=[str(helper),'marketplace-fixture','--descriptor',str(descriptor_path),'--archive',str(archive_path),'--request',str(request_path),'--result',str(result_path)]
            result=subprocess.run(command,check=False,capture_output=True,text=True)
            if result.returncode:raise RuntimeError(result.stderr or result.stdout)
            fixture=json.loads(result_path.read_text());assert fixture['ok'] and fixture['pending'] and not fixture['active']
            print('Prepared isolated local fixture only:',result_path)
    print(json.dumps({'archive':str(archive_path),'descriptor':str(descriptor_path),'sha256':sha,'fileCount':len(files)},indent=2))


if __name__=='__main__':main()
