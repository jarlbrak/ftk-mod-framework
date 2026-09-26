"""Game-free checks for the framework bootstrap distribution boundary."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("thunderstore", Path(__file__).with_name("build_thunderstore.py"))
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class ThunderstoreTests(unittest.TestCase):
    def assets(self, root, extra=None):
        dll, helper = b"MZframework fixture", b"MZhelper fixture"
        files = {
            "FtkModdedLauncher.exe": b"MZlauncher fixture",
            "FtkModdedLauncher.exe.config": b"config",
            "install.ps1": b"installer",
            "FTKModFramework.dll": dll,
            "ftkmf-launcher-helper.exe": helper,
            "bundle-manifest.json": json.dumps({
                "schemaVersion": 1, "frameworkVersion": builder.plugin_version(builder.ROOT),
                "dllSha256": builder.sha256(dll),
                "helpers": {"ftkmf-helper-windows-amd64.exe": builder.sha256(helper)},
            }).encode(),
        }
        if extra:
            files[extra] = b"unwanted"
        archive = io.BytesIO()
        with zipfile.ZipFile(archive, "w") as output:
            for name, content in files.items():
                output.writestr("For The King Modded/" + name, content)
        artifacts = {
            "FTKModFramework.dll": dll, "ftkmf-helper-windows-amd64.exe": helper,
            "FTKThunderstoreBootstrap.dll": b"MZbootstrap fixture",
            "FTKModdedLauncher-windows-x64.zip": archive.getvalue(),
        }
        for name, content in artifacts.items():
            (root / name).write_bytes(content)
        (root / "SHA256SUMS").write_text("".join(
            builder.sha256(content) + "  " + name + "\n" for name, content in artifacts.items()))
        return artifacts

    def test_bootstrap_only_and_deterministic(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            arguments = (builder.plugin_version(builder.ROOT), "Fixture", root, root / "output", builder.ROOT)
            result = builder.framework_package(*arguments)
            self.assertEqual(result["sha256"], builder.framework_package(*arguments)["sha256"])
            with zipfile.ZipFile(result["archive"]) as archive:
                self.assertIsNone(archive.testzip())
                dlls = [name for name in archive.namelist() if name.endswith(".dll")]
                self.assertEqual(dlls, ["plugins/FTKSetup/FTKThunderstoreBootstrap.dll"])
                metadata = json.loads(archive.read("manifest.json"))
                self.assertEqual(metadata["dependencies"], ["BepInEx-BepInExPack_ForTheKing-5.4.19001"])
                payload = archive.read("plugins/FTKSetup/FTKModdedLauncher-windows-x64.zip")
                self.assertEqual(archive.read("plugins/FTKSetup/launcher.sha256").strip().decode(), hashlib.sha256(payload).hexdigest())

    def test_release_asset_tampering_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            (root / "ftkmf-helper-windows-amd64.exe").write_bytes(b"MZmodified")
            with self.assertRaisesRegex(ValueError, "checksum mismatch"):
                builder.release_files(root)

    def test_game_assemblies_and_traversal_rejected(self):
        for extra in ("Assembly-CSharp.dll", "../outside.exe", "other.exe"):
            with self.subTest(extra=extra), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                artifacts = self.assets(root, extra)
                with self.assertRaises(ValueError):
                    builder.validate_launcher(artifacts["FTKModdedLauncher-windows-x64.zip"], builder.plugin_version(builder.ROOT), artifacts)

    def test_framework_helper_pair_must_match_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            artifacts = self.assets(Path(temporary))
            artifacts["ftkmf-helper-windows-amd64.exe"] = b"MZdifferent helper"
            with self.assertRaisesRegex(ValueError, "release pair"):
                builder.validate_launcher(artifacts["FTKModdedLauncher-windows-x64.zip"], builder.plugin_version(builder.ROOT), artifacts)

    def test_content_and_prerelease_tags_rejected(self):
        for tag in ("paladin-v1.4.0", "v1.5.0-beta", "v01.5.0"):
            self.assertIsNone(builder.TAG_PATTERN.fullmatch(tag))


if __name__ == "__main__":
    unittest.main()
