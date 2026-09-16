#!/usr/bin/env python3
"""Verify the retained pre-portrait-fix Kraken visual review and its raw evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REVIEW = ROOT / "docs/evidence/kraken-production-visual-v1/review.json"
EXPECTED_ROUTE = {
    "topologyGroup": "6a28ac3cf4523c24",
    "sourceKind": "resource_prefab_override",
    "nativeEnemy": "krakenHead",
    "resourcePrefab": "enkrakenhead",
    "registeredEnemy": "ftkmf_modeltest_gloamfin_kraken_legacy",
    "rendererPath": "krakenHead",
    "sourceRendererId": 121260,
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rooted(value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def event(capture: dict, method: str, **fields: object) -> dict:
    matches = [item for item in capture["motionObservation"]["events"]
               if item.get("nativeMethod") == method
               and all(item.get(key) == value for key, value in fields.items())]
    require(len(matches) == 1, f"expected one {method} event matching {fields}, found {len(matches)}")
    return matches[0]


def verify(review_path: Path = DEFAULT_REVIEW) -> dict:
    review = json.loads(review_path.read_text())
    require(review.get("schema") == "ftkmf.kraken-production-visual-review.v1", "unexpected schema")
    require(review.get("status") == "appearance_motion_camera_culling_and_progression_reviewed_portrait_failed",
            "this verifier is only for the retained pre-fix review")
    require(review.get("route") == EXPECTED_ROUTE, "exact Kraken route changed")
    require(review["review"]["portrait"]["status"] == "fail", "pre-fix portrait result changed")

    for pointer in (review["case"], review["productionCampaign"]):
        path = rooted(pointer["path"])
        require(path.is_file(), f"missing evidence file: {pointer['path']}")
        require(sha256(path) == pointer["sha256"], f"hash mismatch: {pointer['path']}")

    binding = review["binding"]
    captures: dict[str, dict] = {}
    for pointer in review["captures"]:
        path = rooted(pointer["path"])
        require(path.is_file(), f"missing capture: {pointer['path']}")
        require(sha256(path) == pointer["sha256"], f"capture hash mismatch: {pointer['path']}")
        capture = json.loads(path.read_text())
        require(capture["id"] == pointer["captureId"], "capture id mismatch")
        require(capture["session"] == review["session"], "capture session mismatch")
        require(len(capture["frames"]) == pointer["frames"], "capture frame count mismatch")
        require(capture["ownerInstanceId"] == binding["ownerInstanceId"], "owner identity changed")
        require(capture["celInstanceId"] == binding["celInstanceId"], "CEL identity changed")
        for frame in capture["frames"]:
            require(frame["instanceId"] == binding["rendererInstanceId"], "renderer identity changed")
            require(frame["ownerInstanceId"] == binding["ownerInstanceId"], "frame owner changed")
            require(frame["celInstanceId"] == binding["celInstanceId"], "frame CEL changed")
            require(frame["mesh"] == binding["mesh"], "mesh changed")
            require(frame["boneSignature"] == binding["boneSignature"], "bone signature changed")
        for index in pointer["reviewedFrameIndices"]:
            require(0 <= index < len(capture["frames"]), "reviewed frame index is out of range")
        captures[pointer["captureId"]] = capture

    attack_pointer, hit_pointer, death_pointer = review["captures"]
    attack = captures[attack_pointer["captureId"]]
    hit = captures[hit_pointer["captureId"]]
    death = captures[death_pointer["captureId"]]
    require(attack["ok"] is True and all(f["active"] and f["enabled"] and f["isVisible"] for f in attack["frames"]),
            "attack capture visibility contract failed")
    attack_event = event(attack, "CharacterDummy.PlayAttackSequence entry", role="attacker")
    require(attack_event["attackAnim"] == "AttackProf" and attack_event["override"] == "Attack", "native attack authority changed")
    require(event(attack, "CharacterEventListener.CombatTrigger entry", trigger="Attack")["frame"] == attack_pointer["nativeAttackFrame"],
            "native attack frame changed")

    require(hit["ok"] is True and all(f["active"] and f["enabled"] and f["isVisible"] for f in hit["frames"]),
            "hit capture visibility contract failed")
    hit_event = event(hit, "CharacterDummy.PlayAttackSequence entry", role="victim")
    primary = hit_event["primary"]
    require(primary["damage"] == hit_pointer["damage"] and primary["newHealth"] == hit_pointer["healthAfter"],
            "ordinary damage evidence changed")
    require(hit_pointer["healthBefore"] - hit_pointer["healthAfter"] == hit_pointer["damage"], "health arithmetic changed")
    require(primary["attackResponse"] == hit_pointer["nativeResponse"], "native hit response changed")
    require(event(hit, "CharacterEventListener.CombatTrigger entry", trigger="Damaged")["frame"] == hit_pointer["nativeTriggerFrame"],
            "native damage trigger frame changed")

    require(death["ok"] is False and "Renderer destroyed during capture" in death["error"], "death prefix termination changed")
    require(all(f["active"] and f["enabled"] and f["isVisible"] for f in death["frames"][:90]), "live death-prefix visibility changed")
    require(not death["frames"][90]["active"] and not death["frames"][90]["isVisible"], "death cleanup boundary changed")
    require(death["frames"][89]["animator"]["enabled"] and not death["frames"][90]["animator"]["enabled"],
            "death animator cleanup boundary changed")
    death_event = event(death, "CharacterDummy.PlayAttackSequence entry", role="victim")
    require(death_event["primary"]["attackResponse"] == "Death" and death_event["primary"]["newHealth"] == 0,
            "fixture death response changed")
    require(event(death, "CharacterEventListener.CombatTrigger entry", trigger="Death")["frame"] == death_pointer["nativeTriggerFrame"],
            "native death trigger frame changed")

    selected = {(item["captureId"], item["index"]): item for item in review["selectedFrames"]}
    expected = {(pointer["captureId"], index) for pointer in review["captures"] for index in pointer["reviewedFrameIndices"]}
    require(set(selected) == expected, "selected frames do not exactly match reviewed frame indices")
    for key, pointer in selected.items():
        path = rooted(pointer["path"])
        require(path.is_file() and sha256(path) == pointer["sha256"], f"selected frame mismatch: {pointer['path']}")
        require(path.name == f"{key[1]:04d}.png", "selected frame filename/index mismatch")

    return {
        "ok": True,
        "status": review["status"],
        "captureCount": len(captures),
        "selectedFrameCount": len(selected),
        "portrait": "fail",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--review", type=Path, default=DEFAULT_REVIEW)
    args = parser.parse_args()
    print(json.dumps(verify(args.review), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
