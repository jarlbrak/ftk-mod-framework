#!/usr/bin/env python3
"""Verify the canonical Wildbloom Herbalist player-route archive."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "art-experiments/wildbloom-herbalist/live-validation-v2-canonical"
PREVIEW_SESSION = "e69928c5d0ab49df890edeef870d5feb"
COMBAT_SESSION = "c293b02ed0f34e589867d6aeeed747b2"
PREVIEW_OWNER = -19122
PREVIEW_CEL = -292682

ROUTES = {
    "body": {"path": "player_Herbalist", "renderer": -292700, "mesh": "ftkmf_glb_wildbloom-body.glb", "signature": "33ef1d59071b4cb809e65f6c63ea4d925e2f7dc75a0703033e7e8177f9137149"},
    "hair-top": {"path": "hairTop", "renderer": -292694, "mesh": "ftkmf_glb_wildbloom-hair-top.glb", "signature": "77a9c31c091976a8004ebf40f7871623675169fc43de518ac46423bb472e98df"},
    "hair-bottom": {"path": "hairBottom", "renderer": -292688, "mesh": "ftkmf_glb_wildbloom-hair-bottom.glb", "signature": "ccd5b08dc89616e5470ca4195938ebc3378daeb653d2e8b2cce2f93b3c3bdc2c"},
}


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text())
    require(isinstance(value, dict), f"JSON object required: {path}")
    return value


def load_capture(path: Path) -> dict[str, Any]:
    with gzip.open(path, "rt") as stream:
        value = json.load(stream)
    require(isinstance(value, dict), f"capture object required: {path}")
    return value


def base_layer(frame: dict[str, Any]) -> dict[str, Any]:
    layers = frame.get("animator", {}).get("layers", [])
    require(bool(layers) and layers[0].get("layer") == 0, "base animator layer missing")
    return layers[0]


def maximum_bone_delta(first: dict[str, Any], last: dict[str, Any]) -> float:
    left = {bone["name"]: bone["localToWorld"] for bone in first["bones"]}
    right = {bone["name"]: bone["localToWorld"] for bone in last["bones"]}
    require(set(left) == set(right) and bool(left), "bone set changed")
    return max(abs(a - b) for name in left for a, b in zip(left[name], right[name]))


def verify_preview_capture(archive: Path, phase: str, part: str) -> float:
    route = ROUTES[part]
    capture = load_capture(archive / f"metadata/{phase}-{part}.json.gz")
    require(capture.get("ok") is True and capture.get("session") == PREVIEW_SESSION, "preview session changed")
    require(capture.get("scope") == "player-preview" and capture.get("ownerInstanceId") == PREVIEW_OWNER and capture.get("celInstanceId") == PREVIEW_CEL, "preview owner changed")
    require(capture.get("fixedStep") is True and capture.get("timingMode") == "offline-fixed-step-gameplay", "preview timing changed")
    frames = capture.get("frames")
    require(isinstance(frames, list) and len(frames) == 24, "24 preview frames required")
    expected = {"standardIdle_handsDown", "standardIdle_Extra02"} if phase == "idle" else {"death_overworld"}
    for frame in frames:
        require(frame.get("ownerKind") == "player-preview" and frame.get("ownerInstanceId") == PREVIEW_OWNER and frame.get("celInstanceId") == PREVIEW_CEL, "preview frame owner changed")
        require(frame.get("instanceId") == route["renderer"] and frame.get("celRelativeRendererPath") == route["path"], "preview renderer changed")
        require(frame.get("mesh") == route["mesh"] and frame.get("boneSignature") == route["signature"], "preview mesh changed")
        require(frame.get("active") is True and frame.get("enabled") is True and frame.get("isVisible") is True, "preview visibility changed")
        require(any(clip.get("name") in expected and clip.get("weight", 0.0) > 0.0 for clip in base_layer(frame).get("playing", [])), f"{sorted(expected)} missing")
    layers = [base_layer(frame) for frame in frames]
    require(
        any(right["normalizedTime"] > left["normalizedTime"] for left, right in zip(layers, layers[1:])),
        "preview animation did not advance",
    )
    require(capture.get("provenance") == ("observed-runtime" if phase == "idle" else "native-state-playback"), "preview provenance changed")
    if phase == "death":
        require(layers[0]["normalizedTime"] == 0.0 and layers[-1]["normalizedTime"] >= 1.0, "death clip traversal changed")
    delta = maximum_bone_delta(frames[0], frames[-1])
    require(delta > 0.01, "preview capture has no material motion")
    return delta


def journal_records(path: Path) -> dict[str, dict[str, Any]]:
    records = {}
    for line in path.read_text().splitlines():
        row = json.loads(line)
        if row.get("kind") in ("before", "after"):
            records[row["kind"]] = row["data"]
    require(set(records) == {"before", "after"}, "combat before/after records missing")
    return records


def verify_combat(archive: Path) -> dict[str, Any]:
    capture = load_capture(archive / "metadata/combat-body.json.gz")
    require(capture.get("ok") is True and capture.get("session") == COMBAT_SESSION, "combat session changed")
    require(capture.get("scope") == "player-combat" and capture.get("fixedStep") is True, "combat scope changed")
    frames = capture.get("frames")
    require(isinstance(frames, list) and len(frames) == 120, "120 combat frames required")
    for frame in frames:
        require(frame.get("ownerKind") == "player-combat" and frame.get("ownerInstanceId") == 358974 and frame.get("celInstanceId") == -245318, "combat owner changed")
        require(frame.get("instanceId") == -245340 and frame.get("celRelativeRendererPath") == "player_Herbalist", "combat renderer changed")
        require(frame.get("mesh") == ROUTES["body"]["mesh"] and frame.get("boneSignature") == ROUTES["body"]["signature"], "combat mesh changed")
        require(frame.get("active") is True and frame.get("enabled") is True and frame.get("isVisible") is True, "combat visibility changed")
    clips = [clip.get("name") for frame in frames for clip in base_layer(frame).get("playing", [])]
    require("attack_blunt1H" in clips and "damageLight_blunt1H" in clips, "ordinary attack or hit clip missing")
    records = journal_records(archive / "metadata/combat-action-journal.jsonl")
    before, after = records["before"], records["after"]
    require(before["combat"]["enemies"][0]["hp"] == 72 and after["combat"]["enemies"][0]["hp"] == 62, "ordinary attack HP changed")
    require(before["party"][0]["hp"] == 999 and after["party"][0]["hp"] == 962, "ordinary hit HP changed")
    return {"frames": len(frames), "enemyHp": [72, 62], "heroHp": [999, 962]}


def verify(archive: Path = DEFAULT) -> dict[str, Any]:
    archive = archive.resolve()
    validation = load_json(archive / "validation.json")
    integrity = load_json(archive / "integrity.json")
    require(validation.get("schema") == "ftkmf.player-canonical-route.v1" and validation.get("topologyGroup") == "45c7a9b9fb730195", "validation identity changed")
    require(validation.get("sessions") == {"preview": PREVIEW_SESSION, "combat": COMBAT_SESSION}, "validation sessions changed")
    expected = integrity.get("files")
    require(integrity.get("schema") == "ftkmf.canonical-archive-integrity.v1" and isinstance(expected, dict), "integrity schema changed")
    actual = {str(path.relative_to(archive)) for path in archive.rglob("*") if path.is_file() and path.name != "integrity.json"}
    require(set(expected) == actual, "full archive integrity coverage changed")
    for relative, digest in expected.items():
        require(sha256(archive / relative) == digest, f"archive file changed: {relative}")

    death = validation.get("explicitDeathFixture", {})
    require(death.get("provenance") == "native-state-playback" and death.get("ordinaryPlayerDeath") is False, "death boundary changed")
    require(validation.get("equipment", {}).get("customApparelApplicable") is False, "apparel applicability changed")
    lifecycle = validation.get("lifecycle", {})
    require(lifecycle.get("firstCombatCelInstanceId") == -245318 and lifecycle.get("secondCombatCelInstanceId") == -249004 and lifecycle.get("sharedLeaseId") == 3, "lifecycle evidence changed")
    second = load_json(archive / "metadata/combat-inventory-second.json")
    custom = [row for row in second.get("renderers", []) if str(row.get("mesh", "")).startswith("ftkmf_glb_wildbloom-")]
    require(len(custom) == 3 and {row.get("celInstanceId") for row in custom} == {-249004}, "second combat bindings changed")
    require(all(row.get("resourceLease", {}).get("leaseId") == 3 and row["resourceLease"].get("references") == 2 for row in custom), "second combat lease changed")

    ready = validation.get("gameplay", {}).get("progression", {}).get("strictReady")
    require(ready == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}, "progression Ready changed")
    stage = load_json(archive / "metadata/second-combat-stage.json")
    party = stage.get("finalState", {}).get("party", [])
    require(len(party) == 1 and (party[0]["level"], party[0]["xp"], party[0]["gold"]) == (1, 38, 46), "native progression changed")

    selected = validation.get("selectedFrames", [])
    require(len(selected) == 8, "eight selected frames required")
    prefix = Path("art-experiments/wildbloom-herbalist/live-validation-v2-canonical")
    for row in selected:
        relative = Path(row["path"])
        require(relative.is_relative_to(prefix), "selected frame path changed")
        candidate = archive / relative.relative_to(prefix)
        require(candidate.read_bytes().startswith(b"\x89PNG\r\n\x1a\n") and sha256(candidate) == row["sha256"], "selected frame changed")

    prior = validation.get("priorEvidence", {}).get("v1", {})
    require(sha256(archive / prior["validation"]) == prior["sha256"] and sha256(archive / prior["integrity"]) == prior["integritySha256"], "prior V1 pins changed")
    deltas = {f"{phase}-{part}": verify_preview_capture(archive, phase, part) for phase in ("idle", "death") for part in ROUTES}
    return {"ok": True, "status": validation["status"], "files": len(actual), "topologyGroup": validation["topologyGroup"], "previewMotion": deltas, "combat": verify_combat(archive), "ordinaryPlayerDeath": False, "customApparelApplicable": False}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, nargs="?", default=DEFAULT)
    args = parser.parse_args()
    print(json.dumps(verify(args.archive), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
