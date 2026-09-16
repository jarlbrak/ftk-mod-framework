#!/usr/bin/env python3
"""Verify the exact Gloamfin Kraken portrait follow-up evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "docs/evidence/kraken-portrait-followup-v1/validation.json"


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def path(value: str) -> Path:
    candidate = Path(value)
    return candidate if candidate.is_absolute() else ROOT / candidate


def digest(candidate: Path) -> str:
    return hashlib.sha256(candidate.read_bytes()).hexdigest()


def pinned(pointer: dict, label: str) -> Path:
    candidate = path(pointer["path"])
    require(candidate.is_file(), f"missing {label}")
    require(digest(candidate) == pointer["sha256"], f"{label} hash mismatch")
    return candidate


def verify(evidence_path: Path = DEFAULT) -> dict:
    evidence = json.loads(evidence_path.read_text())
    require(evidence["schema"] == "ftkmf.kraken-portrait-followup-evidence.v1", "schema changed")
    require(evidence["status"] == "portrait_appearance_and_native_hud_use_passed", "status changed")
    require(evidence["route"]["topologyGroup"] == "6a28ac3cf4523c24", "topology changed")
    pinned(evidence["framework"], "framework")
    pinned(evidence["framework"]["legacyPortraitSource"], "legacy portrait source")
    pinned(evidence["framework"]["framingSource"], "portrait framing source")
    pinned(evidence["runtimeHelper"]["portraitTraceSource"], "portrait trace source")
    runner = json.loads(pinned(evidence["runner"], "runner").read_text())
    deployment_records = []
    for index, deployment in enumerate(evidence["deployments"]):
        record = json.loads(pinned(deployment, f"deployment {index}").read_text())
        require(record["status"] == "VERIFIED_COMPLETE", "deployment incomplete")
        deployment_records.append(record)
    helper_hash = evidence["runtimeHelper"]["sha256"]
    require(any(
        row.get("new", {}).get("BepInEx/plugins/FtkRuntimeModelTest.dll", {}).get("sha256") == helper_hash
        for row in deployment_records
    ), "runtime helper is not pinned by a verified deployment")
    portrait_path = pinned(evidence["portrait"], "portrait")
    require(portrait_path.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", "portrait is not PNG")

    require(runner["status"] == "needs_manual_portrait_review", "runner status changed")
    require(runner["session"] == evidence["session"], "session mismatch")
    require(runner["framework"]["sha256"] == evidence["framework"]["sha256"], "runner framework mismatch")
    records = [item for item in runner["trace"]["records"]
               if item.get("identityResolution") == "verified_live_enemy_dummy"]
    require(len(records) == 1, "expected one live portrait trace")
    trace = records[0]
    native = evidence["nativeCapture"]
    require(trace["frame"] == native["traceFrame"], "trace frame mismatch")
    require(trace["enemyDummyInstanceId"] == native["enemyDummyInstanceId"], "dummy mismatch")
    require(trace["fid"] == native["fid"], "FID mismatch")
    require(trace["telemetryComplete"] is True and trace["nativeSnapshotFinalized"] is True
            and trace["nativeSnapshotException"] is None, "native snapshot did not finalize cleanly")
    require(trace["marker"]["firstArgumentAtLastPrefix"] == native["generatedMarker"], "generated marker mismatch")
    require(trace["renderTexture"]["width"] == native["camera"]["renderTextureWidth"]
            and trace["renderTexture"]["height"] == native["camera"]["renderTextureHeight"], "render target changed")
    require(trace["camera"]["fieldOfView"] == native["camera"]["fieldOfView"]
            and trace["camera"]["near"] == native["camera"]["near"]
            and trace["camera"]["far"] == native["camera"]["far"], "native camera changed")
    require(trace["source"]["rootLocal"][0][0] == native["sourceRootUniformScale"], "source scale changed")
    clone_scale = abs(trace["target"]["rootLocal"][0][0])
    require(abs(clone_scale - native["portraitCloneUniformScale"]) < 1e-7, "portrait clone scale changed")
    for view in (trace["source"], trace["target"]):
        require(view["traversalOrRenderersTruncated"] is False and len(view["renderers"]) == 1,
                "renderer telemetry incomplete")
        renderer = view["renderers"][0]
        require(renderer["meshName"] == native["customMesh"], "custom mesh mismatch")
        require(renderer["sharedMaterials"][0]["mainTexture"]["name"] == native["customTexture"], "custom texture mismatch")
    require(trace["source"]["resourceLease"]["references"] == native["sourceLeaseReferencesDuringSnapshot"], "source lease count changed")
    require(trace["target"]["resourceLease"]["references"] == native["portraitCloneLeaseReferencesDuringSnapshot"], "clone lease count changed")

    capture = runner["capture"]
    require(capture["png"]["sha256"] == evidence["portrait"]["sha256"], "capture PNG mismatch")
    require(capture["texture"]["width"] == evidence["portrait"]["width"]
            and capture["texture"]["height"] == evidence["portrait"]["height"], "portrait dimensions changed")
    images = capture["activeRawImages"]
    require(len(images) == native["activeHudRawImagesUsingExactTexture"], "active HUD image count changed")
    require(all(item["textureInstanceId"] == capture["texture"]["instanceId"]
                and item["canvasAlpha"] == 1 and item["canvasCull"] is False for item in images),
            "active HUD image identity/visibility changed")
    require(evidence["portrait"]["review"]["status"] == "pass", "manual review is not accepted")
    require(all(value == "pass" for value in evidence["gates"].values()), "one portrait gate is not pass")
    return {"ok": True, "session": evidence["session"], "activeHudImages": len(images),
            "cloneScale": clone_scale, "portraitSha256": evidence["portrait"]["sha256"]}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence", type=Path, default=DEFAULT)
    args = parser.parse_args()
    print(json.dumps(verify(args.evidence), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
