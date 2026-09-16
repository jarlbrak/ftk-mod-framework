#!/usr/bin/env python3
"""Write the human-reviewed still-frame verdict for the fresh ordinary Roc trial."""
import hashlib
import json
import os
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
CASE = BASE / "case-ac099200b3214d8384ad1c981fff8c16/case-result.json"
OUT = ROOT / "scratch/sunspire-roc-root-visual-review-v2.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


assert not OUT.exists() or os.environ.get("FTK_REVIEW_REBUILD") == "1", "Refusing to overwrite a completed review."
case = json.loads(CASE.read_text())
assert case["session"] == "243d7c94fca146d7a68bf03af5ba5f7f"
assert case["enemy"] == "ftkmf_modeltest_sunspire_roc"
assert case["rendererPath"] == "enRoc01" and case["focusedAttack"] is False
assert case["status"] == "needs_visual_review"
actions = {action["action"]: action for action in case["actions"]}
assert set(actions) == {"pass", "attack", "kill-fixture"}
assert actions["attack"]["actionResult"]["result"]["committed"] == "Attack"
assert actions["attack"]["hpOutcome"]["status"] == "nonlethal_hp_loss"
assert actions["attack"]["hpOutcome"]["beforeHp"] == 81
assert actions["attack"]["hpOutcome"]["afterHp"] == 71
assert all(action["capture"]["boundary"]["completeCapture"] is True for action in actions.values())
assert actions["kill-fixture"]["actionResult"]["result"]["committed"] == "KillSingle"

choices = [
    ("pass", 0, "Initial combat view: the authored dark faceted body, hooked ivory beak, paired wings, talons, and tail plumes make one readable Roc silhouette."),
    ("pass", 60, "Native pass sequence: the opened wing pose remains connected, with no apparent separated wing, tail, or body surface in this combat-distance sample."),
    ("attack", 0, "Before the ordinary player action resolves: the original head, chest mantle, wings, feet, and tail are all visible on the exact spawned owner."),
    ("attack", 25, "Ordinary nonlethal hit sample: native impact effects overlay the torso, while the articulated wing and body panels remain continuous."),
    ("attack", 65, "Native Roc attack motion: legs, talons, wing roots, extended wing sections, head, and tail stay attached during the airborne pose."),
    ("attack", 90, "Later attack sample: the head/beak, broad wings, and tail still read as a coherent bird rather than detached static decoration."),
    ("kill-fixture", 25, "Death-sequence entry immediately before heavy death: the visible original silhouette remains intact."),
    ("kill-fixture", 50, "Heavy-death sample: the original body has lowered and folded under native animation; no separated limb or unskinned wing is apparent in the reviewed frame."),
    ("kill-fixture", 119, "Late fixture sample: native victory and loot UI obscure much of the frame while the Roc remains behind it. This does not establish final corpse or resource lifetime."),
]
frames = []
for action_name, index, observation in choices:
    action = actions[action_name]
    raw = Path(action["capture"]["rawCapture"]["path"])
    image = raw.with_suffix("") / f"{index:04d}.png"
    assert image.is_file()
    frames.append({
        "action": action_name,
        "index": index,
        "path": str(image.relative_to(ROOT)),
        "sha256": sha(image),
        "observation": observation,
    })

renderer = case["initialRenderer"]
review = {
    "status": "SCOPED_LIVE_VISUAL_REVIEW_PASS_FOR_FRESH_ORDINARY_ORIGINAL_ROC",
    "session": case["session"],
    "nativeChassis": "rocA",
    "profile": case["enemy"],
    "rendererPath": "enRoc01",
    "ownerInstanceId": renderer["ownerInstanceId"],
    "catalogSha256": case["profileSha256"],
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
    },
    "ordinaryNoFocusHit": {
        "committed": "Attack",
        "enemyHpBefore": 81,
        "enemyHpAfter": 71,
        "meaning": "A fresh ordinary no-focus same-target action was accepted and observed to reduce HP. This is measured target HP evidence only; it does not infer combat cause beyond the recorded native action.",
    },
    "fixtureDeath": {
        "committed": "KillSingle",
        "capturedFrames": 120,
        "meaning": "Complete explicit fixture-death capture, not proof of ordinary lethal damage or a settled corpse/ragdoll.",
    },
    "frames": frames,
    "conclusion": "The reviewed fresh exact-owner samples show a readable original Roc whose head, beak, torso, wings, talons, and tail stay visually connected across idle/pass, ordinary hit, attack, and the visible death sequence. No obvious detached skinning, open panel inversion, or mesh explosion appears in these selected in-game frames.",
    "limits": [
        "This review samples nine stills rather than every rendered pose or camera angle.",
        "Native combat UI, player overlap, hit flashes, and victory/loot UI obscure fine feather, eye, and death detail.",
        "The registered head PortraitCam marker was not separately pixel-captured here; portrait framing remains a distinct gate.",
        "The final death frame is UI-obscured. No claim is made about settled corpse state, ragdoll, culling envelope, material disposal, or completed art direction.",
        "This verdict covers only the fresh rocA/enRoc01 profile, not rocB, jungle Roc variants, or other bird rigs.",
    ],
}
OUT.write_text(json.dumps(review, indent=2) + "\n")
print(json.dumps({"review": str(OUT), "frames": len(frames), "sha256": sha(OUT)}, indent=2))
