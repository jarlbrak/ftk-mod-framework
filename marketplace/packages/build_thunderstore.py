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
CONTENT_FOLDER = "FTKMFContent"
FRAMEWORK_PACKAGE_NAME = "FTKModFramework"
FRAMEWORK_DEPENDENCY = "BepInEx-BepInExPack_ForTheKing"
FRAMEWORK_DEPENDENCY_VERSION = "5.4.19001"

CONTENT_PACKAGES = {
    "paladin": {
        "package_name": "Paladin",
        "icon": "assets/paladin-guard-icon.png",
    },
    "thief": {
        "package_name": "Thief",
        "icon": "assets/thief-street-twins-icon.png",
    },
    "possum": {
        "package_name": "Possum",
        "icon": "thunderstore/icon.png",
    },
    "lore-store-unlocked": {
        "package_name": "LoreStoreUnlocked",
        "icon": "thunderstore/icon.png",
    },
}

TAG_PATTERN = re.compile(r"^(?:(?P<framework>v)|(?P<package>paladin|thief|possum|lore-store-unlocked)-v)(?P<version>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))$")
PLUGIN_VERSION_PATTERN = re.compile(r'public const string Version = "([0-9]+\.[0-9]+\.[0-9]+)";')


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


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


def require_thunderstore_discovery(source_root):
    manifest = (source_root / "FTKModFramework" / "Core" / "Data" / "ModManifest.cs").read_text(encoding="utf-8")
    discovery = (source_root / "FTKModFramework" / "Core" / "Data" / "ModDiscovery.cs").read_text(encoding="utf-8")
    if "ThunderstoreVersionNumber" not in manifest or "ThunderstoreContentFolderName" not in discovery:
        raise ValueError(
            "This framework release predates Thunderstore content discovery. "
            "Publish a new stable framework release after merging this integration.")


def validate_namespace(namespace):
    if not re.fullmatch(r"[A-Za-z0-9_]+", namespace or ""):
        raise ValueError("Thunderstore namespace must contain only letters, digits, and underscores")


def png_dimensions(data, label):
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(label + " is not a PNG")
    width, height = struct.unpack(">II", data[16:24])
    if (width, height) != (256, 256):
        raise ValueError(label + " must be exactly 256x256 pixels")


def add_file(files, archive_path, source_path):
    source_path = Path(source_path)
    if source_path.is_symlink() or not source_path.is_file():
        raise ValueError("Expected a regular non-symlink file: " + str(source_path))
    if source_path.suffix.lower() in {".dll", ".exe", ".so", ".dylib"} and source_path.name != "FTKModFramework.dll":
        raise ValueError("Refusing unexpected executable in package: " + str(source_path))
    archive_path = Path(archive_path).as_posix()
    if archive_path.startswith("/") or ".." in Path(archive_path).parts:
        raise ValueError("Unsafe package path: " + archive_path)
    files[archive_path] = source_path.read_bytes()


def referenced_thief_assets(content):
    found = set()

    def visit(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key in {"model", "texture", "icon"} and isinstance(child, str):
                    path = Path(child)
                    if path.is_absolute() or ".." in path.parts or not path.parts or path.parts[0] != "assets":
                        raise ValueError("Unsafe Thief asset reference: " + child)
                    found.add(path.as_posix())
                else:
                    visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(content)
    return sorted(found)


def source_content_files(slug, package_dir):
    files = {}
    add_file(files, "manifest.json", package_dir / "manifest.json")
    add_file(files, "content.json", package_dir / "content.json")

    if slug == "thief":
        refs = referenced_thief_assets(read_json(package_dir / "content.json"))
        paths = [package_dir / path for path in refs]
    elif slug in {"paladin", "possum"}:
        assets = package_dir / "assets"
        paths = sorted(assets.iterdir(), key=lambda path: path.as_posix())
    else:
        paths = []

    for path in paths:
        if path.suffix.lower() not in {".png", ".glb"}:
            raise ValueError("Unsupported runtime asset: " + str(path))
        add_file(files, path.relative_to(package_dir), path)
    return files


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


def framework_package(tag_version, namespace, framework_dll, output, source_root):
    source_version = plugin_version(source_root)
    if source_version != tag_version:
        raise ValueError("Framework tag version " + tag_version + " does not match source version " + source_version)
    require_thunderstore_discovery(source_root)
    dll = Path(framework_dll)
    if dll.name != "FTKModFramework.dll" or not dll.is_file() or dll.is_symlink():
        raise ValueError("Expected the published FTKModFramework.dll release asset")
    dll_bytes = dll.read_bytes()
    if not dll_bytes.startswith(b"MZ"):
        raise ValueError("Framework release asset is not a PE/.NET assembly")
    files = {"plugins/FTKModFramework.dll": dll_bytes}
    return write_package(
        output, namespace, FRAMEWORK_PACKAGE_NAME, tag_version,
        "BepInEx 5 framework for the original For The King. Loads curated JSON content packages.",
        "https://github.com/jarlbrak/ftk-mod-framework",
        {FRAMEWORK_DEPENDENCY: FRAMEWORK_DEPENDENCY_VERSION},
        ROOT / "FTKModFramework" / "thunderstore" / "README.md",
        ROOT / "FTKModFramework" / "thunderstore" / "icon.png", files)


def compatible_framework_version(package, framework_version):
    current = version_tuple(framework_version)
    minimum = version_tuple(package["frameworkVersion"])
    match = re.fullmatch(r">=([0-9]+\.[0-9]+\.[0-9]+) <([0-9]+\.[0-9]+\.[0-9]+)", package["frameworkRange"])
    if not match:
        raise ValueError("Unsupported frameworkRange in catalog: " + package["frameworkRange"])
    upper = version_tuple(match.group(2))
    if current < minimum or current >= upper:
        raise ValueError("Thunderstore Framework " + framework_version + " is outside " + package["frameworkRange"])


def content_package(slug, tag_version, namespace, framework_version, output, source_root):
    package_config = CONTENT_PACKAGES[slug]
    package_dir = source_root / "marketplace" / "packages" / slug
    manifest = read_json(package_dir / "manifest.json")
    if manifest.get("version") != tag_version:
        raise ValueError(slug + " release tag version does not match its runtime manifest")
    # The builder checkout supplies the current production catalog. Release tags can predate
    # the catalog commit that promotes their package version, so the catalog is intentionally
    # read from ROOT while runtime content is read from the exact release source root.
    catalog = read_json(ROOT / "marketplace" / "catalog.json")
    descriptor = next((item for item in catalog["packages"] if item["packageId"] == "ftkmf." + slug), None)
    if descriptor is None:
        raise ValueError("Refusing to publish a package absent from marketplace/catalog.json: " + slug)
    if descriptor["version"] != tag_version:
        raise ValueError(slug + " release version does not match marketplace/catalog.json")
    if descriptor["modGuid"] != manifest.get("modGuid") or descriptor["name"] != manifest.get("name"):
        raise ValueError(slug + " catalog identity differs from its runtime manifest")
    compatible_framework_version(descriptor, framework_version)

    source_files = source_content_files(slug, package_dir)
    nested_files = {"plugins/" + CONTENT_FOLDER + "/" + name: data
                    for name, data in source_files.items()}
    icon_path = ROOT / "marketplace" / "packages" / slug / package_config["icon"]
    readme_path = ROOT / "marketplace" / "packages" / slug / "thunderstore" / "README.md"
    return write_package(
        output, namespace, package_config["package_name"], tag_version,
        descriptor["description"], descriptor["sourceUrl"],
        {FRAMEWORK_PACKAGE_NAME: framework_version}, readme_path, icon_path, nested_files)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", help="GitHub release tag, such as v1.5.0 or paladin-v1.4.0")
    parser.add_argument("--all-content", action="store_true", help="Build local candidates for catalog content packages")
    parser.add_argument("--namespace", default="JarlBrak", help="Thunderstore team namespace")
    parser.add_argument("--framework-version", help="Published compatible FTKModFramework package version")
    parser.add_argument("--framework-dll", type=Path, help="Published FTKModFramework.dll for a framework release tag")
    parser.add_argument("--source-root", type=Path, default=ROOT,
                        help="Checkout containing the exact published GitHub release source")
    parser.add_argument("--output", type=Path, required=True, help="Directory for the local candidate packages")
    args = parser.parse_args()
    if bool(args.tag) == bool(args.all_content):
        parser.error("provide exactly one of --tag or --all-content")
    validate_namespace(args.namespace)
    source_root = args.source_root.resolve()

    results = []
    if args.all_content:
        framework_version = args.framework_version or plugin_version(source_root)
        for slug in CONTENT_PACKAGES:
            manifest = read_json(source_root / "marketplace" / "packages" / slug / "manifest.json")
            results.append(content_package(slug, manifest["version"], args.namespace,
                                           framework_version, args.output, source_root))
    else:
        match = TAG_PATTERN.fullmatch(args.tag)
        if not match:
            parser.error("unsupported tag; use vX.Y.Z or one of the published package tag prefixes")
        if match.group("framework"):
            if not args.framework_dll:
                parser.error("framework release packages require --framework-dll")
            results.append(framework_package(match.group("version"), args.namespace,
                                             args.framework_dll, args.output, source_root))
        else:
            if args.framework_dll:
                parser.error("--framework-dll is only valid for framework release tags")
            if not args.framework_version:
                parser.error("content package releases require --framework-version")
            results.append(content_package(match.group("package"), match.group("version"),
                                           args.namespace, args.framework_version, args.output, source_root))
    print(json.dumps(results, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
