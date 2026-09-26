#!/usr/bin/env python3
"""Build deterministic Thunderstore packages from released FTK artifacts."""

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
import zipfile


ROOT = Path(__file__).resolve().parents[2]
COMMUNITY = "for-the-king"
FRAMEWORK_PACKAGE_NAME = "FTKModFramework"
FRAMEWORK_DEPENDENCY = "BepInEx-BepInExPack_ForTheKing"
FRAMEWORK_DEPENDENCY_VERSION = "5.4.19001"

TAG_PATTERN = re.compile(r"^bootstrap-v(?P<version>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))$")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def version_tuple(value):
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", value or ""):
        raise ValueError("Expected strict X.Y.Z version, got: " + str(value))
    return tuple(int(part) for part in value.split("."))


def bootstrap_version(source_root):
    version = (source_root / "launcher" / "thunderstore" / "bootstrap-version.txt").read_text().strip()
    version_tuple(version)
    return version


def validate_namespace(namespace):
    if not re.fullmatch(r"[A-Za-z0-9_]+", namespace or ""):
        raise ValueError("Thunderstore namespace must contain only letters, digits, and underscores")


def png_dimensions(data, label):
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(label + " is not a PNG")
    width, height = struct.unpack(">II", data[16:24])
    if (width, height) != (256, 256):
        raise ValueError(label + " must be exactly 256x256 pixels")


def tstore_manifest(package_name, version, description, website_url, dependencies):
    return {
        "name": package_name,
        "version_number": version,
        "website_url": website_url,
        "description": description,
        "dependencies": dependencies,
    }


def toml_string(value):
    return json.dumps(value, ensure_ascii=False)


def dependency_string(namespace, package_name, version):
    if package_name.startswith("BepInEx-"):
        return package_name + "-" + version
    return namespace + "-" + package_name + "-" + version


def make_toml(namespace, package_name, version, description, website_url, dependencies):
    lines = [
        "[config]",
        'schemaVersion = "0.0.1"',
        "",
        "[package]",
        "namespace = " + toml_string(namespace),
        "name = " + toml_string(package_name),
        "versionNumber = " + toml_string(version),
        "description = " + toml_string(description),
        "websiteUrl = " + toml_string(website_url),
        "containsNsfwContent = false",
        "",
        "[package.dependencies]",
    ]
    for dependency, dependency_version in sorted(dependencies.items()):
        lines.append(toml_string(dependency) + " = " + toml_string(dependency_version))
    lines.extend(["", "[publish]", 'repository = "https://thunderstore.io"',
                  "communities = [" + toml_string(COMMUNITY) + "]", ""])
    return "\n".join(lines)


def write_package(output, namespace, package_name, version, description, website_url,
                  dependencies, readme_path, icon_path, payload_files):
    validate_namespace(namespace)
    if not re.fullmatch(r"[A-Za-z0-9_]{1,128}", package_name or ""):
        raise ValueError("Thunderstore package names must be 1-128 letters, digits, or underscores")
    version_tuple(version)
    if len(description) > 250:
        raise ValueError("Thunderstore package descriptions must not exceed 250 characters")
    icon = Path(icon_path).read_bytes()
    png_dimensions(icon, str(icon_path))
    files = {
        "manifest.json": (json.dumps(tstore_manifest(
            package_name, version, description, website_url,
            [dependency_string(namespace, dependency, dependency_version)
             for dependency, dependency_version in sorted(dependencies.items())]),
            ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8"),
        "README.md": Path(readme_path).read_bytes(),
        "icon.png": icon,
    }
    files.update(payload_files)
    if not all(files[name] for name in ("manifest.json", "README.md", "icon.png")):
        raise ValueError("Thunderstore metadata files must not be empty")

    package_dir = Path(output) / (namespace + "-" + package_name + "-" + version)
    package_dir.mkdir(parents=True, exist_ok=True)
    archive_path = package_dir / "package.zip"
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(files.items()):
            if name.startswith("/") or ".." in Path(name).parts:
                raise ValueError("Unsafe Thunderstore archive entry: " + name)
            info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)

    config_path = package_dir / "thunderstore.toml"
    config_path.write_text(make_toml(namespace, package_name, version, description,
                                     website_url, dependencies), encoding="utf-8")
    receipt = {
        "package": namespace + "-" + package_name,
        "version": version,
        "archive": archive_path.name,
        "sha256": sha256(archive_path.read_bytes()),
        "files": {name: sha256(data) for name, data in sorted(files.items())},
        "status": "Local build only; no Thunderstore upload performed.",
    }
    (package_dir / "build.json").write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return {"archive": str(archive_path), "config": str(config_path), "sha256": receipt["sha256"],
            "package": receipt["package"], "version": version}


RELEASE_FILES = ("FTKThunderstoreBootstrap.dll", "ftkmf-bootstrap-helper.exe")

def release_files(directory):
    checksums = {}
    for line in (directory / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
        digest, name = line.split(maxsplit=1)
        name = name.removeprefix("*").removeprefix("./")
        if name in checksums or not re.fullmatch(r"[a-f0-9]{64}", digest):
            raise ValueError("Invalid or duplicate release checksum")
        checksums[name] = digest
    result = {}
    for name in RELEASE_FILES:
        path = directory / name
        if path.is_symlink() or not path.is_file() or path.stat().st_size > 100 * 1024 * 1024:
            raise ValueError("Missing or unsafe release asset: " + name)
        data = path.read_bytes()
        if checksums.get(name) != sha256(data):
            raise ValueError("Release checksum mismatch: " + name)
        if name.endswith((".dll", ".exe")) and not data.startswith(b"MZ"):
            raise ValueError("Release executable is not a PE image: " + name)
        result[name] = data
    return result


def bootstrap_package(tag_version, namespace, release_dir, output, source_root):
    if bootstrap_version(source_root) != tag_version:
        raise ValueError("Bootstrap tag version does not match its independent source version")
    artifacts = release_files(Path(release_dir))
    files = {"plugins/FTKSetup/" + name: content for name, content in artifacts.items()}
    return write_package(
        output, namespace, FRAMEWORK_PACKAGE_NAME, tag_version,
        "Thin Windows installer for FTK Mod Framework. Downloads the current launcher; framework and mods update through FTK.",
        "https://github.com/jarlbrak/ftk-mod-framework",
        {FRAMEWORK_DEPENDENCY: FRAMEWORK_DEPENDENCY_VERSION},
        ROOT / "FTKModFramework" / "thunderstore" / "README.md",
        ROOT / "FTKModFramework" / "thunderstore" / "icon.png", files)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True, help="Independent bootstrap release tag: bootstrap-vX.Y.Z")
    parser.add_argument("--namespace", default="JarlBrak", help="Thunderstore team namespace")
    parser.add_argument("--release-dir", type=Path, required=True, help="Downloaded release assets and SHA256SUMS")
    parser.add_argument("--source-root", type=Path, default=ROOT, help="Exact release source checkout")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    match = TAG_PATTERN.fullmatch(args.tag)
    if not match:
        parser.error("only bootstrap-vX.Y.Z tags are supported; framework and mods retain their own releases")
    result = bootstrap_package(match.group("version"), args.namespace,
                               args.release_dir, args.output, args.source_root.resolve())
    print(json.dumps([result], indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
