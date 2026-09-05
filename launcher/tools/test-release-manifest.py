#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("release-manifest.py")
HELPERS = ("ftkmf-helper-macos-universal", "ftkmf-helper-linux-amd64", "ftkmf-helper-linux-arm64", "ftkmf-helper-windows-amd64.exe")


class ManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "FTKModFramework.dll").write_bytes(b"MZfixture")
        for name in HELPERS:
            (self.root / name).write_bytes(name.encode())
        self.policy = self.root / "policy.json"
        self.policy.write_text(json.dumps({"schemaVersion": 1, "helperProtocol": 1, "autoUpdateFrom": ">=0.1.0 <0.2.0", "gameAssemblySha256": ["a" * 64]}))

    def tearDown(self):
        self.temp.cleanup()

    def run_manifest(self, mode="release", version="0.1.1", extra=()):
        return subprocess.run([sys.executable, str(SCRIPT), mode, str(self.root), version, "--policy", str(self.policy), *extra], capture_output=True, text=True)

    def test_release_hashes_finished_assets(self):
        result = self.run_manifest()
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = json.loads((self.root / "update.json").read_text())
        self.assertEqual(manifest["frameworkVersion"], "0.1.1")
        self.assertEqual(set(manifest["assets"]), {"FTKModFramework.dll", *HELPERS})
        for name, asset in manifest["assets"].items():
            data = (self.root / name).read_bytes()
            self.assertEqual(asset, {"sha256": hashlib.sha256(data).hexdigest(), "size": len(data)})

    def test_proton_bundle_records_both_platform_helpers(self):
        (self.root / "ftkmf-launcher-helper").write_bytes(b"linux")
        (self.root / "ftkmf-launcher-helper.exe").write_bytes(b"windows")
        result = self.run_manifest("bundle", extra=("--platform", "linux-amd64"))
        self.assertEqual(result.returncode, 0, result.stderr)
        manifest = json.loads((self.root / "bundle-manifest.json").read_text())
        self.assertEqual(set(manifest["helpers"]), {"ftkmf-helper-linux-amd64", "ftkmf-helper-windows-amd64.exe"})
        self.assertEqual(manifest["helpers"]["ftkmf-helper-windows-amd64.exe"], hashlib.sha256(b"windows").hexdigest())

    def test_reject_ambiguous_version_and_policy(self):
        for version in ("0.1.1-preview", "01.1.1", "0.1.1/evil", "0.1"):
            self.assertNotEqual(self.run_manifest(version=version).returncode, 0)
        policy = json.loads(self.policy.read_text())
        for field, value in (("gameAssemblySha256", []), ("autoUpdateFrom", "*"), ("helperProtocol", 2)):
            invalid = dict(policy, **{field: value})
            self.policy.write_text(json.dumps(invalid))
            self.assertNotEqual(self.run_manifest().returncode, 0)

    def test_incomplete_release_cannot_emit_manifest(self):
        (self.root / HELPERS[0]).rename(self.root / "saved-helper")
        self.assertNotEqual(self.run_manifest().returncode, 0)
        self.assertFalse((self.root / "update.json").exists())


if __name__ == "__main__":
    unittest.main()
