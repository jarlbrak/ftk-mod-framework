#!/usr/bin/env python3
"""Record the reviewed scoped live verdict for the Tidecrown Sea King trial."""
import hashlib
import json
import os
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
FOCUS_CASE = BASE / "case-973cc0686a404bdf9cf30a2c18332478/case-result.json"
NO_FOCUS_CASE = BASE / "case-cb2e67a09f2d403492804259d7222b98/case-result.json"
OUT = ROOT / "scratch/tidecrown-root-visual-review-v1.json"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def actions(case: dict) -> dict:
    values = {}
    for action in case["actions"]:
        key = (action["action"], action.get("attempt"))
        assert key not in values, key
        values[key] = action
    return values


def frame(action: dict, index: int) -> Path:
    source = Path(action["capture"]["rawCapture"]["path"])
    image = source.with_suffix("") / f"{index:04d}.png"
    assert image.is_file(), image
    return image


assert not OUT.exists() or os.environ.get("FTK_REVIEW_REBUILD") == "1", "Refusing to overwrite a completed review."
focus = read(FOCUS_CASE)
no_focus = read(NO_FOCUS_CASE)

assert focus["schema"] == "ftkmf.exercise-case.v1"
assert focus["session"] == "fb9c45f58bae421db60332bd1594667c"
assert focus["enemy"] == "ftkmf_modeltest_tidecrown_sea_king"
assert focus["rendererPath"] == "enSeaKing"
assert focus["focusedAttack"] is True and focus["status"] == "needs_visual_review"
assert focus["profileSha256"] == "5c635cece83a0c990a174dbd458e1433cca9b330b3bb597911e7d97cee158611"
focus_actions = actions(focus)
assert set(focus_actions) == {
    ("pass", None), ("attack", 1), ("attack", 2), ("attack", 3), ("attack", 4), ("kill-fixture", None),
}
pass_action = focus_actions[("pass", None)]
hit_action = focus_actions[("attack", 4)]
death_action = focus_actions[("kill-fixture", None)]
for key, action in focus_actions.items():
    boundary = action["capture"]["boundary"]
    if key == ("kill-fixture", None):
        assert boundary["partial"] is True and boundary["retainedFrameCount"] == 91
        assert boundary["termination"] == "renderer_destroyed"
        assert boundary["rawCaptureOk"] is False
    else:
        assert boundary["completeCapture"] is True and boundary["retainedFrameCount"] == 120
        assert boundary["rawCaptureOk"] is True
assert hit_action["actionResult"]["result"]["committed"] == "Attack(focus)"
assert hit_action["hpOutcome"]["status"] == "nonlethal_hp_loss"
assert (hit_action["hpOutcome"]["beforeHp"], hit_action["hpOutcome"]["afterHp"]) == (720, 719)
assert death_action["actionResult"]["result"]["committed"] == "KillSingle"
assert focus["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert focus["collects"] == []

renderer = focus["initialRenderer"]
assert renderer["mesh"] == "ftkmf_glb_tidecrown-sea-king.glb"
assert renderer["boneSignature"] == "4fd853bf7fe19398edeb4cb997a0aba409d3299091249c72a2d17ee01ca0ae6a"
assert renderer["rootBone"] == "Root_M"
assert renderer["rendererPath"] == "enSeaKing(Clone)/enSeaKing"
assert renderer["ownerKind"] == "enemies" and renderer["isVisible"] is True
assert renderer["animator"]["controller"] == "seaKingController"
assert renderer["materials"] == [{
    "instanceId": renderer["materials"][0]["instanceId"],
    "name": "matLoot (Instance)", "shader": "Standard", "emissionKeyword": False,
    "emissionColor": [0.0, 0.0, 0.0, 1.0], "_MainTexSupported": True,
    "_MainTex": {"instanceId": renderer["materials"][0]["_MainTex"]["instanceId"], "name": "ftkmf_tidecrown-sea-king_basecolor.png"},
    "_EmissionMapSupported": True, "_EmissionMap": None,
}]

raw_death = read(Path(death_action["capture"]["rawCapture"]["path"]))
assert raw_death["ok"] is False and len(raw_death["frames"]) == 91
first_dynamic = next(
    index for index, sample in enumerate(raw_death["frames"])
    if any(not body["isKinematic"] for body in sample["ragdoll"]["rigidbodies"])
)
assert first_dynamic == 28
assert sum(not body["isKinematic"] for body in raw_death["frames"][30]["ragdoll"]["rigidbodies"]) == 12
assert sum(not body["isKinematic"] for body in raw_death["frames"][60]["ragdoll"]["rigidbodies"]) == 11
assert raw_death["frames"][90]["active"] is False and raw_death["frames"][90]["isVisible"] is False

assert no_focus["schema"] == "ftkmf.exercise-case.v1"
assert no_focus["session"] == "7f3625d876374db98d01f2aebb0bf10f"
assert no_focus["enemy"] == focus["enemy"] and no_focus["rendererPath"] == "enSeaKing"
assert no_focus["focusedAttack"] is False and no_focus["status"] == "stopped"
assert no_focus["error"] == "Attack gate unmet after bounded native retry: no_hp_loss_unclassified"
assert len(no_focus["attackRetrySummary"]) == 8
assert all(row["status"] == "no_hp_loss_unclassified" and row["beforeHp"] == row["afterHp"] == 720
           for row in no_focus["attackRetrySummary"])

choices = [
    ("pass", None, 12, "Exact-owner combat pass: Tidecrown's dark teal mantle and red coral-reliquary chest panel are visibly present at Sea King scale, with the game-owned trident alongside the replacement body."),
    ("attack", 4, 30, "The accepted focus attack's recorded critical-1 frame visibly retains the custom torso, mantle panels, and native external trident while the player and combat UI overlap the lower body."),
    ("attack", 4, 60, "During the continued native combat exchange, the visible custom torso stays connected and readable behind the player. Effects and the camera angle prevent a full-body deformation judgement."),
    ("attack", 4, 90, "Later in the same complete 120-frame hit capture, the authored sea-monarch surface remains visible with no apparent texture loss or exploded panel in the exposed torso region."),
    ("kill-fixture", None, 0, "At the start of the explicit death fixture, the exact custom body and external trident are still present in the active encounter."),
    ("kill-fixture", None, 30, "Once native ragdoll dynamics have begun, the arms and mantle remain visually associated with the Tidecrown body in this UI-occluded sample; this is not a full anatomy or floor-contact verdict."),
    ("kill-fixture", None, 60, "The mid-fixture sample remains a live custom-renderer frame while the raw state records eleven active dynamic ragdoll bodies. The camera and player obscure much of the body."),
    ("kill-fixture", None, 90, "The last retained frame is after the owner renderer has become inactive and invisible and the game has reached its Ready UI. It does not establish final disposal or a settled corpse state."),
]
frames = []
for action_name, attempt, index, observation in choices:
    action = focus_actions[(action_name, attempt)]
    image = frame(action, index)
    frames.append({
        "action": action_name,
        "attempt": attempt,
        "index": index,
        "path": str(image.relative_to(ROOT)),
        "sha256": sha(image),
        "observation": observation,
    })

review = {
    "status": "SCOPED_LIVE_VISUAL_REVIEW_PASS_FOR_FOCUS_HIT_AND_EXPLICIT_RAGDOLL_FIXTURE_TIDECROWN_SEA_KING",
    "session": focus["session"],
    "nativeChassis": "seaKing",
    "profile": focus["enemy"],
    "rendererPath": "enSeaKing",
    "ownerInstanceId": renderer["ownerInstanceId"],
    "catalogSha256": focus["profileSha256"],
    "binding": {
        "mesh": renderer["mesh"],
        "rendererPath": renderer["rendererPath"],
        "boneSignature": renderer["boneSignature"],
        "rootBone": renderer["rootBone"],
        "visible": renderer["isVisible"],
        "enabled": renderer["enabled"],
        "active": renderer["active"],
        "rendererLocalToWorld": renderer["rendererLocalToWorld"],
        "materials": renderer["materials"],
        "controller": renderer["animator"]["controller"],
        "cullingMode": renderer["animator"]["cullingMode"],
        "initialRagdoll": {
            "m_DoRagdoll": renderer["ragdoll"]["m_DoRagdoll"],
            "animationRoot": renderer["ragdoll"]["animationRoot"],
            "rigidbodyCount": renderer["ragdoll"]["rigidbodyCount"],
        },
    },
    "focusHit": {
        "committed": "Attack(focus)",
        "attempt": 4,
        "enemyHpBefore": 720,
        "enemyHpAfter": 719,
        "meaning": "One fresh accepted focus action measured a one-HP reduction on the same exact target. This is target-HP evidence only; the review does not infer the native combat cause beyond the recorded action.",
    },
    "noFocusDiagnostic": {
        "source": str(NO_FOCUS_CASE.relative_to(ROOT)),
        "sha256": sha(NO_FOCUS_CASE),
        "session": no_focus["session"],
        "attempts": 8,
        "result": "Each bounded ordinary no-focus attempt left the exact 720-HP target unchanged and remains classified no_hp_loss_unclassified.",
    },
    "fixtureDeath": {
        "committed": "KillSingle",
        "retainedFrames": 91,
        "requestedFrames": 120,
        "firstDynamicRagdollFrame": first_dynamic,
        "dynamicBodiesAtFrame30": 12,
        "dynamicBodiesAtFrame60": 11,
        "rendererInactiveAtFrame": 90,
        "meaning": "An explicit fixture switched the recorded Sea King ragdoll bodies from kinematic to dynamic before the native owner became inactive. It is not ordinary lethal-damage, settled-corpse, or final-resource-disposal proof.",
    },
    "progression": {
        "strictReady": focus["finalReady"]["strictReady"],
        "nativeCollectSubmissions": 0,
        "meaning": "The guarded sequence observed native strict Ready at level 0 room 2 without a Collect submission in this particular fixture.",
    },
    "frames": frames,
    "conclusion": "The reviewed fresh exact-owner samples show Tidecrown visibly bound to enSeaKing with its authored mesh and texture, maintaining a coherent exposed torso and mantle through the accepted focus-hit capture and into the explicit native ragdoll fixture. The game-owned trident remains attached as an external native object. The visible record supports scoped integration and sampled animation/ragdoll compatibility, not completed art acceptance.",
    "limits": [
        "The Sea King encounter camera deliberately crops and overlaps the giant body; the head, crown, full robe silhouette, and much of the lower body are not cleanly visible in these combat-distance frames.",
        "This review samples eight stills. It does not establish all controller clips, all camera angles, full motion quality, culling, material lifetime, weapon intersection, or flawless art direction.",
        "The one measured successful hit uses focus. The separately preserved ordinary no-focus run had eight no-loss outcomes, so ordinary no-focus damage remains unresolved rather than inferred.",
        "KillSingle is an explicit fixture. Its capture ends after 91 frames when the renderer is destroyed; no ordinary lethal-damage, finished corpse, or final cleanup claim follows from it.",
        "Portrait marker registration exists in the profile, but no native Sea King portrait pixels were captured here.",
        "This verdict covers only seaKing/enSeaKing and does not establish other 60-bone humanoid rigs, tentacles, breakable props, or other Sea King variants.",
    ],
}
OUT.write_text(json.dumps(review, indent=2) + "\n")
print(json.dumps({"review": str(OUT), "frames": len(frames), "sha256": sha(OUT)}, indent=2))
