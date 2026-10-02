# Paladin 2.0.1 and framework 1.8.0 validation

This release changes Guardian combat rules and package admission. Equipment models,
textures, renderer routes, acquisition, and native icon assets are unchanged from
Paladin 2.0.0. The [2.0.0 record](RELEASE-2.0.0-VALIDATION.md) retains its exact
appearance and acquisition coverage; it does not prove the new combat effects.

## Acceptance contract

- Mercy core: Guard heals immediately, then arms one focused class Smite healing
  bond. Only a landed qualifying attempt heals, but even a miss spends the bond.
  It lasts through the end of the next own turn. Other focused actions cannot
  trigger it. Matching armament raises the heal from 12% to 15% maximum ally HP.
- Verdict: the existing protection, retaliation and matching-armament ward remain.
- Censure core: passive physical scaling is 100%. An actual direct enemy hit
  reduced by Guard readies one +50% qualifying single-target physical attack.
  Guard alone, dodge, damage over time and zero mitigation cannot charge it.
  It is spent on attempt and expires at the end of the next own turn.
- Censure and Kingsfall use the stronger charge, never a product. Charge state
  cannot survive a role/weapon change, incapacity or encounter reset.
- Old profiles default to the existing behavior. New optional profile fields are
  admitted by both framework and helper; Paladin requires framework 1.8.0.

## Evidence status

Release preparation is in progress. Game-free validation passed the Release build,
all 45 framework test projects (including 231 Guardian checks and 31 installed
assembly metadata checks), strict package parsing, Paladin progression negatives,
installer, Go helper, launcher options/Unix fixtures and release-manifest tests.
The authored site built and its 14-page desktop/mobile suite passed before final
published projections. These checks do not establish native combat results.

The candidate package is `paladin-2.0.1-5400e2cd0c6d.zip`, SHA-256
`5400e2cd0c6d0822e2d6ca88bdf7529e026fbba91a7f97adca8909ec7384e464`.
Its inventory contains 306 files. All 304 model/texture/icon assets match the
published 2.0.0 archive exactly; only manifest and content JSON differ. Current
helper admission passes. Native observations, framework identity and publication
receipts remain pending.
A build, a screenshot, and a successful upload are separate claims. No fresh
Windows, Linux/Proton, co-op, full campaign or statistical balance claim is made.

Mercy's healing bond is owned by the acting player's native attack authority.
The native damage-playback payload does not carry Focus spent; peers do not infer
it from UI or asynchronously synchronized Focus counters. The owner performs
healing through the existing synchronized health path. Censure uses the committed
native attack receipt on owner and playback paths. This design preserves the
existing authority boundary; it does not substitute for a new multiplayer trial.

## Pipeline lesson

A new JSON capability must be added to both runtime loading and marketplace
admission before testing installation. The previous helper correctly rejected
2.0.1's unknown profile fields. The framework and helper must ship together, with
explicit package minimum compatibility and positive/negative admission tests.

Website impact: the Paladin guide and equipment explanations must describe the
Guard-driven roles, and generated data must come from the verified public 2.0.1
archive. Update framework download references to 1.8.0 after publication.
