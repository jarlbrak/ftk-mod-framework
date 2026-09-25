#!/usr/bin/env python3
"""Build deterministic Possum release candidates without publishing."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = Path(__file__).resolve().parent / 'possum'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def immutable(path, data):
    if path.exists() and path.read_bytes() != data:
        raise ValueError('Refusing to overwrite different artifact: ' + str(path))
    path.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--game-assembly', type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((PACKAGE / 'manifest.json').read_text())
    files = {}
    paths = [PACKAGE / 'manifest.json', PACKAGE / 'content.json'] + sorted((PACKAGE / 'assets').iterdir())
    for p in paths:
        if p.is_symlink() or not p.is_file():
            raise ValueError('Expected regular package file: ' + str(p))
        name = p.relative_to(PACKAGE).as_posix()
        if name not in ('manifest.json', 'content.json') and p.suffix not in ('.png', '.glb'):
            raise ValueError('Unexpected runtime file: ' + name)
        files[name] = p.read_bytes()
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, 'w') as archive:
        for name, data in sorted(files.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    payload = stream.getvalue()
    sha = digest(payload)
    stem = 'possum-' + manifest['version'] + '-' + sha[:12]
    base = 'https://github.com/jarlbrak/ftk-mod-framework/releases/download/possum-v' + manifest['version'] + '/'
    descriptor = json.loads((PACKAGE / 'listing.json').read_text())
    descriptor.update({k: manifest[k] for k in ('modGuid', 'name', 'version', 'author', 'description', 'frameworkVersion')})
    descriptor.update(frameworkRange='>=' + manifest['frameworkVersion'] + ' <2.0.0',
                      gameFingerprints=[digest(args.game_assembly.read_bytes())],
                      packageUrl=base + stem + '.zip', sha256=sha,
                      compressedSize=len(payload), expandedSize=sum(map(len, files.values())),
                      fileCount=len(files), screenshots=[base + 'possum-banner.png'])
    immutable(output / (stem + '.zip'), payload)
    immutable(output / (stem + '.descriptor.json'), (json.dumps(descriptor, indent=2, sort_keys=True) + '\n').encode())
    receipt = dict(archive=stem + '.zip', descriptor=stem + '.descriptor.json', sha256=sha,
                   files={n: digest(b) for n, b in files.items()}, status='unpublished candidate')
    immutable(output / (stem + '.build.json'), (json.dumps(receipt, indent=2, sort_keys=True) + '\n').encode())
    print(json.dumps(receipt, indent=2))


if __name__ == '__main__':
    main()
