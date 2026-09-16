#!/usr/bin/env python3
"""Rebuild Mireglass Croaker and prove its authored outputs reproduce exactly."""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = Path(__file__).resolve().parent
DEFAULT_REFERENCE = ROOT / "scratch" / "acidblob-b-reference-v1" / "reference.npz"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference", type=Path, default=DEFAULT_REFERENCE)
    parser.add_argument("--output-dir", type=Path,
                        default=ROOT / "scratch" / "mireglass-croaker-proof-rerun")
    parser.add_argument("--proof-output", type=Path, default=PACKAGE / "original-geometry-proof.json")
    parser.add_argument("--overwrite-proof", action="store_true")
    args = parser.parse_args()
    reference = args.reference.resolve()
    output = args.output_dir.resolve()
    proof_output = args.proof_output.resolve()
    if output.exists():
        raise SystemExit(f"refusing to overwrite proof rerun directory: {output}")
    if proof_output.exists() and not args.overwrite_proof:
        raise SystemExit(f"refusing to overwrite proof: {proof_output}; pass --overwrite-proof")
    subprocess.run([
        sys.executable, str(PACKAGE / "build_geometry.py"),
        "--reference", str(reference), "--output-dir", str(output),
    ], check=True)
    files = ["mireglass.glb", "mireglass-palette.png", "mireglass.source.json", "mireglass.pieces.json"]
    checks = []
    for filename in files:
        authored = PACKAGE / filename
        rerun = output / filename
        if not authored.is_file() or not rerun.is_file():
            raise SystemExit(f"missing authored or rerun output: {filename}")
        expected, actual = sha256(authored), sha256(rerun)
        if expected != actual:
            raise SystemExit(f"non-reproducible authored output: {filename}")
        checks.append({"file": filename, "sha256": expected, "reproduced_identical": True})
    proof = {
        "status": "PASS",
        "scope": "Deterministic original-geometry rerun; it establishes byte-identical authored outputs, not studio or live acceptance.",
        "method": "The authoring phase reads only ordered bone_names and inverse bind matrices; independent export validation separately reads the local reference without feeding it into geometry generation.",
        "reference": {"path": str(reference.relative_to(ROOT)), "sha256": sha256(reference)},
        "generatorSha256": sha256(PACKAGE / "build_geometry.py"),
        "files": checks,
    }
    proof_output.write_text(json.dumps(proof, indent=2) + "\n")
    print(json.dumps(proof, indent=2))


if __name__ == "__main__":
    main()
