#!/usr/bin/env python3
"""Rebuild Rivenquill and prove its original authored outputs reproduce exactly."""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = Path(__file__).resolve().parent
DEFAULT_REFERENCE = ROOT / "scratch" / "basey-cockatrice-boss-reference-v1" / "reference.npz"
DEFAULT_COMPATIBLE_REFERENCE = ROOT / "scratch" / "basey-cockatrice-small-reference-v1" / "reference.npz"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=DEFAULT_REFERENCE)
    parser.add_argument("--compatible-reference", type=Path, default=DEFAULT_COMPATIBLE_REFERENCE)
    parser.add_argument("--output-dir", type=Path, default=ROOT / "scratch" / "rivenquill-basey-cockatrice-proof-rerun")
    parser.add_argument("--proof-output", type=Path, default=PACKAGE / "original-geometry-proof.json")
    parser.add_argument("--overwrite-proof", action="store_true")
    args = parser.parse_args()
    reference, compatible, output, proof = (args.reference.resolve(), args.compatible_reference.resolve(),
                                              args.output_dir.resolve(), args.proof_output.resolve())
    if output.exists():
        raise SystemExit(f"refusing to overwrite proof rerun directory: {output}")
    if proof.exists() and not args.overwrite_proof:
        raise SystemExit(f"refusing to overwrite proof: {proof}; pass --overwrite-proof")
    subprocess.run([sys.executable, str(PACKAGE / "build_geometry.py"), "--reference", str(reference),
                    "--compatible-reference", str(compatible), "--output-dir", str(output)], check=True)
    files = ["rivenquill.glb", "rivenquill-palette.png", "rivenquill.source.json", "rivenquill.pieces.json"]
    checks = []
    for name in files:
        authored, rerun = PACKAGE / name, output / name
        if not authored.is_file() or not rerun.is_file():
            raise SystemExit(f"missing authored or rerun output: {name}")
        if sha256(authored) != sha256(rerun):
            raise SystemExit(f"non-reproducible authored output: {name}")
        checks.append({"file": name, "sha256": sha256(authored), "reproduced_identical": True})
    result = {"status": "PASS", "scope": "Deterministic original-geometry rerun; it establishes byte-identical authored outputs, not studio or live acceptance.",
              "method": "The authoring phase reads only ordered bone_names and inverse bind matrices; both resource references are checked for an exact shared palette before export.",
              "references": [{"path": str(reference.relative_to(ROOT)), "sha256": sha256(reference)},
                             {"path": str(compatible.relative_to(ROOT)), "sha256": sha256(compatible)}],
              "generatorSha256": sha256(PACKAGE / "build_geometry.py"), "files": checks}
    proof.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
