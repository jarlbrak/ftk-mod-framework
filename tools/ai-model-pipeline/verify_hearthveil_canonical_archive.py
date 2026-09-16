#!/usr/bin/env python3
"""Verify the canonical Hearthveil Blacksmith player-route archive."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "art-experiments/hearthveil-blacksmith/live-validation-v3-canonical"
SESSION = "d34d1cdb635240aaaeb42b49329cbab2"
OWNER = -234506
CEL = -236190

ROUTES = {
    "body": {
        "path": "playerBlacksmith",
        "renderer": -236208,
        "mesh": "ftkmf_glb_hearthveil-body.glb",
        "signature": "c5145eb658efedf4e2e984e600fb9bb3d39843332ada65f8defe30ffcb306b93",
        "topology": "85c742f628ea6d37",
    },
    "hair": {
        "path": "hairBottom",
        "renderer": -236196,
        "mesh": "ftkmf_glb_hearthveil-hair-bottom.glb",
        "signature": "9253fc539e70c4093f788df51a5def2849357a36dbb58d1d72d05455675a2953",
        "topology": "4082b6c4e777f922",
    },
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
    require(isinstance(value, dict), f"capture JSON object required: {path}")
    return value


def base_layer(frame: dict[str, Any]) -> dict[str, Any]:
    layers = frame.get("animator", {}).get("layers", [])
    require(bool(layers) and layers[0].get("layer") == 0, "base animator layer missing")
    return layers[0]


def maximum_bone_delta(first: dict[str, Any], last: dict[str, Any]) -> float:
    left = {bone["name"]: bone["localToWorld"] for bone in first["bones"]}
    right = {bone["name"]: bone["localToWorld"] for bone in last["bones"]}
    require(set(left) == set(right) and bool(left), "bone set changed across capture")
    return max(abs(a - b) for name in left for a, b in zip(left[name], right[name]))


def verify_capture(archive: Path, phase: str, part: str) -> float:
    route = ROUTES[part]
    capture = load_capture(archive / f"metadata/{phase}-{part}.json.gz")
    require(capture.get("ok") is True and capture.get("session") == SESSION, "capture session or status changed")
    require(capture.get("scope") == "player-preview", "capture scope changed")
    require(capture.get("ownerInstanceId") == OWNER and capture.get("celInstanceId") == CEL,
            "capture owner identity changed")
    require(capture.get("fixedStep") is True and capture.get("timingMode") == "offline-fixed-step-gameplay",
            "fixed-step capture contract changed")
    frames = capture.get("frames")
    require(isinstance(frames, list) and len(frames) == 24, "exactly 24 capture frames required")
    for frame in frames:
        require(frame.get("ownerKind") == "player-preview", "frame owner kind changed")
        require(frame.get("ownerInstanceId") == OWNER and frame.get("celInstanceId") == CEL,
                "frame owner identity changed")
        require(frame.get("instanceId") == route["renderer"] and frame.get("celRelativeRendererPath") == route["path"],
                "renderer identity changed")
        require(frame.get("mesh") == route["mesh"] and frame.get("boneSignature") == route["signature"],
                "mesh or bone signature changed")
        require(frame.get("isVisible") is True and frame.get("enabled") is True and frame.get("active") is True,
                "renderer was not visible, enabled and active throughout")
        require(frame.get("animator", {}).get("enabled") is True, "animator was disabled")
    first_layer = base_layer(frames[0])
    last_layer = base_layer(frames[-1])
    expected_clip = "standardIdle_handsDown" if phase == "idle" else "death_overworld"
    for layer in (first_layer, last_layer):
        playing = layer.get("playing", [])
        require(any(clip.get("name") == expected_clip and clip.get("weight") == 1.0 for clip in playing),
                f"expected {expected_clip} clip missing")
    require(last_layer["normalizedTime"] > first_layer["normalizedTime"], "animation time did not advance")
    if phase == "death":
        require(capture.get("provenance") == "native-state-playback", "death fixture provenance changed")
        require(first_layer["normalizedTime"] == 0.0 and last_layer["normalizedTime"] >= 1.0,
                "death fixture did not traverse the clip")
    else:
        require(capture.get("provenance") == "observed-runtime", "idle provenance changed")
    delta = maximum_bone_delta(frames[0], frames[-1])
    require(delta > 0.01, "capture has no material bone motion")
    return delta


def verify(archive: Path = DEFAULT) -> dict[str, Any]:
    archive = archive.resolve()
    validation = load_json(archive / "validation.json")
    integrity = load_json(archive / "integrity.json")
    require(validation.get("schema") == "ftkmf.player-canonical-route.v1", "validation schema changed")
    require(validation.get("session") == SESSION, "validation session changed")
    require(integrity.get("schema") == "ftkmf.canonical-archive-integrity.v1", "integrity schema changed")

    expected = integrity.get("files")
    require(isinstance(expected, dict), "integrity file map missing")
    actual = {str(path.relative_to(archive)) for path in archive.rglob("*")
              if path.is_file() and path.name != "integrity.json"}
    require(set(expected) == actual, "full archive integrity coverage changed")
    for relative, digest in expected.items():
        require(sha256(archive / relative) == digest, f"archive file changed: {relative}")

    topology = {row.get("topologyGroup") for row in validation.get("renderers", [])}
    require(topology == {route["topology"] for route in ROUTES.values()}, "topology coverage changed")
    death = validation.get("explicitDeathFixture", {})
    require(death.get("provenance") == "native-state-playback" and death.get("ordinaryPlayerDeath") is False,
            "death fixture boundary changed")
    require(validation.get("gameplay", {}).get("ordinaryAttack", {}).get("enemyHp") == [72, 62],
            "ordinary attack evidence changed")
    ready = validation.get("gameplay", {}).get("finalReady", {})
    require(ready.get("ok") is True and ready.get("level") == 0 and ready.get("room") == 3,
            "strict Ready evidence changed")

    selected = validation.get("selectedFrames", [])
    require(len(selected) == 4, "four selected review frames required")
    for row in selected:
        relative = Path(row["path"])
        prefix = Path("art-experiments/hearthveil-blacksmith/live-validation-v3-canonical")
        require(relative.is_relative_to(prefix), "selected frame path changed")
        candidate = archive / relative.relative_to(prefix)
        require(candidate.read_bytes().startswith(b"\x89PNG\r\n\x1a\n") and sha256(candidate) == row["sha256"],
                "selected frame pin changed")

    prior = validation.get("priorEvidence", {})
    for revision in ("v1", "v2"):
        pointer = prior.get(revision, {})
        require(sha256(archive / pointer["validation"]) == pointer["sha256"], f"prior {revision} validation changed")
        require(sha256(archive / pointer["integrity"]) == pointer["integritySha256"],
                f"prior {revision} integrity changed")
    v1 = load_json(archive / prior["v1"]["validation"])
    require(v1.get("gameplay", {}).get("ordinaryAttack", {}).get("enemyHp") == [72, 62], "V1 attack changed")
    require(v1.get("gameplay", {}).get("pass", {}).get("heroHp") == [970, 913], "V1 hit changed")
    require(v1.get("gameplay", {}).get("finalReady") == {"ok": True, "level": 0, "room": 3, "buttonCount": 1},
            "V1 Ready evidence changed")
    require(len(v1.get("equipment", {}).get("oldLeaseDisposal", [])) == 2
            and all(row.get("allResourcesUnityNull") is True for row in v1["equipment"]["oldLeaseDisposal"]),
            "V1 lifecycle evidence changed")

    deltas = {f"{phase}-{part}": verify_capture(archive, phase, part)
              for phase in ("idle", "death") for part in ("body", "hair")}
    return {
        "ok": True,
        "status": validation["status"],
        "files": len(actual),
        "session": SESSION,
        "topologyGroups": sorted(topology),
        "maximumBoneDeltas": deltas,
        "ordinaryPlayerDeath": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, nargs="?", default=DEFAULT)
    args = parser.parse_args()
    print(json.dumps(verify(args.archive), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
