#!/usr/bin/env python3
"""Prepare a fresh macOS reporting proof copy. Does not launch or alter the source."""
import argparse
import hashlib
import json
import plistlib
import re
import shutil
from pathlib import Path

import UnityPy


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def prepare(source, destination, token):
    source = source.resolve(strict=True)
    destination = destination.absolute()
    if destination.exists() or destination.is_symlink():
        raise ValueError("Destination must be new; existing copies are never overwritten")
    if destination.parent.name != "scratch" or destination.parent.resolve() != destination.parent:
        raise ValueError("Destination must be directly beneath a real scratch directory")
    if destination.is_relative_to(source):
        raise ValueError("Destination cannot be inside the source installation")
    if not re.fullmatch(r"[a-z][a-z0-9]{3,24}", token):
        raise ValueError("Use a unique lowercase alphanumeric identity token starting with a letter")
    bundle = "com.ftkmf.reporting.proof." + token
    company, product = "FTKReportingProof", token
    library = Path.home() / "Library"
    data = library / "Application Support" / company / product
    fallback = library / "Application Support" / bundle
    prefs = library / "Preferences" / (bundle + ".plist")
    if any(p.exists() or p.is_symlink() for p in (data, fallback, prefs)):
        raise ValueError("Profile identity already exists; choose a fresh token")
    app = source / "FTK.app"
    globals_relative = Path("Contents/Resources/Data/globalgamemanagers")
    globals_source = app / globals_relative
    managed_relative = Path("Contents/Resources/Data/Managed/Assembly-CSharp.dll")
    before = {"globals": digest(globals_source), "assembly": digest(app / managed_relative)}
    for required in (app, source / "BepInEx/core", source / "run_bepinex.sh", source / "libdoorstop.dylib"):
        if not required.exists():
            raise ValueError("Missing source component: " + str(required))
    destination.mkdir()
    # Copy bytes, never hard-link mutable assets or share plugin/config directories.
    shutil.copytree(app, destination / "FTK.app", symlinks=False)
    shutil.copytree(source / "BepInEx/core", destination / "BepInEx/core", symlinks=False)
    for name in ("run_bepinex.sh", "libdoorstop.dylib"):
        shutil.copy2(source / name, destination / name)
    for name in ("plugins", "config", "patchers"):
        (destination / "BepInEx" / name).mkdir()
    # Keep the app ID available, but the proof plugin must suppress Steam init/relaunch.
    if (source / "steam_appid.txt").exists():
        shutil.copy2(source / "steam_appid.txt", destination / "steam_appid.txt")
    plist = destination / "FTK.app/Contents/Info.plist"
    value = plistlib.loads(plist.read_bytes())
    value["CFBundleIdentifier"] = bundle
    value["CFBundleName"] = "FTK Reporting Proof " + token
    value["CFBundleDisplayName"] = value["CFBundleName"]
    plist.write_bytes(plistlib.dumps(value))
    globals_copy = destination / "FTK.app" / globals_relative
    env = UnityPy.load(str(globals_copy))
    settings = [obj for obj in env.objects if obj.type.name == "PlayerSettings"]
    if len(settings) != 1:
        raise ValueError("Expected one serialized PlayerSettings object")
    tree = settings[0].read_typetree()
    tree["companyName"], tree["productName"] = company, product
    settings[0].save_typetree(tree)
    globals_copy.write_bytes(env.file.save())
    reread = UnityPy.load(str(globals_copy))
    tree = next(obj.read_typetree() for obj in reread.objects if obj.type.name == "PlayerSettings")
    assert (tree["companyName"], tree["productName"]) == (company, product)
    assert digest(globals_source) == before["globals"]
    assert digest(app / managed_relative) == before["assembly"]
    assert digest(destination / "FTK.app" / managed_relative) == before["assembly"]
    record = {"schemaVersion": 1, "bundle": bundle, "company": company, "product": product,
              "sourceHashes": before, "isolatedGlobalsSha256": digest(globals_copy),
              "steamInitializationMustBeBlocked": True}
    (destination / "isolation.json").write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps(record, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--token", required=True)
    args = parser.parse_args()
    prepare(args.source, args.destination, args.token)
