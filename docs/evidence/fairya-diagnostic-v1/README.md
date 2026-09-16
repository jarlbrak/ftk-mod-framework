# fairyA diagnostic baseline

Exact66-bone enFairy01 renderer121395, original calibration geometry only. Three120-frame raw captures are preserved byte-exactly as gzip, with full videos, summaries and five parent-reviewed PNGs. The wrapper's original `needs_visual_review` status remains unchanged in wrapper-result.json; root-review.json supplies the later limited visual interpretation.

Pass0/48 follows native fairy pose/spell motion; pink effects obscure48. Hit30 is hero-occluded, with same-target HP58→55. Death40/60 shows a prone probe, with green effects near40. Native fairy_die begins sampled at normalized.141, so its first cycle is incomplete.

Across all120 death frames m_DoRagdoll is false and Animator stays enabled. The two recorded rigidbodies are inactive WEAPON_HOLDER/fairyA(Clone)/Break descendants, not body ragdoll. This agrees with source inspection finding zero body Rigidbody/CharacterJoint. One Collect reaches strict Ready0/3.

No finished fairy art, all-view culling or all fairy-row acceptance is claimed. See validation.json for source, session, binary, asset, action and Ready pins.
