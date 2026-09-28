#!/usr/bin/env python3
"""Build and validate an unpublished deterministic Blacksmith package candidate."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PACKAGE = HERE / "classgear"
DEFAULT_GAME = ROOT / "scratch/blacksmith-gear-game/FTK.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def immutable(path, data):
    if path.exists():
        if path.read_bytes() != data:
            raise ValueError("Refusing to overwrite different candidate artifact: " + str(path))
    else:
        path.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "scratch/blacksmith-gear-build")
    parser.add_argument("--game-assembly", type=Path, default=DEFAULT_GAME)
    parser.add_argument("--helper", type=Path,
                        help="Existing helper to use instead of building the current source helper")
    parser.add_argument("--platform", choices=("macos", "windows", "linux"), default="macos",
                        help="Local candidate target only; not evidence of public support")
    parser.add_argument("--redesign-manifest", type=Path, help="Explicit approved 32-item art ledger")
    parser.add_argument("--framework-dll", type=Path, help="Exact local framework binary required by the redesign candidate")
    parser.add_argument("--runtime-helper-dll", type=Path, help="Exact local runtime test helper required by the redesign candidate")
    args = parser.parse_args()

    output = args.output.resolve()
    output.relative_to((ROOT / "scratch").resolve())
    if not args.game_assembly.is_file():
        parser.error("Configured isolated game assembly is missing")
    if args.redesign_manifest:
        for name in ("framework_dll", "runtime_helper_dll"):
            path = getattr(args, name)
            if path is None or path.is_symlink() or not path.is_file():
                parser.error("Redesign candidates require an explicit regular --" + name.replace("_", "-"))
        if list(output.glob("*.descriptor.json")):
            parser.error("Use a candidate output directory without exported marketplace descriptors")
    output.mkdir(parents=True, exist_ok=True)
    if args.helper is not None:
        helper = args.helper.resolve()
        if not helper.is_file():
            parser.error("Configured marketplace helper is missing")
    else:
        helper = output / "ftkmf-launcher-helper"
        subprocess.run(["go", "build", "-o", str(helper), "."], cwd=ROOT / "launcher/helper", check=True)

    if args.redesign_manifest:
        extra = ["--redesign-manifest", str(args.redesign_manifest.resolve())]
        subprocess.run([sys.executable, str(HERE / "build_classgear_blacksmith_redesign.py")] + extra, check=True)
        subprocess.run([sys.executable, str(HERE / "validate_classgear_blacksmith_redesign.py")] + extra, check=True)
    else:
        subprocess.run([sys.executable, str(HERE / "build_classgear_blacksmith.py")], check=True)
        subprocess.run([sys.executable, str(HERE / "validate_classgear_blacksmith.py")], check=True)

    manifest = json.loads((PACKAGE / "manifest.json").read_text())
    listing = json.loads((PACKAGE / "listing.json").read_text())
    source_files = [PACKAGE / "manifest.json", PACKAGE / "content.json"]
    provenance = json.loads((PACKAGE / "assets.provenance.json").read_text())
    for relative, record in sorted(provenance["assets"].items()):
        rel = Path(relative)
        if rel.is_absolute() or ".." in rel.parts or len(rel.parts) != 2 or rel.parts[0] != "assets":
            raise ValueError("Invalid runtime inventory path: " + relative)
        asset = PACKAGE / rel
        if digest(asset.read_bytes()) != record["sha256"]:
            raise ValueError("Runtime inventory hash mismatch: " + relative)
        source_files.append(asset)
    files = {}
    for path in source_files:
        if path.is_symlink() or not path.is_file():
            raise ValueError("Candidate archive has a nonregular file: " + str(path))
        name = path.relative_to(PACKAGE).as_posix()
        if name not in ("manifest.json", "content.json") and path.suffix not in (".glb", ".png"):
            raise ValueError("Unsupported candidate runtime asset: " + name)
        files[name] = path.read_bytes()

    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(files.items()):
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)

    payload = stream.getvalue()
    archive_hash = digest(payload)
    banner = PACKAGE / "promo/blacksmith-banner.png"
    banner_bytes = banner.read_bytes()
    if len(banner_bytes) > 2 * 1024 * 1024 or banner_bytes[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Promotional banner is not a valid marketplace-sized PNG")

    tag = "LOCAL-DRAFT-NOT-PUBLISHED"
    release_url = "https://github.com/jarlbrak/ftk-mod-framework/releases/download/" + tag + "/"
    archive_name = "blacksmith-forge-gear-" + manifest["version"] + "-" + archive_hash[:12] + ".zip"
    archive_path = output / archive_name
    output.mkdir(parents=True, exist_ok=True)
    immutable(archive_path, payload)

    descriptor = dict(listing)
    descriptor.update({
        "modGuid": manifest["modGuid"],
        "name": manifest["name"] + " (LOCAL DRAFT)",
        "author": manifest["author"],
        "description": manifest["description"],
        "version": manifest["version"],
        "frameworkVersion": manifest["frameworkVersion"],
        "frameworkRange": ">=" + manifest["frameworkVersion"] + " <" +
                          str(int(manifest["frameworkVersion"].split(".")[0]) + 1) + ".0.0",
        "gameFingerprints": [digest(args.game_assembly.read_bytes())],
        "packageUrl": release_url + archive_name,
        "sha256": archive_hash,
        "compressedSize": len(payload),
        "expandedSize": sum(len(data) for data in files.values()),
        "fileCount": len(files),
        "platforms": [args.platform],
        "screenshots": [release_url + "blacksmith-banner.png"],
    })
    descriptor_bytes = (json.dumps(descriptor, indent=2, sort_keys=True) + "\n").encode()
    if args.redesign_manifest:
        # The marketplace schema cannot express development binary compatibility.
        # Its descriptor exists only for archive validation and is never exported.
        with tempfile.TemporaryDirectory(prefix="blacksmith-archive-validation-") as temporary:
            temporary_descriptor = Path(temporary) / "descriptor.json"
            temporary_descriptor.write_bytes(descriptor_bytes)
            result = subprocess.run([str(helper), "marketplace-validate", "--descriptor",
                                     str(temporary_descriptor), "--archive", str(archive_path)],
                                    check=False, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stderr or result.stdout)
        receipt = {
            "schema": "ftkmf.blacksmith.local-candidate.v1",
            "releaseEligible": False,
            "archive": archive_name,
            "archiveSha256": archive_hash,
            "fileCount": len(files),
            "files": {name: digest(data) for name, data in sorted(files.items())},
            "requiredBinaries": {
                "framework": digest(args.framework_dll.read_bytes()),
                "runtimeHelper": digest(args.runtime_helper_dll.read_bytes()),
                "gameAssembly": digest(args.game_assembly.read_bytes()),
                "marketplaceHelper": digest(helper.read_bytes()),
            },
            "observedFrameworkVersion": manifest["frameworkVersion"],
            "sourceProvenanceSha256": digest((PACKAGE / "assets.provenance.json").read_bytes()),
            "banner": str(banner.relative_to(ROOT)),
            "bannerSha256": digest(banner_bytes),
            "archiveValidation": {"exitCode": result.returncode, "stdout": result.stdout,
                                  "stderr": result.stderr, "scope": "Archive and schema only; no public compatibility claim."},
            "status": "Local development candidate only. Staging requires exact binary hashes. Public framework compatibility and live release gates remain outstanding.",
        }
        receipt_bytes = (json.dumps(receipt, indent=2, sort_keys=True) + "\n").encode()
        candidate_path = output / (archive_path.stem + "-" + digest(receipt_bytes)[:12] + ".candidate.json")
        immutable(candidate_path, receipt_bytes)
        print(json.dumps({"archive": str(archive_path), "candidate": str(candidate_path),
                          "sha256": archive_hash, "fileCount": len(files), "published": False}, indent=2))
        return
    listing_hash = digest(descriptor_bytes)
    stem = archive_path.stem + "-listing-" + listing_hash[:12]
    descriptor_path = output / (stem + ".descriptor.json")
    immutable(descriptor_path, descriptor_bytes)

    receipt = {
        "archive": archive_name,
        "descriptor": descriptor_path.name,
        "archiveSha256": archive_hash,
        "descriptorSha256": listing_hash,
        "banner": str(banner.relative_to(ROOT)),
        "bannerSha256": digest(banner_bytes),
        "fileCount": len(files),
        "files": {name: digest(data) for name, data in sorted(files.items())},
        "status": "Unpublished local candidate. URLs are placeholders. Platform and game fingerprint identify the isolated test target, not verified public support.",
    }
    write_json(output / (stem + ".build.json"), receipt)

    if helper.is_file():
        command = [str(helper), "marketplace-validate", "--descriptor",
                   str(descriptor_path), "--archive", str(archive_path)]
        result = subprocess.run(command, check=False, capture_output=True, text=True)
        write_json(output / (stem + ".helper-validation.json"), {
            "command": command,
            "helperSha256": digest(helper.read_bytes()),
            "exitCode": result.returncode,
            "stdout": result.stdout,
            "stderr": result.stderr,
        })
        if result.returncode:
            raise RuntimeError(result.stderr or result.stdout)
        print(result.stdout.strip())
    else:
        print("Marketplace helper not found; archive structure is not launcher-validated.")

    print(json.dumps({
        "archive": str(archive_path),
        "descriptor": str(descriptor_path),
        "banner": str(banner),
        "sha256": archive_hash,
        "fileCount": len(files),
        "published": False,
    }, indent=2))


if __name__ == "__main__":
    main()
