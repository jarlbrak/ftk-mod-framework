#!/usr/bin/env python3
"""Verify the reconciled exact-route Gloamfin Kraken canonical archive."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import verify_kraken_production_archive as production
import verify_model_validation_archive as generic


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "art-experiments/gloamfin-kraken/live-validation-v2-canonical"


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify(archive: Path = DEFAULT) -> dict:
    archive = archive.resolve()
    generic_result = generic.verify_archive(archive)
    require(generic_result["metadataMappings"] == 3, "three raw capture mappings required")
    require(generic_result["selectedPNGs"] == 23, "22 combat frames plus portrait required")
    require(generic_result["rootVisualReviewPinned"] is True, "root visual review pin required")

    integrity = json.loads((archive / "integrity.json").read_text())
    require(integrity["schema"] == "ftkmf.canonical-model-route-integrity.v1", "integrity schema changed")
    require(integrity["validationSha256"] == sha256(archive / "validation.json"), "validation integrity pin changed")
    expected = {item["path"]: item for item in integrity["files"]}
    actual = {str(path.relative_to(archive)) for path in archive.rglob("*")
              if path.is_file() and path.name != "integrity.json"}
    require(set(expected) == actual, "full archive integrity coverage changed")
    for relative, pointer in expected.items():
        candidate = archive / relative
        require(candidate.stat().st_size == pointer["bytes"] and sha256(candidate) == pointer["sha256"],
                f"archive file changed: {relative}")

    validation = json.loads((archive / "validation.json").read_text())
    require(validation["schema"] == "ftkmf.canonical-model-route-validation.v1", "validation schema changed")
    require(validation["native_chassis"] == "krakenHead" and validation["resourcePrefab"] == "enkrakenhead",
            "exact route identity changed")
    require(validation["renderer_paths"] == ["krakenHead"] and validation["source_renderer_ids"] == [121260],
            "exact source assignment changed")
    require(validation["ordinaryAttack"]["hpBefore"] == 324
            and validation["ordinaryAttack"]["hpAfter"] == 316, "ordinary hit changed")
    require(validation["fixtureDeath"]["action"] == "KillSingle"
            and validation["fixtureDeath"]["hpAfter"] == 0, "fixture death boundary changed")
    require(validation["finalReady"] == {"ok": True, "level": 0, "room": 2, "readyButtons": 1},
            "strict Ready evidence changed")
    require(validation["portrait"]["status"] == "pass"
            and validation["portrait"]["activeInitiativeImages"] == 6, "portrait acceptance changed")
    require(all(validation["productionGameplay"][name] is True for name in
                ("ordinaryLethal", "ordinaryPartyLossVictory", "naturalTeardownBothRuns")),
            "production gameplay authority changed")

    for name, pointer in validation["components"].items():
        candidate = archive / pointer["path"]
        require(candidate.is_file() and sha256(candidate) == pointer["sha256"], f"component changed: {name}")
    production_result = production.verify_archive(archive / "production-campaign")
    require(production_result["campaignStatus"] == "production_observation_campaign_satisfied",
            "production campaign no longer verifies")
    visual = json.loads((archive / validation["components"]["visualReview"]["path"]).read_text())
    portrait = json.loads((archive / validation["components"]["portraitFollowup"]["path"]).read_text())
    require(visual["review"]["portrait"]["status"] == "fail", "historical portrait failure was rewritten")
    require(portrait["portrait"]["review"]["status"] == "pass", "portrait follow-up review changed")
    require(portrait["reconciliation"]["preFixReview"].endswith("kraken-production-visual-v1/review.json"),
            "portrait reconciliation pointer changed")
    return {
        "ok": True,
        "status": validation["status"],
        "files": len(actual),
        "selectedPNGs": generic_result["selectedPNGs"],
        "ordinaryLethal": True,
        "portrait": "pass",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, nargs="?", default=DEFAULT)
    args = parser.parse_args()
    print(json.dumps(verify(args.archive), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
