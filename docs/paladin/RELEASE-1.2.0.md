# Paladin 1.2.0

Paladin 1.2.0 brings the protector's primary stat and strongest ordinary great
hammers closer to original For The King class and weapon budgets.

- Base Vitality is 80, down from 84, matching the native Blacksmith value.
- Highward Great Hammer deals 32 base damage, down from 33.
- Mercy and Censure Great Hammers deal 34 base damage, down from 36.
- Verdict Great Hammer deals 37 base damage, down from 39.

Guard, focused ally healing, Divine Intervention and Cleansing March retain
their existing rules. The class, 51 equipment items, original art, stable item
identities and acquisition bands remain intact. One-handed hammers, shields,
apparel, accessories and artifacts retain their existing values.

Requires [FTK Mod Framework 1.0.3](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.3)
or a compatible later 1.x version. All co-op participants need the same package.
Do not infer compatibility with old saves solely from unchanged item identities;
retain the previous package for runs that require its exact mod version.

The [balance review](BALANCE-1.2.0.md) records fresh installed-game comparisons,
Focus calculations and the remaining campaign comparisons. The exact candidate
registered all 54 entries without content errors or warnings in an isolated
macOS game using the published framework DLL. A Journeyman Paladin showed
80 Vitality, started with 33 HP and three Focus, and defeated two natural level-0
Beastman Warriors alone in four normal attacks, spending one Focus and finishing
at 19/33 HP. Native loot collection completed. The disposable save resumed in a fresh
process with HP, Focus, Gold and XP preserved. One preceding restart crashed
in native Mono compilation on the framework reporting worker; unchanged bytes
succeeded on retry. See the [live receipt](balance-1.2.0-live.json). This is a bounded gameplay smoke,
not proof of matched class parity or whole-campaign balance.

The existing marketplace installation policy permits macOS, Windows and Linux
for the declared fingerprints. Only macOS gameplay was exercised locally; the
Windows fingerprint is player-reported. Online co-op, Windows/Linux gameplay,
matched full-campaign balance and broad endgame gear trials remain unverified.
The archive contains declarative content and original art, without game
assemblies or executable code.
