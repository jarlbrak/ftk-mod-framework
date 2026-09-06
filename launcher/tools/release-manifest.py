#!/usr/bin/env python3
"""Generate integrity metadata from finished release bytes. Maintainer tooling only."""
import argparse
import hashlib
import json
import re
from pathlib import Path

HELPERS = (
    "ftkmf-helper-macos-universal", "ftkmf-helper-linux-amd64",
    "ftkmf-helper-linux-arm64", "ftkmf-helper-windows-amd64.exe",
)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("release", "bundle"))
    parser.add_argument("directory", type=Path)
    parser.add_argument("version")
    parser.add_argument("--policy", type=Path)
    parser.add_argument("--platform", choices=("macos-universal", "linux-amd64", "linux-arm64", "windows-amd64"))
    args = parser.parse_args()
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", args.version):
        parser.error("version must be a numeric X.Y.Z release version")
    root = args.directory
    dll = root / "FTKModFramework.dll"
    if dll.read_bytes()[:2] != b"MZ":
        parser.error("framework DLL is not a PE image")
    if args.mode == "bundle":
        if not args.platform:
            parser.error("bundle mode requires --platform")
        helpers = {}
        native = root / "ftkmf-launcher-helper"
        windows = root / "ftkmf-launcher-helper.exe"
        if native.is_file():
            helpers["ftkmf-helper-" + args.platform] = digest(native)
        if windows.is_file():
            helpers["ftkmf-helper-windows-amd64.exe"] = digest(windows)
        if not helpers:
            parser.error("bundle contains no native helper")
        write_json(root / "bundle-manifest.json", {
            "schemaVersion": 1, "frameworkVersion": args.version,
            "dllSha256": digest(dll), "helpers": helpers,
        })
        return
    if not args.policy:
        parser.error("release mode requires --policy")
    policy = json.loads(args.policy.read_text(encoding="utf-8"))
    if set(policy) != {"schemaVersion", "helperProtocol", "autoUpdateFrom", "gameAssemblySha256"}:
        parser.error("unexpected or missing update policy fields")
    if policy["schemaVersion"] != 1 or policy["helperProtocol"] != 1:
        parser.error("unsupported update policy protocol")
    hashes = policy["gameAssemblySha256"]
    if not isinstance(hashes, list) or not hashes or any(not isinstance(h, str) or not re.fullmatch(r"[0-9a-f]{64}", h) for h in hashes):
        parser.error("gameAssemblySha256 must contain verified game assembly hashes")
    # Same deliberately narrow grammar as the launcher's marketplace version ranges.
    version = r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)"
    constraint = r"(?:" + version + r"|>=" + version + r" <" + version + r")"
    if not isinstance(policy["autoUpdateFrom"], str) or not re.fullmatch(constraint, policy["autoUpdateFrom"]):
        parser.error("autoUpdateFrom must be an exact X.Y.Z or >=X.Y.Z <X.Y.Z range")
    assets = {}
    for name in ("FTKModFramework.dll",) + HELPERS:
        path = root / name
        assets[name] = {"sha256": digest(path), "size": path.stat().st_size}
    write_json(root / "update.json", dict(policy, frameworkVersion=args.version, assets=assets))


if __name__ == "__main__":
    main()
