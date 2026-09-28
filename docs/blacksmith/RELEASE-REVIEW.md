# Blacksmith Forge Gear release review

**Current verdict: NO GO.** The [full release pass](FULL-RELEASE-PASS.md)
records the frozen candidate, actual published v1.5.0 helper failure, Cat/Demon
fit failures, isolated lifecycle results, and remaining gates. Earlier sections
below preserve their dated evidence; they do not supersede that verdict.

## Expanded release pass

The [current offline review](OFFLINE-RELEASE-PASS.md) supersedes the older latest-release
lookup below: actual published v1.5.0 rejects this package's `classAffinity`.
Expanded native testing additionally found Cat/Demon forearm protrusions; human
fit acceptance does not extend to those races. Full release acceptance remains open.

## Banner correction and native fitting, 2026-09-26

**Unpublished: inspected v12 fitting and ordinary one-handed combat passed the observed views.** The user
rejected the preceding in-game appearance. The unchanged original marketplace
banner, SHA-256 `bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b`,
remains the design authority for the [correction campaign](../../art-experiments/blacksmith-forge-rodin/banner-faithful/README.md).
Earlier fit and combat observations below concern superseded Kilnward visuals.

The replacement source package was built, validated, staged, and loaded in the
isolated macOS game. It retains all 30 item identities, stats, acquisition rules,
and class-affinity declarations, with **141 runtime assets: 81 GLBs and 60 PNGs**.
The native loader registered 30 entries with zero errors and zero warnings.
The first female trial exposed a floating circlet and pale material response.
Subsequent fitting and material revisions addressed those findings. The selected
apparel is `fitted-v12`; rigid assets use `native-fit-v1`. Native female front,
three-quarter, and rear views of v12 showed no hovering bracer or exposed right
elbow patch. Male v11 was visually inspected; its model bytes are unchanged in
v12. This establishes the inspected views, not every class, animation, or route.

Final v12 combat used an ordinary solo Vale Imp encounter. Sampled block,
one-handed attack, and victory views showed no hovering bracers or knee holes.
The enemy had 9 HP; a normal attack with no Focus or cheat action dealt 29
damage and won. Incoming frames 49, 60, 75, and 90, native attack frames 18 and
30, and studio victory frame 60 were inspected. The supplemental studio camera
loses framing during the attack lunge; all close attack views are not claimed.
This limited encounter does not replace representative end-game balance,
two-handed combat, multiplayer, or lifecycle checks. See the
[correction findings](BANNER-MODELS.md) and
[final native fitting receipt](../../art-experiments/blacksmith-forge-rodin/banner-faithful/native-final-fit-review.json)
for the revision sequence, source pins, and observed-scope acceptance.

The rebuilt local archive is
`blacksmith-forge-gear-0.1.0-dd48a48e0c2f.zip`, SHA-256
`dd48a48e0c2f385dca8b72802111c6f15c83b45300fe775b563d50a6e2e6b732`.
Its 143 runtime files comprise the manifest, content JSON, and 141 assets.
The descriptor uses listing fingerprint `7108deffdc41`; the local helper's
archive validation passed. The framework binary remains SHA-256
`2787ae9cf305f31ad5952ed358486c70c6f07a43f39b29bde4f15553e748ba92`.
These are local candidate checks, not publication or verification against a
published framework supporting class affinity.

The selected Kilnward male/female armor, shared boot pair, one-handed hammer,
two-handed hammer variant, and shield derive from new Rodin sources fitted and
processed in Blender by Astra High agents. The circlet is original Blender
geometry. Its `helmetCrown` template supplies the native equipped `.` and
inventory `helmCrown` routes; lower-tier helmets remain display-only. The
[selection receipt](../../marketplace/packages/classgear/assets.provenance.json)
records the selected files and source-manifest hashes separately from the
retained lower-tier sources. Do not promote a subsequent repair without
regenerating and validating its selection and package receipts.

The [credit ledger](../../art-experiments/blacksmith-forge-rodin/banner-faithful/budget.json)
records four replacement generation jobs at a quoted 0.5 credits each, totaling
**2 quoted credits**, within the existing campaign allocation. The two-handed
variant uses the generated hammer source. No paid retries or paid add-ons were
used for these corrections. The receipts do not expose actual billed totals.
Distribution rights, published-framework compatibility, natural acquisition,
representative balance, and the remaining live/platform gates below still block
release. No publication or production-catalog acceptance is claimed.

## Offline review, 2026-09-26

**Verdict: not ready to ship.** The unpublished package passes the current
working-tree validators. Its required class-affinity capability is absent from
the published framework, and the live gates below remain open in the existing
evidence. The offline reviewer performed no game operations or publication. The live
release-pass observations below were supplied by the game reviewer.

### Framework compatibility blocker

The actual latest published macOS helper checked in the full pass is v1.5.0.
It rejects the frozen candidate with `content.json: json: unknown field
"classAffinity"` and exits 1. The [offline release pass](OFFLINE-RELEASE-PASS.md)
records the verified helper hash, release commit, and source evidence. This
supersedes the earlier v1.4.0 tagged-source-only compatibility review.

The package minimum remains `1.0.1`; no compatible public release has been
verified. Release and verify the affinity capability before assigning a real
minimum and rebuilding the candidate. No future version is assigned here.

### Commands and observed results before the armor repair

Commands ran from the repository root unless a directory is specified.

| Command | Result and scope |
|---|---|
| `python3 marketplace/packages/validate_classgear_blacksmith.py` | Passed: 30 unique unrestricted equipment entries, four item bands, and 140 runtime assets matched their hashed sources. |
| `dotnet run --project FTKModFramework/Tests/ClassAffinity/ClassAffinity.csproj -c Release` | Passed: 25 game-free contract assertions. |
| `dotnet run --project FTKModFramework/Tests/HotReloadResources/HotReloadResources.csproj -c Release` | Passed: 321 resource-ledger checks. Unity lifecycle remains a live gate. |
| `dotnet run --project FTKModFramework/Tests/PlayerMods/PlayerMods.csproj -c Release` | Passed with exit code 0, including affinity JSON parsing and marketplace selection/activation fixtures. |
| `dotnet build -c Release`, from `FTKModFramework` | Passed: zero errors and seven existing `CS0649` warnings in `EnemyVisualPatch.cs`. Targets `net35`; this does not establish in-game behavior. |
| `go test ./...`, from `launcher/helper` | Passed; Go reported a cached test result. |
| `git diff --check` | Passed. |

Before the armor repair, the current helper passed this exact local archive validation, run from
`launcher/helper`:

```sh
go run . marketplace-validate \
  --descriptor ../../scratch/blacksmith-gear-build/blacksmith-forge-gear-0.1.0-1a6af6edfd88-listing-c44d4262b4cd.descriptor.json \
  --archive ../../scratch/blacksmith-gear-build/blacksmith-forge-gear-0.1.0-1a6af6edfd88.zip
```

The helper reported: `Package descriptor and archive validation passed. Game
review remains required.` The descriptor uses local draft URL placeholders;
no public download or catalog installation is established by this check.

A Python `zipfile` inventory read compared every archive entry byte-for-byte
with its corresponding package source file. All 142 entries matched the source at that time, with
72,939,673 compressed bytes and 125,920,368 expanded bytes. The archive contains
the manifest, content JSON, and 140 runtime assets. Documentation, source
scripts, promotional art, and provenance receipts are outside that runtime
archive.

| Artifact | SHA-256 |
|---|---|
| Pre-repair candidate ZIP, superseded | `1a6af6edfd88e3c4a002aff67a2d29f4f9d7231f0b6c737a7e0335f16c2868e2` |
| Built `FTKModFramework/bin/Release/net35/FTKModFramework.dll` | `2787ae9cf305f31ad5952ed358486c70c6f07a43f39b29bde4f15553e748ba92` |

Every local Markdown link in the package README and MARKETING document
resolved to an existing file. Their `../../../docs/` paths are correct.
Promotional artwork provenance was preserved.

### Earlier repaired armor release pass, superseded Kilnward visuals

The game reviewer inspected repaired Rivetwatch female and male Blacksmith
previews from front, three-quarter, and rear views and found good leg coverage.
At that stage, all four armor tiers included original trousers, adding 500 triangles to
each variant. Final counts by tier are 10,040, 14,384, 12,392, and 13,090.
Preview overrides were restored and cleared before the native campaign resumed.
Native save/resume preserved the exact saved equipment snapshot. Female resumed
inventory captures cover all four tiers from the same three angles; their
complete visual review is still in progress.

The immutable archive above predates both that repair and the banner correction.
It does not represent the current source package. The rebuilt v12 archive is recorded above; the old archive remains superseded.

### Existing live evidence and gates

The [gear ledger](GEAR.md) records prior isolated macOS registration, native
full-set equips on a female Blacksmith, and the controlled level-14 combat
stress test. Those observations were not repeated by this offline review.

The apparel API registers garment prefabs and mesh assignments per equipped
item. Its Blacksmith female/male binding names do not restrict the wearer's
class. That code supports the unrestricted equipment design; it does not prove
fit on every native class or body variant.

The [full release pass](FULL-RELEASE-PASS.md) adds Cat and Demon forearm fit
failures, confirms limited manual lifecycle results, and records the current
Fire Cave trial. Remaining gates include:

- Male fit beyond the reviewed Rivetwatch and Kilnward views, other native-class
  fit, broader movement and deformation, broader final-asset combat coverage, ordinary equipment
  swaps and avatar rebuilds, break-fragment behavior, and item-card framing.
- Managed public install/update remains blocked. Manual isolated native-panel
  disable/restart, enable, removal, exact-file reinstall, and Resume passed in
  the full release pass; those observations do not establish the public flow.
- Natural shop/drop acquisition and pool frequency, representative full-party
  final-dungeon balance, and two-handed balance. Native two-handed attack and
  victory motion were observed in the full pass.
- Multiplayer parity and online co-op. Windows and Linux are untested; the
  draft descriptor claims only macOS.

Lower-tier helmets, necklaces, and trinkets are authored for inventory display.
The replacement Kilnward circlet adds original equipped geometry; its
initial native fit failed; the subsequent equipped correction is included in
the native fitting observations above.

The preceding model inventory described the superseded sources. Current
provenance includes the Rodin-derived Kilnward apparel and rigid replacements
and the Blender-authored circlet described above. The Hyper3D
distribution-rights caveat remains relevant. Confirm rights for the Rodin-derived assets before release; hash
and geometry validators do not establish license rights.

Publication remains outside the requested scope. Public archive availability,
catalog inclusion, and the public Discover installation flow can only be
verified after separately authorized publication in the order prescribed by
the [publishing guide](../PUBLISHING-MODS.md).
