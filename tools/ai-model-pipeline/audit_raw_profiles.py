#!/usr/bin/env python3
"""Roundtrip one native representative of every raw exact rig profile locally.

Includes non-CEL renderers. No-fingerprint renderers remain explicit exclusions;
this audit does not establish original-art or live-animation support.
"""

import argparse
import json
from pathlib import Path


def select_profiles(inventory, extra_only=False):
    known = {r["rig_profile_fingerprint"] for r in inventory["candidate_profiles"]}
    representatives = {}
    excluded = []
    for renderer in inventory["renderers"]:
        fingerprint = renderer.get("rig_profile_fingerprint")
        if not fingerprint:
            excluded.append(
                {
                    key: renderer.get(key)
                    for key in (
                        "renderer_path_id",
                        "mesh_path_id",
                        "joint_count",
                        "bindpose_count",
                        "errors",
                    )
                }
            )
        elif not extra_only or fingerprint not in known:
            representatives.setdefault(fingerprint, renderer["renderer_path_id"])
    if not representatives:
        raise ValueError("No valid profiles selected")
    return representatives, excluded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument(
        "--output",
        type=Path,
        required=True,
        help="Gitignored local output directory, normally scratch/",
    )
    parser.add_argument(
        "--extra-only",
        action="store_true",
        help="Exclude exact profiles already in the CEL candidate catalog",
    )
    parser.add_argument(
        "--selection-only",
        action="store_true",
        help="Print the selection and missing-profile metadata without extracting",
    )
    args = parser.parse_args()
    from inventory_skeletons import file_hash

    inventory = json.loads(args.inventory.read_text())
    if file_hash(args.assets) != inventory["source_sha256"]:
        raise ValueError("Inventory source hash does not match assets")
    representatives, excluded = select_profiles(inventory, args.extra_only)
    selection = dict(
        source_sha256=inventory["source_sha256"],
        scope="additional_raw_profiles" if args.extra_only else "all_raw_profiles",
        selected_profiles=len(representatives),
        representative_renderer_ids=list(representatives.values()),
        no_fingerprint_renderers=excluded,
    )
    if args.selection_only:
        print(json.dumps(selection, indent=2))
        return
    from audit_skeletons import audit

    report = audit(
        args.assets, args.inventory, args.output, list(representatives.values())
    )
    (args.output / "raw-profile-selection.json").write_text(
        json.dumps(selection, indent=2) + "\n"
    )
    print(json.dumps(report["summary"], indent=2))
    if report["summary"]["failed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
