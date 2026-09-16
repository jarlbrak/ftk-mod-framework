#!/usr/bin/env python3
"""Summarize strict structured validation evidence by FTK topology candidate.

This joins the topology ownership plan to the per-archive validation-gate and
artifact-integrity ledgers. It keeps direct enemy rows, ResourceManager prefab
overrides, and player skinsets distinct. A canonical route representative has
one exact source-identity archive with the complete renderer set, all
source-specific core evidence, and an intact, independently verified archived
artifact. It never approves a sibling source pair, a different asset revision,
or art quality.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


Json = dict[str, Any]


SOURCE_FIELDS = (
    "sourceKind",
    "nativeEnemy",
    "resourcePrefab",
    "rendererPath",
    "sourceRendererId",
)
SOURCE_IDENTITY_FIELDS = (
    "sourceKind",
    "nativeEnemy",
    "resourcePrefab",
)


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def read_json(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def input_reference(root: Path, path: Path) -> Json:
    return {"path": relative(root, path), "sha256": sha256(path)}


def source_key(value: Json) -> tuple[Any, ...]:
    return tuple(value.get(field) for field in SOURCE_FIELDS)


def compact_source(value: Json) -> Json:
    return {field: value.get(field) for field in SOURCE_FIELDS}


def source_identity_key(value: Json) -> tuple[Any, ...]:
    return tuple(value.get(field) for field in SOURCE_IDENTITY_FIELDS)


def compact_source_identity(value: Json) -> Json:
    return {field: value.get(field) for field in SOURCE_IDENTITY_FIELDS}


def valid_source_identity(value: Json) -> bool:
    kind = value.get("sourceKind")
    native = value.get("nativeEnemy")
    resource = value.get("resourcePrefab")
    if not isinstance(native, str) or not native:
        return False
    if kind == "native_enemy_row":
        return resource is None
    if kind == "resource_prefab_override":
        return isinstance(resource, str) and bool(resource)
    return False


def group_source_assignments(group: Json) -> list[Json]:
    """Recover the complete exact-pair inventory preserved by the topology plan."""
    values: list[Json] = []
    for evidence_key in ("directEnemyEvidence", "resourcePrefabEvidence"):
        for raw_event in as_list(group.get(evidence_key)):
            target = as_dict(as_dict(raw_event).get("target"))
            if target:
                values.append(target)
    values.extend(
        as_dict(item) for item in as_list(group.get("remainingExactSourcePairRepresentatives"))
    )
    distinct: dict[tuple[Any, ...], Json] = {}
    for value in values:
        if valid_source_identity(value):
            distinct.setdefault(source_key(value), compact_source(value))
    return list(distinct.values())


def source_inventory(groups: dict[str, Json]) -> tuple[dict[tuple[Any, ...], list[Json]], dict[tuple[Any, ...], set[str]]]:
    by_group_identity: dict[tuple[Any, ...], dict[tuple[Any, ...], Json]] = {}
    groups_by_identity: dict[tuple[Any, ...], set[str]] = {}
    for name, group in groups.items():
        for assignment in group_source_assignments(group):
            assignment_key = source_key(assignment)
            identity_key = source_identity_key(assignment)
            by_group_identity.setdefault((name, *identity_key), {}).setdefault(assignment_key, assignment)
            groups_by_identity.setdefault(identity_key, set()).add(name)
    return (
        {identity: list(assignments.values()) for identity, assignments in by_group_identity.items()},
        groups_by_identity,
    )


def group_index(coverage: Json) -> tuple[dict[str, Json], dict[int, list[str]]]:
    groups: dict[str, Json] = {}
    by_renderer_id: dict[int, list[str]] = {}
    for raw_group in as_list(coverage.get("groups")):
        group = as_dict(raw_group)
        name = group.get("topologyGroup")
        if not isinstance(name, str) or not name:
            raise ValueError("topology coverage group lacks topologyGroup")
        if name in groups:
            raise ValueError(f"duplicate topology group: {name}")
        groups[name] = group
        for renderer_id in as_list(group.get("sourceRendererIds")):
            if isinstance(renderer_id, int):
                by_renderer_id.setdefault(renderer_id, []).append(name)
    return groups, by_renderer_id


def matching_group_names(assignment: Json, by_renderer_id: dict[int, list[str]]) -> list[str]:
    renderer_id = assignment.get("sourceRendererId")
    if not isinstance(renderer_id, int):
        return []
    # Renderer IDs come from the same local asset inventory as the ownership
    # plan, so they are the stable route identity. Preserve nonunique matches as
    # ambiguity rather than guessing from names.
    return list(by_renderer_id.get(renderer_id, []))


def integrity_index(report: Json) -> dict[str, Json]:
    records: dict[str, Json] = {}
    for raw_record in as_list(report.get("records")):
        record = as_dict(raw_record)
        validation = record.get("validation")
        if not isinstance(validation, str) or not validation:
            raise ValueError("archive-integrity record lacks validation path")
        if validation in records:
            raise ValueError(f"duplicate archive-integrity validation path: {validation}")
        records[validation] = record
    return records


def archive_integrity(record: Json, integrity_by_validation: dict[str, Json], require_integrity: bool) -> Json:
    if not require_integrity:
        return {"status": "not_checked_fixture", "canonicalEligible": True}
    archive = as_dict(record.get("archive"))
    validation_path = archive.get("path")
    if not isinstance(validation_path, str) or not validation_path:
        return {"status": "integrity_archive_path_unavailable", "canonicalEligible": False}
    evidence = as_dict(integrity_by_validation.get(validation_path))
    if not evidence:
        return {"status": "integrity_not_indexed", "canonicalEligible": False}
    current_hash = archive.get("actualSha256")
    audited_hash = evidence.get("validationSha256")
    if not isinstance(current_hash, str) or not isinstance(audited_hash, str) or current_hash != audited_hash:
        return {
            "status": "integrity_stale_or_validation_hash_mismatch",
            "canonicalEligible": False,
            "auditedStatus": evidence.get("status"),
            "auditedValidationSha256": audited_hash,
        }
    return {
        "status": evidence.get("status"),
        "canonicalEligible": evidence.get("status") == "artifact_integrity_verified",
        "layout": evidence.get("layout"),
        "reason": evidence.get("reason"),
        "validationSha256": audited_hash,
    }


def exact_source_archive_check(
    all_assignments: list[Json],
    grouped_assignments: list[Json],
    topology_group: str,
    expected_by_group_identity: dict[tuple[Any, ...], list[Json]],
    groups_by_identity: dict[tuple[Any, ...], set[str]],
) -> Json:
    compact_actual = [compact_source(item) for item in all_assignments]
    actual_keys = [source_key(item) for item in compact_actual]
    identities = {source_identity_key(item) for item in compact_actual}
    identity = compact_source_identity(compact_actual[0]) if compact_actual else {}
    identity_key = source_identity_key(identity) if identity else ()
    expected = expected_by_group_identity.get((topology_group, *identity_key), []) if identity else []
    expected_keys = {source_key(item) for item in expected}
    actual_key_set = set(actual_keys)
    grouped_keys = {source_key(item) for item in grouped_assignments}
    expected_groups = sorted(groups_by_identity.get(identity_key, set()))
    single_identity = len(identities) == 1 and bool(identity) and valid_source_identity(identity)
    distinct_assignments = len(actual_keys) == len(actual_key_set)
    all_in_group = (
        len(all_assignments) == len(grouped_assignments)
        and actual_key_set == grouped_keys
    )
    complete_renderer_set = bool(expected_keys) and actual_key_set == expected_keys

    if not compact_actual:
        status = "no_archive_assignments"
    elif not single_identity:
        status = "multiple_or_invalid_source_identities"
    elif not distinct_assignments:
        status = "duplicate_archive_assignments"
    elif not expected_keys:
        status = "topology_source_inventory_unavailable"
    elif not all_in_group:
        status = "archive_assignments_extend_outside_topology_group"
    elif not complete_renderer_set:
        status = "incomplete_or_extra_source_renderer_set"
    else:
        status = "complete_exact_source_renderer_set"

    return {
        "status": status,
        "topologyGroup": topology_group,
        "sourceIdentity": identity or None,
        "singleSourceIdentity": single_identity,
        "distinctArchiveAssignments": distinct_assignments,
        "allAssignmentsInThisTopologyGroup": all_in_group,
        "sourceIdentityTopologyGroups": expected_groups,
        "completeSourceRendererSet": complete_renderer_set,
        "expectedSourceAssignments": expected,
        "missingSourceAssignments": [
            item for item in expected if source_key(item) not in actual_key_set
        ],
        "unexpectedArchiveAssignments": [
            item for item in compact_actual if source_key(item) not in expected_keys
        ],
        "canonicalEligible": status == "complete_exact_source_renderer_set",
    }


def compact_enemy_record(
    record: Json,
    assignments: list[Json],
    integrity: Json,
    topology_group: str,
    expected_by_group_identity: dict[tuple[Any, ...], list[Json]],
    groups_by_identity: dict[tuple[Any, ...], set[str]],
) -> Json:
    missing = [item for item in as_list(record.get("unrecordedSourceSpecificCoreEvidence")) if isinstance(item, str)]
    all_assignments = [as_dict(item) for item in as_list(record.get("exactAssignments"))]
    exact_source = exact_source_archive_check(
        all_assignments,
        assignments,
        topology_group,
        expected_by_group_identity,
        groups_by_identity,
    )
    return {
        "indexPointer": record.get("indexPointer"),
        "name": record.get("name"),
        "status": record.get("status"),
        "archive": as_dict(record.get("archive")),
        "assignmentsInThisTopologyGroup": [compact_source(item) for item in assignments],
        "allArchiveAssignments": [compact_source(item) for item in all_assignments],
        "singleExactSourceArchive": len(all_assignments) == 1,
        "exactSourceArchive": exact_source.get("canonicalEligible") is True,
        "exactSourceArchiveCheck": exact_source,
        "unrecordedSourceSpecificCoreEvidence": missing,
        "archiveIntegrity": integrity,
        "canonicalSingleSourceEvidence": (
            len(all_assignments) == 1 and not missing and integrity.get("canonicalEligible") is True
        ),
        "canonicalExactSourceEvidence": (
            exact_source.get("canonicalEligible") is True
            and not missing
            and integrity.get("canonicalEligible") is True
        ),
    }


def compact_player_record(record: Json, integrity: Json) -> Json:
    missing = [item for item in as_list(record.get("unrecordedSourceSpecificCoreEvidence")) if isinstance(item, str)]
    return {
        "indexPointer": record.get("indexPointer"),
        "name": record.get("name"),
        "status": record.get("status"),
        "archive": as_dict(record.get("archive")),
        "profile": record.get("playerProfile"),
        "skinset": record.get("skinset"),
        "rendererPaths": as_list(record.get("rendererPaths")),
        "apparelPaths": as_list(record.get("apparelPaths")),
        "unrecordedSourceSpecificCoreEvidence": missing,
        "archiveIntegrity": integrity,
        "canonicalProfileEvidence": not missing and integrity.get("canonicalEligible") is True,
    }


def priority_record(records: list[Json]) -> Json | None:
    if not records:
        return None
    return min(
        records,
        key=lambda item: (
            len(as_list(item.get("unrecordedSourceSpecificCoreEvidence"))),
            str(item.get("indexPointer")),
        ),
    )


def route_summary(required: bool, records: list[Json], *, player: bool = False) -> Json:
    canonical_key = "canonicalProfileEvidence" if player else "canonicalExactSourceEvidence"
    canonical = [record for record in records if record.get(canonical_key) is True]
    single_source = records if player else [record for record in records if record.get("singleExactSourceArchive") is True]
    exact_source = records if player else [record for record in records if record.get("exactSourceArchive") is True]
    if not required:
        status = "not_applicable"
    elif canonical:
        status = "canonical_representative_archive_recorded"
    elif records:
        status = "indexed_archive_needs_canonical_follow_up"
    else:
        status = "no_indexed_original_archive"
    return {
        "required": required,
        "indexedArchiveRecords": len(records),
        "singleExactSourceArchiveRecords": len(single_source),
        "completeExactSourceArchiveRecords": len(exact_source),
        "canonicalRepresentativeArchiveRecords": len(canonical),
        "artifactIntegrityVerifiedArchiveRecords": sum(
            as_dict(record.get("archiveIntegrity")).get("status") == "artifact_integrity_verified"
            for record in records
        ),
        "integrityBlockedArchiveRecords": sum(
            as_dict(record.get("archiveIntegrity")).get("canonicalEligible") is False
            for record in records
        ),
        "status": status,
        "priorityRecord": priority_record(records),
    }


def group_route_required(group: Json, kind: str) -> bool:
    if kind == "direct_enemy_row":
        return group.get("nativeDirectEnemyPairs", 0) > 0
    if kind == "resource_prefab_override":
        return group.get("nativeResourcePrefabPairs", 0) > 0
    if kind == "player_skinset_avatar":
        return bool(as_list(group.get("playerEvidence")))
    raise ValueError(kind)


def group_status(group: Json, routes: Json) -> str:
    raw_status = str(group.get("status") or "")
    if "unsupported" in raw_status:
        return "explicitly_unsupported"
    applicable = [
        item for item in routes.values()
        if isinstance(item, dict) and item.get("required") is True
    ]
    if not applicable:
        return "no_supported_route_declared"
    if all(item.get("status") == "canonical_representative_archive_recorded" for item in applicable):
        if group.get("remainingExactSourcePairRepresentatives"):
            return "canonical_route_representative_recorded_exact_source_pairs_remain"
        return "canonical_route_representative_recorded"
    if any(item.get("indexedArchiveRecords", 0) for item in applicable):
        return "partial_indexed_evidence_needs_canonical_follow_up"
    return "no_indexed_original_evidence"


def route_priority(kind: str, summary: Json) -> Json | None:
    record = as_dict(summary.get("priorityRecord"))
    if not record:
        return None
    return {
        "routeKind": kind,
        "indexPointer": record.get("indexPointer"),
        "name": record.get("name"),
        "archive": as_dict(record.get("archive")).get("path"),
        "unrecordedSourceSpecificCoreEvidence": as_list(record.get("unrecordedSourceSpecificCoreEvidence")),
        "singleExactSourceArchive": record.get("singleExactSourceArchive"),
        "exactSourceArchive": record.get("exactSourceArchive"),
        "exactSourceArchiveCheck": as_dict(record.get("exactSourceArchiveCheck")),
        "archiveIntegrity": as_dict(record.get("archiveIntegrity")),
    }


def next_action(priority: Json | None) -> str:
    if not priority:
        return (
            "Author and preflight an original package for one exact supported source assignment "
            "before a fresh isolated trial."
        )
    integrity = as_dict(priority.get("archiveIntegrity"))
    if integrity.get("canonicalEligible") is False:
        return (
            "Preserve the historical record and create a fresh immutable replacement with its own "
            "exact source evidence and verified archive artifacts."
        )
    exact_source = as_dict(priority.get("exactSourceArchiveCheck"))
    if exact_source.get("canonicalEligible") is False:
        return (
            "Run one fresh isolated trial for the complete renderer set of one exact source identity, then archive "
            "that trial's own binding, visual review, idle, attack, hit, death fixture, ordinary damage, and Ready evidence."
        )
    return (
        "Run one fresh isolated trial for an exact source assignment and archive its own explicit binding, "
        "visual review, idle, attack, hit, death fixture, ordinary damage, and Ready evidence."
    )


def build_report(
    coverage: Json,
    gates: Json,
    inputs: list[Json],
    integrity_by_validation: dict[str, Json] | None = None,
    require_integrity: bool = False,
) -> Json:
    groups, by_renderer_id = group_index(coverage)
    expected_by_group_identity, groups_by_identity = source_inventory(groups)
    integrity_by_validation = integrity_by_validation or {}
    enemy_by_group: dict[str, list[Json]] = {name: [] for name in groups}
    player_by_group: dict[str, list[Json]] = {name: [] for name in groups}
    unmapped_enemy: list[Json] = []
    ambiguous_enemy: list[Json] = []

    for raw_record in as_list(gates.get("enemyRecords")):
        record = as_dict(raw_record)
        integrity = archive_integrity(record, integrity_by_validation, require_integrity)
        assignments = [as_dict(value) for value in as_list(record.get("exactAssignments"))]
        matches: dict[str, list[Json]] = {}
        for assignment in assignments:
            names = matching_group_names(assignment, by_renderer_id)
            if len(names) == 1:
                matches.setdefault(names[0], []).append(assignment)
            elif not names:
                unmapped_enemy.append({
                    "indexPointer": record.get("indexPointer"),
                    "assignment": compact_source(assignment),
                })
            else:
                ambiguous_enemy.append({
                    "indexPointer": record.get("indexPointer"),
                    "assignment": compact_source(assignment),
                    "topologyGroups": names,
                })
        for name, grouped_assignments in matches.items():
            enemy_by_group[name].append(compact_enemy_record(
                record,
                grouped_assignments,
                integrity,
                name,
                expected_by_group_identity,
                groups_by_identity,
            ))

    player_records = {
        record.get("indexPointer"): as_dict(record)
        for record in as_list(gates.get("playerRecords"))
        if isinstance(as_dict(record).get("indexPointer"), str)
    }
    unmatched_player_evidence: list[Json] = []
    player_pointers_seen: set[str] = set()
    for name, group in groups.items():
        for raw_evidence in as_list(group.get("playerEvidence")):
            evidence = as_dict(raw_evidence)
            pointer = evidence.get("indexPointer")
            if not isinstance(pointer, str):
                continue
            record = player_records.get(pointer)
            if record is None:
                unmatched_player_evidence.append({"topologyGroup": name, "indexPointer": pointer})
                continue
            player_by_group[name].append(compact_player_record(
                record,
                archive_integrity(record, integrity_by_validation, require_integrity),
            ))
            player_pointers_seen.add(pointer)
    unmapped_player = [
        {"indexPointer": pointer, "profile": record.get("playerProfile"), "skinset": record.get("skinset")}
        for pointer, record in player_records.items() if pointer not in player_pointers_seen
    ]

    output_groups: list[Json] = []
    backlog: list[Json] = []
    for name, group in groups.items():
        enemy_records = enemy_by_group[name]
        direct = [record for record in enemy_records if any(
            assignment.get("sourceKind") == "native_enemy_row"
            for assignment in as_list(record.get("assignmentsInThisTopologyGroup"))
        )]
        resource = [record for record in enemy_records if any(
            assignment.get("sourceKind") == "resource_prefab_override"
            for assignment in as_list(record.get("assignmentsInThisTopologyGroup"))
        )]
        routes = {
            "directEnemy": route_summary(group_route_required(group, "direct_enemy_row"), direct),
            "resourcePrefab": route_summary(group_route_required(group, "resource_prefab_override"), resource),
            "playerSkinset": route_summary(
                group_route_required(group, "player_skinset_avatar"),
                player_by_group[name],
                player=True,
            ),
        }
        item = {
            "topologyGroup": name,
            "topologyStatus": group.get("status"),
            "sourceRendererIds": as_list(group.get("sourceRendererIds")),
            "nativeDirectEnemyPairs": group.get("nativeDirectEnemyPairs"),
            "nativeResourcePrefabPairs": group.get("nativeResourcePrefabPairs"),
            "exactSourcePairsWithIndexedOriginalEvidence": group.get("exactSourcePairsWithIndexedOriginalEvidence"),
            "remainingExactSourcePairRepresentativeCount": len(
                as_list(group.get("remainingExactSourcePairRepresentatives"))
            ),
            "routes": routes,
            "enemyArchiveRecords": enemy_records,
            "playerArchiveRecords": player_by_group[name],
            "candidateCoverageStatus": group_status(group, routes),
        }
        output_groups.append(item)
        for route_name, summary in routes.items():
            if summary.get("required") is not True:
                continue
            if summary.get("status") == "canonical_representative_archive_recorded":
                continue
            priority = route_priority(route_name, summary)
            backlog.append({
                "topologyGroup": name,
                "routeKind": route_name,
                "routeStatus": summary.get("status"),
                "priorityRecord": priority,
                "nextAction": next_action(priority),
            })

    output_groups.sort(key=lambda item: item["topologyGroup"])
    backlog.sort(key=lambda item: (
        0 if item.get("priorityRecord") else 1,
        len(as_list(as_dict(item.get("priorityRecord")).get("unrecordedSourceSpecificCoreEvidence"))),
        item["topologyGroup"],
        item["routeKind"],
    ))
    all_records = [record for group in output_groups for record in (
        as_list(group.get("enemyArchiveRecords")) + as_list(group.get("playerArchiveRecords"))
    )]
    canonical_route_representatives = sum(
        summary.get("canonicalRepresentativeArchiveRecords", 0)
        for group in output_groups for summary in as_dict(group.get("routes")).values()
    )
    return {
        "schemaVersion": 3,
        "scope": (
            "Topology-candidate structured-evidence coverage. A canonical representative is one indexed archive that "
            "has every source-specific core evidence shape and an independently verified immutable artifact for one "
            "exact direct, resource-prefab, or player route. Enemy archives must contain the complete renderer set "
            "for one source identity within one topology group. It is not an art verdict, all-controller proof, "
            "profile-revision match, all-source-pair acceptance, or game-wide PASS."
        ),
        "inputs": inputs,
        "summary": {
            "topologyGroups": len(output_groups),
            "enemyArchiveRecords": len(as_list(gates.get("enemyRecords"))),
            "playerArchiveRecords": len(as_list(gates.get("playerRecords"))),
            "routedArchiveRecordsWithVerifiedIntegrity": sum(
                as_dict(record.get("archiveIntegrity")).get("status") == "artifact_integrity_verified"
                for record in all_records
            ),
            "routedArchiveRecordsBlockedByArchiveIntegrity": sum(
                as_dict(record.get("archiveIntegrity")).get("canonicalEligible") is False
                for record in all_records
            ),
            "canonicalRouteRepresentativeArchives": canonical_route_representatives,
            "groupsWithAnyCanonicalRouteRepresentative": sum(any(
                route.get("canonicalRepresentativeArchiveRecords", 0) > 0
                for route in as_dict(group.get("routes")).values()
            ) for group in output_groups),
            "groupsWithAllRequiredCanonicalRouteRepresentatives": sum(
                group["candidateCoverageStatus"] in {
                    "canonical_route_representative_recorded",
                    "canonical_route_representative_recorded_exact_source_pairs_remain",
                }
                for group in output_groups
            ),
            "backlogRoutes": len(backlog),
            "unmappedEnemyAssignments": len(unmapped_enemy),
            "ambiguousEnemyAssignments": len(ambiguous_enemy),
            "unmappedPlayerArchives": len(unmapped_player),
            "unmatchedPlayerEvidence": len(unmatched_player_evidence),
        },
        "groups": output_groups,
        "backlog": backlog,
        "unmappedEnemyAssignments": unmapped_enemy,
        "ambiguousEnemyAssignments": ambiguous_enemy,
        "unmappedPlayerArchives": unmapped_player,
        "unmatchedPlayerEvidence": unmatched_player_evidence,
        "limits": [
            "The report never combines fields from separate archives or revisions to make a canonical result.",
            "A multipart enemy archive can become canonical only when every assignment belongs to one exact source identity in one topology group and exactly matches that identity's complete renderer set.",
            "A partial, mixed-identity, cross-topology, duplicate, or extra-assignment archive is retained but cannot credit a route.",
            "A record whose exact validation artifact is absent from the integrity ledger, stale, or integrity-unverified cannot become canonical, even when its structured evidence fields are complete.",
            "A complete structured row records evidence shape only. Inspect its archive for visual acceptance, culling, materials, portraits, lifetime, and stated limitations.",
            "An unrecorded exact source pair is a coverage boundary, not evidence that a sibling route fails.",
        ],
    }


def markdown(report: Json) -> str:
    summary = as_dict(report.get("summary"))
    lines = [
        "# Model candidate validation coverage",
        "",
        "This report joins topology ownership to the conservative validation-evidence and archive-integrity ledgers. A `canonical representative` means one indexed archive records every required source-specific evidence shape for one exact source identity, includes exactly its complete renderer set within one topology group, and has a verified immutable validation artifact. It does not approve artwork, a different revision, a sibling source pair, all controllers, culling, portraits, lifetime, or a full campaign.",
        "",
        "| Topology groups | Enemy archives | Player archives | Route-assigned records with verified archive integrity | Canonical route representatives | Groups with all required route representatives | Backlog routes |",
        "|---:|---:|---:|---:|---:|---:|---:|",
        "| {topologyGroups} | {enemyArchiveRecords} | {playerArchiveRecords} | {routedArchiveRecordsWithVerifiedIntegrity} | {canonicalRouteRepresentativeArchives} | {groupsWithAllRequiredCanonicalRouteRepresentatives} | {backlogRoutes} |".format(**summary),
        "",
        "A complete multipart archive can credit its exact route. Partial renderer sets, mixed source identities, cross-topology assignments, duplicates, and extras remain useful history but cannot become canonical. The topology plan remains the authority for route ownership and exact-pair inventory; this report only makes the next validation gap explicit.",
        "",
        "## Candidate routes",
        "",
        "| Group | Candidate status | Direct route | Resource route | Player route | Exact source-pair representatives still unindexed |",
        "|---|---|---|---|---|---:|",
    ]
    for group in as_list(report.get("groups")):
        item = as_dict(group)
        routes = as_dict(item.get("routes"))

        def marker(route: Json) -> str:
            if route.get("required") is not True:
                return "n/a"
            return f"{route.get('status')} ({route.get('canonicalRepresentativeArchiveRecords', 0)})"

        lines.append(
            "| `{group}` | `{status}` | {direct} | {resource} | {player} | {remaining} |".format(
                group=item.get("topologyGroup"),
                status=item.get("candidateCoverageStatus"),
                direct=marker(as_dict(routes.get("directEnemy"))),
                resource=marker(as_dict(routes.get("resourcePrefab"))),
                player=marker(as_dict(routes.get("playerSkinset"))),
                remaining=item.get("remainingExactSourcePairRepresentativeCount", 0),
            )
        )
    lines.extend([
        "",
        "## Priority follow-ups",
        "",
        "These are route-specific planning records. A listed archive identifies the closest existing structured record; its missing fields or artifact-preservation gap are not behavior failures and must not be filled from a sibling archive.",
        "",
        "| Group | Route | Current evidence | Artifact integrity | Missing structured fields | Next action |",
        "|---|---|---|---|---|---|",
    ])
    for item in as_list(report.get("backlog")):
        row = as_dict(item)
        priority = as_dict(row.get("priorityRecord"))
        evidence = "no indexed original archive"
        integrity_status = "n/a"
        missing = "n/a"
        if priority:
            evidence = "`{pointer}` [{archive}]".format(
                pointer=priority.get("indexPointer"),
                archive=priority.get("archive"),
            )
            integrity_status = as_dict(priority.get("archiveIntegrity")).get("status")
            missing = ", ".join(as_list(priority.get("unrecordedSourceSpecificCoreEvidence"))) or "none"
        lines.append(
            "| `{group}` | `{route}` | {evidence} | `{integrity}` | {missing} | {action} |".format(
                group=row.get("topologyGroup"),
                route=row.get("routeKind"),
                evidence=evidence,
                integrity=integrity_status,
                missing=missing,
                action=row.get("nextAction"),
            )
        )
    lines.extend([
        "",
        "## Audit boundaries",
        "",
        "The counters answer only whether the archived JSON has the named machine-readable evidence shape and whether the exact frozen artifact verifies. A positive row never overrides the linked archive's art status, rejection, or limitation. The report intentionally does not merge complements from historical archives, because that would silently transfer proof across capture sessions or asset revisions.",
        "",
    ])
    return "\n".join(lines)


def write_new(path: Path, content: str, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise FileExistsError(f"refusing to overwrite existing output: {path}; pass --overwrite")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--topology", type=Path, default=Path("docs/topology-coverage.json"))
    parser.add_argument("--gates", type=Path, default=Path("docs/model-validation-gates.json"))
    parser.add_argument("--integrity", type=Path, default=Path("docs/model-validation-archive-integrity.json"))
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--fail-on-unmapped", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    topology = args.topology if args.topology.is_absolute() else root / args.topology
    gates = args.gates if args.gates.is_absolute() else root / args.gates
    integrity = args.integrity if args.integrity.is_absolute() else root / args.integrity
    for path in (topology, gates, integrity):
        if not path.is_file():
            parser.error(f"missing input: {path}")
    integrity_report = read_json(integrity)
    report = build_report(
        read_json(topology),
        read_json(gates),
        [
            input_reference(root, topology),
            input_reference(root, gates),
            input_reference(root, integrity),
        ],
        integrity_index(integrity_report),
        require_integrity=True,
    )
    output_json = args.output_json if args.output_json.is_absolute() else root / args.output_json
    output_markdown = args.output_markdown if args.output_markdown.is_absolute() else root / args.output_markdown
    write_new(output_json, json.dumps(report, indent=2) + "\n", args.overwrite)
    write_new(output_markdown, markdown(report), args.overwrite)
    print(json.dumps(report["summary"], indent=2))
    if args.fail_on_unmapped and any(report["summary"][key] for key in (
        "unmappedEnemyAssignments", "ambiguousEnemyAssignments", "unmappedPlayerArchives", "unmatchedPlayerEvidence",
    )):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
