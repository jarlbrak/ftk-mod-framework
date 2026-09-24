# Guardian combat-exit cleanup regression

## Failure and invariant

[Report #196](https://github.com/jarlbrak/ftk-mod-framework/issues/196) attached 94
identical exceptions over its retained three-second window. The failure runs from
`EncounterSession.TurnOffDioramaObjects` through the dummy combat-finished prefix
to `GuardianRuntime.EndLegendaryCombat`, `Identity`, and `CharacterDummy.get_FID`.
The diagnosis was first made from the uploaded report alone.

Subsequent installed-assembly inspection established that the hub calls
`CombatFinished` on every configured dummy, including unused pooled entries. The
base FID getter directly reads `m_CharacterOverworld.m_FTKPlayerID`; the native
combat-finished method itself tolerates a missing overworld character. Its normal
completion sets `m_CombatFinished` and proceeds with optional avatar cleanup.
`TurnOffDioramaObjects` resets these dummies before deactivating the diorama.

Assembly SHA-256:
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.

The fix adds the same null/unbound-actor precondition already used by Guard expiry
to legendary cleanup, then resolves identity once. Bound actors still clear only
their own legendary state and pending focus receipt. Shared identity lookup stays
unchanged, including the enemy FID override. No exception is swallowed and no native
method is replaced or suppressed.

## Verification method

The opt-in `guardian-cleanup` probe in `tools/reporting-proof` requires the isolated
session receipt and a disconnected pristine title. It creates owned inactive
components, calls the actual Harmony-patched native `CombatFinished` method, and
checks its completion field. Additional cases check null cleanup, repeated calls,
matching versus unrelated actor state, enemy identity and fixture teardown. The
probe never adopts the player party. Its outcome is native integration evidence,
not a completed gameplay encounter.

Game-free verification passed: Release/net35 build with seven existing warnings,
193 GuardianCombat checks and 29 installed native method/parameter checks. The
proof plugin builds in Release/net35 with zero warnings.

## Before and after in the isolated macOS game

The same proof DLL was run against both framework builds with fresh isolation and
Steam-suppression receipts. The prior build failed both native unbound-dummy calls
with the same `get_FID > Identity > EndLegendaryCombat > GuardianEndPatch` stack as
#196; the null-actor case also failed with a null dictionary key. The fixed build
passed all seven checks, including native completion and per-actor preservation.
Fixture objects stayed inactive and the owned log contained no fixture Awake/FSM
errors. Both runs used the actual game assembly and installed Harmony prefix.

Prior framework SHA-256:
`799365744f34b15272b8992867517b49c99705c41705054c9b524f2ec5ac974a`.
Fixed framework SHA-256:
`01112dbde6632b5c1b4441ec7c1574035717c61ec82dd5ffb5f8240b76a55514`.
Proof DLL SHA-256:
`fa0f0adf42ebdd8bca6cff8ef7c79cfa9f38a734c8181dfffa469c7b2348ea3f`.

Raw receipts and logs remain private scratch artifacts. No game source, assemblies,
logs or saves are committed.

## Ordinary combat and local deployment

The fixed build also completed an ordinary encounter in a fresh isolated solo
adventure through the native-input harness. Blacksmith and Hunter fought a
Timberwolf using normal attack buttons. The enemy reached zero health, both
participants received two XP, and sharing the four-gold reward gave each two gold.
The game returned to the overworld with combat inactive. Clicking End Turn then
advanced from Blacksmith to Hunter, whose next turn rolled five movement points.
No combat outcome or cleanup method was forced in this gameplay trial.

The fresh owned log contained zero `GuardianEndPatch` or `EndLegendaryCombat`
exception stacks. It did contain an unrelated startup
`AkInitializer.OnApplicationFocus` null reference, so this is not a claim of an
entirely error-free game log. No report was uploaded during the trial.

The isolated process was closed normally. The same fixed DLL was installed into
the stopped local Steam copy, with the previous DLL and installation receipt
backed up privately. The receipt hash and installed DLL match the fixed hash above;
the helper and other installed content were preserved. Other running FTK copies
were not driven, signalled or modified.

Remaining gates: launching that installed Steam copy and replaying the user's
original Paladin save, multiplayer, Windows and Linux. The ordinary encounter used
the default party without marketplace mods; the native regression independently
checked Guardian state preservation. The Steam deployment is hash-verified, not a
claim that a Steam launch or its launcher preflight was exercised this turn.
