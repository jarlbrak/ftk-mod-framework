#!/usr/bin/env python3
"""Inventory independently verifiable FTK model-validation archives.

This is an artifact-preservation report. It does not evaluate model behavior,
art quality, source assignment, or the structured validation gates.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys
from typing import Any

import verify_model_validation_archive as verifier
import verify_hearthveil_canonical_archive as hearthveil_verifier
import verify_wildbloom_canonical_archive as wildbloom_verifier


Json = dict[str, Any]


def find_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir():
            return candidate
    raise ValueError(f"could not locate FTK workspace above {start}")


def relative(root: Path, path: Path) -> str:
    return str(path.resolve().relative_to(root.resolve()))


def archive_paths(experiments: Path) -> list[Path]:
    return sorted(
        path.parent
        for path in experiments.rglob("validation.json")
        if path.parent.name.startswith("live-validation") and "metadata" not in path.parts
    )


def build_report(root: Path, experiments: Path, *, check_video_metadata: bool = False) -> Json:
    records: list[Json] = []
    for archive in archive_paths(experiments):
        try:
            validation = json.loads((archive / "validation.json").read_text())
            if validation.get("schema") == "ftkmf.player-canonical-route.v1":
                if validation.get("topologyGroup") == "45c7a9b9fb730195":
                    specialized = wildbloom_verifier.verify(archive)
                    metadata_mappings = 15
                    selected_pngs = 8
                else:
                    specialized = hearthveil_verifier.verify(archive)
                    metadata_mappings = 4
                    selected_pngs = 4
                report = {
                    "layout": "canonical-player-route",
                    "metadataMappings": metadata_mappings,
                    "sourceImagePins": 0,
                    "captureImagePins": 0,
                    "assetPins": 0,
                    "selectedPNGs": selected_pngs,
                    "rootVisualReviewPinned": True,
                    "videos": 0,
                    "videoMetadataChecked": False,
                    "specializedStatus": specialized["status"],
                }
            else:
                report = verifier.verify_archive(archive, check_video_metadata=check_video_metadata)
        except (ValueError, verifier.ArchiveIntegrityError) as error:
            records.append({
                "archive": relative(root, archive),
                "validation": relative(root, archive / "validation.json"),
                "validationSha256": verifier.sha256_file(archive / "validation.json"),
                "status": "integrity_unverified",
                "reason": str(error),
            })
            continue
        records.append({
            "archive": relative(root, archive),
            "validation": relative(root, archive / "validation.json"),
            "validationSha256": verifier.sha256_file(archive / "validation.json"),
            "status": "artifact_integrity_verified",
            "layout": report["layout"],
            "metadataMappings": report["metadataMappings"],
            "sourceImagePins": report["sourceImagePins"],
            "captureImagePins": report["captureImagePins"],
            "assetPins": report["assetPins"],
            "selectedPNGs": report["selectedPNGs"],
            "rootVisualReviewPinned": report["rootVisualReviewPinned"],
            "videos": report["videos"],
            "videoMetadataChecked": report["videoMetadataChecked"],
        })
    verified = [row for row in records if row["status"] == "artifact_integrity_verified"]
    unverified = [row for row in records if row["status"] != "artifact_integrity_verified"]
    layouts: dict[str, int] = {}
    for row in verified:
        layout = str(row.get("layout"))
        layouts[layout] = layouts.get(layout, 0) + 1
    return {
        "schemaVersion": 1,
        "scope": (
            "Artifact integrity for archived live-model validation records. A verified record has intact "
            "mapped metadata and declared image, selected-frame, and media artifacts supported by its layout. "
            "It is not a claim that a model bound, animated, played, or met an art-review standard."
        ),
        "inputs": {
            "experiments": relative(root, experiments),
            "videoMetadataChecked": check_video_metadata,
        },
        "summary": {
            "archives": len(records),
            "artifactIntegrityVerified": len(verified),
            "integrityUnverified": len(unverified),
            "layouts": layouts,
            "verifiedArchivesWithPinnedRootVisualReview": sum(row["rootVisualReviewPinned"] is True for row in verified),
        },
        "records": records,
        "limits": [
            "An integrity-unverified historical record may still contain useful evidence; its preserved artifact layout needs repair or a fresh replacement before this report can verify it.",
            "Artifact integrity never transfers evidence between source assignments, revisions, routes, or skeletons.",
            "The validation-gate ledger remains the source for recorded structured binding, motion, gameplay, and player-preview evidence shapes.",
        ],
    }


def markdown(report: Json) -> str:
    summary = report["summary"]
    lines = [
        "# Model-validation archive integrity",
        "",
        "This ledger checks whether each live-validation archive can be independently verified as preserved artifacts. It does not accept binding, animation, gameplay, or art quality.",
        "",
        "| Archives | Artifact integrity verified | Integrity unverified | Verified archives with pinned root visual review |",
        "|---:|---:|---:|---:|",
        "| {archives} | {artifactIntegrityVerified} | {integrityUnverified} | {verifiedArchivesWithPinnedRootVisualReview} |".format(**summary),
        "",
        "## Archives",
        "",
        "| Archive | Status | Layout or preservation gap | Metadata mappings | Source image pins | Selected PNGs | Videos |",
        "|---|---|---|---:|---:|---:|---:|",
    ]
    for row in report["records"]:
        if row["status"] == "artifact_integrity_verified":
            detail = row.get("layout", "")
            metadata = row.get("metadataMappings", 0)
            sources = row.get("sourceImagePins", 0)
            selected = row.get("selectedPNGs", 0)
            videos = row.get("videos", 0)
        else:
            detail = row["reason"].replace("|", "\\|")
            metadata = sources = selected = videos = "n/a"
        lines.append(
            f"| `{row['archive']}` | `{row['status']}` | {detail} | {metadata} | {sources} | {selected} | {videos} |"
        )
    lines.extend([
        "",
        "A verified archive can still be limited by the observations recorded in its own review. An unverified archive is not a behavior failure; preserve it and create a fresh immutable archive when the missing artifact link matters.",
        "",
    ])
    return "\n".join(lines)


def write_output(path: Path, content: str, *, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise ValueError(f"refusing to overwrite existing output: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--experiments", type=Path, default=Path("art-experiments"))
    parser.add_argument("--output-json", type=Path, default=Path("docs/model-validation-archive-integrity.json"))
    parser.add_argument("--output-markdown", type=Path, default=Path("docs/MODEL-VALIDATION-ARCHIVE-INTEGRITY.md"))
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--check-video-metadata", action="store_true")
    parser.add_argument("--fail-on-unverified", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = find_root(Path.cwd())
    experiments = args.experiments if args.experiments.is_absolute() else root / args.experiments
    json_path = args.output_json if args.output_json.is_absolute() else root / args.output_json
    markdown_path = args.output_markdown if args.output_markdown.is_absolute() else root / args.output_markdown
    try:
        report = build_report(root, experiments, check_video_metadata=args.check_video_metadata)
        write_output(json_path, json.dumps(report, indent=2) + "\n", overwrite=args.overwrite)
        write_output(markdown_path, markdown(report), overwrite=args.overwrite)
    except (OSError, ValueError, verifier.ArchiveIntegrityError) as error:
        print(json.dumps({"status": "FAIL", "errors": [str(error)]}, indent=2), file=sys.stderr)
        return 1
    print(json.dumps({"status": "PASS", "summary": report["summary"], "json": relative(root, json_path), "markdown": relative(root, markdown_path)}, indent=2))
    return 1 if args.fail_on_unverified and report["summary"]["integrityUnverified"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
