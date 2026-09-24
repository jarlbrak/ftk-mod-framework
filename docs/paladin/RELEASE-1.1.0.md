# Paladin 1.1.0

Paladin 1.1.0 adds Cleansing March. While exploring, the Paladin cannot gain
new Poison or Curse, including from hazardous tiles. Existing conditions remain.
Fire damage, chaos resource or item losses, and combat ailments still use their
normal game rules. The passive works with any equipment and requires
[FTK Mod Framework 1.0.3](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.3)
or a later compatible 1.x release.

An [isolated macOS game trial](cleansing-march-live-2026-09-24.json) observed
ordinary movement onto a native Poison tile without Poison and native entry onto
a Curse tile without a curse. A Hunter entering fresh copies of those hazards
gained Poison level 1 and the Clumsy curse. The Paladin's combat immunity query
returned false. Natural hazard spawning, ordinary Curse walking, actual combat
ailment application, existing-condition behavior, save/resume, online co-op,
and Windows/Linux gameplay remain unverified for 1.1.0.

The archive contains the Paladin class, Guard, Divine Intervention, 51 original
equipment items and original art. It contains no game assemblies or executable
code. The marketplace permits installation on macOS, Windows and Linux for the
declared game fingerprints; the additional platforms are not gameplay-tested.
The [validation record](VALIDATION.md) separates earlier Paladin checks from
the exact 1.1.0 trial. A separate [prerelease smoke](release-smoke-2026-09-24.json)
registered the same package archive against a later 1.0.3 framework build and
started a native single-player session; it did not repeat hazard entry.
