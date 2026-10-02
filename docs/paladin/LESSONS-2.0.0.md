# Paladin 2.0.0 release lessons

These findings cover Paladin 2.0.0 and the framework 1.7.1 Guard indicator follow-up.
They are recommendations for the consolidated pipeline task. Shared skills have
not been changed. The [release validation record](RELEASE-2.0.0-VALIDATION.md)
records tested identities, native observations and remaining scope.

## Defects that escaped review

| Finding | Why it escaped | First-pass prevention |
| --- | --- | --- |
| The first Guard indicator stayed visible after expiry and duplicated after retargeting. | Native `SetStatusIcons` is called inside the `m_UpdateHud` gate. Reviewing the call without its enclosing condition led to a false per-frame-refresh assumption. State tests and a correct icon screenshot did not test presentation transitions. | Inspect complete native callers. Test application, expiry, retargeting, former-icon hover and encounter exit. Own the cloned icon and refresh from effective Guard state. R46 passed these cases; active-Guard incapacity, target loss and simultaneous Protected coexistence remain separately untested. |
| Human helmet fits missed the Undead Censure crown defect; the first correction still failed on the opposite side. | Whole-body views and repeated classes using the same head shape did not cover the actual anatomy or both sides. | Key fit coverage by skin/head shape and renderer route. Inspect front, both sides and rear, then relevant motion. Add class-specific controls where actions or renderer routes differ. Keep direct captures distinct from transferred evidence. |
| Material colors and scalar properties did not reliably predict equipped appearance. | Inherited maps, shader keywords and UV settings remained active, and equipped and item-display routes could inherit different materials. | Inspect the resolved material on each route. Author masks for the actual UV regions, including cloth, leather, exposed wood and metal. Check both sides and fracture parts. PNG validity or a scalar Metallic value cannot approve material appearance. |
| A seated primary grip or attractive standing view hid offhand and shield issues. | Static framing was treated as attack coverage; one camera could not establish plate direction or handle contact. Native two-hand animation also deliberately releases and regrips the offhand. | Declare source grip origin, handle axis and shield face normal. Compare the same native animation and pose before changing geometry. Observe windup, impact, recovery and return to idle using actual simulation time. |
| Accepted models still had stale promotional derivatives or evidence paths. | Model acceptance did not automatically update display exports, portraits or a gallery pointing at mutable staging files. | Freeze model, texture, mask, display transform, render recipe and portrait hashes together. Generate final studio media after native model acceptance. Compare actual gear to the approved banner; preserve the governing design reference. |

The [presentation coverage record](RELEASE-2.0.0-VALIDATION.md#presentation-and-bounded-native-fit)
joins final package assets to standing, gait and historical hammer-motion reviews.
It does not establish every skin/action combination or hidden surface. The
[Guard follow-up record](RELEASE-2.0.0-VALIDATION.md#framework-171-guard-indicator-follow-up)
keeps the failed R45 candidate separate from the exact released R46 binary.

## Design, acquisition and test scope

**Separate class identity from item usability.** Paladin owns hammer-gated Censure
and Smite; Paladin hammers provide ordinary Strike. The visible Mercy, Verdict and
Censure armor combinations establish the role and its drawbacks. Define base-class
power, partial/full-set bonuses, mixed sets, weapon swaps and a traded-hammer
non-Paladin control before balancing numbers. Tooltip text and derived profiles
need separate applied-effect evidence. See the [combat design](COMBAT.md) and
[bounded mechanics record](RELEASE-2.0.0-VALIDATION.md#native-mechanics-and-acquisition-evidence).

**Make shared acquisition a single framework service.** The Exchange trials
separately covered legacy-token survival, inactivity without catalogs and multiple
catalogs sharing one currency/provider. Test class filtering, backpack and every
equipped slot, duplicate hiding, cancellation, debit/delivery and save/resume. Use
the native vendor interaction. A purchase made with setup tokens does not prove an
earned-token route. This distinction is retained in the acquisition evidence above.

**Choose the fixture for the claim.** R44c and R46 observed Mercy healing and three
eligible token misses, ending at pity three. No built-in earned token was observed.
R46 won with two survivors after the Paladin fell, followed by native revival. That
supports the recorded healing and UI transitions, not campaign balance or token
supply rates. Declare setup, ordinary versus modified HP, enemy composition and
all outcomes. Stop after the requested gate instead of silently expanding the test.

**Prefer native presentation and narrow ownership.** The Guard badge uses the
existing Protected icon, status grid and tooltip style while reading custom Guard
state. Reusing presentation does not require applying a fake native debuff or
changing its mechanics. Keep framework-owned instances separate from native assets,
and verify lifecycle behavior rather than assuming matching appearance proves it.

## Release and evidence closure

**Verify the public install separately.** The public macOS smoke installed the
actual Paladin ZIP through the online native Mods catalog, restarted and registered
57/57 entries. It restored the original profile exactly. Local staging alone could
not establish this route. Keep the tested commit and binary pinned, independently
download release assets, verify manifests and archive inventories, and test public
activation. Preserve immutable releases and increment the framework for follow-up
fixes. See [public installation evidence](RELEASE-2.0.0-VALIDATION.md#package-public-installation-and-registration).

**Reuse evidence through explicit source comparisons.** Unchanged mesh, albedo and
mask hashes supported bounded reuse of prior motion reviews. Runtime behavior
changes still needed separate scrutiny. An archive title, successful capture or
shared MVID label cannot establish equivalence by itself. Preserve rejected
candidates and distinguish native gameplay, native item-camera renders, diagnostic
views and studio art.

**Reconcile status at the end.** Earlier status summaries still said unpublished,
pending portraits and title-screen testing after those stages had completed.
Derive the final status from release identities, tested hashes, process/save
cleanup, gallery/media, website deployment and unresolved scope. Keep historical
records intact but label superseded summaries. Publication, artistic acceptance,
platform availability, native platform validation and long-run balance are separate
claims.

## Proposed changes for the consolidated skill task

1. **create-advanced-model:** establish native-relative triangle, exported-vertex,
   texture-memory, material and assembled-load budgets before generation. Record
   grip axes and crown, brow and neck landmarks. Preserve strong original/Rodin
   geometry and tolerate minor native-like ear contact. Budget checks supplement
   visual review; they do not replace it.
2. **ftk-custom-models:** inspect resolved equipped and display material maps,
   keywords and UV transforms; review combined armor/boot fit; verify winding,
   skinning and unaffected regions after local deformation. Require same-pose
   native controls for ambiguous grip or fit issues.
3. **preview-ftk-assets:** pin the governing art reference and every derivative's
   inputs. Require native model acceptance before final studio portraits. Record
   native item-camera framing independently of equipped size and reach.
4. **author-gear-set:** define class/action ownership, coherent visible set roles,
   mixed-set tradeoffs and acquisition before tuning stats. Add slot-complete owned
   filtering and one shared currency/provider with dormant and multi-catalog cases.
5. **decompile-lookup, ingame-smoke and verify-change:** inspect enclosing native
   control flow; record exact tested inputs, simulation phases, explicit visual
   verdicts and transition outcomes. Keep failures, source-based reuse and final
   save/profile restoration. Separate skin-fit coverage from class mechanics.
6. **ftk-release and publish-mod:** verify public bytes and native activation,
   dependency minimums, release links, deployed website versions/media and unchanged
   unrelated packages. Close with one bounded validation record and current status.

Website impact: this is maintainer workflow documentation. Published player
behavior, packages and media are unchanged; no website edit or new release is
required. Shared skill edits remain deferred to the consolidated pipeline task.
