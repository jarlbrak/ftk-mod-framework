"""Game-free checks for independently versioned thin bootstrap packages."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("thunderstore", Path(__file__).with_name("build_thunderstore.py"))
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class ThunderstoreTests(unittest.TestCase):
    def assets(self, root):
        artifacts = {name: b"MZfixture " + name.encode() for name in builder.RELEASE_FILES}
        for name, content in artifacts.items():
            (root / name).write_bytes(content)
        (root / "SHA256SUMS").write_text("".join(
            builder.sha256(content) + "  " + name + "\n" for name, content in artifacts.items()))

    def test_thin_bootstrap_only_and_deterministic(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            args = (builder.bootstrap_version(builder.ROOT), "Fixture", root, root / "output", builder.ROOT)
            result = builder.bootstrap_package(*args)
            self.assertEqual(result["sha256"], builder.bootstrap_package(*args)["sha256"])
            with zipfile.ZipFile(result["archive"]) as archive:
                self.assertIsNone(archive.testzip())
                self.assertEqual(set(archive.namelist()), {
                    "README.md", "manifest.json", "icon.png",
                    "plugins/FTKSetup/FTKThunderstoreBootstrap.dll",
                    "plugins/FTKSetup/ftkmf-bootstrap-helper.exe"})
                metadata = json.loads(archive.read("manifest.json"))
                self.assertEqual(metadata["version_number"], builder.bootstrap_version(builder.ROOT))
                self.assertEqual(metadata["dependencies"], ["BepInEx-BepInExPack_ForTheKing-5.4.19001"])

    def test_release_tampering_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            (root / "ftkmf-bootstrap-helper.exe").write_bytes(b"MZmodified")
            with self.assertRaisesRegex(ValueError, "checksum mismatch"):
                builder.release_files(root)

    def test_missing_checksum_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            (root / "SHA256SUMS").write_text("")
            with self.assertRaisesRegex(ValueError, "checksum mismatch"):
                builder.release_files(root)

    def test_bootstrap_version_must_match(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.assets(root)
            with self.assertRaisesRegex(ValueError, "independent source version"):
                builder.bootstrap_package("999.0.0", "Fixture", root, root / "out", builder.ROOT)

    def test_framework_content_and_preview_tags_rejected(self):
        self.assertIsNotNone(builder.TAG_PATTERN.fullmatch("bootstrap-v1.0.0"))
        for tag in ("paladin-v1.4.0", "v1.5.0", "bootstrap-v1.0.0-beta", "bootstrap-v01.0.0"):
            self.assertIsNone(builder.TAG_PATTERN.fullmatch(tag))


if __name__ == "__main__":
    unittest.main()
