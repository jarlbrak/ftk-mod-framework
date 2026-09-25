# Framework 1.2.0 verification receipt

This receipt records verification of the framework and matching launcher helper
prepared for Paladin 1.3.0. It does not certify publication or platform coverage
beyond the checks listed below.

The Release framework DLL has SHA-256:

```text
58a0448bc92704acf980f9b976ad752731197ab094b03f9376a77ed865a61760
```

## Game-free verification

| Check | Result |
| --- | --- |
| Framework Release build | Passed; seven existing CS0649 warnings |
| Portable framework test projects | All 31 passed, including CombatProficiencies and HotReloadResources |
| Performance probe tests | 24 passed |
| Reporting runtime busy scenario | Passed |
| Installed-assembly Guardian signature checks | 31 passed; metadata inspection, not gameplay |
| Go launcher helper tests | Passed |
| Installer fixtures | 27 passed |
| Unix launcher fixtures | Passed |
| Release-manifest tests | 4 passed |
| Harness tests | 13 passed |
| Runtime Python tests | 222 passed |
| Model pipeline tests | 411 passed, 96 skipped |
| Windows launcher cross-build | Passed with zero warnings and errors |
| Launcher packaging | All four platform archives built successfully |
| Archive integrity and inventory | Embedded framework/helper hashes matched every bundle manifest; no game assemblies or private development files included |
| Repository whitespace check | `git diff --check` passed |

The packaged framework matched the DLL hash above. The Windows helper was
verified as an x64 PE image. Packaging covered macOS universal, Linux amd64,
Linux arm64, and Windows x64. Cross-builds and archive checks do not execute
the Windows launcher or installer.

## Fresh-process native smoke

A fresh macOS game process reported `SELF-TEST PASS` for behavior primitives,
quest logic, Guard, AddPassive, and data determinism. Paladin content loaded
57 of 57 entries with zero content warnings and zero content errors. Deliberate
rejection cases in the self-tests emit expected error messages; those messages
are distinct from content registration failures.

## Limits

Live co-op, new-capability hot activation, and Windows/Linux gameplay remain
unverified. Native Windows launcher and installer execution were not performed.
Skipped model checks are not passing coverage. The native smoke above does not
prove every combat branch, equipment lifecycle, animation, or multiplayer path.
Use a full game restart for content declaring the new capabilities.

See the [release notes](../releases/v1.2.0.md) for upgrade requirements and
platform limitations. Final clean-source release staging and published-asset
verification remain separate release workflow steps.
