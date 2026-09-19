"""Skip helpers for tests that read gitignored local inputs.

Several current-ledger and archive tests read extracted game data under
`scratch/`, bulk capture media under `art-experiments/` and `docs/evidence/`,
or build outputs. None of that is distributed with the repository, so a clean
clone must skip those tests with a reason that names the absent path instead
of failing with FileNotFoundError. The assertions themselves are unchanged:
when the inputs are present the tests run exactly as before.
"""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
from typing import Any, Callable, Iterable
import unittest


ROOT = Path(__file__).resolve().parents[2]
REQUIREMENTS = "tools/ai-model-pipeline/requirements.txt"


def _relative(path: Path | str) -> str:
    candidate = Path(path)
    if candidate.is_absolute():
        try:
            return candidate.relative_to(ROOT).as_posix()
        except ValueError:
            return candidate.as_posix()
    return candidate.as_posix()


def missing_inputs(*paths: Path | str) -> list[str]:
    """Return the repository-relative paths that do not exist locally."""
    missing = []
    for path in paths:
        candidate = Path(path)
        if not candidate.is_absolute():
            candidate = ROOT / candidate
        if not candidate.exists():
            missing.append(_relative(candidate))
    return missing


def skip_reason(missing: Iterable[str]) -> str:
    listed = ", ".join(missing)
    return f"local-only test: missing gitignored input(s) {listed}"


def require_local_inputs(*paths: Path | str) -> None:
    """Raise SkipTest naming every absent input. Safe at module, class, or test level."""
    missing = missing_inputs(*paths)
    if missing:
        raise unittest.SkipTest(skip_reason(missing))


def skip_without_local_inputs(*paths: Path | str) -> Callable[[Any], Any]:
    """Decorator form of require_local_inputs for a TestCase class or test method."""
    missing = missing_inputs(*paths)
    if missing:
        return unittest.skip(skip_reason(missing))
    return lambda target: target


def require_python_modules(*names: str) -> None:
    """Skip when an optional pipeline dependency from requirements.txt is not installed."""
    missing = [name for name in names if importlib.util.find_spec(name) is None]
    if missing:
        listed = ", ".join(missing)
        raise unittest.SkipTest(
            f"local-only test: missing Python package(s) {listed}; install {REQUIREMENTS}")


def pinned_paths(document: Any, base: Path) -> list[Path]:
    """Collect every {"path": ..., "sha256": ...} pointer in a JSON document, relative to base."""
    found: list[Path] = []

    def walk(node: Any) -> None:
        if isinstance(node, dict):
            if isinstance(node.get("path"), str) and "sha256" in node:
                found.append(base / node["path"])
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(document)
    return found


def integrity_files(archive: Path | str) -> list[Path]:
    """Files pinned by an archive's integrity.json, in either the map or the list layout."""
    root = ROOT / archive if not Path(archive).is_absolute() else Path(archive)
    manifest = root / "integrity.json"
    if not manifest.is_file():
        return [manifest]
    files = json.loads(manifest.read_text()).get("files")
    if isinstance(files, dict):
        return [root / name for name in files]
    if isinstance(files, list):
        return pinned_paths(files, root)
    return []


def lossless_archive_files(archive: Path | str) -> list[Path]:
    """Archive-relative files named by validation.json losslessMappings or the legacy sidecar."""
    root = ROOT / archive if not Path(archive).is_absolute() else Path(archive)
    validation = root / "validation.json"
    if not validation.is_file():
        return [validation]
    document = json.loads(validation.read_text())
    mappings = document.get("losslessMappings")
    if not isinstance(mappings, list):
        sidecar = root / "integrity.json"
        if not sidecar.is_file():
            return [sidecar]
        mappings = json.loads(sidecar.read_text()).get("metadata") or []
    return [root / row["archive"] for row in mappings if isinstance(row, dict) and "archive" in row]


def evidence_paths(document_path: Path | str) -> list[Path]:
    """Repository-relative files pinned by a docs/evidence JSON document."""
    path = ROOT / document_path if not Path(document_path).is_absolute() else Path(document_path)
    if not path.is_file():
        return [path]
    return pinned_paths(json.loads(path.read_text()), ROOT)
