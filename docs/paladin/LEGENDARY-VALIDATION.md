# Paladin legendary equipment validation

Historical candidate validation, 2026-09-22. These receipts predate the
published Paladin 1.0.0 archive and do not establish its full gameplay
acceptance. See [Launch 1.0.0](LAUNCH-1.0.0.md) for final-package observations.
The approved mechanics and tuning are described in
[Legendary equipment concepts](ARTIFACTS.md). General release gates
remain in [Launch 1.0.0](LAUNCH-1.0.0.md).

## Candidate scope and identities

The unpublished Paladin 1.0.0 archive adds The Last Vigil, Kingsfall and The Last
Bastion. It contains 42 content rows, including 39 equipment rows, and 150
runtime assets. The complete archive has 152 files: the manifest, content JSON
and original GLB/PNG assets. Fixture classes, game assemblies, saves, logs and
promotional material are excluded.

| Artifact | SHA-256 | Evidence scope |
|---|---|---|
| Framework used for the first legendary live trial | `f9306cc93408201ab502556761b209136ae39a9f3ac598d472c532317c97e8d5` | Isolated native equipment and combat observations below |
| Final framework candidate | `168e098a490b8fb5f3f96c4de63326dc088b136109fcefd6059560152a1e5e76` | Fresh normal-install registration passed; includes the constructor compatibility fix and removal of overlapping preview text |
| Final helper candidate | `535fd7df6d45f7b2e63d74018c914259127755b05c44ddb2fb2c0ee843d61c29` | Helper validation, packaging, CLI lifecycle and paired normal-install deployment |
| Paladin archive | `c91a408f9d2047f89f2e07a53e2ed0842d4496d073282385e966c843a0c0a7c1` | Built, validated and activated in the normal installation; not a public download |

The first live trial used an isolated gear fixture containing the equipment.
It did not install this final archive through a public catalog. Its combat
observations remain evidence for the earlier framework build; the fresh normal
installation check below establishes loading and marketplace presentation only.

## Game-free verification

The candidate work passed the Release build, PlayerMods checks, 193 GuardianCombat
checks, 29 installed-assembly method/signature checks, the Go helper suite and
legendary content validation. Packaging recorded four release-manifest checks
and helper cross-builds for macOS, Linux and Windows. Cross-builds do not prove
gameplay on those platforms.

The Guardian checks cover state-level deduplication, overlapping Guardians,
Focus capping, charge consumption and expiry, equipment changes, condition
priority, capability constraints and constructor compatibility. Source review
checked the native damage-calculation, owner synchronization, equipment-change
and turn-end integration points. These checks do not execute the Unity combat
adapters or prove multiplayer agreement.

## Observed native trial

The trial ran in the protected single-player gear installation. Equipment and
party setup were fixture-assisted. The Focus/Poison fixture only established
inputs on an exact owned ally; it did not invoke Guard or alter legendary state.
A separate damage fixture staged one enemy hit so the Guard transform could be
observed. Gameplay conclusions below are limited to those inputs.

| Observation | Result | Limit |
|---|---|---|
| Original gear on native male and female characters | Legendary equipment rendered in native scenes and combat; fully framed world captures show Kingsfall and the Vigil/Bastion pairing | Does not cover every race, animation or loot-display pose |
| Last Bastion Guard against an existing Poison condition | Native Guard removed the fixture-applied Poison; observation changed from one level to zero | Stun, Daze, Curse and mixed-condition priority were not exercised live |
| Last Vigil Focus reward | With Last Vigil on the female Paladin, a staged hit changed from 9 damage to 5 and the ally's HP from 31 to 26. Native impact was followed by ally Focus increasing from 2 to 3; the wielder remained at 4 | Proves one fixture-assisted grant to the ally, not full-cap or repeated-hit behavior |
| Kingsfall Guard mitigation | The staged incoming hit changed from 9 damage to 5; the read-only observer then reported Reckoning charged | Demonstrates the integration with a controlled hit, not ordinary attack distribution |
| Kingsfall charged preview | The native action preview showed 63 instead of the uncharged base 42 | The first build also exposed overlapping explanatory text, removed in the staged framework |
| Kingsfall charged native attack | A normal three-of-five slot roll dealt 38 damage and killed a Hag | Consistent with rounding `42 * 3/5 * 1.5`; one result does not isolate armor, criticals or all proficiency variants |

The attempted second-hit check for Unbroken Watch aborted because the native
Warlock attack contained a secondary splash outcome. The fixture committed no
replacement outcome. That attempt is not a live pass for once-per-Guard
deduplication; the existing game-free checks cover that rule.

Earlier no-Paladin acquisition and save evidence for ordinary Paladin gear does
not establish those behaviors for these three newly added Artifact items.

## Final candidate normal-install check

The exact final framework and helper pair above was deployed through
`prepare-launch`, and the exact Paladin archive was activated in the normal
Steam installation. A fresh windowed game process loaded the package without
test fixtures. The log registered all 42 entries with zero errors and zero
warnings, including each of the three legendary equipment IDs. The captured
framework log has SHA-256
`4a2ac98e0afdf5e72a2c51a97f0717ccd45362876985c975b8e4b1434062105b`.

The native Mods Installed page displayed the Paladin banner, JarlBrak, version
1.0.0, enabled status, and copy describing six equipment sets and three legendary
artifacts. This passes fresh-load registration and local installed-page checks.
It does not prove public catalog installation or replay the charged combat
preview after its text-overlap fix.

## Remaining acceptance gates

- Verify the revised charged preview and repeat relevant combat acceptance on
  the final framework candidate; fresh normal-install registration has passed.
- Extend the observed Unbroken Watch grant to full-cap behavior, a spent
  opportunity at full Focus, and no repeated or overlapping grant.
- Exercise Reckoning against mitigation and criticals, an eligible miss,
  ineligible area/secondary attacks, expiry without attacking, swapping away and
  back, and the next encounter.
- Exercise Stun, Daze and Curse cleansing, mixed-condition priority, duplicate
  delivery, an unaffected ally and preservation of permanent curses. Confirm
  that cleansing does not restore an already-lost action.
- Acquire the new Artifact items through ordinary drops or eligible merchants,
  including a party without a Paladin; verify new-item save/load round-trips.
- Complete equipped/display animation coverage and other-race checks. Record
  actual multiplayer and other-platform outcomes separately.
- Publish and verify immutable release assets and the public marketplace path
  only after the applicable release gates are met.

The isolated fixture is not a production feature. Discard trials containing
staged status or damage inputs rather than treating those saves as player
compatibility evidence.
