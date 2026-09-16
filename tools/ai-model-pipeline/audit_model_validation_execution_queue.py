#!/usr/bin/env python3
"""Join unfinished topology validation routes to exact preflighted model profiles.

This operational queue connects the candidate-validation backlog to package
readiness without promoting offline preflight or a profile match to evidence of
binding, animation, gameplay, visual quality, or acceptance. Every listed
profile is still only a source-specific candidate for a fresh isolated trial.
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


def find_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir():
            return candidate
    raise ValueError(f"could not locate FTK workspace above {start}")


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def input_reference(root: Path, path: Path) -> Json:
    return {"path": relative(root, path), "sha256": sha256(path)}


def discover_adapter_contracts(root: Path) -> list[Json]:
    """Load explicit production adapter contracts without inferring them.

    Other evidence directories may also contain a file named ``contract.json``.
    Only the exact schema owned by this queue participates in route status.
    """
    contracts: list[Json] = []
    evidence = root / "docs/evidence"
    if not evidence.is_dir():
        return contracts
    for path in sorted(evidence.glob("*/contract.json")):
        value = read_json(path)
        if value.get("schema") != "ftkmf.kraken-production-adapter-contract.v1":
            continue
        contracts.append({
            "document": input_reference(root, path),
            "contract": value,
        })
    return contracts


def discover_adapter_campaigns(root: Path) -> list[Json]:
    """Load exact production-observation campaign evidence for adapter routes."""
    campaigns: list[Json] = []
    evidence = root / "docs/evidence"
    if not evidence.is_dir():
        return campaigns
    for path in sorted(evidence.glob("*/validation.json")):
        value = read_json(path)
        if value.get("schema") != "ftkmf.kraken-production-adapter-evidence.v1":
            continue
        integrity_path = path.parent / "integrity.json"
        if not integrity_path.is_file():
            raise ValueError(f"adapter campaign evidence lacks integrity manifest: {relative(root, path)}")
        integrity = read_json(integrity_path)
        if (
            integrity.get("schema") != "ftkmf.kraken-production-adapter-archive-integrity.v1"
            or integrity.get("validationSha256") != sha256(path)
        ):
            raise ValueError(f"adapter campaign integrity does not pin validation: {relative(root, path)}")
        route = as_dict(value.get("route"))
        source_kind = route.get("sourceKind")
        if source_kind == "resource_prefab_override":
            route_kind = "resourcePrefab"
        elif source_kind == "native_enemy_row":
            route_kind = "directEnemy"
        else:
            raise ValueError(f"adapter campaign has unsupported source kind: {source_kind!r}")
        campaigns.append({
            "document": input_reference(root, path),
            "integrity": input_reference(root, integrity_path),
            "status": value.get("status"),
            "routeKind": route_kind,
            "route": route,
        })
    return campaigns


def compact_source(value: Json) -> Json:
    return {field: value.get(field) for field in SOURCE_FIELDS}


def source_kind_for_route(route_kind: str) -> str | None:
    if route_kind == "directEnemy":
        return "native_enemy_row"
    if route_kind == "resourcePrefab":
        return "resource_prefab_override"
    return None


def topology_index(report: Json) -> dict[str, Json]:
    result: dict[str, Json] = {}
    for raw_group in as_list(report.get("groups")):
        group = as_dict(raw_group)
        key = group.get("topologyGroup")
        if not isinstance(key, str) or not key:
            raise ValueError("topology group lacks topologyGroup")
        if key in result:
            raise ValueError(f"duplicate topology group: {key}")
        result[key] = group
    return result


def candidate_group_index(report: Json) -> dict[str, Json]:
    result: dict[str, Json] = {}
    for raw_group in as_list(report.get("groups")):
        group = as_dict(raw_group)
        key = group.get("topologyGroup")
        if not isinstance(key, str) or not key:
            raise ValueError("candidate coverage group lacks topologyGroup")
        if key in result:
            raise ValueError(f"duplicate candidate coverage group: {key}")
        result[key] = group
    return result


def profile_rows(readiness: Json) -> list[Json]:
    result: list[Json] = []
    for raw_package in as_list(readiness.get("packages")):
        package = as_dict(raw_package)
        preflight = as_dict(package.get("preflight"))
        document = as_dict(package.get("profileDocument"))
        if preflight.get("status") != "pass" or not document:
            continue
        for raw_profile in as_list(package.get("profiles")):
            profile = as_dict(raw_profile)
            key = profile.get("key")
            route_kind = profile.get("routeKind")
            if not isinstance(key, str) or not key or not isinstance(route_kind, str) or not route_kind:
                continue
            assignments = [
                {
                    "rendererPath": assignment.get("rendererPath"),
                    "sourceRendererId": assignment.get("sourceRendererId"),
                    "rendererKind": assignment.get("rendererKind"),
                }
                for raw_assignment in as_list(profile.get("assignments"))
                if (assignment := as_dict(raw_assignment))
            ]
            result.append({
                "package": package.get("package"),
                "profileDocument": document,
                "preflight": {
                    "status": preflight.get("status"),
                    "result": preflight.get("result"),
                },
                "key": key,
                "routeKind": route_kind,
                "baseEnemy": profile.get("baseEnemy"),
                "resourcePrefab": profile.get("resourcePrefab"),
                "baseClass": profile.get("baseClass"),
                "skinset": profile.get("skinset"),
                "assignments": assignments,
            })
    result.sort(key=lambda item: (
        str(as_dict(item.get("profileDocument")).get("path")),
        str(item.get("key")),
    ))
    return result


def enemy_profile_matches(profile: Json, assignment: Json, route_kind: str) -> bool:
    expected_route = "direct_enemy" if route_kind == "directEnemy" else "resource_prefab_override"
    if profile.get("routeKind") != expected_route:
        return False
    if route_kind == "directEnemy" and profile.get("baseEnemy") != assignment.get("nativeEnemy"):
        return False
    if route_kind == "resourcePrefab" and profile.get("resourcePrefab") != assignment.get("resourcePrefab"):
        return False
    for raw_renderer in as_list(profile.get("assignments")):
        renderer = as_dict(raw_renderer)
        if (
            renderer.get("sourceRendererId") == assignment.get("sourceRendererId")
            and renderer.get("rendererPath") == assignment.get("rendererPath")
        ):
            return True
    return False


def player_profile_matches(profile: Json, evidence: Json, renderer_ids: set[int]) -> bool:
    if profile.get("routeKind") != "player_skinset_avatar":
        return False
    expected_key = evidence.get("profile")
    expected_skinset = evidence.get("skinset")
    if isinstance(expected_key, str) and expected_key and profile.get("key") != expected_key:
        return False
    if isinstance(expected_skinset, str) and expected_skinset and profile.get("skinset") != expected_skinset:
        return False
    expected_paths = {path for path in as_list(evidence.get("rendererPaths")) if isinstance(path, str)}
    for raw_renderer in as_list(profile.get("assignments")):
        renderer = as_dict(raw_renderer)
        if (
            renderer.get("sourceRendererId") in renderer_ids
            and (not expected_paths or renderer.get("rendererPath") in expected_paths)
        ):
            return True
    return False


def matching_enemy_profiles(
    route_kind: str,
    assignments: list[Json],
    profiles: list[Json],
) -> tuple[list[Json], list[Json]]:
    """Return static-passing profiles for the explicitly selected source rows."""
    matched: list[Json] = []
    covered: set[tuple[Any, ...]] = set()
    for profile in profiles:
        profile_matches = [
            assignment for assignment in assignments
            if enemy_profile_matches(profile, assignment, route_kind)
        ]
        if not profile_matches:
            continue
        covered.update(tuple(assignment.get(field) for field in SOURCE_FIELDS) for assignment in profile_matches)
        matched.append({
            **profile,
            "matchingSourceAssignments": profile_matches,
        })
    unmatched = [
        assignment for assignment in assignments
        if tuple(assignment.get(field) for field in SOURCE_FIELDS) not in covered
    ]
    return matched, unmatched


def compact_player_evidence(evidence: Json) -> Json:
    return {
        "indexPointer": evidence.get("indexPointer"),
        "profile": evidence.get("profile"),
        "skinset": evidence.get("skinset"),
        "rendererPaths": as_list(evidence.get("rendererPaths")),
    }


def matching_player_profiles(
    evidence_rows: list[Json],
    renderer_ids: set[int],
    profiles: list[Json],
) -> tuple[list[Json], list[Json]]:
    """Return static-passing profiles for the explicitly selected player evidence."""
    matched: list[Json] = []
    covered_keys: set[str] = set()
    for profile in profiles:
        matching_evidence = [
            evidence for evidence in evidence_rows
            if player_profile_matches(profile, evidence, renderer_ids)
        ]
        if not matching_evidence:
            continue
        covered_keys.update(str(evidence.get("indexPointer")) for evidence in matching_evidence)
        matched.append({
            **profile,
            "matchingPlayerEvidence": [compact_player_evidence(evidence) for evidence in matching_evidence],
        })
    unmatched = [
        compact_player_evidence(evidence)
        for evidence in evidence_rows
        if str(evidence.get("indexPointer")) not in covered_keys
    ]
    return matched, unmatched


def priority_source_assignments(route_kind: str, priority: Json, candidate_group: Json) -> list[Json]:
    if not priority:
        return []
    pointer = priority.get("indexPointer")
    source_kind = source_kind_for_route(route_kind)
    if source_kind is None:
        raise ValueError(f"not an enemy route: {route_kind}")
    for raw_record in as_list(candidate_group.get("enemyArchiveRecords")):
        record = as_dict(raw_record)
        if record.get("indexPointer") != pointer:
            continue
        return [
            compact_source(as_dict(raw))
            for raw in as_list(record.get("assignmentsInThisTopologyGroup"))
            if as_dict(raw).get("sourceKind") == source_kind
        ]
    raise ValueError(
        f"priority record {pointer!r} has no detailed enemy archive record for {route_kind}"
    )


def priority_player_evidence(priority: Json, candidate_group: Json) -> list[Json]:
    """Recover the exact player profile identity omitted from the compact backlog row."""
    if not priority:
        return []
    pointer = priority.get("indexPointer")
    for raw_record in as_list(candidate_group.get("playerArchiveRecords")):
        record = as_dict(raw_record)
        if record.get("indexPointer") == pointer:
            return [compact_player_evidence(record)]
    raise ValueError(f"priority record {pointer!r} has no detailed player archive record")


def fallback_enemy_assignments(route_kind: str, group: Json) -> list[Json]:
    source_kind = source_kind_for_route(route_kind)
    if source_kind is None:
        raise ValueError(f"not an enemy route: {route_kind}")
    return [
        compact_source(as_dict(raw))
        for raw in as_list(group.get("remainingExactSourcePairRepresentatives"))
        if as_dict(raw).get("sourceKind") == source_kind
    ]


def compact_profile_for_queue(profile: Json) -> Json:
    result = {
        "package": profile.get("package"),
        "profileDocument": as_dict(profile.get("profileDocument")),
        "preflight": as_dict(profile.get("preflight")),
        "key": profile.get("key"),
        "routeKind": profile.get("routeKind"),
        "baseEnemy": profile.get("baseEnemy"),
        "resourcePrefab": profile.get("resourcePrefab"),
        "baseClass": profile.get("baseClass"),
        "skinset": profile.get("skinset"),
        "assignments": as_list(profile.get("assignments")),
    }
    if "matchingSourceAssignments" in profile:
        result["matchingSourceAssignments"] = as_list(profile.get("matchingSourceAssignments"))
    if "matchingPlayerEvidence" in profile:
        result["matchingPlayerEvidence"] = as_list(profile.get("matchingPlayerEvidence"))
    return result


def queue_source_target(assignment: Json) -> Json:
    return {"targetType": "source_assignment", **compact_source(assignment)}


def queue_player_target(evidence: Json) -> Json:
    return {"targetType": "player_profile_evidence", **compact_player_evidence(evidence)}


def matching_adapter_contract(
    topology_name: str,
    route_kind: str,
    contracts: list[Json],
) -> Json | None:
    matches = []
    for raw in contracts:
        row = as_dict(raw)
        contract = as_dict(row.get("contract"))
        route = as_dict(contract.get("route"))
        if route.get("topologyGroup") == topology_name and route.get("routeKind") == route_kind:
            matches.append(row)
    if len(matches) > 1:
        raise ValueError(f"multiple adapter contracts match {topology_name!r} / {route_kind!r}")
    return matches[0] if matches else None


def matching_adapter_campaign(
    topology_name: str,
    route_kind: str,
    contract: Json,
    campaigns: list[Json],
) -> Json | None:
    matches = [
        as_dict(raw)
        for raw in campaigns
        if as_dict(raw).get("routeKind") == route_kind
        and as_dict(as_dict(raw).get("route")).get("topologyGroup") == topology_name
    ]
    if len(matches) > 1:
        raise ValueError(f"multiple adapter campaigns match {topology_name!r} / {route_kind!r}")
    if not matches:
        return None
    campaign = matches[0]
    campaign_route = as_dict(campaign.get("route"))
    contract_route = as_dict(contract.get("route"))
    for field in ("nativeEnemy", "resourcePrefab", "rendererPath", "sourceRendererId"):
        if campaign_route.get(field) != contract_route.get(field):
            raise ValueError(
                f"adapter campaign route differs from contract for {topology_name!r}: {field}"
            )
    return campaign


def execution_status(
    topology_name: str,
    route_kind: str,
    topology_group: Json,
    candidates: list[Json],
    adapter_contracts: list[Json],
    adapter_campaigns: list[Json],
) -> Json:
    """Classify whether the next bounded task is staging, authoring, or an adapter.

    This only surfaces an explicit ownership finding already present in the
    topology ledger. It does not attempt to infer controller compatibility from
    a missing profile document.
    """
    if candidates:
        return {
            "status": "profile_staging_candidate_available",
            "reason": "One or more exact static-passing profile documents are listed.",
        }
    ownership = as_dict(topology_group.get("ownershipClassification"))
    ownership_status = ownership.get("status")
    if (
        route_kind == "resourcePrefab"
        and isinstance(ownership_status, str)
        and "controller_incompatible" in ownership_status
    ):
        contract_row = matching_adapter_contract(topology_name, route_kind, adapter_contracts)
        if contract_row is not None:
            contract = as_dict(contract_row.get("contract"))
            contract_status = contract.get("status")
            if contract_status == "design_complete_implementation_pending":
                return {
                    "status": "adapter_implementation_required",
                    "reason": (
                        "A route-specific adapter design is hash-pinned, but the current public API "
                        "does not implement it. Implement and review that contract before live staging."
                    ),
                    "ownershipFindingStatus": ownership_status,
                    "adapterContract": as_dict(contract_row.get("document")),
                    "adapterContractStatus": contract_status,
                }
            if contract_status == "implementation_complete_validation_pending":
                campaign = matching_adapter_campaign(
                    topology_name,
                    route_kind,
                    contract,
                    adapter_campaigns,
                )
                if campaign is not None:
                    campaign_status = campaign.get("status")
                    if campaign_status != "production_observation_campaign_satisfied":
                        raise ValueError(
                            f"unsupported adapter campaign status for {topology_name!r}: "
                            f"{campaign_status!r}"
                        )
                    return {
                        "status": "adapter_visual_archive_review_required",
                        "reason": (
                            "The exact production route has verified native binding, behavior, ordinary "
                            "lethal death, party-loss victory, and natural teardown. Supplemental visual "
                            "evidence remains review-pending, and canonical route coverage still requires "
                            "appearance, camera, culling, portrait, progression, and archive review."
                        ),
                        "ownershipFindingStatus": ownership_status,
                        "adapterContract": as_dict(contract_row.get("document")),
                        "adapterContractStatus": contract_status,
                        "adapterCampaign": as_dict(campaign.get("document")),
                        "adapterCampaignIntegrity": as_dict(campaign.get("integrity")),
                        "adapterCampaignStatus": campaign_status,
                    }
                return {
                    "status": "adapter_validation_required",
                    "reason": (
                        "The route-specific adapter reports implementation complete. It still needs "
                        "fresh isolated binding, behavior, visual, gameplay, lifetime, and archive evidence."
                    ),
                    "ownershipFindingStatus": ownership_status,
                    "adapterContract": as_dict(contract_row.get("document")),
                    "adapterContractStatus": contract_status,
                }
            raise ValueError(
                f"unsupported adapter contract status for {topology_name!r}: {contract_status!r}"
            )
        return {
            "status": "adapter_or_retarget_design_required",
            "reason": (
                "The topology ownership finding records this resource-prefab route as "
                "controller-incompatible with its direct-enemy contrast. Design and "
                "preflight a route-specific adapter or retarget before creating a generic profile."
            ),
            "ownershipFindingStatus": ownership_status,
        }
    return {
        "status": "profile_authoring_required",
        "reason": "No exact static-passing profile document is available for the selected validation target.",
    }


def build_report(
    topology: Json,
    coverage: Json,
    readiness: Json,
    inputs: list[Json],
    adapter_contracts: list[Json] | None = None,
    adapter_campaigns: list[Json] | None = None,
) -> Json:
    adapter_contracts = adapter_contracts or []
    adapter_campaigns = adapter_campaigns or []
    topology_groups = topology_index(topology)
    coverage_groups = candidate_group_index(coverage)
    profiles = profile_rows(readiness)
    routes: list[Json] = []
    for raw_backlog in as_list(coverage.get("backlog")):
        backlog = as_dict(raw_backlog)
        group_name = backlog.get("topologyGroup")
        route_kind = backlog.get("routeKind")
        if not isinstance(group_name, str) or group_name not in topology_groups:
            raise ValueError(f"candidate backlog references unknown topology group: {group_name!r}")
        if group_name not in coverage_groups:
            raise ValueError(f"candidate backlog lacks detailed group: {group_name!r}")
        if not isinstance(route_kind, str):
            raise ValueError(f"candidate backlog route has no route kind: {group_name!r}")
        priority = as_dict(backlog.get("priorityRecord"))
        topology_group = topology_groups[group_name]
        candidate_group = coverage_groups[group_name]
        if route_kind == "playerSkinset":
            if priority:
                selected_player_evidence = priority_player_evidence(priority, candidate_group)
                selection_basis = "priority_archive_record_exact_player_profile"
            else:
                selected_player_evidence = [
                    compact_player_evidence(as_dict(raw))
                    for raw in as_list(topology_group.get("playerEvidence"))
                ]
                selection_basis = "topology_player_evidence"
            if not selected_player_evidence:
                raise ValueError(
                    f"no player validation target available for backlog route: {group_name}"
                )
            matched, unmatched = matching_player_profiles(
                selected_player_evidence,
                {
                    value for value in as_list(topology_group.get("sourceRendererIds"))
                    if isinstance(value, int)
                },
                profiles,
            )
            selected_targets = [queue_player_target(evidence) for evidence in selected_player_evidence]
            unmatched_targets = [queue_player_target(evidence) for evidence in unmatched]
        else:
            if source_kind_for_route(route_kind) is None:
                raise ValueError(f"unknown route kind: {route_kind}")
            if priority:
                selected_assignments = priority_source_assignments(route_kind, priority, candidate_group)
                selection_basis = "priority_archive_record_exact_source_assignment"
            else:
                selected_assignments = fallback_enemy_assignments(route_kind, topology_group)
                selection_basis = "unindexed_remaining_topology_representative"
            if not selected_assignments:
                raise ValueError(
                    f"no source assignment available for backlog route: {group_name} ({route_kind})"
                )
            matched, unmatched = matching_enemy_profiles(route_kind, selected_assignments, profiles)
            selected_targets = [queue_source_target(assignment) for assignment in selected_assignments]
            unmatched_targets = [queue_source_target(assignment) for assignment in unmatched]
        compact_candidates = [compact_profile_for_queue(profile) for profile in matched]
        status = execution_status(
            group_name,
            route_kind,
            topology_group,
            compact_candidates,
            adapter_contracts,
            adapter_campaigns,
        )
        next_action = backlog.get("nextAction")
        if status["status"] == "adapter_or_retarget_design_required":
            next_action = (
                "Design and preflight a route-specific adapter or retarget. Do not stage this "
                "controller-incompatible resource route through the generic profile contract."
            )
        elif status["status"] == "adapter_implementation_required":
            next_action = (
                "Implement and review the pinned route-specific adapter contract. Do not create a "
                "generic profile or stage this resource route before the implementation exists."
            )
        elif status["status"] == "adapter_validation_required":
            next_action = (
                "Run the adapter contract's exact isolated validation sequence and create a fresh "
                "immutable route archive before awarding coverage."
            )
        elif status["status"] == "adapter_visual_archive_review_required":
            next_action = (
                "Complete manual appearance, camera, culling, portrait, and progression review, then "
                "build and verify the canonical route archive. Do not repeat the satisfied gameplay campaign."
            )
        routes.append({
            "topologyGroup": group_name,
            "routeKind": route_kind,
            "routeStatus": backlog.get("routeStatus"),
            "priorityRecord": priority or None,
            "selectionBasis": selection_basis,
            "selectedValidationTargets": selected_targets,
            "preflightedProfileCandidates": compact_candidates,
            "validationTargetsWithoutPreflightedProfile": unmatched_targets,
            "executionStatus": status,
            "nextAction": next_action,
        })
    routes.sort(key=lambda item: (
        0 if item.get("priorityRecord") else 1,
        len(as_list(as_dict(item.get("priorityRecord")).get("unrecordedSourceSpecificCoreEvidence"))),
        item["topologyGroup"],
        item["routeKind"],
    ))
    return {
        "schemaVersion": 5,
        "scope": (
            "Operational mapping from unfinished source-specific validation routes to preflighted profile documents. "
            "A listed profile only identifies a static-passing candidate for a new isolated trial. It does not prove "
            "deployment, binding, motion, gameplay, visual quality, acceptance, or a match to an older archive revision."
        ),
        "inputs": inputs,
        "summary": {
            "backlogRoutes": len(routes),
            "routesWithPreflightedProfileCandidates": sum(bool(route["preflightedProfileCandidates"]) for route in routes),
            "routesWithoutPreflightedProfileCandidates": sum(not route["preflightedProfileCandidates"] for route in routes),
            "preflightedProfileCandidates": sum(len(route["preflightedProfileCandidates"]) for route in routes),
            "selectedValidationTargetsWithoutPreflightedProfile": sum(
                len(route["validationTargetsWithoutPreflightedProfile"]) for route in routes
            ),
            "routesWithProfileStagingCandidates": sum(
                as_dict(route.get("executionStatus")).get("status") == "profile_staging_candidate_available"
                for route in routes
            ),
            "routesRequiringProfileAuthoring": sum(
                as_dict(route.get("executionStatus")).get("status") == "profile_authoring_required"
                for route in routes
            ),
            "routesRequiringAdapterOrRetargetDesign": sum(
                as_dict(route.get("executionStatus")).get("status") == "adapter_or_retarget_design_required"
                for route in routes
            ),
            "routesRequiringAdapterImplementation": sum(
                as_dict(route.get("executionStatus")).get("status") == "adapter_implementation_required"
                for route in routes
            ),
            "routesRequiringAdapterValidation": sum(
                as_dict(route.get("executionStatus")).get("status") == "adapter_validation_required"
                for route in routes
            ),
            "routesRequiringAdapterVisualArchiveReview": sum(
                as_dict(route.get("executionStatus")).get("status")
                == "adapter_visual_archive_review_required"
                for route in routes
            ),
        },
        "routes": routes,
        "limits": [
            "Profile matching starts from one route-specific target: the candidate ledger's priority archive assignment when present, otherwise a remaining exact topology representative.",
            "A source-assignment match requires the exact direct-enemy or resource-prefab identity and renderer ID/path. A player match requires the exact named profile, skinset, renderer ID, and renderer path.",
            "When more than one profile revision matches, choose and stage one explicitly; the queue never selects a revision or transfers live evidence between them.",
            "A selected validation target without a preflighted profile is a planning gap, not evidence that the game route fails.",
            "An adapter-or-retarget status is surfaced only from an explicit topology ownership finding. It is not inferred merely because a profile is absent.",
            "A design-complete adapter status is surfaced only from a hash-pinned explicit adapter contract. It does not imply framework implementation or live validation.",
            "Adapter campaign completion requires exact route identity, the production-observation evidence schema and status, and an integrity manifest that pins validation.json. The dedicated archive verifier remains authoritative for every contained artifact.",
            "The candidate-coverage, validation-gate, archive-integrity, topology, and package-readiness ledgers remain the authority for their respective observations.",
        ],
    }


def profile_cell(route: Json) -> str:
    profiles = as_list(route.get("preflightedProfileCandidates"))
    if not profiles:
        return "none"
    return "<br>".join(
        "`{key}` [{document}]".format(
            key=as_dict(profile).get("key"),
            document=as_dict(as_dict(profile).get("profileDocument")).get("path"),
        )
        for profile in profiles
    )


def markdown(report: Json) -> str:
    summary = as_dict(report.get("summary"))
    lines = [
        "# Model validation execution queue",
        "",
        "This queue maps unfinished source-specific validation routes to profile documents that already pass the static route preflight. It does not claim that a profile has been deployed, observed live, accepted visually, or proven compatible with a historical archive revision.",
        "",
        "| Backlog routes | Routes with preflighted profile candidates | Routes requiring profile authoring | Adapter design | Adapter implementation | Adapter validation | Adapter visual/archive review | Profile candidates | Selected validation targets without a preflighted profile |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
        "| {backlogRoutes} | {routesWithPreflightedProfileCandidates} | {routesRequiringProfileAuthoring} | {routesRequiringAdapterOrRetargetDesign} | {routesRequiringAdapterImplementation} | {routesRequiringAdapterValidation} | {routesRequiringAdapterVisualArchiveReview} | {preflightedProfileCandidates} | {selectedValidationTargetsWithoutPreflightedProfile} |".format(**summary),
        "",
        "Choose one listed exact profile revision, stage it only in an owned isolated game, run one fresh trial, conduct a manual root review, and create a new immutable archive. Do not use this queue to reuse behavior or visual conclusions from another renderer, profile revision, or source pair.",
        "",
        "## Routes",
        "",
        "| Group | Route | Priority evidence | Target basis | Execution status | Preflighted profile candidates | Selected targets without a profile | Next action |",
        "|---|---|---|---|---|---|---:|---|",
    ]
    for raw_route in as_list(report.get("routes")):
        route = as_dict(raw_route)
        priority = as_dict(route.get("priorityRecord"))
        evidence = "no indexed original archive"
        if priority:
            evidence = "`{pointer}` [{archive}]".format(
                pointer=priority.get("indexPointer"),
                archive=priority.get("archive"),
            )
        lines.append(
            "| `{group}` | `{route_kind}` | {evidence} | `{basis}` | `{status}` | {profiles} | {unmatched} | {action} |".format(
                group=route.get("topologyGroup"),
                route_kind=route.get("routeKind"),
                evidence=evidence,
                basis=route.get("selectionBasis"),
                status=as_dict(route.get("executionStatus")).get("status"),
                profiles=profile_cell(route),
                unmatched=len(as_list(route.get("validationTargetsWithoutPreflightedProfile"))),
                action=route.get("nextAction"),
            )
        )
    lines.extend([
        "",
        "## Boundaries",
        "",
        "The queue is an execution aid. It preserves the difference between a package that passed offline preflight and a model whose exact source assignment has completed a reviewed live trial with an intact archive.",
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
    parser.add_argument("--coverage", type=Path, default=Path("docs/model-candidate-validation-coverage.json"))
    parser.add_argument("--readiness", type=Path, default=Path("docs/model-package-readiness.json"))
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--fail-on-unmatched", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    topology = args.topology if args.topology.is_absolute() else root / args.topology
    coverage = args.coverage if args.coverage.is_absolute() else root / args.coverage
    readiness = args.readiness if args.readiness.is_absolute() else root / args.readiness
    for path in (topology, coverage, readiness):
        if not path.is_file():
            parser.error(f"missing input: {path}")
    report = build_report(
        read_json(topology),
        read_json(coverage),
        read_json(readiness),
        [input_reference(root, topology), input_reference(root, coverage), input_reference(root, readiness)],
        discover_adapter_contracts(root),
        discover_adapter_campaigns(root),
    )
    output_json = args.output_json if args.output_json.is_absolute() else root / args.output_json
    output_markdown = args.output_markdown if args.output_markdown.is_absolute() else root / args.output_markdown
    write_new(output_json, json.dumps(report, indent=2) + "\n", args.overwrite)
    write_new(output_markdown, markdown(report), args.overwrite)
    print(json.dumps(report["summary"], indent=2))
    if args.fail_on_unmatched and report["summary"]["routesWithoutPreflightedProfileCandidates"]:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
