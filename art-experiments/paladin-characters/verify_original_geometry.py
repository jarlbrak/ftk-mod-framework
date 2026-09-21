#!/usr/bin/env python3
"""Rebuild the package from binding-only JSON in a fresh scratch directory."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

PACKAGE=Path(__file__).resolve().parent
ROOT=PACKAGE.parents[1]


def main():
    manifest=json.loads((PACKAGE/"manifest.json").read_text())
    with tempfile.TemporaryDirectory(prefix="paladin-characters-proof-",dir=ROOT/"scratch") as temporary:
        subprocess.run([sys.executable,str(PACKAGE/"build_geometry.py"),"--output",temporary],check=True,stdout=subprocess.DEVNULL)
        rebuilt=json.loads((Path(temporary)/"manifest.json").read_text())
        assert manifest==rebuilt,"Original rebuild differs from recorded manifest"
    report={"status":"PASS","method":"Rebuilt all original geometry from binding-only JSON into a fresh scratch directory and compared every source, piece, GLB and palette hash. No native surface data is available to the generator.","manifestSha256":hashlib.sha256((PACKAGE/"manifest.json").read_bytes()).hexdigest(),"fileCount":len(manifest["files"]),"scope":"Original provenance and deterministic offline rebuild only; not native assembly, fit, motion or art acceptance."}
    (PACKAGE/"original-geometry-proof.json").write_text(json.dumps(report,indent=2)+"\n")
    print("PASS: deterministic binding-only rebuild of",len(manifest["files"]),"files")


if __name__=="__main__":main()
