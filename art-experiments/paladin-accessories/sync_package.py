#!/usr/bin/env python3
"""Explicitly sync this validated original campaign's 25 runtime assets."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]

def main():
    subprocess.run([sys.executable,str(OUT/'validate.py')],check=True)
    manifest=json.loads((OUT/'manifest.json').read_text())
    selected={name:sha for name,sha in manifest['files'].items() if name.endswith('.glb') or name.endswith(('-icon.png','-palette.png'))}
    assert len(selected)==25
    path=ROOT/'marketplace/packages/paladin-assets.provenance.json'
    provenance=json.loads(path.read_text())
    for name,sha in selected.items():
        source=OUT/name;assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
        shutil.copyfile(source,ROOT/'marketplace/packages/paladin/assets'/name)
        provenance['files']['assets/'+name]={'source':str(source.relative_to(ROOT)),'sha256':sha}
    path.write_text(json.dumps(provenance,indent=2)+'\n')
    print('PASS: synced 12 GLBs, 12 icons and original palette with additive provenance')
if __name__=='__main__':main()
