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

TAG_PATTERN = re.compile(r"^v(?P<version>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))$")
PLUGIN_VERSION_PATTERN = re.compile(r'public const string Version = "([0-9]+\.[0-9]+\.[0-9]+)";')


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def version_tuple(value):
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", value or ""):
        raise ValueError("Expected strict X.Y.Z version, got: " + str(value))
    return tuple(int(part) for part in value.split("."))


def plugin_version(source_root):
    source = (source_root / "FTKModFramework" / "Plugin.cs").read_text(encoding="utf-8")
    match = PLUGIN_VERSION_PATTERN.search(source)
    if not match:
        raise ValueError("Could not read FTK Mod Framework version from Plugin.cs")
    version = match.group(1)
    project = (source_root / "FTKModFramework" / "FTKModFramework.csproj").read_text(encoding="utf-8")
    if "<Version>" + version + "</Version>" not in project:
        raise ValueError("Plugin.cs and FTKModFramework.csproj versions differ")
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


RELEASE_FILES = (
    "FTKThunderstoreBootstrap.dll", "ftkmf-helper-windows-amd64.exe",
    "FTKModdedLauncher-windows-x64.zip", "FTKModFramework.dll",
)


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


def validate_launcher(data, version, artifacts):
    import io
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        names = archive.namelist()
        if len(names) > 2000 or len({name.lower() for name in names}) != len(names):
            raise ValueError("Invalid launcher archive inventory")
        root = "For The King Modded/"
        total = 0
        for entry in archive.infolist():
            name = entry.filename
            total += entry.file_size
            if (not name.startswith(root) or ".." in Path(name).parts or "\\" in name
                    or ":" in name or (entry.external_attr >> 16) & 0o170000 == 0o120000
                    or total > 300 * 1024 * 1024):
                raise ValueError("Unsafe launcher archive entry: " + name)
            if entry.is_dir():
                continue
            relative = name[len(root):]
            allowed = {"FtkModdedLauncher.exe", "FtkModdedLauncher.exe.config", "FTKModFramework.dll",
                       "ftkmf-launcher-helper.exe", "bundle-manifest.json", "install.ps1", "README.md"}
            if relative not in allowed and not (relative.startswith("assets/steam/") and relative.endswith(".png")):
                raise ValueError("Unexpected launcher payload: " + name)
        for required in ("FtkModdedLauncher.exe", "FtkModdedLauncher.exe.config", "install.ps1"):
            if not archive.read(root + required):
                raise ValueError("Empty required launcher file: " + required)
        manifest = json.loads(archive.read(root + "bundle-manifest.json"))
        dll = archive.read(root + "FTKModFramework.dll")
        helper = archive.read(root + "ftkmf-launcher-helper.exe")
        if (manifest.get("schemaVersion") != 1 or manifest.get("frameworkVersion") != version
                or dll != artifacts["FTKModFramework.dll"]
                or helper != artifacts["ftkmf-helper-windows-amd64.exe"]
                or manifest.get("dllSha256") != sha256(dll)
                or manifest.get("helpers", {}).get("ftkmf-helper-windows-amd64.exe") != sha256(helper)):
            raise ValueError("Launcher bundle does not match the framework/helper release pair")


def framework_package(tag_version, namespace, release_dir, output, source_root):
    if plugin_version(source_root) != tag_version:
        raise ValueError("Framework tag version does not match its source")
    if not (source_root / "launcher" / "helper" / "thunderstore.go").is_file():
        raise ValueError("This release predates the Thunderstore launcher handoff")
    artifacts = release_files(Path(release_dir))
    archive = artifacts["FTKModdedLauncher-windows-x64.zip"]
    validate_launcher(archive, tag_version, artifacts)
    # Keep the actual framework DLL opaque to the manager's BepInEx discovery.
    # Only the setup plugin loads in the initial profile.
    prefix = "plugins/FTKSetup/"
    files = {
        prefix + "FTKThunderstoreBootstrap.dll": artifacts["FTKThunderstoreBootstrap.dll"],
        prefix + "ftkmf-bootstrap-helper.exe": artifacts["ftkmf-helper-windows-amd64.exe"],
        prefix + "FTKModdedLauncher-windows-x64.zip": archive,
        prefix + "launcher.sha256": (sha256(archive) + "\n").encode("ascii"),
    }
    return write_package(
        output, namespace, FRAMEWORK_PACKAGE_NAME, tag_version,
        "Initial Windows setup for FTK Mod Framework. Continue playing and managing mods through the FTK Modded Launcher.",
        "https://github.com/jarlbrak/ftk-mod-framework",
        {FRAMEWORK_DEPENDENCY: FRAMEWORK_DEPENDENCY_VERSION},
        ROOT / "FTKModFramework" / "thunderstore" / "README.md",
        ROOT / "FTKModFramework" / "thunderstore" / "icon.png", files)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True, help="Stable framework GitHub release tag: vX.Y.Z")
    parser.add_argument("--namespace", default="JarlBrak", help="Thunderstore team namespace")
    parser.add_argument("--release-dir", type=Path, required=True, help="Downloaded release assets and SHA256SUMS")
    parser.add_argument("--source-root", type=Path, default=ROOT, help="Exact release source checkout")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    match = TAG_PATTERN.fullmatch(args.tag)
    if not match:
        parser.error("only stable framework tags vX.Y.Z are supported; mods use the existing marketplace")
    result = framework_package(match.group("version"), args.namespace,
                               args.release_dir, args.output, args.source_root.resolve())
    print(json.dumps([result], indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
