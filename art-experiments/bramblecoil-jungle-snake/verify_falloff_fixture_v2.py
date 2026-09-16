#!/usr/bin/env python3
"""Verify the paired Bramblecoil normal-death fall-off fixture without guessing.

The opted and policy-omitted profiles must be exercised against the same deployed
catalog and DLLs.  Both inputs are summaries written by ``run_falloff_fixture_v2.py``.
This verifier preserves source pins and records the narrow observed distinction:
under the explicit policy the leased custom renderer remains visible through the
native Snake_Death handoff; without it, the custom renderer becomes hidden and
the native thirteen-piece fall-off body becomes dynamic.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = Path(__file__).resolve().parent
MESH = "ftkmf_glb_bramblecoil-jungle-snake.glb"
RENDERER = "enJungleSnakeC"
OPTED = "ftkmf_modeltest_bramblecoil_jungle_snake"
CONTROL = "ftkmf_modeltest_bramblecoil_jungle_snake_native_falloff_control"
POLICY = "preserve-custom-body"


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--opted", type=Path, required=True, help="Opted fixture summary from scratch/.")
parser.add_argument("--native-control", type=Path, required=True, help="Policy-omitted fixture summary from scratch/.")
parser.add_argument("--output", type=Path, default=ASSET / "falloff-policy-comparison-v2.json")
args = parser.parse_args()


def resolve(path: Path) -> Path:
    return path.resolve(strict=True)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rel(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def pin(path: Path) -> dict:
    return {"source": rel(path), "sha256": sha(path), "bytes": path.stat().st_size}


def death_state(frame: dict) -> bool:
    layers = (frame.get("animator") or {}).get("layers") or []
    playing = layers[0].get("playing", []) if layers else []
    return any(str(clip.get("name", "")).startswith("Snake_Death") for clip in playing)


def dynamic_count(frame: dict) -> int:
    return sum(body.get("isKinematic") is False for body in ((frame.get("ragdoll") or {}).get("rigidbodies") or []))


def state_names(frame: dict) -> list[str]:
    layers = (frame.get("animator") or {}).get("layers") or []
    return [str(clip.get("name")) for layer in layers for clip in (layer.get("playing") or [])]


def source_path(summary: dict, item: dict) -> Path:
    raw = Path(item["path"])
    return raw if raw.is_absolute() else ROOT / raw


def validate(label: str, summary_path: Path, expected_profile: str, expected_policy: str | None) -> dict:
    summary = read(summary_path)
    assert summary.get("status") == "RECORDED_FALLOFF_FIXTURE_PENDING_VERIFICATION", summary.get("status")
    assert summary.get("variant") == label
    assert summary.get("profile") == expected_profile
    assert summary.get("rendererPath") == RENDERER
    assert summary.get("expectedPolicy") == expected_policy
    registration = summary["registrationEntry"]
    assert registration.get("key") == expected_profile
    assert registration.get("fallOffPolicy") == expected_policy
    assert summary["fixtureAction"].get("committed") == "KillSingle"

    capture = source_path(summary, summary["capture"])
    assert capture.is_file() and sha(capture) == summary["capture"]["sha256"]
    raw = read(capture)
    frames = raw.get("frames") or []
    assert raw.get("ok") is True and len(frames) == 120
    assert raw.get("session") == summary.get("session")
    assert all(frame.get("mesh") == MESH for frame in frames)
    identity = {key: frames[0].get(key) for key in ("mesh", "ownerInstanceId", "instanceId", "boneSignature")}
    assert all(all(frame.get(key) == value for key, value in identity.items()) for frame in frames)
    assert identity["ownerInstanceId"] is not None and identity["boneSignature"]

    deaths = [index for index, frame in enumerate(frames) if death_state(frame)]
    assert deaths and deaths[0] == summary.get("firstNativeDeathFrame")
    first_death = deaths[0]
    first_hidden = next((index for index, frame in enumerate(frames)
                         if frame.get("active") is False or frame.get("isVisible") is False), None)
    first_dynamic = next((index for index, frame in enumerate(frames) if dynamic_count(frame) > 0), None)
    selected = sorted({0, max(0, first_death - 2), max(0, first_death - 1), first_death,
                       min(119, first_death + 1), min(119, first_death + 3), 40, 60, 119})
    capture_dir = capture.with_suffix("")
    screenshots = {}
    for index in selected:
        image = capture_dir / f"{index:04d}.png"
        assert image.is_file()
        screenshots[str(index)] = pin(image)
    return {
        "summary": pin(summary_path),
        "capture": pin(capture),
        "registration": pin(source_path(summary, summary["registration"])),
        "fixture": pin(source_path(summary, summary["fixture"])),
        "session": summary["session"],
        "profile": expected_profile,
        "policy": expected_policy,
        "identity": identity,
        "firstNativeDeathFrame": first_death,
        "firstNativeDeathGameSeconds": frames[first_death]["gameSeconds"],
        "firstHiddenFrame": first_hidden,
        "firstDynamicFrame": first_dynamic,
        "checkpoints": [
            {
                "frame": index,
                "gameSeconds": frames[index]["gameSeconds"],
                "state": state_names(frames[index]),
                "lastCombatTrigger": frames[index].get("lastCombatTrigger"),
                "active": frames[index].get("active"),
                "visible": frames[index].get("isVisible"),
                "enabled": frames[index].get("enabled"),
                "rigidbodyCount": len(((frames[index].get("ragdoll") or {}).get("rigidbodies") or [])),
                "dynamicBodies": dynamic_count(frames[index]),
            }
            for index in selected
        ],
        "screenshots": screenshots,
        "_frames": frames,
        "_deployment": summary["deployment"],
    }


opted = validate("opted", resolve(args.opted), OPTED, POLICY)
control = validate("native-control", resolve(args.native_control), CONTROL, None)

# These runs must share the exact catalog and three DLL payloads.  A different
# game session is intentional; the comparison is between fresh launches.
for key in ("catalog", "receipt"):
    assert opted["_deployment"][key]["sha256"] == control["_deployment"][key]["sha256"], key
assert set(opted["_deployment"]["plugins"]) == set(control["_deployment"]["plugins"])
for name, item in opted["_deployment"]["plugins"].items():
    assert item["sha256"] == control["_deployment"]["plugins"][name]["sha256"], name
assert opted["identity"]["mesh"] == control["identity"]["mesh"] == MESH
assert opted["identity"]["boneSignature"] == control["identity"]["boneSignature"]

opted_frames = opted.pop("_frames")
control_frames = control.pop("_frames")
opted_deployment = opted.pop("_deployment")
control.pop("_deployment")
first_opted_death = opted["firstNativeDeathFrame"]
first_control_death = control["firstNativeDeathFrame"]

# The policy never replaces the actual native death route: both captures reach
# a Snake_Death state.  It only changes the target renderer's FallOffLimb result.
assert all(frame.get("active") is True and frame.get("isVisible") is True and frame.get("enabled") is True
           for frame in opted_frames[first_opted_death:])
assert len(((opted_frames[first_opted_death + 1].get("ragdoll") or {}).get("rigidbodies") or [])) == 26
assert dynamic_count(opted_frames[first_opted_death + 1]) == 26
assert dynamic_count(opted_frames[-1]) == 26

assert control["firstHiddenFrame"] is not None and control["firstHiddenFrame"] <= first_control_death
assert all(frame.get("active") is False and frame.get("isVisible") is False
           for frame in control_frames[control["firstHiddenFrame"]:])
assert len(((control_frames[first_control_death + 1].get("ragdoll") or {}).get("rigidbodies") or [])) == 13
assert dynamic_count(control_frames[first_control_death + 1]) == 13
assert dynamic_count(control_frames[-1]) == 13

report = {
    "status": "PASS_PAIRED_EXPLICIT_FALLOFF_POLICY_FIXTURE",
    "scope": (
        "Fresh explicit KillSingle fixtures only. This proves the observed renderer/ragdoll handoff under the exact "
        "policy and a same-binary omitted-policy control. It is not ordinary lethal-damage, all-angle, portrait, "
        "corpse-quality, or native cleanup/Ready evidence."
    ),
    "rendererPath": RENDERER,
    "mesh": MESH,
    "sharedDeployment": {
        "receipt": opted_deployment["receipt"],
        "catalog": opted_deployment["catalog"],
        "plugins": opted_deployment["plugins"],
    },
    "opted": opted,
    "nativeControl": control,
    "observedDifference": {
        "sameNativeDeathState": [first_opted_death, first_control_death],
        "opted": (
            "The explicitly leased custom renderer stayed active, visible, and enabled from the first native "
            "Snake_Death frame through the final captured frame; its 26 native ragdoll bodies were dynamic by the next frame."
        ),
        "nativeControl": (
            "With the policy omitted, the same custom renderer became inactive and invisible by capture frame "
            f"{control['firstHiddenFrame']}; the native 13-piece fall-off body was dynamic by the next death frame."
        ),
    },
    "limitations": [
        "KillSingle is an explicit fixture and does not demonstrate ordinary damage or ordinary lethal combat.",
        "The final capture reaches the loot screen but this paired verifier does not submit native Collect votes or assert Ready progression.",
        "Screenshot review samples selected frames; it does not prove every camera angle, material lifetime, or presentation quality.",
    ],
}
output = args.output.resolve()
if output.exists():
    raise SystemExit(f"Refusing to overwrite evidence report: {output}")
output.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"report": str(output), "status": report["status"]}, indent=2))
